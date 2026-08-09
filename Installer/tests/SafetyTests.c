/* ===========================================================================
 *  SafetyTests.c - checks the rules that decide what may be deleted.
 *
 *  Setup and the uninstaller both take their list of what to remove from a
 *  text file. This proves that the checks in Common.c refuse the ways such a
 *  file could point somewhere it should not - whether by accident, by damage,
 *  or because somebody edited it on purpose.
 *
 *  Build and run:  Installer\tests\run-tests.bat
 * ======================================================================== */

#include <windows.h>
#include <stdio.h>

#include "../src/Common.h"

static int g_failures = 0;
static int g_checks = 0;

static void Check(BOOL got, BOOL want, const char *what, const wchar_t *subject)
{
    g_checks++;
    if (got == want)
        return;

    g_failures++;
    wprintf(L"  FAIL  %hs\n        subject: \"%s\"\n        wanted %hs, got %hs\n",
            what, subject, want ? "allowed" : "refused", got ? "allowed" : "refused");
}

static void ExpectRefused(const wchar_t *rel, const char *what)
{
    Check(IsSafeRelativePath(rel), FALSE, what, rel);
}

static void ExpectAllowed(const wchar_t *rel, const char *what)
{
    Check(IsSafeRelativePath(rel), TRUE, what, rel);
}

