/* ===========================================================================
 *  Common.c - see Common.h for what each of these is for.
 * ======================================================================== */

#define COBJMACROS
#define WIN32_LEAN_AND_MEAN

#include "Common.h"

#include <shlobj.h>
#include <objbase.h>
#include <tlhelp32.h>
#include <stdio.h>
#include <string.h>
#include <wchar.h>

/* ---------------------------------------------------------------------------
 *  Small string helpers
 * ------------------------------------------------------------------------ */

void TrimInPlace(wchar_t *s)
{
    wchar_t *start = s;
    size_t len;

    while (*start == L' ' || *start == L'\t' || *start == L'\r' || *start == L'\n')
        start++;

    len = wcslen(start);
    while (len > 0 && (start[len - 1] == L' ' || start[len - 1] == L'\t' ||
                       start[len - 1] == L'\r' || start[len - 1] == L'\n'))
        len--;

    memmove(s, start, len * sizeof(wchar_t));
    s[len] = L'\0';
}

BOOL EqualsNoCase(const wchar_t *a, const wchar_t *b)
{
    return CompareStringW(LOCALE_INVARIANT, NORM_IGNORECASE, a, -1, b, -1) == CSTR_EQUAL;
}

BOOL ContainsNoCase(const wchar_t *haystack, const wchar_t *needle)
{
    size_t nlen = wcslen(needle);
    size_t hlen = wcslen(haystack);
    size_t i;

    if (nlen == 0) return TRUE;
    if (nlen > hlen) return FALSE;

    for (i = 0; i + nlen <= hlen; ++i) {
        if (CompareStringW(LOCALE_INVARIANT, NORM_IGNORECASE,
                           haystack + i, (int)nlen, needle, (int)nlen) == CSTR_EQUAL)
            return TRUE;
    }
    return FALSE;
}

/* ---------------------------------------------------------------------------
 *  Paths
 * ------------------------------------------------------------------------ */

void PathJoin(wchar_t *dst, size_t cch, const wchar_t *a, const wchar_t *b)
{
    size_t len;

    if (cch == 0) return;

    if (dst != a) {
        wcsncpy(dst, a, cch - 1);
        dst[cch - 1] = L'\0';
    }

    len = wcslen(dst);
    while (len > 0 && dst[len - 1] == L'\\')
        dst[--len] = L'\0';

    if (b == NULL || *b == L'\0')
        return;

    while (*b == L'\\')
        b++;

    /* Only when there is something to separate from. Joining onto an empty
     * string must give "Add-Ons", not "\Add-Ons" - the second one is an
     * absolute path to the root of the drive. */
    if (len > 0 && len + 1 < cch) {
        dst[len++] = L'\\';
        dst[len] = L'\0';
    }
    wcsncat(dst, b, cch - wcslen(dst) - 1);
}

BOOL FileExists(const wchar_t *path)
{
    DWORD a = GetFileAttributesW(path);
    return a != INVALID_FILE_ATTRIBUTES && !(a & FILE_ATTRIBUTE_DIRECTORY);
}

BOOL DirExists(const wchar_t *path)
{
    DWORD a = GetFileAttributesW(path);
    return a != INVALID_FILE_ATTRIBUTES && (a & FILE_ATTRIBUTE_DIRECTORY);
}

BOOL EnsureDir(const wchar_t *path)
{
    wchar_t buf[MAX_PATH * 2];
    size_t i;

    if (DirExists(path))
        return TRUE;

    wcsncpy(buf, path, (sizeof(buf) / sizeof(buf[0])) - 1);
    buf[(sizeof(buf) / sizeof(buf[0])) - 1] = L'\0';

    /* Walk forwards creating each level. Start at 3 to step over "C:\" and
     * avoid trying to create a drive root. */
    for (i = 3; buf[i] != L'\0'; ++i) {
        if (buf[i] == L'\\') {
            buf[i] = L'\0';
            if (!DirExists(buf))
                CreateDirectoryW(buf, NULL);
            buf[i] = L'\\';
        }
    }

    if (!CreateDirectoryW(buf, NULL))
        return DirExists(buf);

    return TRUE;
}

BOOL GetExeDir(wchar_t *dst, size_t cch)
{
    wchar_t *slash;

    if (GetModuleFileNameW(NULL, dst, (DWORD)cch) == 0)
        return FALSE;

    slash = wcsrchr(dst, L'\\');
    if (slash == NULL)
        return FALSE;

    *slash = L'\0';
    return TRUE;
}

BOOL GetDocumentsDir(wchar_t *dst, size_t cch)
{
    wchar_t buf[MAX_PATH];

    if (FAILED(SHGetFolderPathW(NULL, CSIDL_PERSONAL, NULL, SHGFP_TYPE_CURRENT, buf)))
        return FALSE;

    wcsncpy(dst, buf, cch - 1);
    dst[cch - 1] = L'\0';
    return TRUE;
}

