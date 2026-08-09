/* ===========================================================================
 *  Blockland MonsterRPG Uninstaller.exe
 *
 *  Built as Uninstaller.exe and renamed by Setup when it writes it out; the
 *  name it ends up with is UNINSTALLER_NAME in Common.h, chosen so it sorts
 *  directly above Blockland MonsterRPG.exe in the game folder.
 *
 *  Sits in the Blockland folder next to the install log Setup wrote, shows
 *  exactly what is about to be removed, and removes it.
 *
 *  IT ONLY REMOVES WHAT THE LOG LISTS
 *
 *  Not "every folder that looks like a mod", and never the game itself. The
 *  log is a plain text file anyone can open and read before pressing the
 *  button, which is the point of it being a file rather than a hidden record.
 *
 *  If the log has been deleted, the standard list is shown instead and said to
 *  be a guess, so the choice of whether to trust it stays with the person
 *  looking at the screen.
 *
 *  REMOVING ITSELF
 *
 *  A running program cannot delete its own file. So at the very end this
 *  copies itself into the temporary folder, starts that copy with /cleanup,
 *  and exits; the copy waits for this one to close, deletes the original and
 *  the log, and then arranges for its own copy to go at the next restart.
 *  Nothing is left behind in the game folder.
 * ======================================================================== */

#define COBJMACROS
#define WIN32_LEAN_AND_MEAN

#include "Common.h"
#include "Resource.h"

#include <commctrl.h>
#include <shlobj.h>
#include <shellapi.h>
#include <objbase.h>
#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define WM_APP_LINE     (WM_APP + 1)
#define WM_APP_PROGRESS (WM_APP + 2)
#define WM_APP_DONE     (WM_APP + 3)

#define MAX_ENTRIES 128

typedef struct {
    wchar_t kind[16];
    wchar_t path[MAX_PATH * 2];
} Entry;

typedef struct {
    HWND      dlg;
    HINSTANCE inst;

    wchar_t   gameDir[MAX_PATH * 2];
    wchar_t   logPath[MAX_PATH * 2];
    wchar_t   selfPath[MAX_PATH * 2];

    Entry     entries[MAX_ENTRIES];
    int       count;
    BOOL      fromLog;

    HANDLE    worker;       /* kept so the process cannot end mid-removal */
    BOOL      removing;
    BOOL      finished;
    int       failures;
    int       refused;      /* lines the safety checks would not act on */

    HFONT     titleFont;
    HBRUSH    whiteBrush;
    int       headerPx;
    int       footerPx;
} Ctx;

static Ctx g_ctx;

/* ---------------------------------------------------------------------------
 *  Window helpers, the same shape as Setup's
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

static void AppendLine(HWND edit, const wchar_t *line)
{
    int len = GetWindowTextLengthW(edit);
    SendMessageW(edit, EM_SETSEL, (WPARAM)len, (LPARAM)len);
    SendMessageW(edit, EM_REPLACESEL, FALSE, (LPARAM)line);
    SendMessageW(edit, EM_REPLACESEL, FALSE, (LPARAM)L"\r\n");
    SendMessageW(edit, EM_SCROLLCARET, 0, 0);
}

/* ---------------------------------------------------------------------------
 *  Reading the install log
 * ------------------------------------------------------------------------ */

static void AddEntry(Ctx *c, const wchar_t *kind, const wchar_t *path)
{
    if (c->count >= MAX_ENTRIES)
        return;
    wcsncpy(c->entries[c->count].kind, kind, 15);
    c->entries[c->count].kind[15] = L'\0';
    wcsncpy(c->entries[c->count].path, path, MAX_PATH * 2 - 1);
    c->entries[c->count].path[MAX_PATH * 2 - 1] = L'\0';
    c->count++;
}

/* Used only when the log has gone. Everything here is checked for existence
 * before it is offered, so nothing that was never installed is listed. */
