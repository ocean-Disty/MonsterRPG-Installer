#include <windows.h>
#include <stdio.h>
#include <stdarg.h>
#include <string.h>

#include "Log.hpp"

namespace MrpgLog {

namespace {

CRITICAL_SECTION s_lock;
bool             s_lockReady = false;
char             s_path[MAX_PATH * 2] = {0};
ULONGLONG        s_startMs = 0;

// Opened and closed per write rather than held open for the life of the process.
//
// A held handle is faster and would be wrong here: this DLL lives inside a game
// that can be killed by the player, by a driver reset, or by a crash in code
// that has nothing to do with us, and a buffered handle loses whatever it was
// holding at exactly the moment the log becomes worth reading. Every line is on
// disk before the call returns.
void AppendRaw(const char* text, int len)
{
    if (!s_path[0]) return;

    HANDLE h = CreateFileA(s_path, FILE_APPEND_DATA, FILE_SHARE_READ | FILE_SHARE_WRITE,
                           nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h == INVALID_HANDLE_VALUE) return;

    SetFilePointer(h, 0, nullptr, FILE_END);
    DWORD written = 0;
    WriteFile(h, text, (DWORD)len, &written, nullptr);
    CloseHandle(h);
}

} // namespace

void Init(const char* dllDir)
{
    if (!s_lockReady) {
        InitializeCriticalSection(&s_lock);
        s_lockReady = true;
    }
    s_startMs = GetTickCount64();

    // dllDir is ...\MonsterRPGAudio\bin — the log goes one level up, beside the
    // .bat, because that is the folder a player is looking at when something has
    // gone wrong and it is the folder the README tells them to look in.
    char dir[MAX_PATH * 2];
    lstrcpynA(dir, dllDir, sizeof(dir));

    size_t n = strlen(dir);
    while (n > 0 && (dir[n - 1] == '\\' || dir[n - 1] == '/')) dir[--n] = '\0';
    char* slash = strrchr(dir, '\\');
    if (!slash) slash = strrchr(dir, '/');
    if (slash) *slash = '\0';

    _snprintf(s_path, sizeof(s_path) - 1, "%s\\MonsterRPGAudio.log", dir);
    s_path[sizeof(s_path) - 1] = '\0';

    // Truncate. One launch, one log — a log that accumulates across launches is
    // one nobody can read, and the interesting launch is always the last one.
    HANDLE h = CreateFileA(s_path, GENERIC_WRITE, FILE_SHARE_READ, nullptr,
                           CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h != INVALID_HANDLE_VALUE) CloseHandle(h);

    SYSTEMTIME st;
    GetLocalTime(&st);
    Write("MonsterRPGAudio  -  ray-traced game audio, played natively");
    Write("launched %04u-%02u-%02u %02u:%02u:%02u  pid %lu",
          st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond,
          GetCurrentProcessId());
    Write("----------------------------------------------------------");
}

void Write(const char* fmt, ...)
{
    if (!s_path[0]) return;

    char body[1024];
    va_list args;
    va_start(args, fmt);
    int n = _vsnprintf(body, sizeof(body) - 1, fmt, args);
    va_end(args);
    if (n < 0) n = (int)sizeof(body) - 1;
    body[n] = '\0';

    // Milliseconds since load, not wall clock. Everything worth diagnosing here
    // is about ORDER and DELAY — did the engine come up before we tried to
    // register, how long did the probe take, how late was that datagram — and a
    // wall clock makes you do the subtraction yourself on every line.
    char line[1200];
    int m = _snprintf(line, sizeof(line) - 1, "[%7llu ms] %s\r\n",
                      (unsigned long long)(GetTickCount64() - s_startMs), body);
    if (m < 0) return;
    line[m] = '\0';

    if (s_lockReady) EnterCriticalSection(&s_lock);
    AppendRaw(line, m);
    if (s_lockReady) LeaveCriticalSection(&s_lock);
}

void Close(const char* reason)
{
    Write("----------------------------------------------------------");
    Write("closing: %s", reason ? reason : "(no reason given)");
}

} // namespace MrpgLog