int wmain(void)
{
    wchar_t game[] = L"C:\\Games\\Blockland";

    wprintf(L"Paths that must be REFUSED\n");

    ExpectRefused(L"", "empty - would mean the game folder itself");
    ExpectRefused(L"..", "the step that walks back out");
    ExpectRefused(L"..\\..\\Windows", "climbing out to somewhere else entirely");
    ExpectRefused(L"Add-Ons\\..\\..\\..\\Users", "climbing out from inside");
    ExpectRefused(L"\\Windows", "starting at the root of the drive");
    ExpectRefused(L"/Windows", "the same with a forward slash");
    ExpectRefused(L"C:\\Windows", "a full path on a drive");
    ExpectRefused(L"C:", "a bare drive letter");
    ExpectRefused(L"\\\\server\\share", "a network location");
    ExpectRefused(L"Add-Ons\\*", "a wildcard, which could mean anything");
    ExpectRefused(L"*.*", "the wildcard that means everything");
    ExpectRefused(L"Add-Ons\\\\Client", "an empty step in the middle");
    ExpectRefused(L"Add-Ons\\", "a trailing slash");
    ExpectRefused(L"Add-Ons ", "a trailing space - Windows would strip it");
    ExpectRefused(L"Add-Ons.", "a trailing dot - Windows would strip it too");
    ExpectRefused(L"Add-Ons \\Client", "a step ending in a space");
    ExpectRefused(L".", "goes nowhere");
    ExpectRefused(L".\\BLTickRate", "a step that goes nowhere");

    wprintf(L"\nPaths that must be ALLOWED\n");

    ExpectAllowed(L"BLTickRate", "a folder this installer really creates");
    ExpectAllowed(L"MonsterRPGAudio", "and another");
    ExpectAllowed(L"Add-Ons\\Client_MonsterRPG", "one inside Add-Ons");
    ExpectAllowed(L"Add-Ons\\Client_MonsterRPG.zip", "the zip beside it");
    ExpectAllowed(L"Blockland MonsterRPG.exe", "the launcher, spaces and all");
    ExpectAllowed(L"Uninstall MonsterRPG.exe", "the uninstaller");
    ExpectAllowed(L"Add-Ons\\Folder With Spaces\\file.txt", "spaces deeper down");

    wprintf(L"\nBlockland's own files, which must be REFUSED\n");

    Check(IsProtectedGameItem(L"Blockland.exe"), TRUE, "the game itself", L"Blockland.exe");
    Check(IsProtectedGameItem(L"Add-Ons"), TRUE, "every add-on anyone has", L"Add-Ons");
    Check(IsProtectedGameItem(L"base"), TRUE, "the game's own data", L"base");
    Check(IsProtectedGameItem(L"config"), TRUE, "settings and key bindings", L"config");
    Check(IsProtectedGameItem(L"saves"), TRUE, "saved builds", L"saves");
    Check(IsProtectedGameItem(L""), TRUE, "nothing at all", L"");

    wprintf(L"\n...while the installer's own things stay removable\n");

    Check(IsProtectedGameItem(L"Add-Ons\\Client_MonsterRPG"), FALSE,
          "inside Add-Ons is not Add-Ons", L"Add-Ons\\Client_MonsterRPG");
    Check(IsProtectedGameItem(L"BLTickRate"), FALSE, "a mod folder", L"BLTickRate");
    Check(IsProtectedGameItem(L"MonsterRPGAudio"), FALSE, "a mod folder", L"MonsterRPGAudio");

    wprintf(L"\nStaying inside the game folder\n");

    Check(IsInsideFolder(game, L"C:\\Games\\Blockland\\BLTickRate"), TRUE,
          "a folder inside it", L"C:\\Games\\Blockland\\BLTickRate");
    Check(IsInsideFolder(game, L"C:\\Games\\Blockland\\Add-Ons\\Client_MonsterRPG"), TRUE,
          "deeper inside it", L"C:\\Games\\Blockland\\Add-Ons\\Client_MonsterRPG");
    Check(IsInsideFolder(game, L"C:\\Games\\Blockland"), FALSE,
          "the folder itself is not inside itself", L"C:\\Games\\Blockland");
    Check(IsInsideFolder(game, L"C:\\Games"), FALSE,
          "the folder above it", L"C:\\Games");
    Check(IsInsideFolder(game, L"C:\\Games\\Blockland2\\thing"), FALSE,
          "a folder whose name merely starts the same", L"C:\\Games\\Blockland2\\thing");
    Check(IsInsideFolder(game, L"C:\\Games\\Blockland\\..\\..\\Windows"), FALSE,
          "a path that climbs out once resolved", L"C:\\Games\\Blockland\\..\\..\\Windows");
    Check(IsInsideFolder(game, L"C:\\Windows"), FALSE,
          "somewhere else entirely", L"C:\\Windows");

    wprintf(L"\nShortcuts\n");

    Check(IsSafeShortcutPath(L"C:\\Windows\\System32\\drivers\\etc\\hosts"), FALSE,
          "not a shortcut at all", L"C:\\Windows\\System32\\drivers\\etc\\hosts");
    Check(IsSafeShortcutPath(L"C:\\Windows\\notepad.lnk"), FALSE,
          "a .lnk somewhere it has no business being", L"C:\\Windows\\notepad.lnk");
    Check(IsSafeShortcutPath(L""), FALSE, "nothing at all", L"");

    {
        wchar_t desktop[MAX_PATH * 2];
        wchar_t lnk[MAX_PATH * 2];

        if (GetDesktopDir(desktop, MAX_PATH * 2)) {
            PathJoin(lnk, MAX_PATH * 2, desktop, L"MonsterRPG.lnk");
            Check(IsSafeShortcutPath(lnk), TRUE, "the one on your Desktop", lnk);

            PathJoin(lnk, MAX_PATH * 2, desktop, L"MonsterRPG.exe");
            Check(IsSafeShortcutPath(lnk), FALSE,
                  "a program on the Desktop is not a shortcut", lnk);
        }
    }

    wprintf(L"\n=====================================================\n");
    if (g_failures == 0) {
        wprintf(L"  ALL %d CHECKS PASSED\n", g_checks);
        wprintf(L"=====================================================\n");
        return 0;
    }

    wprintf(L"  %d of %d CHECKS FAILED\n", g_failures, g_checks);
    wprintf(L"=====================================================\n");
    return 1;
}