static void AddStandardGuess(Ctx *c)
{
    static const wchar_t *const folders[] = {
        L"Add-Ons\\Client_MonsterRPG",
        FOLDER_TICKRATE,
        FOLDER_AUDIO,
        NULL
    };
    static const wchar_t *const files[] = {
        L"Add-Ons\\Client_MonsterRPG.zip",
        LAUNCHER_NAME,
        NULL
    };
    wchar_t full[MAX_PATH * 2];
    wchar_t lnk[MAX_PATH * 2];
    wchar_t dir[MAX_PATH * 2];
    int i;

    for (i = 0; folders[i] != NULL; ++i) {
        PathJoin(full, sizeof(full) / sizeof(full[0]), c->gameDir, folders[i]);
        if (DirExists(full))
            AddEntry(c, L"FOLDER", folders[i]);
    }
    for (i = 0; files[i] != NULL; ++i) {
        PathJoin(full, sizeof(full) / sizeof(full[0]), c->gameDir, files[i]);
        if (FileExists(full))
            AddEntry(c, L"FILE", files[i]);
    }

    if (GetStartMenuProgramsDir(dir, sizeof(dir) / sizeof(dir[0]))) {
        PathJoin(lnk, sizeof(lnk) / sizeof(lnk[0]), dir, SHORTCUT_NAME);
        if (FileExists(lnk))
            AddEntry(c, L"SHORTCUT", lnk);
    }
    if (GetDesktopDir(dir, sizeof(dir) / sizeof(dir[0]))) {
        PathJoin(lnk, sizeof(lnk) / sizeof(lnk[0]), dir, SHORTCUT_NAME);
        if (FileExists(lnk))
            AddEntry(c, L"SHORTCUT", lnk);

        PathJoin(lnk, sizeof(lnk) / sizeof(lnk[0]), dir, SHORTCUT_UNINST);
        if (FileExists(lnk))
            AddEntry(c, L"SHORTCUT", lnk);
    }
}

static void LoadEntries(Ctx *c)
{
    wchar_t *text;
    wchar_t *line;

    c->count = 0;
    c->fromLog = FALSE;

    text = ReadTextFile(c->logPath);
    if (text == NULL) {
        AddStandardGuess(c);
        return;
    }

    line = text;
    while (line != NULL && *line != L'\0') {
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

        if (tab[1] == L'\0')
            continue;

        if (EqualsNoCase(buf, L"FOLDER") || EqualsNoCase(buf, L"FILE") ||
            EqualsNoCase(buf, L"SHORTCUT")) {
            AddEntry(c, buf, tab + 1);
            c->fromLog = TRUE;
        }
    }

    LocalFree(text);

    if (c->count == 0)
        AddStandardGuess(c);
}

static void DescribeEntries(Ctx *c, HWND list)
{
    int i;

    SetWindowTextW(list, L"");

    if (c->count == 0) {
        AppendLine(list,
            L"There is nothing here to remove. Either MonsterRPG has already been "
            L"taken off this computer, or this program is not in the Blockland "
            L"folder it was installed into.");
        return;
    }

    for (i = 0; i < c->count; ++i) {
        wchar_t line[MAX_PATH * 2 + 64];
        wchar_t full[MAX_PATH * 2];

        if (EqualsNoCase(c->entries[i].kind, L"SHORTCUT")) {
            _snwprintf(line, sizeof(line) / sizeof(line[0]),
                       L"Shortcut     %s", c->entries[i].path);
        } else {
            PathJoin(full, sizeof(full) / sizeof(full[0]), c->gameDir, c->entries[i].path);
            _snwprintf(line, sizeof(line) / sizeof(line[0]), L"%s       %s",
                       EqualsNoCase(c->entries[i].kind, L"FOLDER") ? L"Folder  " : L"File    ",
                       full);
        }
        line[(sizeof(line) / sizeof(line[0])) - 1] = L'\0';
        AppendLine(list, line);
    }

    AppendLine(list, L"");
    AppendLine(list, L"Also: the MonsterRPG entry in Windows' Apps and features list,");
    AppendLine(list, L"this uninstaller, and the install log next to it.");
    AppendLine(list, L"");
    AppendLine(list, L"Blockland itself is not touched.");
}

/* ---------------------------------------------------------------------------
 *  Doing it
 * ------------------------------------------------------------------------ */

/* The last word on whether a line from the install log may be deleted.
 *
 * The log is a text file. It can be edited by hand, damaged, or written by
 * some future version that got something wrong. None of that is allowed to
 * become "delete the wrong folder", so every line is checked here against
 * where it claims to be, whatever it says.
 *
 * A line that fails is skipped and reported on screen. Refusing to delete
 * something is always recoverable; deleting the wrong thing is not. */