BOOL GetDesktopDir(wchar_t *dst, size_t cch)
{
    wchar_t buf[MAX_PATH];

    if (FAILED(SHGetFolderPathW(NULL, CSIDL_DESKTOPDIRECTORY, NULL, SHGFP_TYPE_CURRENT, buf)))
        return FALSE;

    wcsncpy(dst, buf, cch - 1);
    dst[cch - 1] = L'\0';
    return TRUE;
}

BOOL GetStartMenuProgramsDir(wchar_t *dst, size_t cch)
{
    wchar_t buf[MAX_PATH];

    if (FAILED(SHGetFolderPathW(NULL, CSIDL_PROGRAMS, NULL, SHGFP_TYPE_CURRENT, buf)))
        return FALSE;

    wcsncpy(dst, buf, cch - 1);
    dst[cch - 1] = L'\0';
    return TRUE;
}

/* ---------------------------------------------------------------------------
 *  Guard rails - see the long note in Common.h
 * ------------------------------------------------------------------------ */

BOOL IsSafeRelativePath(const wchar_t *rel)
{
    size_t i;
    size_t len;

    if (rel == NULL)
        return FALSE;

    len = wcslen(rel);
    if (len == 0 || len >= MAX_PATH * 2)
        return FALSE;

    /* An absolute path, or one on another drive, is not a path "inside" the
     * game folder no matter what it is joined onto. */
    if (rel[0] == L'\\' || rel[0] == L'/')
        return FALSE;
    if (wcschr(rel, L':') != NULL)
        return FALSE;

    /* Wildcards would be taken literally by everything here, but a path
     * containing them is a sign something has gone wrong upstream. */
    if (wcspbrk(rel, L"*?\"<>|") != NULL)
        return FALSE;

    /* No step may be "..", which is the one thing that walks back out. A
     * trailing dot or space is refused too: Windows silently strips those,
     * so "Add-Ons " and "Add-Ons" would name the same folder while comparing
     * as different strings. */
    if (rel[len - 1] == L' ' || rel[len - 1] == L'.')
        return FALSE;

    /* Walked by remembering where the current step started rather than
     * copying it out. Nothing is stored between calls: this runs on Setup's
     * worker thread as well as its window thread, and a leftover from a
     * previous call is exactly the kind of thing that makes a safety check
     * pass when it should not. */
    {
        size_t start = 0;

        for (i = 0; i <= len; ++i) {
            size_t seg;

            if (i != len && rel[i] != L'\\' && rel[i] != L'/')
                continue;

            seg = i - start;

            if (seg == 0)
                return FALSE;                     /* "a\\b", or a trailing slash */
            if (seg == 2 && rel[start] == L'.' && rel[start + 1] == L'.')
                return FALSE;                     /* the step that walks back out */
            if (seg == 1 && rel[start] == L'.')
                return FALSE;                     /* "." goes nowhere */

            /* Windows drops a trailing dot or space from each step too, not
             * just from the end of the whole path. */
            if (rel[i - 1] == L' ' || rel[i - 1] == L'.')
                return FALSE;

            start = i + 1;
        }
    }

    return TRUE;
}

BOOL IsInsideFolder(const wchar_t *parent, const wchar_t *child)
{
    wchar_t fullParent[MAX_PATH * 4];
    wchar_t fullChild[MAX_PATH * 4];
    size_t plen;

    if (parent == NULL || child == NULL || parent[0] == L'\0' || child[0] == L'\0')
        return FALSE;

    if (GetFullPathNameW(parent, MAX_PATH * 4, fullParent, NULL) == 0)
        return FALSE;
    if (GetFullPathNameW(child, MAX_PATH * 4, fullChild, NULL) == 0)
        return FALSE;

    plen = wcslen(fullParent);
    while (plen > 0 && fullParent[plen - 1] == L'\\')
        fullParent[--plen] = L'\0';

    if (plen == 0 || wcslen(fullChild) <= plen + 1)
        return FALSE;                            /* same folder, or shorter */

    if (fullChild[plen] != L'\\')
        return FALSE;                            /* "C:\Game2" is not inside "C:\Game" */

    return CompareStringW(LOCALE_INVARIANT, NORM_IGNORECASE,
                          fullChild, (int)plen, fullParent, (int)plen) == CSTR_EQUAL;
}

BOOL IsProtectedGameItem(const wchar_t *rel)
{
    /* Blockland's own. Matched whole, not by first step, because
     * "Add-Ons\Client_MonsterRPG" is something this installer really does put
     * there while "Add-Ons" on its own never is. */
    static const wchar_t *const protectedItems[] = {
        L"Blockland.exe", L"Add-Ons", L"base", L"config", L"saves",
        L"shaders", L"screenshots", L"modules", L"serveronlycache",
        L".", L"..", NULL
    };
    int i;

    if (rel == NULL || rel[0] == L'\0')
        return TRUE;

    for (i = 0; protectedItems[i] != NULL; ++i)
        if (EqualsNoCase(rel, protectedItems[i]))
            return TRUE;

    return FALSE;
}

