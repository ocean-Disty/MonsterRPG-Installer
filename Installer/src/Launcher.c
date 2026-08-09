/* ===========================================================================
 *  Blockland MonsterRPG.exe - the thing you double-click to play.
 *
 *  Built as MonsterRPG.exe and renamed by Setup when it writes it out; the
 *  name it ends up with is LAUNCHER_NAME in Common.h.
 *
 *  It sits in the Blockland folder, next to Blockland.exe, and starts the game
 *  with the MonsterRPG mods loaded.
 *
 *  WHY IT RUNS ONE .BAT AND NOT BOTH
 *
 *  BLTickRate.bat and MonsterRPGAudio.bat both start the game, and both of
 *  them load every mod that is installed - that is what PAIRING=1 and AUTO
 *  mean inside them. Running both would therefore start Blockland twice, with
 *  the same mods in each copy, and the second one would fight the first over
 *  the game's files.
 *
 *  So this runs the one that has to go first (BLTickRate, because it rewrites
 *  engine settings the game only reads while starting) and that single run
 *  loads BLTickRate and MonsterRPGAudio together. If BLTickRate is not
 *  installed, whichever mod is installed is used instead. Either way you end
 *  up with one Blockland running everything you installed.
 *
 *  The .bat files are used rather than the injector inside them on purpose:
 *  anyone who has edited a .bat - to set BLOCKLAND_DIR, or PAIRING=0 to visit
 *  somebody else's server - gets those edits honoured here too.
 * ======================================================================== */

#define WIN32_LEAN_AND_MEAN

#include "Common.h"

#include <shellapi.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define MAX_MODS 16

typedef struct {
    wchar_t folder[MAX_PATH];   /* full path of the mod folder */
    wchar_t name[64];           /* folder name, which is also the .bat name */
    wchar_t bat[MAX_PATH * 2];
    int     order;              /* from bl_inject.cfg; lower runs first */
} ModFolder;

/* ---------------------------------------------------------------------------
 *  Messages
 * ------------------------------------------------------------------------ */

static void SayError(const wchar_t *text)
{
    MessageBoxW(NULL, text, APP_NAME, MB_OK | MB_ICONWARNING);
}

/* ---------------------------------------------------------------------------
 *  Finding the game and the mods
 * ------------------------------------------------------------------------ */

static BOOL FindGameFolder(wchar_t *dst, size_t cch)
{
    wchar_t here[MAX_PATH * 2];
    wchar_t candidate[MAX_PATH * 2];
    wchar_t exe[MAX_PATH * 2];

    /* Normally we are sitting right next to it. */
    if (GetExeDir(here, sizeof(here) / sizeof(here[0]))) {
        PathJoin(exe, sizeof(exe) / sizeof(exe[0]), here, GAME_EXE);
        if (FileExists(exe)) {
            wcsncpy(dst, here, cch - 1);
            dst[cch - 1] = L'\0';
            return TRUE;
        }
    }

    /* Someone has copied this shortcut-style into another folder. The usual
     * place is Documents\Blockland. */
    if (GetDocumentsDir(candidate, sizeof(candidate) / sizeof(candidate[0]))) {
        PathJoin(candidate, sizeof(candidate) / sizeof(candidate[0]), candidate, L"Blockland");
        PathJoin(exe, sizeof(exe) / sizeof(exe[0]), candidate, GAME_EXE);
        if (FileExists(exe)) {
            wcsncpy(dst, candidate, cch - 1);
            dst[cch - 1] = L'\0';
            return TRUE;
        }
    }

    return FALSE;
}

/* Reads "order=" out of a mod's bl_inject.cfg. Anything unreadable is put at
 * the back rather than treated as an error - the ordering is a preference,
 * the mod still works. */