static BOOL MayRemove(Ctx *c, const Entry *e, const wchar_t *full, const wchar_t **why)
{
    if (EqualsNoCase(e->kind, L"SHORTCUT")) {
        /* Shortcut lines are full paths, so they are held to living in the
         * user's own Desktop or Start menu and being a .lnk. */
        if (!IsSafeShortcutPath(e->path)) {
            *why = L"not a shortcut in your Desktop or Start menu";
            return FALSE;
        }
        return TRUE;
    }

    if (!IsSafeRelativePath(e->path)) {
        *why = L"not a plain path inside the Blockland folder";
        return FALSE;
    }

    if (IsProtectedGameItem(e->path)) {
        *why = L"part of Blockland itself";
        return FALSE;
    }

    /* Belt and braces: whatever the two checks above concluded, the thing
     * about to be deleted has to actually sit inside the game folder once
     * Windows has resolved the path. */
    if (!IsInsideFolder(c->gameDir, full)) {
        *why = L"outside the Blockland folder";
        return FALSE;
    }

    return TRUE;
}

static DWORD WINAPI RemoveThread(LPVOID param)
{
    Ctx *c = (Ctx *)param;
    int i;

    for (i = 0; i < c->count; ++i) {
        wchar_t full[MAX_PATH * 2];
        const wchar_t *why = NULL;
        BOOL ok = TRUE;

        if (EqualsNoCase(c->entries[i].kind, L"SHORTCUT")) {
            wcsncpy(full, c->entries[i].path, (sizeof(full) / sizeof(full[0])) - 1);
            full[(sizeof(full) / sizeof(full[0])) - 1] = L'\0';
        } else {
            PathJoin(full, sizeof(full) / sizeof(full[0]), c->gameDir, c->entries[i].path);
        }

        if (!MayRemove(c, &c->entries[i], full, &why)) {
            PostText(c->dlg, WM_APP_LINE, L"LEFT ALONE  %s", full);
            PostText(c->dlg, WM_APP_LINE, L"            (%s, so it was not touched)", why);
            c->refused++;
            PostMessageW(c->dlg, WM_APP_PROGRESS,
                         (WPARAM)(((i + 1) * 100) / (c->count > 0 ? c->count : 1)), 0);
            continue;
        }

        /* This program and the log it is reading are on the list too, and a
         * running program cannot delete its own file. They are left to the
         * copy started by ScheduleSelfRemoval once this window has closed.
         * Trying here would fail every time and turn a clean uninstall into
         * one that reports problems and then refuses to tidy up after itself. */
        if (EqualsNoCase(c->entries[i].path, UNINSTALLER_NAME) ||
            EqualsNoCase(c->entries[i].path, MANIFEST_NAME) ||
            EqualsNoCase(full, c->selfPath)) {
            PostText(c->dlg, WM_APP_LINE, L"Will remove when this window closes  %s", full);
            PostMessageW(c->dlg, WM_APP_PROGRESS,
                         (WPARAM)(((i + 1) * 100) / (c->count > 0 ? c->count : 1)), 0);
            continue;
        }

        /* Judged by whether the thing is actually still there afterwards, not
         * by what the delete call returned. Those two disagree more often than
         * you would think, and the only one a person can check is the first. */
        if (EqualsNoCase(c->entries[i].kind, L"FOLDER")) {
            if (DirExists(full)) {
                DeleteTree(full);
                ok = !DirExists(full);
                PostText(c->dlg, WM_APP_LINE, ok ? L"Removed  %s"
                                                 : L"COULD NOT REMOVE  %s", full);
            } else {
                PostText(c->dlg, WM_APP_LINE, L"Already gone  %s", full);
            }
        } else {
            if (FileExists(full)) {
                DeleteFileHard(full);
                ok = !FileExists(full);
                PostText(c->dlg, WM_APP_LINE, ok ? L"Removed  %s"
                                                 : L"COULD NOT REMOVE  %s", full);
            } else {
                PostText(c->dlg, WM_APP_LINE, L"Already gone  %s", full);
            }
        }

        if (!ok)
            c->failures++;

        PostMessageW(c->dlg, WM_APP_PROGRESS,
                     (WPARAM)(((i + 1) * 100) / (c->count > 0 ? c->count : 1)), 0);
    }

    if (RegDeleteKeyW(HKEY_CURRENT_USER, REG_UNINSTALL_PATH) == ERROR_SUCCESS)
        PostText(c->dlg, WM_APP_LINE, L"Removed the entry in Apps and features");

    PostMessageW(c->dlg, WM_APP_DONE, 0, 0);
    return 0;
}