BOOL IsSafeShortcutPath(const wchar_t *path)
{
    wchar_t dir[MAX_PATH * 2];
    size_t len;

    if (path == NULL)
        return FALSE;

    len = wcslen(path);
    if (len < 5 || !EqualsNoCase(path + len - 4, L".lnk"))
        return FALSE;

    if (GetDesktopDir(dir, sizeof(dir) / sizeof(dir[0])) && IsInsideFolder(dir, path))
        return TRUE;
    if (GetStartMenuProgramsDir(dir, sizeof(dir) / sizeof(dir[0])) && IsInsideFolder(dir, path))
        return TRUE;

    return FALSE;
}

/* ---------------------------------------------------------------------------
 *  Deleting
 * ------------------------------------------------------------------------ */

/* One second of trying, in short steps. See the note in Common.h: the first
 * attempt failing does not mean the file is stuck, it usually means the last
 * handle to it has not finished closing yet. */
#define DELETE_TRIES  20
#define DELETE_WAIT   50

BOOL DeleteFileHard(const wchar_t *path)
{
    int i;

    for (i = 0; i < DELETE_TRIES; ++i) {
        DWORD attrs;

        if (!FileExists(path))
            return TRUE;

        attrs = GetFileAttributesW(path);
        if (attrs != INVALID_FILE_ATTRIBUTES && (attrs & FILE_ATTRIBUTE_READONLY))
            SetFileAttributesW(path, attrs & ~FILE_ATTRIBUTE_READONLY);

        if (DeleteFileW(path))
            return TRUE;

        Sleep(DELETE_WAIT);
    }

    return !FileExists(path);
}

static BOOL RemoveDirHard(const wchar_t *path)
{
    int i;

    for (i = 0; i < DELETE_TRIES; ++i) {
        if (!DirExists(path))
            return TRUE;
        if (RemoveDirectoryW(path))
            return TRUE;
        Sleep(DELETE_WAIT);
    }

    return !DirExists(path);
}

BOOL DeleteTree(const wchar_t *path)
{
    wchar_t pattern[MAX_PATH * 2];
    wchar_t child[MAX_PATH * 2];
    WIN32_FIND_DATAW fd;
    HANDLE h;
    BOOL ok = TRUE;

    if (!DirExists(path))
        return TRUE;

    PathJoin(pattern, sizeof(pattern) / sizeof(pattern[0]), path, L"*");

    h = FindFirstFileW(pattern, &fd);
    if (h == INVALID_HANDLE_VALUE)
        return FALSE;

    do {
        if (wcscmp(fd.cFileName, L".") == 0 || wcscmp(fd.cFileName, L"..") == 0)
            continue;

        PathJoin(child, sizeof(child) / sizeof(child[0]), path, fd.cFileName);

        if (fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) {
            if (!DeleteTree(child))
                ok = FALSE;
        } else {
            if (!DeleteFileHard(child))
                ok = FALSE;
        }
    } while (FindNextFileW(h, &fd));

    /* The search handle has to be closed before the folder it is reading can
     * be removed, so this cannot wait for the end of the function. */
    FindClose(h);

    if (!RemoveDirHard(path))
        ok = FALSE;

    return ok;
}

static BOOL IsSettingsFile(const wchar_t *name)
{
    size_t len = wcslen(name);
    return len > 4 && EqualsNoCase(name + len - 4, L".cfg");
}

BOOL DeleteTreeKeepSettings(const wchar_t *path)
{
    wchar_t pattern[MAX_PATH * 2];
    wchar_t child[MAX_PATH * 2];
    WIN32_FIND_DATAW fd;
    HANDLE h;
    BOOL ok = TRUE;

    if (!DirExists(path))
        return TRUE;

    PathJoin(pattern, sizeof(pattern) / sizeof(pattern[0]), path, L"*");

    h = FindFirstFileW(pattern, &fd);
    if (h == INVALID_HANDLE_VALUE)
        return FALSE;

    do {
        if (wcscmp(fd.cFileName, L".") == 0 || wcscmp(fd.cFileName, L"..") == 0)
            continue;

        PathJoin(child, sizeof(child) / sizeof(child[0]), path, fd.cFileName);

        if (fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) {
            if (!DeleteTreeKeepSettings(child))
                ok = FALSE;
        } else if (!IsSettingsFile(fd.cFileName)) {
            if (!DeleteFileHard(child))
                ok = FALSE;
        }
    } while (FindNextFileW(h, &fd));

    FindClose(h);

    /* Folders that held nothing but deleted files go too. RemoveDirectory
     * refuses anything with contents, so the ones still holding a .cfg simply
     * stay - which is the behaviour wanted, without having to check for it. */
    RemoveDirectoryW(path);

    return ok;
}

/* ---------------------------------------------------------------------------
 *  Copying
 * ------------------------------------------------------------------------ */