static int ReadOrder(const wchar_t *cfgPath)
{
    wchar_t *text = ReadTextFile(cfgPath);
    wchar_t *line;
    int order = 1000;

    if (text == NULL)
        return order;

    line = text;
    while (line != NULL && *line != L'\0') {
        wchar_t buf[256];
        wchar_t *next = wcschr(line, L'\n');

        if (next != NULL) *next = L'\0';
        wcsncpy(buf, line, (sizeof(buf) / sizeof(buf[0])) - 1);
        buf[(sizeof(buf) / sizeof(buf[0])) - 1] = L'\0';
        if (next != NULL) { *next = L'\n'; next++; }
        line = next;

        TrimInPlace(buf);
        if (buf[0] == L'#')
            continue;

        if (wcsncmp(buf, L"order=", 6) == 0) {
            int v = _wtoi(buf + 6);
            if (v > 0) order = v;
            break;
        }
    }

    LocalFree(text);
    return order;
}

/* Every folder beside Blockland.exe that has a bl_inject.cfg and a .bat named
 * after itself. That pair is what makes a folder a startable mod. */
static int FindMods(const wchar_t *gameDir, ModFolder *out, int max)
{
    wchar_t pattern[MAX_PATH * 2];
    WIN32_FIND_DATAW fd;
    HANDLE h;
    int n = 0;

    PathJoin(pattern, sizeof(pattern) / sizeof(pattern[0]), gameDir, L"*");

    h = FindFirstFileW(pattern, &fd);
    if (h == INVALID_HANDLE_VALUE)
        return 0;

    do {
        wchar_t folder[MAX_PATH * 2];
        wchar_t cfg[MAX_PATH * 2];
        wchar_t bat[MAX_PATH * 2];
        wchar_t batName[128];

        if (!(fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY))
            continue;
        if (wcscmp(fd.cFileName, L".") == 0 || wcscmp(fd.cFileName, L"..") == 0)
            continue;

        PathJoin(folder, sizeof(folder) / sizeof(folder[0]), gameDir, fd.cFileName);
        PathJoin(cfg, sizeof(cfg) / sizeof(cfg[0]), folder, L"bl_inject.cfg");
        if (!FileExists(cfg))
            continue;

        _snwprintf(batName, sizeof(batName) / sizeof(batName[0]), L"%s.bat", fd.cFileName);
        batName[(sizeof(batName) / sizeof(batName[0])) - 1] = L'\0';
        PathJoin(bat, sizeof(bat) / sizeof(bat[0]), folder, batName);
        if (!FileExists(bat))
            continue;

        if (n >= max)
            break;

        wcsncpy(out[n].folder, folder, MAX_PATH - 1);
        out[n].folder[MAX_PATH - 1] = L'\0';
        wcsncpy(out[n].name, fd.cFileName, 63);
        out[n].name[63] = L'\0';
        wcsncpy(out[n].bat, bat, (sizeof(out[n].bat) / sizeof(out[n].bat[0])) - 1);
        out[n].order = ReadOrder(cfg);
        n++;
    } while (FindNextFileW(h, &fd));

    FindClose(h);
    return n;
}

/* ---------------------------------------------------------------------------
 *  Running the .bat
 * ------------------------------------------------------------------------ */

/* Runs it with no window, its input already at end of file, and its output
 * collected. End-of-file input matters: the .bat calls pause when it wants to
 * complain about something, and a pause in a window nobody can see would hang
 * here forever. With no input left, pause returns straight away and the text
 * it printed ends up in the box we show instead. */
