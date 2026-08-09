/* ===========================================================================
 *  MonsterRPG Setup.exe
 *
 *  Copies the folders sitting beside it into a Blockland folder, puts a
 *  MonsterRPG.exe next to Blockland.exe to start the game with, and leaves an
 *  uninstaller and a plain-text list of everything it did.
 *
 *  WHERE THE DESTINATIONS COME FROM
 *
 *  Not from this program. They are read out of README.txt, which sits beside
 *  Setup.exe and says, in the same words a person would use:
 *
 *      Client_MonsterRPG Documents -> Blockland -> Add-Ons
 *      Rest is Documents -> Blockland
 *
 *  So adding another folder to the download means adding a line to a text
 *  file, not rebuilding this. Common.c has the reader; the "Documents ->
 *  Blockland" part is dropped because the player picks that folder here, and
 *  everything after it is used as the path inside it.
 *
 *  NOTHING IS WRITTEN OUTSIDE THE PLAYER'S OWN FILES
 *
 *  The game folder, a shortcut or two, and one per-user registry entry so the
 *  mod appears in Windows' own "Apps & features" list. That is the whole
 *  footprint, and it is why Setup never asks for an administrator password.
 * ======================================================================== */

#define COBJMACROS
#define WIN32_LEAN_AND_MEAN

#include "Common.h"
#include "Zip.h"
#include "Unzip.h"
#include "Resource.h"

#include <commctrl.h>
#include <shlobj.h>
#include <shellapi.h>
#include <objbase.h>
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

/* Worker thread to window. The strings are allocated by the worker and freed
 * by the window, which is the only safe way to hand text between threads
 * without either of them waiting for the other. */
#define WM_APP_LOG      (WM_APP + 1)
#define WM_APP_STATUS   (WM_APP + 2)
#define WM_APP_PROGRESS (WM_APP + 3)
#define WM_APP_DONE     (WM_APP + 4)

#define MAX_PAYLOAD  16
#define MAX_ENTRIES  128

/* Setup is three screens, shown one at a time in the same window.
 *
 * The first exists because of what this mod does: it loads code into the
 * running game. Anyone deciding whether to trust that deserves to read it
 * before being asked to choose anything, not to find it in small print beside
 * a checkbox. */
#define PAGE_INTRO    0
#define PAGE_OPTIONS  1
#define PAGE_WORK     2

/* What each folder in the download is, as far as the player's choice goes.
 *
 * Everything MonsterRPG needs to run is PART_ALWAYS and has no checkbox: there
 * is no useful game left without it, so offering the choice would only be a
 * way to end up with a broken install. The single exception is the audio,
 * which is a real choice because it is the only part that opens a network
 * port - see the note beside its box. */
typedef enum {
    PART_ALWAYS = 0,   /* installed no matter what - MonsterRPG needs it */
    PART_AUDIO         /* optional, and OFF until turned on */
} PartKind;

typedef struct {
    wchar_t name[64];
    wchar_t path[MAX_PATH * 2];
} Payload;

typedef struct {
    wchar_t kind[16];              /* FOLDER, FILE or SHORTCUT */
    wchar_t path[MAX_PATH * 2];    /* inside the game folder, or absolute for SHORTCUT */
} Entry;

typedef struct {
    HWND     dlg;
    HINSTANCE inst;

    wchar_t  srcDir[MAX_PATH * 2];
    wchar_t  gameDir[MAX_PATH * 2];

    /* Set only by the standalone build, when the folders had to be unpacked
     * out of this .exe into the temporary folder. Emptied again on the way
     * out, after the unpacked copy has been deleted. */
    wchar_t  unpackedTo[MAX_PATH * 2];

    /* What the search came up with when Setup opened, and whether it was a
     * real find. Kept so the wording at the top can go on being true after the
     * player edits the box: "we found this for you" is only honest while the
     * box still holds what was found. */
    wchar_t  autoPath[MAX_PATH * 2];
    BOOL     autoFound;

    InstallPlan plan;
    Payload  payload[MAX_PAYLOAD];
    int      payloadCount;

    BOOL     installAudio;
    BOOL     desktopShortcut;
    int      page;

    volatile LONG cancelled;
    HANDLE   worker;         /* kept so the process cannot end mid-copy */
    BOOL     installing;
    BOOL     finished;
    BOOL     succeeded;

    unsigned totalFiles;
    unsigned doneFiles;
    int      lastPercent;

    Entry    entries[MAX_ENTRIES];
    int      entryCount;

    /* What the last run of Setup left behind, read once at the start. Used to
     * clear out an older version before copying the new one over it, and then
     * again at the end to take away anything this run no longer installs. */
    Entry    previous[MAX_ENTRIES];
    int      previousCount;

    HFONT    titleFont;
    HBRUSH   whiteBrush;
    int      headerPx;
    int      footerPx;
} Ctx;

static Ctx g_ctx;

/* ---------------------------------------------------------------------------
 *  Talking to the window
 * ------------------------------------------------------------------------ */

static void PostText(HWND dlg, UINT msg, const wchar_t *fmt, ...)
{
    wchar_t buf[1024];
    wchar_t *copy;
    va_list ap;

    va_start(ap, fmt);
    _vsnwprintf(buf, (sizeof(buf) / sizeof(buf[0])) - 1, fmt, ap);
    va_end(ap);
    buf[(sizeof(buf) / sizeof(buf[0])) - 1] = L'\0';

    copy = (wchar_t *)LocalAlloc(LPTR, (wcslen(buf) + 1) * sizeof(wchar_t));
    if (copy == NULL)
        return;
    wcscpy(copy, buf);

    if (!PostMessageW(dlg, msg, 0, (LPARAM)copy))
        LocalFree(copy);
}

static void AppendLog(HWND edit, const wchar_t *line)
{
    int len = GetWindowTextLengthW(edit);

    SendMessageW(edit, EM_SETSEL, (WPARAM)len, (LPARAM)len);
    SendMessageW(edit, EM_REPLACESEL, FALSE, (LPARAM)line);
    SendMessageW(edit, EM_REPLACESEL, FALSE, (LPARAM)L"\r\n");
    SendMessageW(edit, EM_SCROLLCARET, 0, 0);
}

/* ---------------------------------------------------------------------------
 *  What is in the download
 * ------------------------------------------------------------------------ */

/* Every folder beside Setup.exe that is part of the download.
 *
 * Deliberately not a fixed list of what to include: dropping another mod
 * folder in and adding a line to README.txt is enough to have it installed.
 * What is listed instead is what to leave out, which is the repository's own
 * furniture. Those folders are not in the released zip, but they are there
 * when Setup is run from a checkout, and without this a developer testing a
 * build gets "release" and "Installer" copied into their Blockland folder. */
static BOOL IsRepositoryFolder(const wchar_t *name)
{
    static const wchar_t *const notPayload[] = {
        L"Installer",     /* Setup's own source */
        L"release",       /* where make-release.ps1 puts the zip */
        L".git",
        L".github",
        L".vs",
        NULL
    };
    int i;

    /* Anything starting with a dot is tooling, not game content. */
    if (name[0] == L'.')
        return TRUE;

    for (i = 0; notPayload[i] != NULL; ++i)
        if (EqualsNoCase(name, notPayload[i]))
            return TRUE;

    return FALSE;
}

static int FindPayload(const wchar_t *srcDir, Payload *out, int max)
{
    wchar_t pattern[MAX_PATH * 2];
    WIN32_FIND_DATAW fd;
    HANDLE h;
    int n = 0;

    PathJoin(pattern, sizeof(pattern) / sizeof(pattern[0]), srcDir, L"*");

    h = FindFirstFileW(pattern, &fd);
    if (h == INVALID_HANDLE_VALUE)
        return 0;

    do {
        if (!(fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY))
            continue;
        if (fd.dwFileAttributes & (FILE_ATTRIBUTE_HIDDEN | FILE_ATTRIBUTE_SYSTEM))
            continue;
        if (wcscmp(fd.cFileName, L".") == 0 || wcscmp(fd.cFileName, L"..") == 0)
            continue;
        if (IsRepositoryFolder(fd.cFileName))
            continue;
        if (n >= max)
            break;

        wcsncpy(out[n].name, fd.cFileName, 63);
        out[n].name[63] = L'\0';
        PathJoin(out[n].path, sizeof(out[n].path) / sizeof(out[n].path[0]),
                 srcDir, fd.cFileName);
        n++;
    } while (FindNextFileW(h, &fd));

    FindClose(h);
    return n;
}

/* ---------------------------------------------------------------------------
 *  Getting hold of the folders to install
 *
 *  There are two ways Setup is shipped, and this is the only place that cares
 *  which one is running:
 *
 *    * Beside the folders. What you get from the source download. The folders
 *      sit next to Setup.exe and are read straight off the disk.
 *
 *    * On its own. One .exe with the whole download zipped up inside it, for
 *      people who just want to click one thing. The zip is unpacked into the
 *      temporary folder and then everything downstream behaves identically,
 *      because all it sees is a folder full of folders.
 *
 *  Folders on the disk win when both are available, so that a developer
 *  testing a build gets their working copy and not a stale copy baked into
 *  the .exe months ago.
 * ------------------------------------------------------------------------ */

static BOOL UnpackProgress(void *user, const wchar_t *nameInZip)
{
    (void)user; (void)nameInZip;
    return TRUE;
}