BOOL IsWorkingFile(const wchar_t *name)
{
    static const wchar_t *const suffixes[] = {
        L".prepair-backup",   /* build script's own backup copies */
        L".log",              /* rewritten every launch; a stale one is misleading */
        L".tmp",
        NULL
    };
    static const wchar_t *const exact[] = {
        L"Thumbs.db",
        L"desktop.ini",
        NULL
    };

    size_t nlen = wcslen(name);
    int i;

    for (i = 0; suffixes[i] != NULL; ++i) {
        size_t slen = wcslen(suffixes[i]);
        if (nlen >= slen && EqualsNoCase(name + nlen - slen, suffixes[i]))
            return TRUE;
    }
    for (i = 0; exact[i] != NULL; ++i) {
        if (EqualsNoCase(name, exact[i]))
            return TRUE;
    }

    /* Notes kept beside the source while it was being written. They are dated
     * text files and nothing in the game reads them. */
    if (nlen > 11 && name[0] == L'2' && name[1] == L'0' &&
        name[4] == L'-' && name[7] == L'-' && name[10] == L'-' &&
        nlen >= 4 && EqualsNoCase(name + nlen - 4, L".txt"))
        return TRUE;

    return FALSE;
}

/* Keeps a .cfg the player has already edited. Everything else is overwritten,
 * so reinstalling really does replace the mod. */
static BOOL KeepExisting(const wchar_t *dstFile)
{
    size_t len = wcslen(dstFile);

    if (len < 4 || !EqualsNoCase(dstFile + len - 4, L".cfg"))
        return FALSE;

    return FileExists(dstFile);
}

static unsigned CountTreeInner(const wchar_t *src)
{
    wchar_t pattern[MAX_PATH * 2];
    wchar_t child[MAX_PATH * 2];
    WIN32_FIND_DATAW fd;
    HANDLE h;
    unsigned n = 0;

    PathJoin(pattern, sizeof(pattern) / sizeof(pattern[0]), src, L"*");

    h = FindFirstFileW(pattern, &fd);
    if (h == INVALID_HANDLE_VALUE)
        return 0;

    do {
        if (wcscmp(fd.cFileName, L".") == 0 || wcscmp(fd.cFileName, L"..") == 0)
            continue;

        if (fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) {
            PathJoin(child, sizeof(child) / sizeof(child[0]), src, fd.cFileName);
            n += CountTreeInner(child);
        } else if (!IsWorkingFile(fd.cFileName)) {
            n++;
        }
    } while (FindNextFileW(h, &fd));

    FindClose(h);
    return n;
}

unsigned CountTree(const wchar_t *src)
{
    if (!DirExists(src))
        return 0;
    return CountTreeInner(src);
}

static BOOL CopyTreeInner(const wchar_t *src, const wchar_t *dst,
                          const wchar_t *relPrefix,
                          CopyCallback cb, void *user)
{
    wchar_t pattern[MAX_PATH * 2];
    wchar_t srcChild[MAX_PATH * 2];
    wchar_t dstChild[MAX_PATH * 2];
    wchar_t rel[MAX_PATH * 2];
    WIN32_FIND_DATAW fd;
    HANDLE h;
    BOOL ok = TRUE;

    if (!EnsureDir(dst))
        return FALSE;

    PathJoin(pattern, sizeof(pattern) / sizeof(pattern[0]), src, L"*");

    h = FindFirstFileW(pattern, &fd);
    if (h == INVALID_HANDLE_VALUE)
        return FALSE;

    do {
        if (wcscmp(fd.cFileName, L".") == 0 || wcscmp(fd.cFileName, L"..") == 0)
            continue;

        PathJoin(srcChild, sizeof(srcChild) / sizeof(srcChild[0]), src, fd.cFileName);
        PathJoin(dstChild, sizeof(dstChild) / sizeof(dstChild[0]), dst, fd.cFileName);

        if (relPrefix[0] != L'\0')
            _snwprintf(rel, sizeof(rel) / sizeof(rel[0]), L"%s\\%s", relPrefix, fd.cFileName);
        else
            _snwprintf(rel, sizeof(rel) / sizeof(rel[0]), L"%s", fd.cFileName);
        rel[(sizeof(rel) / sizeof(rel[0])) - 1] = L'\0';

        if (fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) {
            if (!CopyTreeInner(srcChild, dstChild, rel, cb, user)) {
                ok = FALSE;
                break;
            }
            continue;
        }

        if (IsWorkingFile(fd.cFileName))
            continue;

        if (!KeepExisting(dstChild)) {
            /* Clear read-only first; files that came off a CD or out of a
             * zip sometimes carry it, and CopyFile will not overwrite one. */
            if (FileExists(dstChild)) {
                DWORD a = GetFileAttributesW(dstChild);
                if (a != INVALID_FILE_ATTRIBUTES && (a & FILE_ATTRIBUTE_READONLY))
                    SetFileAttributesW(dstChild, a & ~FILE_ATTRIBUTE_READONLY);
            }
            if (!CopyFileW(srcChild, dstChild, FALSE)) {
                ok = FALSE;
                break;
            }
        }

        if (cb != NULL && !cb(user, rel)) {
            ok = FALSE;
            break;
        }
    } while (FindNextFileW(h, &fd));

    FindClose(h);
    return ok;
}

