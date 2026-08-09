/* ===========================================================================
 *  Common.h - the pieces Setup, the launcher and the uninstaller all share.
 *
 *  Names and file names live here so the three programs cannot drift apart.
 *  If you rename the install log, you rename it once, here.
 * ======================================================================== */

#ifndef MONSTERRPG_COMMON_H
#define MONSTERRPG_COMMON_H

#include <windows.h>

#define APP_NAME            L"MonsterRPG"
#define SETUP_TITLE         L"MonsterRPG Setup"
#define UNINSTALL_TITLE     L"Remove MonsterRPG"

/* Files Setup puts into the Blockland folder.
 *
 * Both names start with "Blockland MonsterRPG" so that a folder sorted by name
 * puts the three together, in this order:
 *
 *     Blockland MonsterRPG Uninstaller.exe
 *     Blockland MonsterRPG.exe
 *     Blockland.exe
 *
 * That is not a coincidence of spelling, it is the reason for the names. A
 * space sorts before a dot, so " Uninstaller.exe" lands above ".exe", and
 * whoever installed this finds both of our programs sitting right on top of
 * the one they already knew about. */
#define LAUNCHER_NAME       L"Blockland MonsterRPG.exe"
#define UNINSTALLER_NAME    L"Blockland MonsterRPG Uninstaller.exe"
#define MANIFEST_NAME       L"MonsterRPG install log.txt"

/* Shortcut names, chosen the same way and for the same reason: a space sorts
 * before a dot, so the uninstaller shortcut lands directly above the one that
 * starts the game. */
#define SHORTCUT_NAME       L"MonsterRPG.lnk"
#define SHORTCUT_UNINST     L"MonsterRPG Uninstaller.lnk"

/* The game itself. */
#define GAME_EXE            L"Blockland.exe"

/* Windows' own "Apps & features" list reads this. Per-user, so Setup never
 * needs to ask for administrator rights. */
#define REG_UNINSTALL_PATH  L"Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\MonsterRPG"

/* The folder that must be installed for the mod to do anything, and the one
 * the player can turn off. Matched against the folder names sitting beside
 * Setup.exe, so they are the folder names, not display names. */
#define FOLDER_AUDIO        L"MonsterRPGAudio"
#define FOLDER_TICKRATE     L"BLTickRate"
#define FOLDER_CLIENT       L"Client_MonsterRPG"

/* ---------------------------------------------------------------------------
 *  Paths and files
 * ------------------------------------------------------------------------ */

/* Joins two path pieces with exactly one backslash between them. */
void    PathJoin(wchar_t *dst, size_t cch, const wchar_t *a, const wchar_t *b);

BOOL    FileExists(const wchar_t *path);
BOOL    DirExists(const wchar_t *path);

/* Creates a folder and every missing folder above it. */
BOOL    EnsureDir(const wchar_t *path);

/* Folder holding the running .exe, with no trailing backslash. */
BOOL    GetExeDir(wchar_t *dst, size_t cch);

/* The user's Documents folder. Follows OneDrive redirection, so on a machine
 * where Documents lives in OneDrive this is the OneDrive one - which is where
 * the game actually is on those machines. */
BOOL    GetDocumentsDir(wchar_t *dst, size_t cch);

/* Removes a whole folder tree. Read-only files are cleared first; a file the
 * game still has open will fail, and that failure is reported rather than
 * swallowed. */
BOOL    DeleteTree(const wchar_t *path);

/* Deletes one file, clearing read-only first.
 *
 * Both this and DeleteTree keep trying for about a second, because Windows
 * reports a file as deleted while the last handle to it is still closing. A
 * folder emptied a moment ago often refuses to go on the first attempt and
 * then disappears on its own - so a single try reports a failure that is not
 * real, which in an uninstaller means telling someone their files are stuck
 * when they are already gone. */
BOOL    DeleteFileHard(const wchar_t *path);

/* Empties a folder of everything except .cfg files, keeping only the folders
 * those sit in. Used before reinstalling over an older version: copying on top
 * would leave any file the new version no longer has lying around, which is
 * how a mod ends up half one version and half another. Settings the player
 * edited are the one thing worth keeping, so they stay. */
BOOL    DeleteTreeKeepSettings(const wchar_t *path);

/* ---------------------------------------------------------------------------
 *  Copying
 *
 *  CopyCallback is called once per file with a path relative to the top of the
 *  copy. Returning FALSE stops the copy - that is how Cancel works.
 * ------------------------------------------------------------------------ */

typedef BOOL (*CopyCallback)(void *user, const wchar_t *relative);

/* Counts the files CopyTree would copy, so a progress bar can be sized before
 * anything is written. */
unsigned CountTree(const wchar_t *src);

/* Copies src into dst, creating dst. Working files are skipped (see
 * IsWorkingFile in Common.c). Existing .cfg files in dst are left alone so a
 * reinstall does not throw away settings the player has edited. */
BOOL    CopyTree(const wchar_t *src, const wchar_t *dst,
                 CopyCallback cb, void *user);

/* TRUE for the leftovers of building and running the mod - build backups,
 * logs, Windows' own folder droppings. These are useful on the machine the
 * mod was built on and only confusing on a player's. */
BOOL    IsWorkingFile(const wchar_t *name);