static BOOL PreparePayload(Ctx *c)
{
    HRSRC found;
    HGLOBAL loaded;
    const void *zip;
    DWORD zipSize;
    wchar_t tempRoot[MAX_PATH];
    wchar_t folder[64];
    HCURSOR busy;

    c->payloadCount = FindPayload(c->srcDir, c->payload, MAX_PAYLOAD);
    if (c->payloadCount > 0)
        return TRUE;

    found = FindResourceW(c->inst, MAKEINTRESOURCEW(IDR_PAYLOAD_ZIP), RT_RCDATA);
    if (found == NULL)
        return FALSE;               /* ordinary build, and nothing beside it */

    zipSize = SizeofResource(c->inst, found);
    loaded  = LoadResource(c->inst, found);
    if (loaded == NULL || zipSize == 0)
        return FALSE;

    zip = LockResource(loaded);
    if (zip == NULL)
        return FALSE;

    if (GetTempPathW(MAX_PATH, tempRoot) == 0)
        return FALSE;

    _snwprintf(folder, sizeof(folder) / sizeof(folder[0]),
               L"MonsterRPG-setup-%lu", GetCurrentProcessId());
    folder[(sizeof(folder) / sizeof(folder[0])) - 1] = L'\0';
    PathJoin(c->unpackedTo, sizeof(c->unpackedTo) / sizeof(c->unpackedTo[0]),
             tempRoot, folder);

    /* This runs before there is a window to put a progress bar in, and takes
     * a few seconds. The busy cursor is the only thing there is to say so. */
    busy = LoadCursorW(NULL, IDC_WAIT);
    if (busy != NULL)
        SetCursor(busy);

    if (!UnzipToFolder(zip, zipSize, c->unpackedTo, UnpackProgress, c)) {
        DeleteTree(c->unpackedTo);
        c->unpackedTo[0] = L'\0';
        SetCursor(LoadCursorW(NULL, IDC_ARROW));
        return FALSE;
    }

    SetCursor(LoadCursorW(NULL, IDC_ARROW));

    wcsncpy(c->srcDir, c->unpackedTo, (sizeof(c->srcDir) / sizeof(c->srcDir[0])) - 1);
    c->srcDir[(sizeof(c->srcDir) / sizeof(c->srcDir[0])) - 1] = L'\0';

    c->payloadCount = FindPayload(c->srcDir, c->payload, MAX_PAYLOAD);
    return c->payloadCount > 0;
}

/* ---------------------------------------------------------------------------
 *  Finding Blockland
 * ------------------------------------------------------------------------ */

/* How sure we are that a folder is the Blockland folder.
 *
 * A real one has Blockland.exe and an Add-Ons folder sitting together. Just
 * the .exe is still worth offering - a copy can be missing Add-Ons and Setup
 * makes one anyway - but it loses to a folder that has both, which is what
 * decides things when the game is installed twice, say once from Steam and
 * once by hand. */
typedef enum {
    GAME_NO = 0,        /* not the game folder */
    GAME_EXE_ONLY = 1,  /* Blockland.exe, but no Add-Ons beside it */
    GAME_FULL = 2       /* both - this is the one */
} GameCheck;

static GameCheck CheckGameFolder(const wchar_t *dir)
{
    wchar_t path[MAX_PATH * 2];

    if (dir == NULL || dir[0] == L'\0')
        return GAME_NO;

    PathJoin(path, sizeof(path) / sizeof(path[0]), dir, GAME_EXE);
    if (!FileExists(path))
        return GAME_NO;

    PathJoin(path, sizeof(path) / sizeof(path[0]), dir, L"Add-Ons");
    return DirExists(path) ? GAME_FULL : GAME_EXE_ONLY;
}

static BOOL LooksLikeGameFolder(const wchar_t *dir)
{
    return CheckGameFolder(dir) != GAME_NO;
}

/* Keeps the best folder seen so far while several are being tried. */
typedef struct {
    wchar_t   path[MAX_PATH * 2];
    GameCheck rank;
} Candidate;

static BOOL Consider(Candidate *best, const wchar_t *dir)
{
    GameCheck rank = CheckGameFolder(dir);

    if (rank > best->rank) {
        best->rank = rank;
        wcsncpy(best->path, dir, (sizeof(best->path) / sizeof(best->path[0])) - 1);
        best->path[(sizeof(best->path) / sizeof(best->path[0])) - 1] = L'\0';
    }

    /* Nothing beats having both, so there is no reason to keep looking. */
    return best->rank == GAME_FULL;
}

/* Looks for a Blockland.exe in the folders under root.
 *
 * Bounded on purpose, in two ways: it goes at most `depth` folders down, and
 * it gives up after `budget` folders whatever happens. Searching a whole disk
 * for a file is how installers end up sitting on a spinning cursor for two
 * minutes, and the answer is nearly always within arm's reach of Documents. */
static BOOL SearchForGame(const wchar_t *root, int depth, int *budget,
                          Candidate *best)
{
    wchar_t pattern[MAX_PATH * 2];
    WIN32_FIND_DATAW fd;
    HANDLE h;
    BOOL found = FALSE;

    if (depth <= 0 || *budget <= 0 || !DirExists(root))
        return FALSE;

    PathJoin(pattern, sizeof(pattern) / sizeof(pattern[0]), root, L"*");

    h = FindFirstFileW(pattern, &fd);
    if (h == INVALID_HANDLE_VALUE)
        return FALSE;

    do {
        wchar_t child[MAX_PATH * 2];

        if (*budget <= 0)
            break;
        if (!(fd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY))
            continue;
        if (fd.dwFileAttributes & (FILE_ATTRIBUTE_HIDDEN | FILE_ATTRIBUTE_SYSTEM |
                                   FILE_ATTRIBUTE_REPARSE_POINT))
            continue;
        if (wcscmp(fd.cFileName, L".") == 0 || wcscmp(fd.cFileName, L"..") == 0)
            continue;

        (*budget)--;
        PathJoin(child, sizeof(child) / sizeof(child[0]), root, fd.cFileName);

        if (Consider(best, child)) {
            found = TRUE;
            break;
        }

        if (SearchForGame(child, depth - 1, budget, best)) {
            found = TRUE;
            break;
        }
    } while (FindNextFileW(h, &fd));

    FindClose(h);
    return found;
}

/* Where Steam itself is, according to Steam. */
static BOOL GetSteamDir(wchar_t *dst, size_t cch)
{
    static const struct { HKEY root; const wchar_t *path; const wchar_t *value; } places[] = {
        { HKEY_CURRENT_USER,  L"Software\\Valve\\Steam",              L"SteamPath" },
        { HKEY_LOCAL_MACHINE, L"SOFTWARE\\WOW6432Node\\Valve\\Steam", L"InstallPath" },
        { HKEY_LOCAL_MACHINE, L"SOFTWARE\\Valve\\Steam",              L"InstallPath" }
    };
    int i;

    for (i = 0; i < (int)(sizeof(places) / sizeof(places[0])); ++i) {
        HKEY key;
        DWORD type = 0;
        DWORD bytes = (DWORD)(cch * sizeof(wchar_t));

        if (RegOpenKeyExW(places[i].root, places[i].path, 0, KEY_READ, &key) != ERROR_SUCCESS)
            continue;

        if (RegQueryValueExW(key, places[i].value, NULL, &type,
                             (BYTE *)dst, &bytes) == ERROR_SUCCESS &&
            type == REG_SZ && bytes > sizeof(wchar_t)) {
            RegCloseKey(key);
            dst[cch - 1] = L'\0';
            /* Steam writes forward slashes in this one. */
            for (; *dst != L'\0'; ++dst)
                if (*dst == L'/') *dst = L'\\';
            return TRUE;
        }

        RegCloseKey(key);
    }

    return FALSE;
}

/* The shapes a Blockland folder takes on a drive that is not the one Windows
 * is on. Steam games move between drives constantly, so every fixed drive is
 * checked rather than just C. This is a handful of file lookups per drive,
 * not a search. */
static void ConsiderDriveLayouts(Candidate *best)
{
    static const wchar_t *const shapes[] = {
        L"Program Files (x86)\\Steam\\steamapps\\common\\Blockland",
        L"Program Files\\Steam\\steamapps\\common\\Blockland",
        L"Steam\\steamapps\\common\\Blockland",
        L"SteamLibrary\\steamapps\\common\\Blockland",
        L"Games\\Steam\\steamapps\\common\\Blockland",
        L"Program Files (x86)\\Blockland",
        L"Program Files\\Blockland",
        L"Games\\Blockland",
        L"Blockland",
        NULL
    };
    DWORD drives = GetLogicalDrives();
    int letter, i;

    for (letter = 0; letter < 26; ++letter) {
        wchar_t root[8];

        if (!(drives & (1u << letter)))
            continue;

        _snwprintf(root, sizeof(root) / sizeof(root[0]), L"%c:\\", (wchar_t)(L'A' + letter));
        root[(sizeof(root) / sizeof(root[0])) - 1] = L'\0';

        /* Fixed disks only. Spinning up a DVD drive or waiting on a
         * disconnected network share would stall the window before it opens. */
        if (GetDriveTypeW(root) != DRIVE_FIXED)
            continue;

        for (i = 0; shapes[i] != NULL; ++i) {
            wchar_t candidate[MAX_PATH * 2];
            PathJoin(candidate, sizeof(candidate) / sizeof(candidate[0]), root, shapes[i]);
            if (Consider(best, candidate))
                return;
        }
    }
}

/* Works out which folder to put in the box when Setup opens.
 *
 * Everywhere the game is normally installed gets tried, and the best answer
 * wins rather than the first one - so a machine with both a Steam copy and a
 * hand-installed copy ends up on whichever is actually a working game folder.
 * If both are, the order below decides, and Documents comes first because that
 * is what README.txt describes. */
/* TRUE if the game was actually found. FALSE means the box has been filled in
 * with the usual place as a starting point and the player has to point Setup
 * at their own folder - which the window then says plainly, rather than
 * showing a path that looks found but is not. */