BOOL CopyTree(const wchar_t *src, const wchar_t *dst, CopyCallback cb, void *user)
{
    if (!DirExists(src))
        return FALSE;
    return CopyTreeInner(src, dst, L"", cb, user);
}

/* ---------------------------------------------------------------------------
 *  Is the game open?
 * ------------------------------------------------------------------------ */

/* The full path of a running process, or FALSE if it cannot be had.
 *
 * QueryFullProcessImageNameW is asked for by name rather than called directly
 * so this still builds and still runs whatever the toolchain headers think the
 * minimum Windows version is. If it is missing, the caller falls back to
 * matching on the file name alone. */
static BOOL PathOfProcess(DWORD pid, wchar_t *out, DWORD cch)
{
    typedef BOOL (WINAPI *QueryFullProcessImageNameW_t)(HANDLE, DWORD, LPWSTR, PDWORD);
    static QueryFullProcessImageNameW_t queryName = NULL;
    static BOOL looked = FALSE;

    HANDLE proc;
    BOOL ok = FALSE;

    if (!looked) {
        HMODULE k32 = GetModuleHandleW(L"kernel32.dll");
        if (k32 != NULL)
            queryName = (QueryFullProcessImageNameW_t)(void *)
                        GetProcAddress(k32, "QueryFullProcessImageNameW");
        looked = TRUE;
    }
    if (queryName == NULL)
        return FALSE;

    /* LIMITED_INFORMATION is enough to ask a process where it lives, and does
     * not need the rights that reading its memory would. */
    proc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
    if (proc == NULL)
        return FALSE;

    ok = queryName(proc, 0, out, &cch);
    CloseHandle(proc);
    return ok;
}

DWORD FindRunningGame(const wchar_t *gameDir)
{
    wchar_t wanted[MAX_PATH * 2];
    HANDLE snap;
    PROCESSENTRY32W entry;
    DWORD found = 0;

    if (gameDir == NULL || gameDir[0] == L'\0')
        return 0;

    PathJoin(wanted, sizeof(wanted) / sizeof(wanted[0]), gameDir, GAME_EXE);

    snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snap == INVALID_HANDLE_VALUE)
        return 0;

    entry.dwSize = sizeof(entry);
    if (Process32FirstW(snap, &entry)) {
        do {
            wchar_t path[MAX_PATH * 2];

            if (!EqualsNoCase(entry.szExeFile, GAME_EXE))
                continue;

            if (PathOfProcess(entry.th32ProcessID, path, MAX_PATH * 2)) {
                /* Two copies of Blockland is a normal thing to have. Only the
                 * one being installed into is any of our business. */
                if (!EqualsNoCase(path, wanted))
                    continue;
            }

            found = entry.th32ProcessID;
            break;
        } while (Process32NextW(snap, &entry));
    }

    CloseHandle(snap);
    return found;
}

static BOOL StillAlive(DWORD pid)
{
    HANDLE h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
    DWORD code = 0;
    BOOL alive;

    if (h == NULL)
        return FALSE;               /* gone, or not ours to ask about */

    alive = GetExitCodeProcess(h, &code) && code == STILL_ACTIVE;
    CloseHandle(h);
    return alive;
}

/* Posts WM_CLOSE to every top-level window the process owns. */
static BOOL CALLBACK CloseWindowsOf(HWND wnd, LPARAM param)
{
    DWORD owner = 0;

    GetWindowThreadProcessId(wnd, &owner);
    if (owner == (DWORD)param)
        PostMessageW(wnd, WM_CLOSE, 0, 0);

    return TRUE;
}

BOOL AskGameToClose(DWORD pid, DWORD waitMs)
{
    HANDLE proc;
    DWORD waited;

    if (pid == 0)
        return TRUE;

    proc = OpenProcess(SYNCHRONIZE, FALSE, pid);

    EnumWindows(CloseWindowsOf, (LPARAM)pid);

    if (proc == NULL) {
        /* No rights to wait on it, so fall back to asking whether it is still
         * in the process list at all. */
        for (waited = 0; waited < waitMs; waited += 250) {
            Sleep(250);
            if (!StillAlive(pid))
                return TRUE;
        }
        return !StillAlive(pid);
    }

    {
        DWORD rc = WaitForSingleObject(proc, waitMs);
        CloseHandle(proc);
        return rc == WAIT_OBJECT_0;
    }
}

