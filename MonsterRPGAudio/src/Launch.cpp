// =============================================================================
// MonsterRPGAudioLaunch — starts Blockland with one or more DLLs attached
//
//   usage: MonsterRPGAudioLaunch.exe <Blockland.exe> <dll>[;<dll>...]|AUTO <workdir> [game args...]
//
// Players do not run this directly — MonsterRPGAudio.bat does it for you.
//
// Descended from BLTickRateLaunch.cpp, which is in the folder next door, with
// two deliberate changes: the DLL argument is a LIST, and it can be the word
// AUTO.
//
// WHY. Two launchers cannot each create the process, so whichever .bat the
// player double-clicks has to carry everything they installed. AUTO makes both
// launchers behave identically: they discover every participating mod folder
// beside Blockland.exe and inject them all, in priority order. Double-clicking
// MonsterRPGAudio.bat and double-clicking BLTickRate.bat now produce exactly the
// same running game, which is the only arrangement a player never has to think
// about. See Pairing.h.
//
// The rejected alternative — MonsterRPGAudio.dll quietly LoadLibrary-ing
// BLTickRate.dll from its own DllMain — would make one mod responsible for the
// other's failure modes, and BLTickRate's patcher is specifically sensitive
// about running before engine init.
//
// The process is created SUSPENDED and every DLL is loaded before it is allowed
// to run a single instruction. That timing is not optional for BLTickRate, whose
// constants are read once during startup; MonsterRPGAudio does not need it, but
// inherits it for free and uses the extra time to identify the GPU.
// =============================================================================

#include <windows.h>
#include <stdio.h>
#include <string.h>

#include "Pairing.h"

// Injects one DLL into an already-suspended process. Returns false and explains
// itself on any failure — the caller kills the process rather than resuming a
// half-injected one.
static bool InjectOne(HANDLE hProcess, const char* dll)
{
    if (GetFileAttributesA(dll) == INVALID_FILE_ATTRIBUTES) {
        printf("ERROR: cannot find DLL:\n  %s\n", dll);
        return false;
    }

    size_t len = strlen(dll) + 1;
    void* remote = VirtualAllocEx(hProcess, NULL, len,
                                  MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    if (!remote || !WriteProcessMemory(hProcess, remote, dll, len, NULL)) {
        printf("ERROR: could not write into the game process (%lu)\n", GetLastError());
        return false;
    }

    // kernel32 is at the same address in every process of this architecture, so
    // our own LoadLibraryA is a valid function pointer in the target.
    HMODULE k32 = GetModuleHandleA("kernel32.dll");
    FARPROC loadLibrary = GetProcAddress(k32, "LoadLibraryA");

    HANDLE thread = CreateRemoteThread(hProcess, NULL, 0,
                                       (LPTHREAD_START_ROUTINE)loadLibrary, remote, 0, NULL);
    if (!thread) {
        printf("ERROR: could not inject %s (%lu)\n", dll, GetLastError());
        return false;
    }

    WaitForSingleObject(thread, 30000);

    DWORD module = 0;
    GetExitCodeThread(thread, &module);
    CloseHandle(thread);

    // LoadLibraryA returns the module handle, so zero means it genuinely failed
    // to load rather than merely failing to do anything useful. On a 32-bit
    // target the overwhelmingly common cause is a 64-bit DLL.
    if (!module) {
        printf("ERROR: %s failed to load. Is it 32-bit?\n", dll);
        return false;
    }

    printf("  injected %s\n", dll);
    return true;
}

int main(int argc, char** argv)
{
    if (argc < 4) {
        printf("usage: MonsterRPGAudioLaunch.exe <exe> <dll>[;<dll>...] <workdir> [args...]\n");
        return 1;
    }

    const char* exe = argv[1];
    const char* dllList = argv[2];
    const char* cwd = argv[3];

    if (GetFileAttributesA(exe) == INVALID_FILE_ATTRIBUTES) {
        printf("ERROR: cannot find Blockland.exe at:\n  %s\n", exe);
        return 1;
    }

    // Blockland skips its first argument on release builds, so anything passed
    // through has to sit behind the exe name or the game never sees it.
    char cmd[4096];
    _snprintf(cmd, sizeof(cmd), "\"%s\"", exe);
    for (int i = 4; i < argc; i++) {
        strncat(cmd, " ", sizeof(cmd) - strlen(cmd) - 1);
        strncat(cmd, argv[i], sizeof(cmd) - strlen(cmd) - 1);
    }

    STARTUPINFOA si;
    PROCESS_INFORMATION pi;
    ZeroMemory(&si, sizeof(si));
    si.cb = sizeof(si);
    ZeroMemory(&pi, sizeof(pi));

    // DETACHED_PROCESS matters as much as CREATE_SUSPENDED, and this is not a
    // preference — it is a bug that has already been paid for once, next door.
    //
    // Without it the game inherits this launcher's console. When the .bat is
    // double-clicked, cmd exits the moment the batch finishes, the console is
    // destroyed, and every process still attached to it is torn down — so the
    // game starts, injects correctly, and then vanishes a second later. It only
    // "works" when something keeps that console alive, which is why it survives
    // when run from an already-open terminal and dies when clicked.
    if (!CreateProcessA(exe, cmd, NULL, NULL, FALSE,
                        CREATE_SUSPENDED | DETACHED_PROCESS, NULL, cwd, &si, &pi)) {
        printf("ERROR: could not start Blockland (windows error %lu)\n", GetLastError());
        return 1;
    }

    bool ok = true;

    if (_stricmp(dllList, "AUTO") == 0) {
        // Discover every participating mod beside Blockland.exe. `cwd` is the
        // Blockland folder — the .bat has already normalised it, which is also
        // what makes it safe to use as a search root here.
        BlPair::Entry mods[BlPair::MAX_ENTRIES];
        int n = BlPair::Discover(cwd, mods, BlPair::MAX_ENTRIES);

        if (n == 0) {
            // Not a crash, but not something to paper over either: AUTO found
            // nothing at all, which means the caller's own DLL is missing too.
            printf("ERROR: AUTO found no mod DLLs beside:\n  %s\n", cwd);
            TerminateProcess(pi.hProcess, 1);
            CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);
            return 1;
        }

        printf("Found %d mod%s to load:\n", n, n == 1 ? "" : "s");
        for (int i = 0; i < n; i++)
            printf("  %-18s (order %d)\n", mods[i].name, mods[i].order);
        printf("\n");

        for (int i = 0; i < n; i++)
            if (!InjectOne(pi.hProcess, mods[i].path)) { ok = false; break; }
    } else {
        // Explicit list. Injection order is the order given.
        char list[2048];
        _snprintf(list, sizeof(list), "%s", dllList);
        list[sizeof(list) - 1] = '\0';

        char* ctx = NULL;
        for (char* tok = strtok_r(list, ";", &ctx); tok; tok = strtok_r(NULL, ";", &ctx)) {
            while (*tok == ' ') ++tok;
            if (!*tok) continue;
            if (!InjectOne(pi.hProcess, tok)) { ok = false; break; }
        }
    }

    // A half-injected process is worse than none. If any DLL failed, the game is
    // killed rather than resumed — the player relaunches normally and gets a
    // completely stock Blockland, which is always the correct failure.
    if (!ok) {
        TerminateProcess(pi.hProcess, 1);
        CloseHandle(pi.hThread);
        CloseHandle(pi.hProcess);
        return 1;
    }

    ResumeThread(pi.hThread);

    printf("\nBlockland started (pid %lu).\n", pi.dwProcessId);
    printf("Check MonsterRPGAudio.log to confirm what happened.\n");

    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    return 0;
}