static BOOL GuessGameFolder(wchar_t *dst, size_t cch)
{
    Candidate best;
    wchar_t docs[MAX_PATH * 2];
    wchar_t desktop[MAX_PATH * 2];
    wchar_t candidate[MAX_PATH * 2];
    int budget;

    memset(&best, 0, sizeof(best));
    best.rank = GAME_NO;

    /* 1. Documents \ Blockland, which is what README.txt describes. */
    if (GetDocumentsDir(docs, sizeof(docs) / sizeof(docs[0]))) {
        PathJoin(candidate, sizeof(candidate) / sizeof(candidate[0]), docs, L"Blockland");
        if (Consider(&best, candidate))
            goto done;
    }

    /* 2. Steam, where the other half of players have it. */
    if (GetSteamDir(candidate, sizeof(candidate) / sizeof(candidate[0]))) {
        PathJoin(candidate, sizeof(candidate) / sizeof(candidate[0]), candidate,
                 L"steamapps\\common\\Blockland");
        if (Consider(&best, candidate))
            goto done;
    }

    /* 3. The usual folder shapes, on every fixed drive. */
    ConsiderDriveLayouts(&best);
    if (best.rank == GAME_FULL)
        goto done;

    /* 4. The download is often unzipped into the game folder, or right beside
     *    it. */
    if (Consider(&best, g_ctx.srcDir))
        goto done;
    {
        wchar_t parent[MAX_PATH * 2];

        PathJoin(candidate, sizeof(candidate) / sizeof(candidate[0]), g_ctx.srcDir, L"..");
        if (GetFullPathNameW(candidate, (DWORD)(sizeof(parent) / sizeof(parent[0])),
                             parent, NULL) != 0) {
            if (Consider(&best, parent))
                goto done;
            budget = 60;
            if (SearchForGame(parent, 2, &budget, &best))
                goto done;
        }
    }

    /* 5. Still nothing certain, so go looking. Plenty of people keep the game
     *    a folder or two inside Documents rather than directly in it, and
     *    hunting for your own game folder is exactly the sort of thing an
     *    installer should be doing for you. */
    if (GetDocumentsDir(docs, sizeof(docs) / sizeof(docs[0]))) {
        budget = 400;
        if (SearchForGame(docs, 3, &budget, &best))
            goto done;
    }
    if (GetDesktopDir(desktop, sizeof(desktop) / sizeof(desktop[0]))) {
        budget = 150;
        if (SearchForGame(desktop, 2, &budget, &best))
            goto done;
    }

done:
    if (best.rank != GAME_NO) {
        wcsncpy(dst, best.path, cch - 1);
        dst[cch - 1] = L'\0';
        return TRUE;
    }

    /* Nothing found anywhere. Show the usual place rather than an empty box,
     * so there is something to correct instead of something to invent. */
    if (GetDocumentsDir(docs, sizeof(docs) / sizeof(docs[0]))) {
        PathJoin(dst, cch, docs, L"Blockland");
    } else {
        wcsncpy(dst, L"C:\\Blockland", cch - 1);
        dst[cch - 1] = L'\0';
    }

    return FALSE;
}

static int CALLBACK BrowseInit(HWND wnd, UINT msg, LPARAM param, LPARAM data)
{
    (void)param;
    if (msg == BFFM_INITIALIZED)
        SendMessageW(wnd, BFFM_SETSELECTION, TRUE, data);
    return 0;
}

static BOOL AskForFolder(HWND owner, wchar_t *path, size_t cch)
{
    BROWSEINFOW bi;
    LPITEMIDLIST idl;
    wchar_t chosen[MAX_PATH];

    ZeroMemory(&bi, sizeof(bi));
    bi.hwndOwner = owner;
    bi.lpszTitle =
        L"Find your Blockland folder.\r\n\r\n"
        L"It is the folder with Blockland.exe and the Add-Ons folder inside it, "
        L"side by side. Pick that folder itself, not Add-Ons and not Blockland.exe.";
    bi.ulFlags   = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE;
    bi.lpfn      = BrowseInit;
    bi.lParam    = (LPARAM)path;

    idl = SHBrowseForFolderW(&bi);
    if (idl == NULL)
        return FALSE;

    if (!SHGetPathFromIDListW(idl, chosen)) {
        CoTaskMemFree(idl);
        return FALSE;
    }
    CoTaskMemFree(idl);

    wcsncpy(path, chosen, cch - 1);
    path[cch - 1] = L'\0';
    return TRUE;
}

static BOOL CanWriteTo(const wchar_t *dir)
{
    wchar_t probe[MAX_PATH * 2];
    HANDLE h;

    PathJoin(probe, sizeof(probe) / sizeof(probe[0]), dir, L"monsterrpg-write-test.tmp");

    h = CreateFileW(probe, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS,
                    FILE_ATTRIBUTE_TEMPORARY | FILE_FLAG_DELETE_ON_CLOSE, NULL);
    if (h == INVALID_HANDLE_VALUE)
        return FALSE;

    CloseHandle(h);
    return TRUE;
}

/* ---------------------------------------------------------------------------
 *  The install log
 *
 *  Written last, in plain English, and read back by the uninstaller. It is the
 *  only record of what was put where, which is why it is a file a person can
 *  open and check rather than something hidden in the registry.
 * ------------------------------------------------------------------------ */

static void RecordEntry(Ctx *c, const wchar_t *kind, const wchar_t *path)
{
    int i;

    for (i = 0; i < c->entryCount; ++i) {
        if (EqualsNoCase(c->entries[i].kind, kind) &&
            EqualsNoCase(c->entries[i].path, path))
            return;
    }

    if (c->entryCount >= MAX_ENTRIES)
        return;

    wcsncpy(c->entries[c->entryCount].kind, kind, 15);
    c->entries[c->entryCount].kind[15] = L'\0';
    wcsncpy(c->entries[c->entryCount].path, path, MAX_PATH * 2 - 1);
    c->entries[c->entryCount].path[MAX_PATH * 2 - 1] = L'\0';
    c->entryCount++;
}

static void AppendToBuffer(wchar_t **buf, size_t *cch, size_t *used, const wchar_t *text)
{
    size_t len = wcslen(text);

    if (*used + len + 1 > *cch) {
        size_t grow = (*cch * 2) + len + 1024;
        wchar_t *bigger = (wchar_t *)LocalAlloc(LPTR, grow * sizeof(wchar_t));
        if (bigger == NULL)
            return;
        memcpy(bigger, *buf, *used * sizeof(wchar_t));
        LocalFree(*buf);
        *buf = bigger;
        *cch = grow;
    }

    memcpy(*buf + *used, text, len * sizeof(wchar_t));
    *used += len;
    (*buf)[*used] = L'\0';
}

static BOOL WriteInstallLog(Ctx *c)
{
    wchar_t path[MAX_PATH * 2];
    wchar_t line[MAX_PATH * 3];
    SYSTEMTIME now;
    wchar_t *buf;
    size_t cch = 4096, used = 0;
    int i;
    BOOL ok;

    buf = (wchar_t *)LocalAlloc(LPTR, cch * sizeof(wchar_t));
    if (buf == NULL)
        return FALSE;

    GetLocalTime(&now);

    _snwprintf(line, sizeof(line) / sizeof(line[0]),
        L"MonsterRPG install log\r\n"
        L"=====================================================================\r\n"
        L"\r\n"
        L"Written by MonsterRPG Setup on %04d-%02d-%02d at %02d:%02d.\r\n"
        L"\r\n"
        L"Game folder:\r\n"
        L"    %s\r\n"
        L"\r\n"
        L"Everything listed below was put there by Setup. \"%s\"\r\n"
        L"in the same folder reads this list and removes exactly these things and\r\n"
        L"nothing else. If you delete this file, the uninstaller has nothing to go\r\n"
        L"on and you would have to remove the folders by hand.\r\n"
        L"\r\n"
        L"FOLDER and FILE lines are inside the game folder above.\r\n"
        L"SHORTCUT lines are the full path to a shortcut.\r\n"
        L"\r\n",
        now.wYear, now.wMonth, now.wDay, now.wHour, now.wMinute, c->gameDir,
        UNINSTALLER_NAME);
    line[(sizeof(line) / sizeof(line[0])) - 1] = L'\0';
    AppendToBuffer(&buf, &cch, &used, line);

    for (i = 0; i < c->entryCount; ++i) {
        _snwprintf(line, sizeof(line) / sizeof(line[0]), L"%s\t%s\r\n",
                   c->entries[i].kind, c->entries[i].path);
        line[(sizeof(line) / sizeof(line[0])) - 1] = L'\0';
        AppendToBuffer(&buf, &cch, &used, line);
    }

    AppendToBuffer(&buf, &cch, &used,
        L"\r\n"
        L"Settings files (.cfg) are never overwritten by Setup, so anything you\r\n"
        L"changed in them survives reinstalling.\r\n");

    PathJoin(path, sizeof(path) / sizeof(path[0]), c->gameDir, MANIFEST_NAME);
    ok = WriteTextFileUtf8(path, buf);
    LocalFree(buf);

    return ok;
}

/* Reads the list from a previous run so anything that install no longer
 * includes - the audio folder, if it has just been turned off - can be taken
 * away rather than left behind. */
static int ReadPreviousLog(const wchar_t *gameDir, Entry *out, int max)
{
    wchar_t path[MAX_PATH * 2];
    wchar_t *text;
    wchar_t *line;
    int n = 0;

    PathJoin(path, sizeof(path) / sizeof(path[0]), gameDir, MANIFEST_NAME);

    text = ReadTextFile(path);
    if (text == NULL)
        return 0;

    line = text;
    while (line != NULL && *line != L'\0' && n < max) {
        wchar_t buf[MAX_PATH * 3];
        wchar_t *next = wcschr(line, L'\n');
        wchar_t *tab;

        if (next != NULL) *next = L'\0';
        wcsncpy(buf, line, (sizeof(buf) / sizeof(buf[0])) - 1);
        buf[(sizeof(buf) / sizeof(buf[0])) - 1] = L'\0';
        if (next != NULL) { *next = L'\n'; next++; }
        line = next;

        tab = wcschr(buf, L'\t');
        if (tab == NULL)
            continue;
        *tab = L'\0';

        TrimInPlace(buf);
        TrimInPlace(tab + 1);

        if (!EqualsNoCase(buf, L"FOLDER") && !EqualsNoCase(buf, L"FILE") &&
            !EqualsNoCase(buf, L"SHORTCUT"))
            continue;
        if (tab[1] == L'\0')
            continue;

        wcsncpy(out[n].kind, buf, 15);
        out[n].kind[15] = L'\0';
        wcsncpy(out[n].path, tab + 1, MAX_PATH * 2 - 1);
        out[n].path[MAX_PATH * 2 - 1] = L'\0';
        n++;
    }

    LocalFree(text);
    return n;
}