BOOL EnsureGameClosed(HWND owner, const wchar_t *gameDir,
                      const wchar_t *title, const wchar_t *what)
{
    wchar_t message[1024];
    DWORD pid;
    int answer;

    pid = FindRunningGame(gameDir);
    if (pid == 0)
        return TRUE;

    _snwprintf(message, sizeof(message) / sizeof(message[0]),
        L"Blockland is open at the moment.\r\n"
        L"\r\n"
        L"MonsterRPG cannot be %s while the game is running. Windows will not "
        L"let anything change files that an open program is using, and the mod "
        L"files are exactly the ones Blockland has open.\r\n"
        L"\r\n"
        L"Close Blockland now?\r\n"
        L"\r\n"
        L"Yes  -  close it for me. Anything you have not saved in the game "
        L"will be lost, the same as closing it yourself.\r\n"
        L"No   -  leave it alone. Close it yourself and try again.",
        what);
    message[(sizeof(message) / sizeof(message[0])) - 1] = L'\0';

    answer = MessageBoxW(owner, message, title, MB_YESNO | MB_ICONWARNING);
    if (answer != IDYES)
        return FALSE;

    if (AskGameToClose(pid, 15000))
        return TRUE;

    /* It was asked and did not go. Forcing it from here would throw away
     * whatever the player was building, which is a worse outcome than making
     * them close it themselves. */
    MessageBoxW(owner,
        L"Blockland did not close.\r\n"
        L"\r\n"
        L"It may be asking you something, or be busy. Switch to it, close it "
        L"yourself, then try again.",
        title, MB_OK | MB_ICONINFORMATION);

    return FALSE;
}

/* ---------------------------------------------------------------------------
 *  Running only once at a time
 * ------------------------------------------------------------------------ */

HANDLE ClaimSingleInstance(const wchar_t *uniqueName, const wchar_t *windowTitle)
{
    HANDLE mutex;

    /* "Local\" keeps this to the signed-in user. Two different people signed
     * into the same machine are doing separate things to separate folders and
     * should not block each other. */
    mutex = CreateMutexW(NULL, TRUE, uniqueName);

    if (mutex == NULL)
        return NULL;

    if (GetLastError() == ERROR_ALREADY_EXISTS) {
        /* Dialogs all share this class name; the title is what tells them
         * apart. Bring the one already open forward so the answer to "nothing
         * happened when I double-clicked it" is visibly on screen. */
        HWND existing = FindWindowW(L"#32770", windowTitle);

        if (existing != NULL) {
            if (IsIconic(existing))
                ShowWindow(existing, SW_RESTORE);
            SetForegroundWindow(existing);
            FlashWindow(existing, TRUE);
        }

        CloseHandle(mutex);
        return NULL;
    }

    return mutex;
}

/* ---------------------------------------------------------------------------
 *  Text files
 * ------------------------------------------------------------------------ */

wchar_t *ReadTextFile(const wchar_t *path)
{
    HANDLE h;
    DWORD size, got = 0;
    char *raw;
    wchar_t *text = NULL;

    h = CreateFileW(path, GENERIC_READ, FILE_SHARE_READ, NULL,
                    OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);
    if (h == INVALID_HANDLE_VALUE)
        return NULL;

    size = GetFileSize(h, NULL);
    if (size == INVALID_FILE_SIZE || size > 16u * 1024u * 1024u) {
        CloseHandle(h);
        return NULL;
    }

    raw = (char *)LocalAlloc(LPTR, size + 2);
    if (raw == NULL) {
        CloseHandle(h);
        return NULL;
    }

    if (!ReadFile(h, raw, size, &got, NULL))
        got = 0;
    CloseHandle(h);

    if (got >= 2 && (unsigned char)raw[0] == 0xFF && (unsigned char)raw[1] == 0xFE) {
        /* Already UTF-16. */
        size_t chars = (got - 2) / 2;
        text = (wchar_t *)LocalAlloc(LPTR, (chars + 1) * sizeof(wchar_t));
        if (text != NULL) {
            memcpy(text, raw + 2, chars * sizeof(wchar_t));
            text[chars] = L'\0';
        }
    } else {
        char *start = raw;
        DWORD len = got;
        int need;

        if (len >= 3 && (unsigned char)start[0] == 0xEF &&
            (unsigned char)start[1] == 0xBB && (unsigned char)start[2] == 0xBF) {
            start += 3;
            len -= 3;
        }

        need = MultiByteToWideChar(CP_UTF8, 0, start, (int)len, NULL, 0);
        if (need > 0) {
            text = (wchar_t *)LocalAlloc(LPTR, ((size_t)need + 1) * sizeof(wchar_t));
            if (text != NULL) {
                MultiByteToWideChar(CP_UTF8, 0, start, (int)len, text, need);
                text[need] = L'\0';
            }
        } else {
            /* Not valid UTF-8 - fall back to the machine's own code page so a
             * README saved by an older editor still reads. */
            need = MultiByteToWideChar(CP_ACP, 0, start, (int)len, NULL, 0);
            if (need > 0) {
                text = (wchar_t *)LocalAlloc(LPTR, ((size_t)need + 1) * sizeof(wchar_t));
                if (text != NULL) {
                    MultiByteToWideChar(CP_ACP, 0, start, (int)len, text, need);
                    text[need] = L'\0';
                }
            }
        }
    }

    LocalFree(raw);
    return text;
}