static BOOL RunBat(const wchar_t *bat, const wchar_t *workingDir,
                   const wchar_t *extraArgs, DWORD *exitCode,
                   wchar_t *output, size_t outputCch)
{
    wchar_t comspec[MAX_PATH];
    wchar_t *cmdLine;
    size_t cmdCch;
    STARTUPINFOW si;
    PROCESS_INFORMATION pi;
    SECURITY_ATTRIBUTES sa;
    HANDLE nul = INVALID_HANDLE_VALUE;
    HANDLE readEnd = NULL, writeEnd = NULL;
    char *buffer = NULL;
    DWORD total = 0;
    BOOL started;

    if (output != NULL && outputCch > 0)
        output[0] = L'\0';

    if (GetEnvironmentVariableW(L"COMSPEC", comspec, MAX_PATH) == 0)
        wcscpy(comspec, L"cmd.exe");

    /* /d skips any AutoRun command the machine has configured, so a stray
     * setting on one computer cannot change what this does. /s makes cmd take
     * everything after /c literally once the outer quotes are stripped, which
     * is the only quoting rule that survives spaces in folder names. */
    cmdCch = wcslen(bat) + wcslen(extraArgs) + 64;
    cmdLine = (wchar_t *)LocalAlloc(LPTR, cmdCch * sizeof(wchar_t));
    if (cmdLine == NULL)
        return FALSE;

    _snwprintf(cmdLine, cmdCch, L"\"%s\" /d /s /c \"\"%s\" %s\"",
               comspec, bat, extraArgs);
    cmdLine[cmdCch - 1] = L'\0';

    sa.nLength = sizeof(sa);
    sa.lpSecurityDescriptor = NULL;
    sa.bInheritHandle = TRUE;

    nul = CreateFileW(L"NUL", GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
                      &sa, OPEN_EXISTING, 0, NULL);

    if (!CreatePipe(&readEnd, &writeEnd, &sa, 0)) {
        readEnd = writeEnd = NULL;
    } else {
        SetHandleInformation(readEnd, HANDLE_FLAG_INHERIT, 0);
    }

    ZeroMemory(&si, sizeof(si));
    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESHOWWINDOW;
    si.wShowWindow = SW_HIDE;
    if (nul != INVALID_HANDLE_VALUE && writeEnd != NULL) {
        si.dwFlags |= STARTF_USESTDHANDLES;
        si.hStdInput  = nul;
        si.hStdOutput = writeEnd;
        si.hStdError  = writeEnd;
    }

    ZeroMemory(&pi, sizeof(pi));

    started = CreateProcessW(NULL, cmdLine, NULL, NULL, TRUE,
                             CREATE_NO_WINDOW, NULL, workingDir, &si, &pi);

    LocalFree(cmdLine);

    if (writeEnd != NULL)
        CloseHandle(writeEnd);      /* our copy, or the read below never ends */
    if (nul != INVALID_HANDLE_VALUE)
        CloseHandle(nul);

    if (!started) {
        if (readEnd != NULL) CloseHandle(readEnd);
        return FALSE;
    }

    if (readEnd != NULL) {
        buffer = (char *)LocalAlloc(LPTR, 8192);
        if (buffer != NULL) {
            for (;;) {
                DWORD got = 0;
                if (total >= 8000) break;
                if (!ReadFile(readEnd, buffer + total, 8000 - total, &got, NULL) || got == 0)
                    break;
                total += got;
            }
            buffer[total] = '\0';
        }
        CloseHandle(readEnd);
    }

    /* Not INFINITE. The .bat's job is to start the game and return, which
     * takes a moment - it does not stay alive while you play. If it somehow
     * never returns, waiting forever would leave this sitting in Task Manager
     * with no window and no way to close it. Two minutes is far longer than
     * the job takes; past that we stop waiting and say nothing, because by
     * then the game has either started or the .bat will report it itself. */
    if (WaitForSingleObject(pi.hProcess, 120000) != WAIT_OBJECT_0) {
        if (exitCode != NULL)
            *exitCode = 0;
        CloseHandle(pi.hThread);
        CloseHandle(pi.hProcess);
        if (buffer != NULL)
            LocalFree(buffer);
        return TRUE;
    }

    if (exitCode != NULL && !GetExitCodeProcess(pi.hProcess, exitCode))
        *exitCode = 0;

    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);

    if (buffer != NULL) {
        if (output != NULL && outputCch > 1 && total > 0)
            MultiByteToWideChar(CP_OEMCP, 0, buffer, -1, output, (int)outputCch);
        LocalFree(buffer);
    }

    return TRUE;
}