/* ---------------------------------------------------------------------------
 *  Windows' own uninstall list
 * ------------------------------------------------------------------------ */

static void WriteUninstallKey(Ctx *c)
{
    HKEY key;
    wchar_t uninstaller[MAX_PATH * 2];
    wchar_t quoted[MAX_PATH * 2 + 4];
    DWORD flags = 1;

    if (RegCreateKeyExW(HKEY_CURRENT_USER, REG_UNINSTALL_PATH, 0, NULL, 0,
                        KEY_WRITE, NULL, &key, NULL) != ERROR_SUCCESS)
        return;

    PathJoin(uninstaller, sizeof(uninstaller) / sizeof(uninstaller[0]),
             c->gameDir, UNINSTALLER_NAME);
    _snwprintf(quoted, sizeof(quoted) / sizeof(quoted[0]), L"\"%s\"", uninstaller);
    quoted[(sizeof(quoted) / sizeof(quoted[0])) - 1] = L'\0';

    RegSetValueExW(key, L"DisplayName", 0, REG_SZ,
                   (const BYTE *)L"MonsterRPG for Blockland",
                   (DWORD)(wcslen(L"MonsterRPG for Blockland") + 1) * sizeof(wchar_t));
    RegSetValueExW(key, L"DisplayVersion", 0, REG_SZ, (const BYTE *)VER_STRING_W,
                   (DWORD)(wcslen(VER_STRING_W) + 1) * sizeof(wchar_t));
    RegSetValueExW(key, L"Publisher", 0, REG_SZ, (const BYTE *)L"MonsterRPG",
                   (DWORD)(wcslen(L"MonsterRPG") + 1) * sizeof(wchar_t));
    RegSetValueExW(key, L"InstallLocation", 0, REG_SZ, (const BYTE *)c->gameDir,
                   (DWORD)(wcslen(c->gameDir) + 1) * sizeof(wchar_t));
    RegSetValueExW(key, L"UninstallString", 0, REG_SZ, (const BYTE *)quoted,
                   (DWORD)(wcslen(quoted) + 1) * sizeof(wchar_t));
    RegSetValueExW(key, L"DisplayIcon", 0, REG_SZ, (const BYTE *)uninstaller,
                   (DWORD)(wcslen(uninstaller) + 1) * sizeof(wchar_t));
    RegSetValueExW(key, L"NoModify", 0, REG_DWORD, (const BYTE *)&flags, sizeof(flags));
    RegSetValueExW(key, L"NoRepair", 0, REG_DWORD, (const BYTE *)&flags, sizeof(flags));

    RegCloseKey(key);
}

/* ---------------------------------------------------------------------------
 *  Unpacking the two programs Setup carries
 * ------------------------------------------------------------------------ */

static BOOL WriteResourceToFile(HINSTANCE inst, int id, const wchar_t *path)
{
    HRSRC found;
    HGLOBAL loaded;
    const void *data;
    DWORD size, written = 0;
    HANDLE file;
    BOOL ok;

    found = FindResourceW(inst, MAKEINTRESOURCEW(id), RT_RCDATA);
    if (found == NULL)
        return FALSE;

    size = SizeofResource(inst, found);
    loaded = LoadResource(inst, found);
    if (loaded == NULL || size == 0)
        return FALSE;

    data = LockResource(loaded);
    if (data == NULL)
        return FALSE;

    if (FileExists(path)) {
        DWORD attrs = GetFileAttributesW(path);
        if (attrs != INVALID_FILE_ATTRIBUTES && (attrs & FILE_ATTRIBUTE_READONLY))
            SetFileAttributesW(path, attrs & ~FILE_ATTRIBUTE_READONLY);
    }

    file = CreateFileW(path, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS,
                       FILE_ATTRIBUTE_NORMAL, NULL);
    if (file == INVALID_HANDLE_VALUE)
        return FALSE;

    ok = WriteFile(file, data, size, &written, NULL) && written == size;
    CloseHandle(file);

    if (!ok)
        DeleteFileW(path);

    return ok;
}

/* ---------------------------------------------------------------------------
 *  The install itself, on its own thread so the window keeps drawing
 * ------------------------------------------------------------------------ */

static BOOL FileProgress(void *user, const wchar_t *relative)
{
    Ctx *c = (Ctx *)user;
    int percent;

    (void)relative;

    if (InterlockedCompareExchange(&c->cancelled, 0, 0) != 0)
        return FALSE;

    c->doneFiles++;

    percent = (c->totalFiles > 0)
            ? (int)((c->doneFiles * 100ull) / c->totalFiles)
            : 0;
    if (percent > 100) percent = 100;

    if (percent != c->lastPercent) {
        c->lastPercent = percent;
        PostMessageW(c->dlg, WM_APP_PROGRESS, (WPARAM)percent, 0);
    }

    return TRUE;
}

static PartKind KindOf(const wchar_t *folderName)
{
    if (EqualsNoCase(folderName, FOLDER_AUDIO))
        return PART_AUDIO;
    return PART_ALWAYS;
}

static BOOL IsWanted(Ctx *c, const wchar_t *folderName)
{
    if (KindOf(folderName) == PART_AUDIO)
        return c->installAudio;
    return TRUE;
}

/* Turns "Add-Ons" plus "Client_MonsterRPG" into "Add-Ons\Client_MonsterRPG",
 * and an empty destination into just the folder name. */
static void RelativeFor(const PlanRule *rule, const wchar_t *name,
                        wchar_t *out, size_t cch)
{
    out[0] = L'\0';
    if (rule != NULL && rule->relative[0] != L'\0') {
        wcsncpy(out, rule->relative, cch - 1);
        out[cch - 1] = L'\0';
    }
    PathJoin(out, cch, out, name);
}

static void DescribeDestination(const PlanRule *rule, wchar_t *out, size_t cch)
{
    if (rule == NULL || rule->relative[0] == L'\0')
        _snwprintf(out, cch, L"the Blockland folder");
    else
        _snwprintf(out, cch, L"Blockland\\%s", rule->relative);
    out[cch - 1] = L'\0';
}

/* TRUE if the last run of Setup put this exact thing there. Only those are
 * cleared out or removed - a folder somebody else put in the game folder is
 * never touched, whatever it is called. */
static BOOL WasInstalledBefore(Ctx *c, const wchar_t *kind, const wchar_t *path)
{
    int i;

    for (i = 0; i < c->previousCount; ++i) {
        if (EqualsNoCase(c->previous[i].kind, kind) &&
            EqualsNoCase(c->previous[i].path, path))
            return TRUE;
    }
    return FALSE;
}

static void RemoveLeftovers(Ctx *c)
{
    Entry *old = c->previous;
    int oldCount = c->previousCount;
    int i, j;

    for (i = 0; i < oldCount; ++i) {
        BOOL stillThere = FALSE;
        wchar_t full[MAX_PATH * 2];

        for (j = 0; j < c->entryCount; ++j) {
            if (EqualsNoCase(c->entries[j].kind, old[i].kind) &&
                EqualsNoCase(c->entries[j].path, old[i].path)) {
                stillThere = TRUE;
                break;
            }
        }
        if (stillThere)
            continue;

        if (EqualsNoCase(old[i].kind, L"SHORTCUT")) {
            /* Full paths, so held to being a .lnk in the user's own Desktop or
             * Start menu. See the same checks in Uninstall.c. */
            if (!IsSafeShortcutPath(old[i].path)) {
                PostText(c->dlg, WM_APP_LOG,
                         L"Left alone: %s is not a shortcut this installer made",
                         old[i].path);
                continue;
            }
            if (FileExists(old[i].path)) {
                DeleteFileHard(old[i].path);
                PostText(c->dlg, WM_APP_LOG, L"Removed the old shortcut %s", old[i].path);
            }
            continue;
        }

        PathJoin(full, sizeof(full) / sizeof(full[0]), c->gameDir, old[i].path);

        /* The install log is a text file and can be edited or damaged. A line
         * that does not name a plain path inside the game folder, or that
         * names part of Blockland itself, is refused rather than acted on. */
        if (!IsSafeRelativePath(old[i].path) || IsProtectedGameItem(old[i].path) ||
            !IsInsideFolder(c->gameDir, full)) {
            PostText(c->dlg, WM_APP_LOG,
                     L"Left alone: \"%s\" in the previous install log is not something "
                     L"Setup may remove", old[i].path);
            continue;
        }

        if (EqualsNoCase(old[i].kind, L"FOLDER")) {
            if (DirExists(full)) {
                DeleteTree(full);
                PostText(c->dlg, WM_APP_LOG, L"Removed %s, which is no longer being installed",
                         old[i].path);
            }
        } else if (FileExists(full)) {
            DeleteFileHard(full);
            PostText(c->dlg, WM_APP_LOG, L"Removed %s, which is no longer being installed",
                     old[i].path);
        }
    }
}

static BOOL Cancelled(Ctx *c)
{
    return InterlockedCompareExchange(&c->cancelled, 0, 0) != 0;
}

