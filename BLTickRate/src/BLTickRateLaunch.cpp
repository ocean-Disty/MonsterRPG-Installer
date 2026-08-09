//////////////////////////////////////////////////
// BLTickRateLaunch — starts Blockland with BLTickRate.dll attached
//
// The DLL has to be in place before the engine registers its classes, so this
// creates the process SUSPENDED, injects the DLL, and only then lets it run.
// That is the same timing as editing Blockland.exe's import table with Stud_PE
// (which is how CompMix's original was loaded), except nothing on disk is
// touched, so "uninstalling" is just not using this launcher.
//
//   usage: BLTickRateLaunch.exe <Blockland.exe> <dll>[;<dll>...]|AUTO <workdir> [game args...]
//
// Players do not run this directly — BLTickRate.bat does it for you.
//
// ── WHY THE DLL ARGUMENT IS A LIST, AND CAN BE "AUTO" ────────────────────────
//
// Only one launcher can create the process. So if a player has BLTickRate and
// MonsterRPGAudio both installed, whichever .bat they double-click has to carry
// both — otherwise half of what they installed silently does not run, and the
// only way they find out is by noticing something is missing.
//
// AUTO discovers every participating mod folder beside Blockland.exe and injects
// them in priority order. The identical code is in MonsterRPGAudioLaunch.exe, so
// BOTH .bat files produce exactly the same running game. That is the point: a
// player should never have to know which icon is "the one that starts
// everything".
//
// Order is explicit rather than alphabetical, and BLTickRate is deliberately
// first: it rewrites constants the engine reads once during startup. See
// Pairing.h.

#include <windows.h>
#include <stdio.h>
#include <string.h>

#include "Pairing.h"

// Injects one DLL into an already-suspended process. Returns false and explains
// itself on any failure — the caller kills the process rather than resuming a
// half-injected one.
static bool InjectOne(HANDLE hProcess, const char *dll)
{
    if(GetFileAttributesA(dll) == INVALID_FILE_ATTRIBUTES)
    {
        printf("ERROR: cannot find DLL:\n  %s\n", dll);
        return false;
    }

    // Push the DLL path into the target and load it from a remote thread.
    size_t len = strlen(dll) + 1;
    void *remote = VirtualAllocEx(hProcess, NULL, len,
                                  MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    if(!remote || !WriteProcessMemory(hProcess, remote, dll, len, NULL))
    {
        printf("ERROR: could not write into the game process (%lu)\n", GetLastError());
        return false;
    }

    // kernel32 sits at the same address in every process of this architecture,
    // so our own LoadLibraryA is valid in the target.
    HMODULE k32 = GetModuleHandleA("kernel32.dll");
    FARPROC loadLibrary = GetProcAddress(k32, "LoadLibraryA");

    HANDLE thread = CreateRemoteThread(hProcess, NULL, 0,
                                       (LPTHREAD_START_ROUTINE)loadLibrary,
                                       remote, 0, NULL);
    if(!thread)
    {
        printf("ERROR: could not inject %s (%lu)\n", dll, GetLastError());
        return false;
    }

    WaitForSingleObject(thread, 30000);

    DWORD module = 0;
    GetExitCodeThread(thread, &module);
    CloseHandle(thread);

    if(!module)
    {
        printf("ERROR: %s failed to load. Is it 32-bit?\n", dll);
        return false;
    }

    printf("  injected %s\n", dll);
    return true;
}

int main(int argc, char **argv)
{
    if(argc < 4)
    {
        printf("usage: BLTickRateLaunch.exe <exe> <dll>[;<dll>...]|AUTO <workdir> [args...]\n");
        return 1;
    }

    const char *exe     = argv[1];
    const char *dllList = argv[2];
    const char *cwd     = argv[3];

    if(GetFileAttributesA(exe) == INVALID_FILE_ATTRIBUTES)
    {
        printf("ERROR: cannot find Blockland.exe at:\n  %s\n", exe);
        return 1;
    }

    // Blockland skips its first argument on release builds, so anything passed
    // through has to sit behind a placeholder or the game never sees it.
    char cmd[4096];
    _snprintf(cmd, sizeof(cmd), "\"%s\"", exe);
    for(int i = 4; i < argc; i++)
    {
        strncat(cmd, " ", sizeof(cmd) - strlen(cmd) - 1);
        strncat(cmd, argv[i], sizeof(cmd) - strlen(cmd) - 1);
    }

    STARTUPINFOA si;
    PROCESS_INFORMATION pi;
    ZeroMemory(&si, sizeof(si));
    si.cb = sizeof(si);
    ZeroMemory(&pi, sizeof(pi));

    // DETACHED_PROCESS matters as much as CREATE_SUSPENDED here.
    //
    // Without it the game inherits this launcher's console. When BLTickRate.bat
    // is double-clicked, cmd exits the moment the batch finishes, the console
    // is destroyed, and every process still attached to it is torn down —
    // so the game starts, patches correctly, and then vanishes a second later.
    // It only "works" when something keeps that console alive, which is why it
    // survives when run from an already-open terminal and dies when clicked.
    //
    // Detaching also suits both modes: a GUI client wants no console at all,
    // and a dedicated server allocates its own with enableWinConsole().
    if(!CreateProcessA(exe, cmd, NULL, NULL, FALSE,
                       CREATE_SUSPENDED | DETACHED_PROCESS, NULL, cwd, &si, &pi))
    {
        printf("ERROR: could not start Blockland (windows error %lu)\n", GetLastError());
        return 1;
    }

    bool ok = true;

    if(_stricmp(dllList, "AUTO") == 0)
    {
        // `cwd` is the Blockland folder, already normalised by the .bat — which
        // is what makes it safe to use as a search root.
        BlPair::Entry mods[BlPair::MAX_ENTRIES];
        int n = BlPair::Discover(cwd, mods, BlPair::MAX_ENTRIES);

        if(n == 0)
        {
            printf("ERROR: AUTO found no mod DLLs beside:\n  %s\n", cwd);
            TerminateProcess(pi.hProcess, 1);
            CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);
            return 1;
        }

        printf("Found %d mod%s to load:\n", n, n == 1 ? "" : "s");
        for(int i = 0; i < n; i++)
            printf("  %-18s (order %d)\n", mods[i].name, mods[i].order);
        printf("\n");

        for(int i = 0; i < n; i++)
            if(!InjectOne(pi.hProcess, mods[i].path)) { ok = false; break; }
    }
    else
    {
        // Explicit list, injected in the order given. A single path with no
        // semicolons is the old single-DLL form and behaves exactly as before,
        // which is what keeps an older BLTickRate.bat working against this exe.
        char list[2048];
        _snprintf(list, sizeof(list), "%s", dllList);
        list[sizeof(list) - 1] = '\0';

        char *ctx = NULL;
        for(char *tok = strtok_r(list, ";", &ctx); tok; tok = strtok_r(NULL, ";", &ctx))
        {
            while(*tok == ' ') ++tok;
            if(!*tok) continue;
            if(!InjectOne(pi.hProcess, tok)) { ok = false; break; }
        }
    }

    // A half-injected process is worse than none. If any DLL failed, the game is
    // killed rather than resumed — the player relaunches normally and gets a
    // completely stock Blockland, which is always the correct failure.
    if(!ok)
    {
        TerminateProcess(pi.hProcess, 1);
        CloseHandle(pi.hThread);
        CloseHandle(pi.hProcess);
        return 1;
    }

    ResumeThread(pi.hThread);

    printf("\nBlockland started (pid %lu).\n", pi.dwProcessId);
    printf("Check BLTickRate.log to confirm the patches applied.\n");

    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    return 0;
}