/* Hands the last two files - this program and the log beside it - to a copy of
 * itself running out of the temporary folder, because neither can be deleted
 * by the process that has them open. */
static void ScheduleSelfRemoval(Ctx *c)
{
    wchar_t tempDir[MAX_PATH];
    wchar_t tempExe[MAX_PATH * 2];
    wchar_t cmd[MAX_PATH * 6];
    STARTUPINFOW si;
    PROCESS_INFORMATION pi;
    wchar_t name[64];

    if (GetTempPathW(MAX_PATH, tempDir) == 0)
        return;

    _snwprintf(name, sizeof(name) / sizeof(name[0]),
               L"MonsterRPG-cleanup-%lu.exe", GetCurrentProcessId());
    name[(sizeof(name) / sizeof(name[0])) - 1] = L'\0';
    PathJoin(tempExe, sizeof(tempExe) / sizeof(tempExe[0]), tempDir, name);

    if (!CopyFileW(c->selfPath, tempExe, FALSE))
        return;

    _snwprintf(cmd, sizeof(cmd) / sizeof(cmd[0]),
               L"\"%s\" /cleanup %lu \"%s\" \"%s\"",
               tempExe, GetCurrentProcessId(), c->selfPath, c->logPath);
    cmd[(sizeof(cmd) / sizeof(cmd[0])) - 1] = L'\0';

    ZeroMemory(&si, sizeof(si));
    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESHOWWINDOW;
    si.wShowWindow = SW_HIDE;
    ZeroMemory(&pi, sizeof(pi));

    if (CreateProcessW(NULL, cmd, NULL, NULL, FALSE, CREATE_NO_WINDOW,
                       NULL, tempDir, &si, &pi)) {
        CloseHandle(pi.hThread);
        CloseHandle(pi.hProcess);
    } else {
        DeleteFileW(tempExe);
    }
}

/* The temporary copy. No window, no messages: wait, delete, disappear. */
static int RunCleanup(int argc, wchar_t **argv)
{
    DWORD pid;
    HANDLE other;
    wchar_t self[MAX_PATH * 2];
    int i;

    if (argc < 4)
        return 1;

    pid = (DWORD)_wtoi(argv[2]);

    other = OpenProcess(SYNCHRONIZE, FALSE, pid);
    if (other != NULL) {
        WaitForSingleObject(other, 15000);
        CloseHandle(other);
    }

    /* The handle can be closed a moment before Windows has finished releasing
     * the file, so the delete is retried for a few seconds rather than being
     * given one try and reported as a failure nobody can see. */
    for (i = 3; i < argc; ++i) {
        int tries;
        for (tries = 0; tries < 40; ++tries) {
            if (!FileExists(argv[i]) || DeleteFileW(argv[i]))
                break;
            Sleep(250);
        }
    }

    /* Now delete this copy of ourselves.
     *
     * MoveFileEx with MOVEFILE_DELAY_UNTIL_REBOOT looks like the obvious way
     * and is the wrong one: it records the rename in a machine-wide registry
     * key, so it needs administrator rights, which this installer deliberately
     * never asks for. It fails silently without them - and the helper then
     * sits in the temporary folder for good. Ten of them turned up during
     * testing, one per uninstall.
     *
     * A short-lived cmd does the job with no special rights at all: it waits
     * for this process to be gone, deletes the file, and exits. Nothing is
     * left behind either way. */
    if (GetModuleFileNameW(NULL, self, MAX_PATH * 2) != 0) {
        wchar_t comspec[MAX_PATH];
        wchar_t cmd[MAX_PATH * 4];
        STARTUPINFOW si;
        PROCESS_INFORMATION pi;
        BOOL handedOver = FALSE;

        if (GetEnvironmentVariableW(L"COMSPEC", comspec, MAX_PATH) == 0)
            wcscpy(comspec, L"cmd.exe");

        /* ping is the wait: it is always present, unlike timeout, which is not
         * on older Windows and refuses to run without a console anyway. */
        _snwprintf(cmd, sizeof(cmd) / sizeof(cmd[0]),
                   L"\"%s\" /d /s /c \"ping -n 3 127.0.0.1 >nul & del /f /q \"%s\"\"",
                   comspec, self);
        cmd[(sizeof(cmd) / sizeof(cmd[0])) - 1] = L'\0';

        ZeroMemory(&si, sizeof(si));
        si.cb = sizeof(si);
        si.dwFlags = STARTF_USESHOWWINDOW;
        si.wShowWindow = SW_HIDE;
        ZeroMemory(&pi, sizeof(pi));

        if (CreateProcessW(NULL, cmd, NULL, NULL, FALSE,
                           CREATE_NO_WINDOW | DETACHED_PROCESS,
                           NULL, NULL, &si, &pi)) {
            CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);
            handedOver = TRUE;
        }

        /* Only as a last resort, and only useful if this somehow runs with
         * administrator rights. Better than leaving the file for certain. */
        if (!handedOver)
            MoveFileExW(self, NULL, MOVEFILE_DELAY_UNTIL_REBOOT);
    }

    return 0;
}