static DWORD WINAPI InstallThread(LPVOID param)
{
    Ctx *c = (Ctx *)param;
    wchar_t path[MAX_PATH * 2];
    wchar_t rel[MAX_PATH * 2];
    wchar_t where[MAX_PATH];
    int i;
    BOOL ok = TRUE;

    CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);

    c->previousCount = ReadPreviousLog(c->gameDir, c->previous, MAX_ENTRIES);

    /* Size the progress bar before writing anything, so it moves steadily
     * instead of jumping about. */
    c->totalFiles = 0;
    for (i = 0; i < c->payloadCount; ++i) {
        const PlanRule *rule;
        if (!IsWanted(c, c->payload[i].name))
            continue;
        c->totalFiles += CountTree(c->payload[i].path);
        rule = PlanFor(&c->plan, c->payload[i].name);
        if (rule != NULL && rule->zipToo)
            c->totalFiles += CountTree(c->payload[i].path);
    }
    c->totalFiles += 2;      /* the two programs written out at the end */

    PostText(c->dlg, WM_APP_LOG, L"Installing into %s", c->gameDir);
    if (c->plan.fromFile)
        PostText(c->dlg, WM_APP_LOG, L"Destinations read from README.txt.");
    else
        PostText(c->dlg, WM_APP_LOG,
                 L"README.txt could not be read, so the standard layout is being used.");
    PostText(c->dlg, WM_APP_LOG, L"");

    for (i = 0; i < c->payloadCount && ok && !Cancelled(c); ++i) {
        const PlanRule *rule = PlanFor(&c->plan, c->payload[i].name);
        unsigned before = c->doneFiles;

        if (!IsWanted(c, c->payload[i].name)) {
            PostText(c->dlg, WM_APP_LOG, L"Skipped %s - you turned it off.",
                     c->payload[i].name);
            continue;
        }

        RelativeFor(rule, c->payload[i].name, rel, sizeof(rel) / sizeof(rel[0]));
        PathJoin(path, sizeof(path) / sizeof(path[0]), c->gameDir, rel);
        DescribeDestination(rule, where, sizeof(where) / sizeof(where[0]));

        /* The destination came out of README.txt, which is a text file anyone
         * can edit. Before writing - and long before deleting - it has to be a
         * plain path inside the game folder that is not part of Blockland
         * itself. A line that is not gets skipped and said so, rather than
         * being followed somewhere it should not go. */
        if (!IsSafeRelativePath(rel) || IsProtectedGameItem(rel) ||
            !IsInsideFolder(c->gameDir, path)) {
            PostText(c->dlg, WM_APP_LOG,
                     L"Skipped %s - README.txt sends it to \"%s\", which is not a place "
                     L"inside your Blockland folder that Setup may write to.",
                     c->payload[i].name, rel);
            continue;
        }

        /* Reinstalling over an older version: take the old files away first,
         * or anything the new version has dropped would be left sitting there
         * mixed in with it. Only ever done to a folder Setup itself put there,
         * and .cfg files are kept so edited settings survive. */
        if (WasInstalledBefore(c, L"FOLDER", rel) && DirExists(path)) {
            PostText(c->dlg, WM_APP_STATUS, L"Clearing out the old %s", c->payload[i].name);
            DeleteTreeKeepSettings(path);
            PostText(c->dlg, WM_APP_LOG,
                     L"Cleared out the previous %s first, keeping its .cfg settings",
                     c->payload[i].name);
        }

        PostText(c->dlg, WM_APP_STATUS, L"Copying %s", c->payload[i].name);

        if (!CopyTree(c->payload[i].path, path, FileProgress, c)) {
            if (Cancelled(c))
                break;
            PostText(c->dlg, WM_APP_LOG,
                     L"FAILED to copy %s into %s.", c->payload[i].name, where);
            ok = FALSE;
            break;
        }

        RecordEntry(c, L"FOLDER", rel);
        PostText(c->dlg, WM_APP_LOG, L"Copied %s into %s  (%u files)",
                 c->payload[i].name, where, c->doneFiles - before);

        if (rule != NULL && rule->zipToo && !Cancelled(c)) {
            wchar_t zipRel[MAX_PATH * 2];
            wchar_t zipPath[MAX_PATH * 2];
            ZipWriter *zip;

            _snwprintf(zipRel, sizeof(zipRel) / sizeof(zipRel[0]), L"%s.zip", rel);
            zipRel[(sizeof(zipRel) / sizeof(zipRel[0])) - 1] = L'\0';
            PathJoin(zipPath, sizeof(zipPath) / sizeof(zipPath[0]), c->gameDir, zipRel);

            PostText(c->dlg, WM_APP_STATUS, L"Packing %s.zip", c->payload[i].name);

            zip = ZipCreate(zipPath);
            if (zip == NULL) {
                PostText(c->dlg, WM_APP_LOG, L"FAILED to create %s.", zipRel);
                ok = FALSE;
                break;
            }

            if (!ZipAddDirContents(zip, c->payload[i].path, FileProgress, c)) {
                ZipAbort(zip);
                if (Cancelled(c))
                    break;
                PostText(c->dlg, WM_APP_LOG, L"FAILED while packing %s.", zipRel);
                ok = FALSE;
                break;
            }

            if (!ZipFinish(zip)) {
                PostText(c->dlg, WM_APP_LOG, L"FAILED to finish %s.", zipRel);
                ok = FALSE;
                break;
            }

            RecordEntry(c, L"FILE", zipRel);
            PostText(c->dlg, WM_APP_LOG, L"Packed %s as well, because README.txt asks for it",
                     zipRel);
        }
    }

    /* --- the two programs ------------------------------------------------ */

    if (ok && !Cancelled(c)) {
        PostText(c->dlg, WM_APP_STATUS, L"Writing MonsterRPG.exe");

        PathJoin(path, sizeof(path) / sizeof(path[0]), c->gameDir, LAUNCHER_NAME);
        if (WriteResourceToFile(c->inst, IDR_LAUNCHER_EXE, path)) {
            RecordEntry(c, L"FILE", LAUNCHER_NAME);
            FileProgress(c, LAUNCHER_NAME);
            PostText(c->dlg, WM_APP_LOG,
                     L"Put %s next to Blockland.exe - that is what you double-click to play",
                     LAUNCHER_NAME);
        } else {
            PostText(c->dlg, WM_APP_LOG,
                     L"FAILED to write %s. If the game or the launcher is running, "
                     L"close it and run Setup again.", LAUNCHER_NAME);
            ok = FALSE;
        }
    }

    if (ok && !Cancelled(c)) {
        PathJoin(path, sizeof(path) / sizeof(path[0]), c->gameDir, UNINSTALLER_NAME);
        if (WriteResourceToFile(c->inst, IDR_UNINSTALLER_EXE, path)) {
            RecordEntry(c, L"FILE", UNINSTALLER_NAME);
            FileProgress(c, UNINSTALLER_NAME);
            PostText(c->dlg, WM_APP_LOG, L"Put %s beside it", UNINSTALLER_NAME);
        } else {
            PostText(c->dlg, WM_APP_LOG, L"FAILED to write %s.", UNINSTALLER_NAME);
            ok = FALSE;
        }
    }

    /* --- shortcuts ------------------------------------------------------- */

    if (ok && !Cancelled(c)) {
        wchar_t target[MAX_PATH * 2];
        wchar_t remover[MAX_PATH * 2];
        wchar_t lnk[MAX_PATH * 2];
        wchar_t dir[MAX_PATH * 2];

        PathJoin(target, sizeof(target) / sizeof(target[0]), c->gameDir, LAUNCHER_NAME);
        PathJoin(remover, sizeof(remover) / sizeof(remover[0]), c->gameDir, UNINSTALLER_NAME);

        if (GetStartMenuProgramsDir(dir, sizeof(dir) / sizeof(dir[0]))) {
            PathJoin(lnk, sizeof(lnk) / sizeof(lnk[0]), dir, SHORTCUT_NAME);
            if (CreateShortcut(lnk, target, c->gameDir, L"Play Blockland with MonsterRPG")) {
                RecordEntry(c, L"SHORTCUT", lnk);
                PostText(c->dlg, WM_APP_LOG, L"Added MonsterRPG to the Start menu");
            }
        }

        /* The uninstaller gets a shortcut wherever the game does, so the way to
         * undo this is sitting next to the way to run it rather than being
         * something you have to know to go and look for. The name puts it
         * directly above the other one in a list sorted by name. */
        if (c->desktopShortcut && GetDesktopDir(dir, sizeof(dir) / sizeof(dir[0]))) {
            PathJoin(lnk, sizeof(lnk) / sizeof(lnk[0]), dir, SHORTCUT_NAME);
            if (CreateShortcut(lnk, target, c->gameDir, L"Play Blockland with MonsterRPG")) {
                RecordEntry(c, L"SHORTCUT", lnk);
                PostText(c->dlg, WM_APP_LOG, L"Put a MonsterRPG shortcut on your Desktop");
            }

            PathJoin(lnk, sizeof(lnk) / sizeof(lnk[0]), dir, SHORTCUT_UNINST);
            if (CreateShortcut(lnk, remover, c->gameDir, L"Remove MonsterRPG from Blockland")) {
                RecordEntry(c, L"SHORTCUT", lnk);
                PostText(c->dlg, WM_APP_LOG,
                         L"Put a MonsterRPG Uninstaller shortcut beside it");
            }
        }
    }

    /* --- tidy up --------------------------------------------------------- */

    /* The log and the uninstaller are written even when this went wrong or was
     * stopped part way, because half a copy still needs a way to be removed. */
    if (c->entryCount > 0) {
        if (ok && !Cancelled(c))
            RemoveLeftovers(c);

        if (!WriteInstallLog(c))
            PostText(c->dlg, WM_APP_LOG,
                     L"WARNING: could not write \"%s\". The uninstaller will not know "
                     L"what to remove.", MANIFEST_NAME);
    }

    if (ok && !Cancelled(c)) {
        WriteUninstallKey(c);
        PostText(c->dlg, WM_APP_LOG, L"Listed MonsterRPG in Windows' Apps and features");
    }

    CoUninitialize();

    c->succeeded = ok && !Cancelled(c);
    PostMessageW(c->dlg, WM_APP_DONE, (WPARAM)(c->succeeded ? 1 : 0), 0);
    return 0;
}