/* ---------------------------------------------------------------------------
 *  Reading README.txt
 *
 *  Setup does not have the destinations written into it. It reads them out of
 *  README.txt sitting beside it, so the person maintaining the download edits
 *  a text file rather than rebuilding a program.
 *
 *  Lines it understands:
 *
 *      Client_MonsterRPG Documents -> Blockland -> Add-Ons
 *      Rest is Documents -> Blockland
 *      We usually want the Client_MonsterRPG zipped too in that folder.
 *
 *  Everything after "Blockland" in the arrow chain is taken as a path inside
 *  whichever game folder the player picked, so moving the game somewhere else
 *  does not break the file.
 * ------------------------------------------------------------------------ */

#define PLAN_MAX_RULES  16

typedef struct {
    wchar_t source[64];         /* folder name beside Setup.exe; empty = the catch-all rule */
    wchar_t relative[MAX_PATH]; /* where it goes inside the game folder; empty = the top of it */
    BOOL    isCatchAll;
    BOOL    zipToo;             /* also write <source>.zip next to the copied folder */
} PlanRule;

typedef struct {
    PlanRule rules[PLAN_MAX_RULES];
    int      count;
    int      catchAll;          /* index into rules, or -1 */
    BOOL     fromFile;          /* FALSE means README.txt was missing or said nothing usable */
    wchar_t  source[MAX_PATH];  /* the README.txt actually read */
} InstallPlan;

/* Reads README.txt from dir. Never fails: if the file is missing or has no
 * usable lines, the built-in layout is filled in and fromFile is FALSE. */
void    ReadPlan(const wchar_t *dir, InstallPlan *plan);

/* The rule that applies to a folder name, never NULL as long as a catch-all
 * exists (the built-in one always does). */
const PlanRule *PlanFor(const InstallPlan *plan, const wchar_t *folderName);

/* ---------------------------------------------------------------------------
 *  Guard rails
 *
 *  Setup and the uninstaller both delete things, and both take the list of
 *  what to delete from a text file - README.txt and the install log. Text
 *  files can be edited, damaged, or half-written. None of that is allowed to
 *  turn into a program that deletes the wrong folder, so every path is checked
 *  against these before anything is removed.
 * ------------------------------------------------------------------------ */

/* TRUE only for a plain path inside a folder: something like
 * "Add-Ons\Client_MonsterRPG".
 *
 * Refuses an empty path (which would mean the game folder itself), anything
 * with a drive letter, anything starting at the root, any ".." step, and any
 * wildcard. Those are the ways a relative path stops being relative. */
BOOL    IsSafeRelativePath(const wchar_t *rel);

/* TRUE if child really sits inside parent once both are resolved to full
 * paths. The last check before a delete, so that even a path that passed
 * everything else cannot end up pointing outside the game folder. */
BOOL    IsInsideFolder(const wchar_t *parent, const wchar_t *child);

/* TRUE for the game's own files and folders - Blockland.exe, base, config,
 * saves, the Add-Ons folder itself. Nothing this installer creates is ever
 * one of these, so anything claiming to be is wrong and is refused. */
BOOL    IsProtectedGameItem(const wchar_t *rel);

/* ---------------------------------------------------------------------------
 *  Shortcuts and the Windows uninstall list
 * ------------------------------------------------------------------------ */

/* TRUE for a .lnk in the user's own Desktop or Start menu. Shortcut lines in
 * the install log are full paths, so they get their own check. */
BOOL    IsSafeShortcutPath(const wchar_t *path);

/* Writes a .lnk at lnkPath pointing at target. iconPath may be NULL to use
 * the target's own icon. Needs COM to have been started on this thread. */
BOOL    CreateShortcut(const wchar_t *lnkPath, const wchar_t *target,
                       const wchar_t *workingDir, const wchar_t *description);

BOOL    GetDesktopDir(wchar_t *dst, size_t cch);
BOOL    GetStartMenuProgramsDir(wchar_t *dst, size_t cch);

/* ---------------------------------------------------------------------------
 *  Running only once at a time
 *
 *  Two copies of Setup copying the same files into the same folder at the same
 *  time is a genuinely bad state - each would be deleting the other's work
 *  half way through. So the second copy does not start; it points at the one
 *  already open instead.
 * ------------------------------------------------------------------------ */

/* Claims the name for this program. Returns NULL if another copy already
 * holds it, in which case that copy's window is brought to the front. The
 * handle is closed by CloseHandle, or by the process ending. */
HANDLE  ClaimSingleInstance(const wchar_t *uniqueName, const wchar_t *windowTitle);

/* ---------------------------------------------------------------------------
 *  Text files
 *
 *  Everything written is UTF-8 with a byte-order mark, which is what Notepad
 *  expects, so folder names with accents in them survive a round trip.
 * ------------------------------------------------------------------------ */

/* Reads a whole text file and returns it as wide text the caller must free
 * with LocalFree. Understands UTF-8 with or without a mark, and UTF-16.
 * Returns NULL if the file cannot be read. */
wchar_t *ReadTextFile(const wchar_t *path);

BOOL    WriteTextFileUtf8(const wchar_t *path, const wchar_t *text);

/* ---------------------------------------------------------------------------
 *  Small string helpers
 * ------------------------------------------------------------------------ */

void    TrimInPlace(wchar_t *s);
BOOL    EqualsNoCase(const wchar_t *a, const wchar_t *b);
BOOL    ContainsNoCase(const wchar_t *haystack, const wchar_t *needle);

#endif /* MONSTERRPG_COMMON_H */