/* ---------------------------------------------------------------------------
 *  The window
 * ------------------------------------------------------------------------ */

static void PaintBands(HWND dlg, Ctx *c)
{
    PAINTSTRUCT ps;
    HDC dc = BeginPaint(dlg, &ps);
    RECT client, band;
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

static INT_PTR CALLBACK UninstallProc(HWND dlg, UINT msg, WPARAM wp, LPARAM lp)
{
    Ctx *c = &g_ctx;

    switch (msg) {

    case WM_INITDIALOG: {
        HICON big, small;
        RECT r;
        HDC dc;
        int dpi;

        c->dlg = dlg;

        big   = (HICON)LoadImageW(c->inst, MAKEINTRESOURCEW(IDI_APP), IMAGE_ICON, 32, 32, 0);
        small = (HICON)LoadImageW(c->inst, MAKEINTRESOURCEW(IDI_APP), IMAGE_ICON, 16, 16, 0);
        if (big != NULL) {
            SendMessageW(dlg, WM_SETICON, ICON_BIG, (LPARAM)big);
            SendDlgItemMessageW(dlg, IDC_U_HEAD_ICON, STM_SETICON, (WPARAM)big, 0);
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
            SendDlgItemMessageW(dlg, IDC_U_HEAD_TITLE, WM_SETFONT, (WPARAM)c->titleFont, TRUE);

        c->whiteBrush = CreateSolidBrush(GetSysColor(COLOR_WINDOW));

        r.left = 0; r.top = 0; r.right = 4; r.bottom = 48;
        MapDialogRect(dlg, &r);
        c->headerPx = r.bottom;

        GetWindowRect(GetDlgItem(dlg, IDC_U_REMOVE), &r);
        MapWindowPoints(NULL, dlg, (POINT *)&r, 2);
        c->footerPx = r.top - (r.bottom - r.top) / 2;

        SetDlgItemTextW(dlg, IDC_U_HEAD_SUB, c->gameDir);

        if (c->fromLog) {
            SetDlgItemTextW(dlg, IDC_U_SUMMARY,
                L"This is everything MonsterRPG Setup put on the computer, taken "
                L"from the install log it left behind. Nothing else is touched.");
        } else {
            SetDlgItemTextW(dlg, IDC_U_SUMMARY,
                L"The install log is missing, so this is the standard list rather "
                L"than a record of what was actually installed. Read it before "
                L"pressing Remove.");
        }

        DescribeEntries(c, GetDlgItem(dlg, IDC_U_LIST));

        ShowWindow(GetDlgItem(dlg, IDC_U_PROGRESS), SW_HIDE);
        EnableWindow(GetDlgItem(dlg, IDC_U_REMOVE), c->count > 0);
        SetFocus(GetDlgItem(dlg, IDCANCEL));
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

        if (id == IDC_U_HEAD_TITLE || id == IDC_U_HEAD_SUB || id == IDC_U_HEAD_ICON) {
            SetBkMode(dc, TRANSPARENT);
            SetTextColor(dc, RGB(20, 20, 20));
            return (INT_PTR)c->whiteBrush;
        }
        if (id == IDC_U_LIST)
            break;

        SetBkMode(dc, TRANSPARENT);
        return (INT_PTR)GetSysColorBrush(COLOR_BTNFACE);
    }

    case WM_COMMAND:
        switch (LOWORD(wp)) {

        case IDC_U_REMOVE:
            if (c->finished) {
                EndDialog(dlg, 0);
                return TRUE;
            }
            if (c->removing)
                return TRUE;

            if (MessageBoxW(dlg,
                    L"Remove MonsterRPG from this computer?\n\n"
                    L"Blockland itself stays exactly as it is. Only the files in "
                    L"the list are removed.",
                    UNINSTALL_TITLE, MB_YESNO | MB_ICONQUESTION) != IDYES)
                return TRUE;

            /* Asked after they have said yes, not before, so nobody is
             * quizzed about closing their game until they have decided they
             * actually want this. The mod DLLs are loaded into Blockland while
             * it runs, and Windows will not delete a file that is open. */
            if (!EnsureGameClosed(dlg, c->gameDir, UNINSTALL_TITLE, L"removed"))
                return TRUE;

            c->removing = TRUE;
            c->failures = 0;
            c->refused  = 0;
            SetWindowTextW(GetDlgItem(dlg, IDC_U_LIST), L"");
            ShowWindow(GetDlgItem(dlg, IDC_U_PROGRESS), SW_SHOW);
            SendDlgItemMessageW(dlg, IDC_U_PROGRESS, PBM_SETRANGE32, 0, 100);
            SendDlgItemMessageW(dlg, IDC_U_PROGRESS, PBM_SETPOS, 0, 0);
            SetDlgItemTextW(dlg, IDC_U_STATUS, L"Removing...");
            EnableWindow(GetDlgItem(dlg, IDC_U_REMOVE), FALSE);
            EnableWindow(GetDlgItem(dlg, IDCANCEL), FALSE);

            {
                HANDLE t = CreateThread(NULL, 0, RemoveThread, c, 0, NULL);
                if (t == NULL) {
                    c->removing = FALSE;
                    EnableWindow(GetDlgItem(dlg, IDC_U_REMOVE), TRUE);
                    EnableWindow(GetDlgItem(dlg, IDCANCEL), TRUE);
                    MessageBoxW(dlg, L"Could not start. Restart the computer and try again.",
                                UNINSTALL_TITLE, MB_OK | MB_ICONERROR);
                } else {
                    c->worker = t;      /* closed at the end of wWinMain */
                }
            }
            return TRUE;

        case IDCANCEL:
            if (!c->removing)
                EndDialog(dlg, 1);
            return TRUE;
        }
        break;

    case WM_APP_LINE: {
        wchar_t *text = (wchar_t *)lp;
        AppendLine(GetDlgItem(dlg, IDC_U_LIST), text);
        LocalFree(text);
        return TRUE;
    }

    case WM_APP_PROGRESS:
        SendDlgItemMessageW(dlg, IDC_U_PROGRESS, PBM_SETPOS, wp, 0);
        return TRUE;

    case WM_APP_DONE:
        c->removing = FALSE;
        c->finished = TRUE;

        if (c->refused > 0) {
            SetDlgItemTextW(dlg, IDC_U_HEAD_TITLE, L"Some things were left alone on purpose");
            SetDlgItemTextW(dlg, IDC_U_STATUS, L"Finished, and some lines were refused.");
            AppendLine(GetDlgItem(dlg, IDC_U_LIST), L"");
            AppendLine(GetDlgItem(dlg, IDC_U_LIST),
                L"The lines marked LEFT ALONE named things outside the Blockland folder, "
                L"or parts of Blockland itself. This program only ever removes what it "
                L"put there, so it refused them rather than guess.");
            AppendLine(GetDlgItem(dlg, IDC_U_LIST),
                L"Nothing is wrong with your game. If you believe those files really are "
                L"part of MonsterRPG, remove them yourself.");
        } else if (c->failures == 0) {
            SetDlgItemTextW(dlg, IDC_U_HEAD_TITLE, L"MonsterRPG has been removed");
            SetDlgItemTextW(dlg, IDC_U_STATUS, L"Finished.");
            AppendLine(GetDlgItem(dlg, IDC_U_LIST), L"");
            AppendLine(GetDlgItem(dlg, IDC_U_LIST),
                L"Blockland is back to normal. Start it the way you did before.");
        } else {
            SetDlgItemTextW(dlg, IDC_U_HEAD_TITLE, L"Some files could not be removed");
            SetDlgItemTextW(dlg, IDC_U_STATUS, L"Finished, with problems.");
            AppendLine(GetDlgItem(dlg, IDC_U_LIST), L"");
            AppendLine(GetDlgItem(dlg, IDC_U_LIST),
                L"The lines above marked COULD NOT REMOVE are usually files that "
                L"are still open. Close Blockland and run this again.");
        }

        SetDlgItemTextW(dlg, IDC_U_REMOVE, L"Close");
        EnableWindow(GetDlgItem(dlg, IDC_U_REMOVE), TRUE);
        ShowWindow(GetDlgItem(dlg, IDCANCEL), SW_HIDE);
        SetFocus(GetDlgItem(dlg, IDC_U_REMOVE));
        InvalidateRect(dlg, NULL, TRUE);
        return TRUE;

    case WM_CLOSE:
        if (!c->removing)
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
    int argc = 0;
    wchar_t **argv;

    (void)prev; (void)cmdLine; (void)show;

    argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    if (argv != NULL && argc >= 2 && wcscmp(argv[1], L"/cleanup") == 0) {
        int rc = RunCleanup(argc, argv);
        LocalFree(argv);
        return rc;
    }
    if (argv != NULL)
        LocalFree(argv);

    ZeroMemory(&g_ctx, sizeof(g_ctx));
    g_ctx.inst = inst;

    /* One at a time. Two copies working through the same list would each be
     * reporting the other's deletions as failures of its own. The cleanup
     * helper above never gets here, so it is not affected. */
    instanceLock = ClaimSingleInstance(L"Local\\MonsterRPGUninstall.SingleInstance",
                                       UNINSTALL_TITLE);
    if (instanceLock == NULL) {
        MessageBoxW(NULL,
            L"The MonsterRPG remover is already open.\n\n"
            L"It has been brought to the front for you. If you cannot see it, "
            L"look for it on the taskbar.",
            UNINSTALL_TITLE, MB_OK | MB_ICONINFORMATION);
        return 0;
    }

    icc.dwSize = sizeof(icc);
    icc.dwICC  = ICC_PROGRESS_CLASS | ICC_STANDARD_CLASSES | ICC_WIN95_CLASSES;
    InitCommonControlsEx(&icc);

    CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);

    if (GetModuleFileNameW(NULL, g_ctx.selfPath,
                           sizeof(g_ctx.selfPath) / sizeof(g_ctx.selfPath[0])) == 0 ||
        !GetExeDir(g_ctx.gameDir, sizeof(g_ctx.gameDir) / sizeof(g_ctx.gameDir[0]))) {
        MessageBoxW(NULL, L"This program could not work out which folder it is in.",
                    UNINSTALL_TITLE, MB_OK | MB_ICONERROR);
        CoUninitialize();
        CloseHandle(instanceLock);
        return 1;
    }

    PathJoin(g_ctx.logPath, sizeof(g_ctx.logPath) / sizeof(g_ctx.logPath[0]),
             g_ctx.gameDir, MANIFEST_NAME);

    LoadEntries(&g_ctx);

    if (DialogBoxParamW(inst, MAKEINTRESOURCEW(IDD_UNINSTALL), NULL,
                        UninstallProc, 0) == -1) {
        wchar_t why[256];
        _snwprintf(why, sizeof(why) / sizeof(why[0]),
                   L"This program could not open its window (Windows error %lu).",
                   GetLastError());
        why[(sizeof(why) / sizeof(why[0])) - 1] = L'\0';
        MessageBoxW(NULL, why, UNINSTALL_TITLE, MB_OK | MB_ICONERROR);
        CoUninitialize();
        CloseHandle(instanceLock);
        return 1;
    }

    /* Make certain the removing thread has stopped before this process does. */
    if (g_ctx.worker != NULL) {
        WaitForSingleObject(g_ctx.worker, 15000);
        CloseHandle(g_ctx.worker);
        g_ctx.worker = NULL;
    }

    /* Only when everything else went. If something is still there - because it
     * would not delete, or because a safety check refused it - this program
     * and the log it reads have to stay so it can be run again. */
    if (g_ctx.finished && g_ctx.failures == 0 && g_ctx.refused == 0)
        ScheduleSelfRemoval(&g_ctx);

    CoUninitialize();
    return 0;
}