/* ---------------------------------------------------------------------------
 *  The window
 * ------------------------------------------------------------------------ */

static const int g_introControls[] = {
    IDC_INTRO_TITLE, IDC_INTRO_TEXT,
    0
};

static const int g_chooseControls[] = {
    IDC_STEP1, IDC_FOLDER, IDC_BROWSE, IDC_FOLDER_STATE,
    IDC_STEP2, IDC_PARTS_ALWAYS,
    IDC_CHK_AUDIO, IDC_AUDIO_NOTE,
    IDC_CHK_DESKTOP,
    0
};

static const int g_workControls[] = {
    IDC_PROGRESS, IDC_STATUS, IDC_LOG,
    0
};

static void ShowGroup(HWND dlg, const int *ids, BOOL show)
{
    int i;
    for (i = 0; ids[i] != 0; ++i)
        ShowWindow(GetDlgItem(dlg, ids[i]), show ? SW_SHOW : SW_HIDE);
}

static void UpdateFolderState(HWND dlg)
{
    Ctx *c = &g_ctx;
    wchar_t folder[MAX_PATH * 2];
    GameCheck rank;

    GetDlgItemTextW(dlg, IDC_FOLDER, folder, (int)(sizeof(folder) / sizeof(folder[0])));
    TrimInPlace(folder);

    rank = CheckGameFolder(folder);

    /* The heading and the Continue/Install button belong to whichever page is
     * up. While that is not the options page, this has nothing to say and must
     * not overwrite them. ShowPage calls back here on arrival, so the options
     * page is always correct by the time it is looked at. */
    if (c->page != PAGE_OPTIONS)
        return;

    /* The two lines at the top follow the box, so they cannot go on claiming
     * the folder was found after somebody has typed a different one in. */
    if (rank == GAME_NO) {
        SetDlgItemTextW(dlg, IDC_STEP1, L"1.   Where is Blockland on this computer?");
        SetDlgItemTextW(dlg, IDC_HEAD_SUB,
            L"Point Setup at your Blockland folder to carry on.");
    } else if (c->autoFound && EqualsNoCase(folder, c->autoPath)) {
        SetDlgItemTextW(dlg, IDC_STEP1, L"1.   Blockland was found here");
        SetDlgItemTextW(dlg, IDC_HEAD_SUB,
            L"Your Blockland folder was found. Check it below, then press Install.");
    } else {
        SetDlgItemTextW(dlg, IDC_STEP1, L"1.   Your Blockland folder");
        SetDlgItemTextW(dlg, IDC_HEAD_SUB,
            L"Ready to install into the folder below. Press Install when you are.");
    }

    if (rank == GAME_FULL) {
        SetDlgItemTextW(dlg, IDC_FOLDER_STATE,
            L"Blockland.exe and the Add-Ons folder are both here. This is the right one.");
    } else if (rank == GAME_EXE_ONLY) {
        SetDlgItemTextW(dlg, IDC_FOLDER_STATE,
            L"Blockland.exe is here, but there is no Add-Ons folder beside it. "
            L"Setup will make one. If you have the game installed twice, check this "
            L"is the copy you actually play.");
    } else if (DirExists(folder)) {
        SetDlgItemTextW(dlg, IDC_FOLDER_STATE,
            L"There is no Blockland.exe in this folder. Press Choose folder... and "
            L"find the one with Blockland.exe and Add-Ons sitting side by side in it.");
    } else {
        SetDlgItemTextW(dlg, IDC_FOLDER_STATE,
            L"There is no folder with this name. Press Choose folder... and find the "
            L"one with Blockland.exe and Add-Ons sitting side by side in it.");
    }

    InvalidateRect(GetDlgItem(dlg, IDC_FOLDER_STATE), NULL, TRUE);
    EnableWindow(GetDlgItem(dlg, IDC_INSTALL), rank != GAME_NO);
}

/* The always-installed part of the list. The two optional folders have their
 * own checkboxes underneath, so listing them here as well would only make it
 * look like they are going in regardless. */
static void BuildPartsText(Ctx *c, wchar_t *out, size_t cch)
{
    int i;
    BOOL any = FALSE;

    out[0] = L'\0';

    for (i = 0; i < c->payloadCount; ++i) {
        const PlanRule *rule;
        wchar_t where[MAX_PATH];
        wchar_t line[MAX_PATH + 128];

        if (KindOf(c->payload[i].name) != PART_ALWAYS)
            continue;

        rule = PlanFor(&c->plan, c->payload[i].name);
        DescribeDestination(rule, where, sizeof(where) / sizeof(where[0]));

        _snwprintf(line, sizeof(line) / sizeof(line[0]),
                   L"%s%s  goes into  %s   (always installed)",
                   any ? L"\r\n" : L"", c->payload[i].name, where);
        line[(sizeof(line) / sizeof(line[0])) - 1] = L'\0';

        wcsncat(out, line, cch - wcslen(out) - 1);
        any = TRUE;
    }

    if (!any)
        wcsncpy(out, L"Nothing found to install. Setup.exe has to sit in the same "
                     L"folder as the MonsterRPG folders it came with.", cch - 1);

    out[cch - 1] = L'\0';
}

static void ShowPage(HWND dlg, Ctx *c, int page)
{
    c->page = page;

    ShowGroup(dlg, g_introControls,  page == PAGE_INTRO);
    ShowGroup(dlg, g_chooseControls, page == PAGE_OPTIONS);
    ShowGroup(dlg, g_workControls,   page == PAGE_WORK);

    if (page == PAGE_INTRO) {
        SetDlgItemTextW(dlg, IDC_HEAD_TITLE, L"Install MonsterRPG");
        SetDlgItemTextW(dlg, IDC_HEAD_SUB,
            L"Please read this first. It is short, and it is the whole of what "
            L"installing does.");
        SetDlgItemTextW(dlg, IDC_INSTALL, L"Continue");
        EnableWindow(GetDlgItem(dlg, IDC_INSTALL), TRUE);
        SetFocus(GetDlgItem(dlg, IDC_INSTALL));
    } else if (page == PAGE_OPTIONS) {
        SetDlgItemTextW(dlg, IDC_HEAD_TITLE, L"Install MonsterRPG");
        SetDlgItemTextW(dlg, IDC_INSTALL, L"Install");
        UpdateFolderState(dlg);          /* sets the heading and the button */
        SetFocus(GetDlgItem(dlg, IDC_FOLDER));
    }

    /* The header sits on the white band, which is painted by hand, so it has
     * to be asked to redraw when the words on it change. */
    InvalidateRect(dlg, NULL, TRUE);
}

static void StartInstall(HWND dlg, Ctx *c)
{
    wchar_t folder[MAX_PATH * 2];
    HANDLE thread;

    GetDlgItemTextW(dlg, IDC_FOLDER, folder, (int)(sizeof(folder) / sizeof(folder[0])));
    TrimInPlace(folder);

    if (!LooksLikeGameFolder(folder)) {
        MessageBoxW(dlg,
            L"There is no Blockland.exe in that folder, so that is not where the "
            L"game lives.\n\n"
            L"Use Choose folder... and look for the folder that has Blockland.exe "
            L"directly inside it. On most computers that is Documents \\ Blockland.",
            SETUP_TITLE, MB_OK | MB_ICONINFORMATION);
        return;
    }

    if (!CanWriteTo(folder)) {
        MessageBoxW(dlg,
            L"Windows will not let Setup write into that folder.\n\n"
            L"That normally means the game is installed somewhere protected, like "
            L"Program Files.\n\n"
            L"Close Setup, right-click \"MonsterRPG Setup.exe\", choose "
            L"\"Run as administrator\", and pick the same folder again.",
            SETUP_TITLE, MB_OK | MB_ICONWARNING);
        return;
    }

    if (c->payloadCount == 0) {
        MessageBoxW(dlg,
            L"Setup cannot find the MonsterRPG folders.\n\n"
            L"\"MonsterRPG Setup.exe\" has to stay in the folder it was downloaded "
            L"in, next to the folders it installs. If you unzipped only the Setup "
            L"file, unzip the whole download and run it from there.",
            SETUP_TITLE, MB_OK | MB_ICONWARNING);
        return;
    }

    wcsncpy(c->gameDir, folder, (sizeof(c->gameDir) / sizeof(c->gameDir[0])) - 1);
    c->gameDir[(sizeof(c->gameDir) / sizeof(c->gameDir[0])) - 1] = L'\0';

    c->installAudio    = IsDlgButtonChecked(dlg, IDC_CHK_AUDIO) == BST_CHECKED;
    c->desktopShortcut = IsDlgButtonChecked(dlg, IDC_CHK_DESKTOP) == BST_CHECKED;
    c->installing      = TRUE;
    c->cancelled       = 0;
    c->doneFiles       = 0;
    c->lastPercent     = -1;
    c->entryCount      = 0;

    ShowPage(dlg, c, PAGE_WORK);

    SetDlgItemTextW(dlg, IDC_HEAD_TITLE, L"Installing MonsterRPG");

    /* The folder goes on screen, not just into the scrolling log. Where the
     * files are going is the one thing worth being able to see at a glance
     * while it happens, and afterwards. */
    {
        wchar_t into[MAX_PATH * 2 + 32];
        _snwprintf(into, sizeof(into) / sizeof(into[0]), L"Into %s", c->gameDir);
        into[(sizeof(into) / sizeof(into[0])) - 1] = L'\0';
        SetDlgItemTextW(dlg, IDC_HEAD_SUB, into);
    }
    SetDlgItemTextW(dlg, IDCANCEL, L"Stop");
    EnableWindow(GetDlgItem(dlg, IDC_INSTALL), FALSE);

    SendDlgItemMessageW(dlg, IDC_PROGRESS, PBM_SETRANGE32, 0, 100);
    SendDlgItemMessageW(dlg, IDC_PROGRESS, PBM_SETPOS, 0, 0);

    thread = CreateThread(NULL, 0, InstallThread, c, 0, NULL);
    if (thread == NULL) {
        c->installing = FALSE;
        MessageBoxW(dlg, L"Setup could not start copying. Restart the computer and try again.",
                    SETUP_TITLE, MB_OK | MB_ICONERROR);
        return;
    }

    /* Held rather than closed here, so the end of wWinMain can make certain
     * this thread has stopped before the process does. A process that exits
     * while a thread is still writing files is how a half-copied folder gets
     * left behind. */
    c->worker = thread;
}