/* ---------------------------------------------------------------------------
 *  Entry point
 * ------------------------------------------------------------------------ */

/* Anything typed after MonsterRPG.exe is handed on to the game, the same way
 * the .bat files hand their own arguments on. */
static const wchar_t *ArgumentsAfterProgramName(void)
{
    const wchar_t *p = GetCommandLineW();

    if (*p == L'"') {
        p++;
        while (*p != L'\0' && *p != L'"') p++;
        if (*p == L'"') p++;
    } else {
        while (*p != L'\0' && *p != L' ' && *p != L'\t') p++;
    }
    while (*p == L' ' || *p == L'\t') p++;

    return p;
}

int WINAPI wWinMain(HINSTANCE inst, HINSTANCE prev, PWSTR cmdLine, int show)
{
    wchar_t gameDir[MAX_PATH * 2];
    wchar_t gameExe[MAX_PATH * 2];
    wchar_t message[8192];
    wchar_t output[4096];
    ModFolder mods[MAX_MODS];
    int count, i, best;
    DWORD exitCode = 0;

    (void)inst; (void)prev; (void)cmdLine; (void)show;

    if (!FindGameFolder(gameDir, sizeof(gameDir) / sizeof(gameDir[0]))) {
        SayError(
            L"MonsterRPG could not find Blockland.\n\n"
            L"This file has to sit in the same folder as Blockland.exe.\n\n"
            L"To fix it, run MonsterRPG Setup again and point it at the folder "
            L"that has Blockland.exe in it.");
        return 1;
    }

    PathJoin(gameExe, sizeof(gameExe) / sizeof(gameExe[0]), gameDir, GAME_EXE);

    count = FindMods(gameDir, mods, MAX_MODS);

    if (count == 0) {
        int answer = MessageBoxW(NULL,
            L"The MonsterRPG mod folders are not in your Blockland folder, so "
            L"there is nothing extra to load.\n\n"
            L"Run MonsterRPG Setup again to put them back.\n\n"
            L"Start Blockland on its own in the meantime?",
            APP_NAME, MB_YESNO | MB_ICONQUESTION);

        if (answer != IDYES)
            return 1;

        {
            SHELLEXECUTEINFOW ei;
            ZeroMemory(&ei, sizeof(ei));
            ei.cbSize = sizeof(ei);
            ei.lpFile = gameExe;
            ei.lpParameters = L"-client";
            ei.lpDirectory = gameDir;
            ei.nShow = SW_SHOWNORMAL;
            ei.fMask = SEE_MASK_NOASYNC;
            if (!ShellExecuteExW(&ei)) {
                SayError(L"Windows would not start Blockland.exe.");
                return 1;
            }
        }
        return 0;
    }

    /* Lowest order wins. Ties go to whichever was found first, which is
     * alphabetical, so the choice is at least always the same one. */
    best = 0;
    for (i = 1; i < count; ++i)
        if (mods[i].order < mods[best].order)
            best = i;

    if (!RunBat(mods[best].bat, mods[best].folder,
                ArgumentsAfterProgramName(), &exitCode,
                output, sizeof(output) / sizeof(output[0]))) {
        _snwprintf(message, sizeof(message) / sizeof(message[0]),
            L"MonsterRPG could not run:\n\n    %s\n\n"
            L"Check that the file is still there and that your antivirus has "
            L"not removed it.", mods[best].bat);
        message[(sizeof(message) / sizeof(message[0])) - 1] = L'\0';
        SayError(message);
        return 1;
    }

    if (exitCode != 0) {
        _snwprintf(message, sizeof(message) / sizeof(message[0]),
            L"Blockland did not start.\n\n"
            L"%s said:\n\n%s\n"
            L"There is more detail in %s\\%s.log",
            mods[best].name, output[0] != L'\0' ? output : L"(nothing)",
            mods[best].folder, mods[best].name);
        message[(sizeof(message) / sizeof(message[0])) - 1] = L'\0';
        SayError(message);
        return 1;
    }

    return 0;
}