BOOL WriteTextFileUtf8(const wchar_t *path, const wchar_t *text)
{
    static const unsigned char bom[3] = { 0xEF, 0xBB, 0xBF };
    HANDLE h;
    int need;
    char *utf8;
    DWORD written = 0;
    BOOL ok;

    need = WideCharToMultiByte(CP_UTF8, 0, text, -1, NULL, 0, NULL, NULL);
    if (need <= 0)
        return FALSE;

    utf8 = (char *)LocalAlloc(LPTR, need);
    if (utf8 == NULL)
        return FALSE;

    WideCharToMultiByte(CP_UTF8, 0, text, -1, utf8, need, NULL, NULL);

    h = CreateFileW(path, GENERIC_WRITE, 0, NULL,
                    CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (h == INVALID_HANDLE_VALUE) {
        LocalFree(utf8);
        return FALSE;
    }

    ok = WriteFile(h, bom, sizeof(bom), &written, NULL);
    if (ok)
        ok = WriteFile(h, utf8, (DWORD)(need - 1), &written, NULL);   /* -1: drop the terminator */

    CloseHandle(h);
    LocalFree(utf8);
    return ok;
}

/* ---------------------------------------------------------------------------
 *  README.txt
 * ------------------------------------------------------------------------ */

static void AddBuiltInRules(InstallPlan *plan)
{
    PlanRule *r;

    plan->count = 0;
    plan->catchAll = -1;

    r = &plan->rules[plan->count++];
    wcscpy(r->source, FOLDER_CLIENT);
    wcscpy(r->relative, L"Add-Ons");
    r->isCatchAll = FALSE;
    r->zipToo = TRUE;

    r = &plan->rules[plan->count++];
    r->source[0] = L'\0';
    r->relative[0] = L'\0';
    r->isCatchAll = TRUE;
    r->zipToo = FALSE;
    plan->catchAll = plan->count - 1;
}

/* Turns "Documents -> Blockland -> Add-Ons" into "Add-Ons".
 *
 * Everything up to and including the "Blockland" step is dropped, because the
 * player chooses that folder themselves and it may not be under Documents at
 * all. If no step is called Blockland, the first step is treated as the root
 * and the rest is the path. */
static void ChainToRelative(const wchar_t *chain, wchar_t *out, size_t cch)
{
    wchar_t work[MAX_PATH * 2];
    wchar_t segs[8][MAX_PATH];
    int nsegs = 0;
    int start = -1;
    int i;
    wchar_t *p;

    out[0] = L'\0';

    wcsncpy(work, chain, (sizeof(work) / sizeof(work[0])) - 1);
    work[(sizeof(work) / sizeof(work[0])) - 1] = L'\0';

    p = work;
    while (nsegs < 8) {
        wchar_t *arrow = wcsstr(p, L"->");
        if (arrow != NULL)
            *arrow = L'\0';

        wcsncpy(segs[nsegs], p, MAX_PATH - 1);
        segs[nsegs][MAX_PATH - 1] = L'\0';
        TrimInPlace(segs[nsegs]);
        if (segs[nsegs][0] != L'\0')
            nsegs++;

        if (arrow == NULL)
            break;
        p = arrow + 2;
    }

    for (i = 0; i < nsegs; ++i) {
        if (EqualsNoCase(segs[i], L"Blockland")) {
            start = i + 1;
            break;
        }
    }
    if (start < 0)
        start = 1;      /* no Blockland step: drop the root only */

    for (i = start; i < nsegs; ++i) {
        if (out[0] != L'\0') {
            wcsncat(out, L"\\", cch - wcslen(out) - 1);
        }
        wcsncat(out, segs[i], cch - wcslen(out) - 1);
    }
}

/* Given a line containing "->", finds where the arrow chain begins: the token
 * immediately before the first arrow. Everything before that is the subject,
 * i.e. which folder the line is talking about. */
static int FindChainStart(const wchar_t *line)
{
    const wchar_t *arrow = wcsstr(line, L"->");
    int i;

    if (arrow == NULL)
        return -1;

    i = (int)(arrow - line) - 1;
    while (i >= 0 && (line[i] == L' ' || line[i] == L'\t'))
        i--;
    while (i >= 0 && line[i] != L' ' && line[i] != L'\t')
        i--;

    return i + 1;
}

void ReadPlan(const wchar_t *dir, InstallPlan *plan)
{
    wchar_t path[MAX_PATH * 2];
    wchar_t *text;
    wchar_t *line;
    wchar_t *next;
    int i;

    memset(plan, 0, sizeof(*plan));
    plan->catchAll = -1;

    PathJoin(path, sizeof(path) / sizeof(path[0]), dir, L"README.txt");
    wcsncpy(plan->source, path, MAX_PATH - 1);

    text = ReadTextFile(path);
    if (text == NULL) {
        AddBuiltInRules(plan);
        plan->fromFile = FALSE;
        return;
    }

    /* First pass: the destination lines. */
    line = text;
    while (line != NULL && *line != L'\0' && plan->count < PLAN_MAX_RULES) {
        wchar_t buf[1024];
        int chainStart;

        next = wcschr(line, L'\n');
        if (next != NULL)
            *next = L'\0';

        wcsncpy(buf, line, (sizeof(buf) / sizeof(buf[0])) - 1);
        buf[(sizeof(buf) / sizeof(buf[0])) - 1] = L'\0';

        if (next != NULL) {
            *next = L'\n';
            next++;
        }
        line = next;

        TrimInPlace(buf);
        if (buf[0] == L'\0' || buf[0] == L'#')
            continue;

        chainStart = FindChainStart(buf);
        if (chainStart < 0)
            continue;

        {
            wchar_t subject[256];
            PlanRule *r = &plan->rules[plan->count];
            int n = chainStart < 255 ? chainStart : 255;

            wcsncpy(subject, buf, n);
            subject[n] = L'\0';
            TrimInPlace(subject);

            memset(r, 0, sizeof(*r));
            ChainToRelative(buf + chainStart, r->relative,
                            sizeof(r->relative) / sizeof(r->relative[0]));

            /* "Rest is Documents -> Blockland" is the catch-all. Matched on
             * the first word only, so a folder that merely happens to have
             * "rest" inside its name is still treated as a folder name. */
            {
                wchar_t first[64];
                wchar_t *sp = wcspbrk(subject, L" \t");
                size_t flen = (sp != NULL) ? (size_t)(sp - subject) : wcslen(subject);
                if (flen > 63) flen = 63;
                wcsncpy(first, subject, flen);
                first[flen] = L'\0';

                if (subject[0] == L'\0' || EqualsNoCase(first, L"Rest") ||
                    EqualsNoCase(first, L"Everything") || EqualsNoCase(first, L"Others") ||
                    EqualsNoCase(first, L"Other")) {
                    if (plan->catchAll >= 0)
                        continue;                /* first catch-all line wins */
                    r->isCatchAll = TRUE;
                    plan->catchAll = plan->count;
                } else {
                    /* Otherwise the first word is the folder name. */
                    wcsncpy(r->source, first,
                            (sizeof(r->source) / sizeof(r->source[0])) - 1);
                }
            }

            plan->count++;
            plan->fromFile = TRUE;
        }
    }

    /* Second pass: "... zipped too ..." lines, which need the rules to exist
     * before they can be attached to one. */
    line = text;
    while (line != NULL && *line != L'\0') {
        wchar_t buf[1024];

        next = wcschr(line, L'\n');
        if (next != NULL)
            *next = L'\0';

        wcsncpy(buf, line, (sizeof(buf) / sizeof(buf[0])) - 1);
        buf[(sizeof(buf) / sizeof(buf[0])) - 1] = L'\0';

        if (next != NULL) {
            *next = L'\n';
            next++;
        }
        line = next;

        if (!ContainsNoCase(buf, L"zip"))
            continue;

        for (i = 0; i < plan->count; ++i) {
            if (plan->rules[i].source[0] != L'\0' &&
                ContainsNoCase(buf, plan->rules[i].source))
                plan->rules[i].zipToo = TRUE;
        }
    }

    LocalFree(text);

    if (plan->count == 0) {
        AddBuiltInRules(plan);
        plan->fromFile = FALSE;
        return;
    }

    /* A README that names destinations but never says where everything else
     * goes still needs an answer for folders it did not mention. The top of
     * the game folder is the only sensible one. */
    if (plan->catchAll < 0 && plan->count < PLAN_MAX_RULES) {
        PlanRule *r = &plan->rules[plan->count];
        memset(r, 0, sizeof(*r));
        r->isCatchAll = TRUE;
        plan->catchAll = plan->count;
        plan->count++;
    }
}

const PlanRule *PlanFor(const InstallPlan *plan, const wchar_t *folderName)
{
    int i;

    for (i = 0; i < plan->count; ++i) {
        if (plan->rules[i].source[0] != L'\0' &&
            EqualsNoCase(plan->rules[i].source, folderName))
            return &plan->rules[i];
    }

    if (plan->catchAll >= 0)
        return &plan->rules[plan->catchAll];

    return NULL;
}

/* ---------------------------------------------------------------------------
 *  Shortcuts
 * ------------------------------------------------------------------------ */

BOOL CreateShortcut(const wchar_t *lnkPath, const wchar_t *target,
                    const wchar_t *workingDir, const wchar_t *description)
{
    IShellLinkW *link = NULL;
    IPersistFile *file = NULL;
    HRESULT hr;
    BOOL ok = FALSE;

    hr = CoCreateInstance(&CLSID_ShellLink, NULL, CLSCTX_INPROC_SERVER,
                          &IID_IShellLinkW, (void **)&link);
    if (FAILED(hr))
        return FALSE;

    IShellLinkW_SetPath(link, target);
    if (workingDir != NULL)
        IShellLinkW_SetWorkingDirectory(link, workingDir);
    if (description != NULL)
        IShellLinkW_SetDescription(link, description);
    IShellLinkW_SetIconLocation(link, target, 0);

    hr = IShellLinkW_QueryInterface(link, &IID_IPersistFile, (void **)&file);
    if (SUCCEEDED(hr)) {
        hr = IPersistFile_Save(file, lnkPath, TRUE);
        ok = SUCCEEDED(hr);
        IPersistFile_Release(file);
    }

    IShellLinkW_Release(link);
    return ok;
}