static void FinishUp(HWND dlg, Ctx *c, BOOL success)
{
    wchar_t line[MAX_PATH * 2];

    c->installing = FALSE;
    c->finished = TRUE;

    SendDlgItemMessageW(dlg, IDC_PROGRESS, PBM_SETPOS, success ? 100 : 0, 0);

    if (success) {
        SetDlgItemTextW(dlg, IDC_HEAD_TITLE, L"MonsterRPG is installed");

        _snwprintf(line, sizeof(line) / sizeof(line[0]), L"Installed into %s",
                   c->gameDir);
        line[(sizeof(line) / sizeof(line[0])) - 1] = L'\0';
        SetDlgItemTextW(dlg, IDC_HEAD_SUB, line);
        SetDlgItemTextW(dlg, IDC_STATUS, L"Finished.");

        AppendLog(GetDlgItem(dlg, IDC_LOG), L"");
        _snwprintf(line, sizeof(line) / sizeof(line[0]),
                   L"To play: open your Blockland folder and double-click \"%s\". "
                   L"It is right next to Blockland.exe.", LAUNCHER_NAME);
        AppendLog(GetDlgItem(dlg, IDC_LOG), line);
        _snwprintf(line, sizeof(line) / sizeof(line[0]),
                   L"To remove it later: double-click \"%s\" in the same folder.",
                   UNINSTALLER_NAME);
        AppendLog(GetDlgItem(dlg, IDC_LOG), line);

        ShowWindow(GetDlgItem(dlg, IDC_PLAY), SW_SHOW);
        CheckDlgButton(dlg, IDC_PLAY, BST_CHECKED);
    } else {
        SetDlgItemTextW(dlg, IDC_HEAD_TITLE, L"Setup did not finish");
        SetDlgItemTextW(dlg, IDC_HEAD_SUB,
            L"Nothing else will be written. The list below says how far it got.");
        SetDlgItemTextW(dlg, IDC_STATUS, L"Stopped.");

        AppendLog(GetDlgItem(dlg, IDC_LOG), L"");
        _snwprintf(line, sizeof(line) / sizeof(line[0]),
                   L"Anything that was already copied can be removed with \"%s\" "
                   L"in your Blockland folder.", UNINSTALLER_NAME);
        AppendLog(GetDlgItem(dlg, IDC_LOG), line);
    }

    SetDlgItemTextW(dlg, IDC_INSTALL, L"Close");
    EnableWindow(GetDlgItem(dlg, IDC_INSTALL), TRUE);
    ShowWindow(GetDlgItem(dlg, IDCANCEL), SW_HIDE);
    SetFocus(GetDlgItem(dlg, IDC_INSTALL));
}

static void StartTheGame(Ctx *c)
{
    wchar_t launcher[MAX_PATH * 2];
    SHELLEXECUTEINFOW ei;

    PathJoin(launcher, sizeof(launcher) / sizeof(launcher[0]), c->gameDir, LAUNCHER_NAME);
    if (!FileExists(launcher))
        return;

    ZeroMemory(&ei, sizeof(ei));
    ei.cbSize      = sizeof(ei);
    ei.lpFile      = launcher;
    ei.lpDirectory = c->gameDir;
    ei.nShow       = SW_SHOWNORMAL;
    ei.fMask       = SEE_MASK_NOASYNC;
    ShellExecuteExW(&ei);
}

static void PaintBands(HWND dlg, Ctx *c)
{
    PAINTSTRUCT ps;
    HDC dc = BeginPaint(dlg, &ps);
    RECT client;
    RECT band;
    HPEN pen, old;

    GetClientRect(dlg, &client);

    band = client;
    band.bottom = c->headerPx;
    FillRect(dc, &band, c->whiteBrush);

    pen = CreatePen(PS_SOLID, 1, RGB(210, 210, 210));
    old = (HPEN)SelectObject(dc, pen);

    MoveToEx(dc, client.left, c->headerPx - 1, NULL);
    LineTo(dc, client.right, c->headerPx - 1);

    MoveToEx(dc, client.left, c->footerPx, NULL);
    LineTo(dc, client.right, c->footerPx);

    SelectObject(dc, old);
    DeleteObject(pen);

    EndPaint(dlg, &ps);
}

static INT_PTR CALLBACK SetupProc(HWND dlg, UINT msg, WPARAM wp, LPARAM lp)
{
    Ctx *c = &g_ctx;

    switch (msg) {

    case WM_INITDIALOG: {
        HICON big, small;
        wchar_t folder[MAX_PATH * 2];
        wchar_t parts[2048];
        RECT r;
        HDC dc;
        int dpi;

        c->dlg = dlg;

        big   = (HICON)LoadImageW(c->inst, MAKEINTRESOURCEW(IDI_APP), IMAGE_ICON, 32, 32, 0);
        small = (HICON)LoadImageW(c->inst, MAKEINTRESOURCEW(IDI_APP), IMAGE_ICON, 16, 16, 0);
        if (big != NULL) {
            SendMessageW(dlg, WM_SETICON, ICON_BIG, (LPARAM)big);
            SendDlgItemMessageW(dlg, IDC_HEAD_ICON, STM_SETICON, (WPARAM)big, 0);
        }
        if (small != NULL)
            SendMessageW(dlg, WM_SETICON, ICON_SMALL, (LPARAM)small);

        dc = GetDC(dlg);
        dpi = GetDeviceCaps(dc, LOGPIXELSY);
        ReleaseDC(dlg, dc);

        c->titleFont = CreateFontW(-MulDiv(14, dpi, 72), 0, 0, 0, FW_SEMIBOLD,
                                   FALSE, FALSE, FALSE, DEFAULT_CHARSET,
                                   OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
                                   CLEARTYPE_QUALITY, DEFAULT_PITCH, L"Segoe UI");
        if (c->titleFont != NULL)
            SendDlgItemMessageW(dlg, IDC_HEAD_TITLE, WM_SETFONT, (WPARAM)c->titleFont, TRUE);

        c->whiteBrush = CreateSolidBrush(GetSysColor(COLOR_WINDOW));

        /* Where the white band ends and where the line above the buttons goes,
         * both worked out from the layout rather than guessed in pixels. */
        r.left = 0; r.top = 0; r.right = 4; r.bottom = 48;
        MapDialogRect(dlg, &r);
        c->headerPx = r.bottom;

        GetWindowRect(GetDlgItem(dlg, IDC_INSTALL), &r);
        MapWindowPoints(NULL, dlg, (POINT *)&r, 2);
        c->footerPx = r.top - (r.bottom - r.top) / 2;

        /* Filled in below once we know whether the game was actually found. */
        SetDlgItemTextW(dlg, IDC_HEAD_SUB,
            L"This copies MonsterRPG into your Blockland folder.");

        SetDlgItemTextW(dlg, IDC_INTRO_TEXT,
            L"This changes your Blockland installation and changes how the game "
            L"runs. In plain words:\r\n"
            L"\r\n"
            L"WHAT SETUP ADDS\r\n"
            L"It copies new folders into your Blockland folder and puts two programs "
            L"beside Blockland.exe. It does not edit, replace or delete anything "
            L"that was already there.\r\n"
            L"\r\n"
            L"WHILE YOU PLAY\r\n"
            L"Starting the game through MonsterRPG attaches MonsterRPG's own DLL "
            L"files to Blockland.exe and runs their code in the game's memory while "
            L"you play. That is how the mod does what it does, and it is the "
            L"ordinary way Blockland mods have worked for years.\r\n"
            L"\r\n"
            L"NOTHING IS LEFT RUNNING AFTERWARDS\r\n"
            L"That code is held in memory only and is gone the moment you close the "
            L"game. Start Blockland the way you always did and you get a completely "
            L"normal Blockland.\r\n"
            L"\r\n"
            L"YOUR ANTIVIRUS MAY SAY SOMETHING\r\n"
            L"Attaching a DLL to a running game is normal for a mod, but it looks "
            L"unusual to software watching for it. That is why.\r\n"
            L"\r\n"
            L"YOU CAN UNDO ALL OF IT\r\n"
            L"Setup puts \"Blockland MonsterRPG Uninstaller\" in your Blockland folder, "
            L"right next to the one that starts the game, and lists MonsterRPG in "
            L"Windows' Apps and features. Either takes back everything Setup added.");

        /* Deliberately blunt, and deliberately first: the box below is unticked
         * and the sentence that says why is the first thing next to it. */
        SetDlgItemTextW(dlg, IDC_AUDIO_NOTE,
            L"OFF UNLESS YOU TICK IT, because this one listens on your network.\r\n"
            L"While you play, it opens a port on this computer and listens on it. "
            L"Your microphone goes out through that port and game audio comes back "
            L"in. It only ever talks to the MonsterRPG server you are playing on, "
            L"it stays silent until that server invites it, and it stops when you "
            L"close the game. Windows may ask you to allow it through the "
            L"firewall.\r\n"
            L"Tick it and you get music, effects, and voice chat that comes from the "
            L"direction the other player is standing in. Leave it alone and "
            L"everything else in MonsterRPG still works.");

        /* Custom installs are common and there is nothing wrong with one, so
         * what the top of the window says is decided by UpdateFolderState from
         * these two - and goes on being decided by it every time the box
         * changes. Keeping the wording in one place is what stops the header
         * and the line under the box ever disagreeing. */
        c->autoFound = GuessGameFolder(folder, sizeof(folder) / sizeof(folder[0]));
        wcsncpy(c->autoPath, folder, (sizeof(c->autoPath) / sizeof(c->autoPath[0])) - 1);
        c->autoPath[(sizeof(c->autoPath) / sizeof(c->autoPath[0])) - 1] = L'\0';

        SetDlgItemTextW(dlg, IDC_FOLDER, folder);

        BuildPartsText(c, parts, sizeof(parts) / sizeof(parts[0]));
        SetDlgItemTextW(dlg, IDC_PARTS_ALWAYS, parts);

        /* The audio box is NOT ticked here, and must not be. Ticking it is how
         * somebody agrees to a program on their machine listening on a network
         * port; agreeing to that on their behalf, by shipping it pre-ticked, is
         * not consent. */
        CheckDlgButton(dlg, IDC_CHK_AUDIO, BST_UNCHECKED);
        CheckDlgButton(dlg, IDC_CHK_DESKTOP, BST_CHECKED);

        ShowWindow(GetDlgItem(dlg, IDC_PLAY), SW_HIDE);

        ShowPage(dlg, c, PAGE_INTRO);
        return FALSE;
    }

    case WM_PAINT:
        PaintBands(dlg, c);
        return TRUE;

    case WM_CTLCOLORDLG:
        return (INT_PTR)GetSysColorBrush(COLOR_BTNFACE);

    case WM_CTLCOLORSTATIC: {
        HDC dc = (HDC)wp;
        int id = GetDlgCtrlID((HWND)lp);

        if (id == IDC_HEAD_TITLE || id == IDC_HEAD_SUB || id == IDC_HEAD_ICON) {
            SetBkMode(dc, TRANSPARENT);
            SetTextColor(dc, RGB(20, 20, 20));
            return (INT_PTR)c->whiteBrush;
        }

        if (id == IDC_FOLDER_STATE) {
            wchar_t folder[MAX_PATH * 2];
            GameCheck rank;

            GetDlgItemTextW(dlg, IDC_FOLDER, folder,
                            (int)(sizeof(folder) / sizeof(folder[0])));
            TrimInPlace(folder);
            rank = CheckGameFolder(folder);

            SetBkMode(dc, TRANSPARENT);
            /* Green for certain, amber for "this will work but look at it",
             * red for wrong. */
            SetTextColor(dc, rank == GAME_FULL     ? RGB(0, 110, 45)
                           : rank == GAME_EXE_ONLY ? RGB(150, 95, 0)
                                                   : RGB(170, 30, 0));
            return (INT_PTR)GetSysColorBrush(COLOR_BTNFACE);
        }

        /* Not the plain text used elsewhere. This one is the difference
         * between a program that listens on your network and one that does
         * not, so it is coloured to be read rather than skimmed. */
        if (id == IDC_AUDIO_NOTE) {
            SetBkMode(dc, TRANSPARENT);
            SetTextColor(dc, RGB(140, 70, 0));
            return (INT_PTR)GetSysColorBrush(COLOR_BTNFACE);
        }

        if (id == IDC_LOG)
            break;      /* a read-only edit; let it draw itself */

        SetBkMode(dc, TRANSPARENT);
        return (INT_PTR)GetSysColorBrush(COLOR_BTNFACE);
    }

    case WM_COMMAND:
        switch (LOWORD(wp)) {

        case IDC_FOLDER:
            if (HIWORD(wp) == EN_CHANGE)
                UpdateFolderState(dlg);
            return TRUE;

        case IDC_BROWSE: {
            wchar_t folder[MAX_PATH * 2];
            GetDlgItemTextW(dlg, IDC_FOLDER, folder,
                            (int)(sizeof(folder) / sizeof(folder[0])));
            TrimInPlace(folder);
            if (AskForFolder(dlg, folder, sizeof(folder) / sizeof(folder[0]))) {
                SetDlgItemTextW(dlg, IDC_FOLDER, folder);
                UpdateFolderState(dlg);
            }
            return TRUE;
        }

        case IDC_INSTALL:
            if (c->finished) {
                if (c->succeeded && IsDlgButtonChecked(dlg, IDC_PLAY) == BST_CHECKED)
                    StartTheGame(c);
                EndDialog(dlg, 0);
            } else if (c->installing) {
                /* nothing to do; the button is disabled while copying */
            } else if (c->page == PAGE_INTRO) {
                ShowPage(dlg, c, PAGE_OPTIONS);
            } else {
                StartInstall(dlg, c);
            }
            return TRUE;

        case IDCANCEL:
            if (c->installing) {
                if (MessageBoxW(dlg,
                        L"Stop copying?\n\n"
                        L"What has already been copied stays where it is. You can "
                        L"remove it afterwards with the MonsterRPG uninstaller in "
                        L"your Blockland folder, or run Setup again to finish the job.",
                        SETUP_TITLE, MB_YESNO | MB_ICONQUESTION) == IDYES) {
                    InterlockedExchange(&c->cancelled, 1);
                    SetDlgItemTextW(dlg, IDC_STATUS, L"Stopping...");
                    EnableWindow(GetDlgItem(dlg, IDCANCEL), FALSE);
                }
            } else if (!c->finished) {
                EndDialog(dlg, 1);
            }
            return TRUE;
        }
        break;

    case WM_APP_LOG: {
        wchar_t *text = (wchar_t *)lp;
        AppendLog(GetDlgItem(dlg, IDC_LOG), text);
        LocalFree(text);
        return TRUE;
    }

    case WM_APP_STATUS: {
        wchar_t *text = (wchar_t *)lp;
        SetDlgItemTextW(dlg, IDC_STATUS, text);
        LocalFree(text);
        return TRUE;
    }

    case WM_APP_PROGRESS:
        SendDlgItemMessageW(dlg, IDC_PROGRESS, PBM_SETPOS, wp, 0);
        return TRUE;

    case WM_APP_DONE:
        FinishUp(dlg, c, wp != 0);
        return TRUE;

    case WM_CLOSE:
        if (c->installing)
            SendMessageW(dlg, WM_COMMAND, IDCANCEL, 0);
        else
            EndDialog(dlg, c->finished ? 0 : 1);
        return TRUE;

    case WM_DESTROY:
        if (c->titleFont != NULL) DeleteObject(c->titleFont);
        if (c->whiteBrush != NULL) DeleteObject(c->whiteBrush);
        return TRUE;
    }

    return FALSE;
}

/* ------------------------------------------------------------------------ */

int WINAPI wWinMain(HINSTANCE inst, HINSTANCE prev, PWSTR cmdLine, int show)
{
    INITCOMMONCONTROLSEX icc;
    HANDLE instanceLock;

    (void)prev; (void)cmdLine; (void)show;

    ZeroMemory(&g_ctx, sizeof(g_ctx));
    g_ctx.inst = inst;

    /* Before anything else, and before any window exists. Two copies copying
     * into the same folder at once would each delete the other's work part
     * way through. */
    instanceLock = ClaimSingleInstance(L"Local\\MonsterRPGSetup.SingleInstance",
                                       SETUP_TITLE);
    if (instanceLock == NULL) {
        MessageBoxW(NULL,
            L"MonsterRPG Setup is already open.\n\n"
            L"It has been brought to the front for you. If you cannot see it, "
            L"look for it on the taskbar.",
            SETUP_TITLE, MB_OK | MB_ICONINFORMATION);
        return 0;
    }

    icc.dwSize = sizeof(icc);
    icc.dwICC  = ICC_PROGRESS_CLASS | ICC_STANDARD_CLASSES | ICC_WIN95_CLASSES;
    InitCommonControlsEx(&icc);

    CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);

    if (!GetExeDir(g_ctx.srcDir, sizeof(g_ctx.srcDir) / sizeof(g_ctx.srcDir[0]))) {
        MessageBoxW(NULL, L"Setup could not work out which folder it is in.",
                    SETUP_TITLE, MB_OK | MB_ICONERROR);
        CoUninitialize();
        CloseHandle(instanceLock);
        return 1;
    }

    /* Before ReadPlan, because on the standalone build README.txt comes out of
     * the same zip as the folders and does not exist until this has run. */
    PreparePayload(&g_ctx);
    ReadPlan(g_ctx.srcDir, &g_ctx.plan);

    /* A window that never appears is the worst possible failure: from the
     * outside Setup simply does nothing. Say so instead. */
    if (DialogBoxParamW(inst, MAKEINTRESOURCEW(IDD_SETUP), NULL, SetupProc, 0) == -1) {
        wchar_t why[256];
        _snwprintf(why, sizeof(why) / sizeof(why[0]),
                   L"Setup could not open its window (Windows error %lu).",
                   GetLastError());
        why[(sizeof(why) / sizeof(why[0])) - 1] = L'\0';
        MessageBoxW(NULL, why, SETUP_TITLE, MB_OK | MB_ICONERROR);
        CoUninitialize();
        CloseHandle(instanceLock);
        return 1;
    }

    /* The window has gone. If a copy is somehow still running, tell it to stop
     * and give it a moment to put its pen down, so nothing is left half
     * written and nothing lingers in Task Manager after this returns. */
    if (g_ctx.worker != NULL) {
        InterlockedExchange(&g_ctx.cancelled, 1);
        WaitForSingleObject(g_ctx.worker, 10000);
        CloseHandle(g_ctx.worker);
        g_ctx.worker = NULL;
    }

    /* The standalone build unpacked itself into the temporary folder to do
     * any of this. Waiting until here means the copy is still there for the
     * whole run, including a second attempt after a failed one. */
    if (g_ctx.unpackedTo[0] != L'\0') {
        DeleteTree(g_ctx.unpackedTo);
        g_ctx.unpackedTo[0] = L'\0';
    }

    CoUninitialize();
    CloseHandle(instanceLock);
    return 0;
}
