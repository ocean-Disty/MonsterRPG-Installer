// =============================================================================
// MonsterRPGAudio.dll — ray-traced game audio, played natively
//
// Phase 1 of AUDIORT_NATIVE_PLAN.md: get loaded, identify the machine, prove we
// can talk to TorqueScript, and DO NOTHING ELSE.
//
// There is no socket here, no audio device, no hook on anything the game does.
// That is the whole point of this phase — everything that follows is easier to
// debug if the thing it is built on is known to be inert.
// =============================================================================

#include <windows.h>
#include <psapi.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "vendor/BlHooks.hpp"
#include "vendor/BlFuncs.hpp"

#include "Log.hpp"
#include "Cfg.hpp"
#include "GpuProbe.hpp"
#include "Net.hpp"
#include "MrpgCrypto.h"
#include "Audio.hpp"
#include "Capture.hpp"
#include "Devices.hpp"

#define MRPGAUDIO_VERSION "0.7.1-reverb-fused"

namespace {

HINSTANCE g_hDll     = nullptr;
char      g_dllDir[MAX_PATH * 2] = {0};

// The three states this module can be in. Phase 1 only ever reaches READY.
enum State {
    STATE_LOADED = 0,   // DllMain ran; engine not up yet
    STATE_READY,        // console functions registered; waiting to be invited
    STATE_INERT         // deliberately doing nothing (dedicated server, or a
                        // failure we chose not to escalate)
};
volatile LONG g_state  = STATE_LOADED;
const char*   g_inertWhy = "";

// The rendezvous hook, and a latch so it binds exactly once however many
// messages the game pumps before we manage to unhook.
HHOOK         g_msgHook = nullptr;
volatile LONG g_bound   = 0;

// ── TorqueScript surface ─────────────────────────────────────────────────────
//
// Only functions that WORK are registered. Core_AudioRT.cs guards every native
// call with isFunction(), and that guard is only worth anything if the presence
// of a name means the thing behind it is real. MRPGAudio_Connect does not appear
// here because Phase 2 has not been written — script must be able to detect a
// Phase 1 DLL and fall back, not call into a stub and believe it worked.

const char* TS_Version(ADDR, int, const char*[])
{
    return MRPGAUDIO_VERSION;
}

// "state gpuVerdict rayQuery policyPass vendorId deviceId adapterName..."
//
// APPEND ONLY, and the adapter name is LAST because it is the only field that
// can contain spaces. Any field added after it would be unreachable by getWord,
// and any field inserted before it silently shifts every reading after it —
// which is worse than reporting nothing, because it looks like data. The
// server-side AudioRT_Stat has exactly this contract and exactly this comment.
const char* TS_State(ADDR, int, const char*[])
{
    static char out[384];
    const MrpgGpu::Result& g = MrpgGpu::Probe(g_dllDir);

    // "connected" is a new VALUE of the existing first field, not a new field.
    // Adding a field would shift every reading after it, which is why the adapter
    // name is last and why this had to go here instead.
    const char* state = "loaded";
    if (MrpgNet::IsConnected()) state = "connected";
    else if (g_state == STATE_READY) state = "ready";
    else if (g_state == STATE_INERT) state = "inert";

    _snprintf(out, sizeof(out) - 1, "%s %s %d %d %u %u %s",
              state,
              MrpgGpu::VerdictName(g.verdict),
              (int)g.hasRayQuery,
              (int)g.policyPass,
              g.vendorId,
              g.deviceId,
              g.name[0] ? g.name : "unidentified");
    out[sizeof(out) - 1] = '\0';
    return out;
}

// One sentence, for a player who asks why in chat. Kept separate from
// MRPGAudio_State so the machine-readable line never has to carry prose.
const char* TS_GpuWhy(ADDR, int, const char*[])
{
    return MrpgGpu::Probe(g_dllDir).why;
}

// Lets the client add-on put its own markers in our log. The two halves of this
// system fail at different times and in different files; being able to write
// "joined server X" into the DLL's own log is what makes the two readable side
// by side afterwards.
const char* TS_Log(ADDR, int argc, const char* argv[])
{
    if (argc >= 2 && argv[1])
        MrpgLog::Write("script: %s", argv[1]);
    return "1";
}

// MRPGAudio_Connect(ip, port, sessionKeyHex, blId, manifestVer)
//
// THE SESSION KEY IS THE SECURITY OF THE WHOLE LINK. The server generates 16
// random bytes per invite and delivers them over commandToClient — that is,
// over Blockland's own connection, which is already authenticated in the only
// sense that matters here: the server knows who is on the other end of it.
// Possession of that key is what proves a UDP packet came from this player.
//
// Without it the server would be a DDoS amplifier — a small spoofed HELLO makes
// it stream audio to any address an attacker names — and anyone could hijack a
// player's audio by claiming their blId. See MrpgCrypto.h.
//
// The key never appears in the log, and it never touches the UDP wire.
//
// THE CLIENT SUPPLIES THE IP, NOT THE SERVER, and that is deliberate. The server
// does not reliably know its own reachable address: this one is published on a
// public IP and also reached over the LAN on a different one, and a server that
// told every client "connect to me at 192.168.1.107" would work perfectly at
// home and for nobody else. The address the client used to reach the game is by
// construction the address that works, so script passes its own
// ServerConnection.getAddress().
//
// SPLIT THAT ON ':'. It is "1.2.3.4:28000" with NO "IP:" prefix, so
// getWord(%addr, 1) hands you the PORT. That mistake has already shipped once in
// this tree and silently mislabelled every LAN player.
//
// Only the PORT and the TOKEN come from the server, over commandToClient.
const char* TS_Connect(ADDR, int argc, const char* argv[])
{
    if (g_state != STATE_READY && g_state != STATE_LOADED) return "0";
    if (argc < 6) return "0";

    const char*  ip          = argv[1];
    int          port        = atoi(argv[2]);
    const char*  keyHex      = argv[3];
    unsigned int blId        = (unsigned int)strtoul(argv[4], nullptr, 10);
    unsigned int manifestVer = (unsigned int)strtoul(argv[5], nullptr, 10);

    return MrpgNet::Connect(ip, port, keyHex, blId, manifestVer) ? "1" : "0";
}

// MRPGAudio_Release([why]) — leaving a server, or the server asking us to stop.
//
// The client add-on must call this on disconnect. It borrows the link for the
// duration of one server and hands it back; nothing may survive into the next
// one, because this add-on loads on every server including ones that have never
// heard of MonsterRPG.
const char* TS_Release(ADDR, int argc, const char* argv[])
{
    MrpgNet::Release((argc >= 2 && argv[1] && argv[1][0]) ? argv[1] : "script asked");
    return "1";
}

// MRPGAudio_MapProfile(name, path) — one of the client's own AudioProfiles.
//
// THE CLIENT SUPPLIES THE PATHS, NOT THE SERVER, and that is the same argument
// as the address: the server has no idea where this machine keeps its add-ons,
// and guessing would work on the host's computer and nowhere else. Script walks
// its own DataBlockGroup and reports what it actually has; the server only ever
// says which NAME an id refers to.
//
// A name the client has no profile for is simply never mapped, so a player
// missing an add-on skips that sound instead of being told to open a file that
// is not there.
const char* TS_MapProfile(ADDR, int argc, const char* argv[])
{
    if (argc < 3) return "0";
    // The RESULT, not an unconditional "1". Script counts these, and a count
    // that cannot fail is not a measurement.
    return MrpgAudio::MapProfile(argv[1], argv[2]) ? "1" : "0";
}

// MRPGAudio_Listener(x, y, z, fwdX, fwdY, fwdZ) — pushed by the client add-on.
//
// PHASE 4 DELETES THIS. The whole point of the native path is that the client
// re-projects into head space at audio-block rate against its own live camera;
// a script push is still a sampled pose and still lags a turn. It is here
// because Phase 3 has to prove that a sound arrives from roughly the right
// direction before Phase 4 can prove it arrives from exactly the right one.
const char* TS_Listener(ADDR, int argc, const char* argv[])
{
    if (argc < 7) return "0";
    MrpgAudio::SetListener((float)atof(argv[1]), (float)atof(argv[2]), (float)atof(argv[3]),
                           (float)atof(argv[4]), (float)atof(argv[5]), (float)atof(argv[6]));
    return "1";
}

// MRPGAudio_AudioStat() — "running device voices loaded pending played missed dropped underruns"
const char* TS_AudioStat(ADDR, int, const char*[])
{
    return MrpgAudio::StatLine();
}

// MRPGAudio_TestTone([pan]) — prove the speakers work, with nothing else involved.
const char* TS_TestTone(ADDR, int argc, const char* argv[])
{
    if (!MrpgAudio::IsRunning()) return "0";
    float pan = (argc >= 2) ? (float)atof(argv[1]) : 0.0f;
    if (pan < -1.0f) pan = -1.0f;
    if (pan >  1.0f) pan =  1.0f;
    MrpgAudio::PlayTestTone(pan);
    return "1";
}

// MRPGAudio_VoiceKey(0|1) — the push-to-talk key, down or up.
//
// Bound through Client_MonsterRPG's existing keybind table, which already
// borrows keys on join and hands them back on leave, and already persists a
// player's remap. Nothing new was needed for that; this is just another row.
const char* TS_VoiceKey(ADDR, int argc, const char* argv[])
{
    if (argc < 2) return "0";
    MrpgCapture::SetPushToTalk(atoi(argv[1]) != 0);
    return "1";
}

// MRPGAudio_VoiceStat() — see MrpgCapture::StatLine for the field list. Read by
// index; new fields go on the end, never in the middle.
const char* TS_VoiceStat(ADDR, int, const char*[])
{
    return MrpgCapture::StatLine();
}

// ── The settings surface ─────────────────────────────────────────────────────
//
// Everything the options menu needs. Deliberately fine-grained getters rather
// than one blob: a device name can contain spaces, so a list packed into one
// TorqueScript string could not be split reliably by anything.

// MRPGAudio_SetVolume(category, 0..2). 0 master, 1 sfx, 2 music, 3 voice.
const char* TS_SetVolume(ADDR, int argc, const char* argv[])
{
    if (argc < 3) return "0";
    MrpgAudio::SetVolume(atoi(argv[1]), (float)atof(argv[2]));
    return "1";
}

const char* TS_GetVolume(ADDR, int argc, const char* argv[])
{
    static char out[32];
    if (argc < 2) return "0";
    _snprintf(out, sizeof(out) - 1, "%.4f", MrpgAudio::GetVolume(atoi(argv[1])));
    out[sizeof(out) - 1] = '\0';
    return out;
}

// MRPGAudio_DeviceCount(kind) — 0 output, 1 input. Rescans first, because the
// player may have plugged something in with the menu already open.
const char* TS_DeviceCount(ADDR, int argc, const char* argv[])
{
    static char out[16];
    const int kind = (argc >= 2) ? atoi(argv[1]) : 0;
    const int n = MrpgDevices::Refresh(kind ? MrpgDevices::CAPTURE : MrpgDevices::RENDER);
    _snprintf(out, sizeof(out) - 1, "%d", n);
    out[sizeof(out) - 1] = '\0';
    return out;
}

// Name and id are separate calls ON PURPOSE. A friendly name contains spaces
// ("Speakers (Realtek(R) Audio)"), so returning "id name" would be unsplittable
// at the far end without knowing where one stops.
const char* TS_DeviceName(ADDR, int argc, const char* argv[])
{
    if (argc < 3) return "";
    return MrpgDevices::Name(atoi(argv[1]) ? MrpgDevices::CAPTURE : MrpgDevices::RENDER,
                             atoi(argv[2]));
}

const char* TS_DeviceId(ADDR, int argc, const char* argv[])
{
    if (argc < 3) return "";
    return MrpgDevices::Id(atoi(argv[1]) ? MrpgDevices::CAPTURE : MrpgDevices::RENDER,
                           atoi(argv[2]));
}

const char* TS_DeviceIsDefault(ADDR, int argc, const char* argv[])
{
    if (argc < 3) return "0";
    return MrpgDevices::IsDefault(atoi(argv[1]) ? MrpgDevices::CAPTURE : MrpgDevices::RENDER,
                                  atoi(argv[2])) ? "1" : "0";
}

// MRPGAudio_SetDevice(kind, endpointId). Empty id means the system default.
const char* TS_SetDevice(ADDR, int argc, const char* argv[])
{
    if (argc < 2) return "0";
    const int kind = atoi(argv[1]);
    const char* id = (argc >= 3) ? argv[2] : "";
    return (kind ? MrpgCapture::SetInputDevice(id)
                 : MrpgAudio::SetOutputDevice(id)) ? "1" : "0";
}

const char* TS_CurrentDevice(ADDR, int argc, const char* argv[])
{
    const int kind = (argc >= 2) ? atoi(argv[1]) : 0;
    return kind ? MrpgCapture::CurrentInputName() : MrpgAudio::CurrentOutputName();
}

// MRPGAudio_VoiceEnable(0|1) — the microphone, from the settings menu.
const char* TS_VoiceEnable(ADDR, int argc, const char* argv[])
{
    if (argc < 2) return "0";
    return MrpgCapture::SetEnabled(atoi(argv[1]) != 0) ? "1" : "0";
}

// MRPGAudio_Stat() — "connected ageMs hellos sfxDgrams sfxRecords mus bad foreign forged names"
//
// APPEND ONLY. Read by getWord index on the script side, so inserting a field
// anywhere but the end silently shifts every reading after it and the numbers
// start lying under the right labels - which is worse than reporting nothing,
// because it looks like data. Same contract as the server's AudioRT_Stat.
const char* TS_Stat(ADDR, int, const char*[])
{
    return MrpgNet::StatLine();
}

// ── Registration, and the awkward question of WHICH THREAD ───────────────────
//
// THE PROBLEM. This DLL is injected into a SUSPENDED process, so DllMain runs
// before the engine's main thread has executed a single instruction. That is a
// perfect moment for thread safety (nothing else is running) and a useless one
// for us: Con::addCommand needs the StringTable and the namespace system, and
// neither exists yet. By the time they do exist, the main thread is running
// script — and StringTable::insert mutates a shared hash table on every new
// string, so registering from our own thread at that point is a genuine
// memory-corruption race, not a theoretical one.
//
// Neither end of the process is safe, so we need a rendezvous ON the main thread
// at a moment when it is not inside the interpreter.
//
// THE ANSWER IS A THREAD-TARGETED WH_GETMESSAGE HOOK. SetWindowsHookEx with the
// game window's owning thread id installs a callback that runs ON THAT THREAD,
// every time its pump retrieves a message — which for Torque is between frames,
// exactly the moment we want. We post one WM_NULL to guarantee a message flows,
// bind from inside the hook, and unhook immediately. Documented Win32 only, and
// it never touches the window's WndProc.
//
// SetTimer WAS TRIED FIRST AND IS NOT RELIABLE HERE. On the first real launch it
// returned 0 with ERROR_ACCESS_DENIED (5), and every launch silently took the
// unsafe fallback below.
//
// The documented rule is that SetTimer's hWnd "must be owned by the calling
// thread", which our watcher thread does not. But be careful about how much that
// explains: the SAME code succeeds against the test-rig host, whose window it
// does not own either. So Windows enforces the rule in Blockland and not there,
// and the difference has not been chased down. What is certain is that we do not
// satisfy the documented precondition, and a rendezvous that works by luck is
// not a rendezvous.
//
// WH_GETMESSAGE has no such precondition — a thread-targeted hook is the
// documented way to run code on another thread — so it is correct regardless of
// which of those two behaviours a given window produces.
//
// TWO LESSONS WORTH KEEPING. First: the failure was not that the rendezvous fired
// at the wrong time, it was that it never fired at all, and the fallback made
// that look like success. Second: the test rig was TOO FORGIVING — SetTimer works
// there, so the rig could never have caught this, and only a real launch did.
//
// OTHER REJECTED ALTERNATIVES, so nobody re-litigates them:
//
//   Subclassing via SetWindowLongPtr — works, but has to get the ANSI/Unicode
//     variant right or it corrupts the window, and it leaves a window of time
//     where another mod's subclass and ours can un-chain each other.
//   QueueUserAPC — an APC only fires in an alertable wait, and a game loop is
//     never in one.
//   PostThreadMessage — a thread message has no window, so DispatchMessage drops
//     it and the game never gives us control.
//   Hooking an engine function (Con::executef and friends) — needs a byte
//     pattern per Blockland build, for a rendezvous Win32 gives us free.
//
// THE WINDOW IS ALSO THE READINESS SIGNAL, and the ordering argument is worth
// stating: Torque calls Con::init() early in main(), well before it creates a
// window. So a window existing implies the console exists. It cannot be the
// other way round.

void RegisterFunctions()
{
    BlAddFunction(nullptr, nullptr, "MRPGAudio_Version", (tsf_StringCallback)TS_Version, "", 1, 1);
    BlAddFunction(nullptr, nullptr, "MRPGAudio_State",   (tsf_StringCallback)TS_State,   "", 1, 1);
    BlAddFunction(nullptr, nullptr, "MRPGAudio_GpuWhy",  (tsf_StringCallback)TS_GpuWhy,  "", 1, 1);
    BlAddFunction(nullptr, nullptr, "MRPGAudio_Log",     (tsf_StringCallback)TS_Log,     "", 2, 2);
    BlAddFunction(nullptr, nullptr, "MRPGAudio_Connect", (tsf_StringCallback)TS_Connect, "", 6, 6);
    BlAddFunction(nullptr, nullptr, "MRPGAudio_Release", (tsf_StringCallback)TS_Release, "", 1, 2);
    BlAddFunction(nullptr, nullptr, "MRPGAudio_Stat",    (tsf_StringCallback)TS_Stat,    "", 1, 1);
    BlAddFunction(nullptr, nullptr, "MRPGAudio_MapProfile", (tsf_StringCallback)TS_MapProfile, "", 3, 3);
    BlAddFunction(nullptr, nullptr, "MRPGAudio_Listener",   (tsf_StringCallback)TS_Listener,   "", 7, 7);
    BlAddFunction(nullptr, nullptr, "MRPGAudio_AudioStat",  (tsf_StringCallback)TS_AudioStat,  "", 1, 1);
    BlAddFunction(nullptr, nullptr, "MRPGAudio_TestTone",   (tsf_StringCallback)TS_TestTone,   "", 1, 2);
    BlAddFunction(nullptr, nullptr, "MRPGAudio_VoiceKey",   (tsf_StringCallback)TS_VoiceKey,   "", 2, 2);
    BlAddFunction(nullptr, nullptr, "MRPGAudio_VoiceStat",  (tsf_StringCallback)TS_VoiceStat,  "", 1, 1);
    BlAddFunction(nullptr, nullptr, "MRPGAudio_SetVolume",      (tsf_StringCallback)TS_SetVolume,      "", 3, 3);
    BlAddFunction(nullptr, nullptr, "MRPGAudio_GetVolume",      (tsf_StringCallback)TS_GetVolume,      "", 2, 2);
    BlAddFunction(nullptr, nullptr, "MRPGAudio_DeviceCount",    (tsf_StringCallback)TS_DeviceCount,    "", 2, 2);
    BlAddFunction(nullptr, nullptr, "MRPGAudio_DeviceName",     (tsf_StringCallback)TS_DeviceName,     "", 3, 3);
    BlAddFunction(nullptr, nullptr, "MRPGAudio_DeviceId",       (tsf_StringCallback)TS_DeviceId,       "", 3, 3);
    BlAddFunction(nullptr, nullptr, "MRPGAudio_DeviceIsDefault",(tsf_StringCallback)TS_DeviceIsDefault,"", 3, 3);
    BlAddFunction(nullptr, nullptr, "MRPGAudio_SetDevice",      (tsf_StringCallback)TS_SetDevice,      "", 2, 3);
    BlAddFunction(nullptr, nullptr, "MRPGAudio_CurrentDevice",  (tsf_StringCallback)TS_CurrentDevice,  "", 1, 2);
    BlAddFunction(nullptr, nullptr, "MRPGAudio_VoiceEnable",    (tsf_StringCallback)TS_VoiceEnable,    "", 2, 2);
}

bool ScanAndRegister(bool onGameThread)
{
    if (!tsh_InitInternal()) {
        MrpgLog::Write("bind: RedoBlHooks could not find Con::printf.");
        MrpgLog::Write("bind: this almost always means Blockland has been updated and the byte");
        MrpgLog::Write("bind:   patterns have moved. Nothing was written and the game is unmodified.");
        return false;
    }
    if (!tsf_InitInternal()) {
        MrpgLog::Write("bind: BlFuncs could not resolve the console API. Same cause as above.");
        return false;
    }

    RegisterFunctions();
    MrpgLog::Write("bind: registered _Version / _State / _GpuWhy / _Log / _Connect /"
                   " _Release / _Stat / _MapProfile / _Listener / _AudioStat");

    // ── PROVE IT, DO NOT ASSERT IT ───────────────────────────────────────────
    //
    // Everything above only shows that BlAddFunction was CALLED. It does not
    // show that the engine accepted the registration, that the namespace entry
    // is reachable, or that we are on a thread where the interpreter can safely
    // be entered at all. Those are three different failures and all three look
    // identical from here: a log line saying "registered".
    //
    // So ask the engine. Evaluate one of our own functions and compare the
    // answer against the value we know it must return. A round trip through
    // Con::evaluate exercises the string table, the namespace lookup and the
    // callback dispatch - the exact machinery that has to work before any of
    // this is worth anything.
    //
    // Safe HERE and nowhere else in this file: we are on the game thread, from
    // inside its message pump, between frames, and not re-entering a script call
    // that is already running. That is the same argument that makes the
    // registration itself safe, and it is why this check lives in this function
    // rather than in the watcher.
    // ONLY ON THE GAME THREAD. Con::evaluate walks the string stack and the
    // namespace tables, and doing that from our own thread is the very race this
    // whole rendezvous exists to avoid - so on the fallback path the check is
    // SKIPPED rather than run unsafely.
    //
    // That distinction is not hypothetical. The first real launch took the
    // fallback and this eval answered "" - which looks exactly like "the
    // registration failed" and is not evidence of it, because an eval from the
    // wrong thread can return empty while the registration was fine. Running the
    // check where it cannot be trusted produces a scary line that means nothing,
    // which is worse than not checking.
    if (onGameThread) {
        const char* answer = BlEval("return MRPGAudio_Version();");
        if (answer && !strcmp(answer, MRPGAUDIO_VERSION)) {
            MrpgLog::Write("bind: VERIFIED - the engine evaluated MRPGAudio_Version()"
                           " and answered \"%s\"", answer);
        } else {
            MrpgLog::Write("bind: FAILED VERIFICATION - registered, but the engine"
                           " answered \"%s\" instead of \"%s\".",
                           answer ? answer : "(null)", MRPGAUDIO_VERSION);
            MrpgLog::Write("bind:   The console functions are not reachable from script.");
            return false;
        }
    } else {
        MrpgLog::Write("bind: not verifying - an eval from this thread would race the"
                       " interpreter and could not be trusted either way.");
    }

    return true;
}

// Runs ON THE GAME THREAD, from inside its message pump.
void BindHere()
{
    MrpgLog::Write("bind: on the game thread (tid %lu), binding to the console",
                   GetCurrentThreadId());

    if (ScanAndRegister(true)) {
        // The output device starts HERE and not in DllMain. Opening one before
        // we know the game is up would take a device on a machine whose owner
        // may never join a MonsterRPG server - and it must not happen under the
        // loader lock either, because WASAPI is COM and COM calls LoadLibrary.
        //
        // Failure is not fatal to anything: no device means CanPlay() stays
        // false, HELLO advertises 0, and the server keeps this player on stock
        // audio, which is exactly the designed fallback.
        if (!MrpgAudio::Init(g_dllDir))
            MrpgLog::Write("audio: no output device - this player stays on stock audio");

        InterlockedExchange(&g_state, STATE_READY);
        MrpgLog::Write("state: READY - loaded, bound, and waiting to be invited by a server.");
        MrpgLog::Write("state:   Nothing is hooked and no socket is open. Joining a non-MonsterRPG");
        MrpgLog::Write("state:   server will not change that.");
    } else {
        g_inertWhy = "could not bind to the console (Blockland probably updated)";
        InterlockedExchange(&g_state, STATE_INERT);
        MrpgLog::Write("state: INERT - %s", g_inertWhy);
        MrpgLog::Write("state:   Your game is completely normal. Stock audio, nothing patched.");
    }
}

LRESULT CALLBACK GetMsgHook(int code, WPARAM wParam, LPARAM lParam)
{
    // The latch, not just a bool: several messages can be in flight before the
    // unhook below takes effect, and binding twice would register every console
    // function twice.
    const bool mine = (code == HC_ACTION
                       && InterlockedCompareExchange(&g_bound, 1, 0) == 0);
    if (mine) BindHere();

    // Chain on BEFORE unhooking, so the rest of the chain still sees this
    // message and nothing observes a half-removed hook.
    LRESULT r = CallNextHookEx(g_msgHook, code, wParam, lParam);

    if (mine && g_msgHook) {
        UnhookWindowsHookEx(g_msgHook);
        g_msgHook = nullptr;
    }
    return r;
}

// ── Finding the game window ──────────────────────────────────────────────────

struct FindCtx {
    DWORD pid;
    HWND  found;
};

BOOL CALLBACK EnumProc(HWND hwnd, LPARAM lp)
{
    FindCtx* ctx = (FindCtx*)lp;

    DWORD pid = 0;
    GetWindowThreadProcessId(hwnd, &pid);
    if (pid != ctx->pid)                 return TRUE;
    if (!IsWindowVisible(hwnd))          return TRUE;
    if (GetWindow(hwnd, GW_OWNER))       return TRUE;   // a dialog, not the main window

    // A zero-size window is a message sink or a splash that has not laid out
    // yet; the real one has a client area.
    RECT r;
    if (!GetClientRect(hwnd, &r) || r.right <= 16 || r.bottom <= 16) return TRUE;

    ctx->found = hwnd;
    return FALSE;
}

DWORD WINAPI WatcherMain(LPVOID)
{
    // A dedicated server has no window, so the wait below would simply time out
    // — but saying so explicitly is worth more than being right by accident.
    // This DLL is a CLIENT component: it plays audio into speakers that a
    // headless server does not have.
    const char* cmd = GetCommandLineA();
    if (cmd && (strstr(cmd, "-dedicated") || strstr(cmd, "-dedi"))) {
        g_inertWhy = "this is a dedicated server; MonsterRPGAudio is a client component";
        InterlockedExchange(&g_state, STATE_INERT);
        MrpgLog::Write("state: INERT - %s", g_inertWhy);
        return 0;
    }

    // Check the crypto against the published test vectors, once, at startup.
    //
    // Connect() checks it again and refuses the link on failure — this earlier
    // run exists so a broken build says so in the log of EVERY launch, including
    // the ones where the player never joins a MonsterRPG server. A crypto bug
    // that only shows up as "the audio never connects" is a bug nobody diagnoses.
    if (MrpgCrypto::SelfTest()) {
        MrpgLog::Write("crypto: self test passed (RFC 8439 ChaCha20, RFC 4231 HMAC-SHA256)");
    } else {
        MrpgLog::Write("crypto: SELF TEST FAILED - this build is broken and the audio link");
        MrpgLog::Write("crypto:   will refuse to open. Please report this.");
    }

    // Identify the machine while we wait. It touches only a helper process and
    // the registry, so it is safe here and it means the log says what the player
    // has even if the engine never comes up at all.
    MrpgGpu::Probe(g_dllDir);

    const DWORD timeoutMs = 60000;
    const DWORD startMs   = GetTickCount();

    FindCtx ctx;
    ctx.pid = GetCurrentProcessId();

    for (;;) {
        ctx.found = nullptr;
        EnumWindows(EnumProc, (LPARAM)&ctx);

        if (ctx.found) {
            MrpgLog::Write("engine: game window up after %lu ms", GetTickCount() - startMs);

            // A short settle. The window exists a little before the first frame
            // is dispatched, and there is no cost to letting the pump start.
            Sleep(250);

            const DWORD gameTid = GetWindowThreadProcessId(ctx.found, nullptr);

            // Thread-targeted, so the callback runs on the GAME's thread rather
            // than ours. That is the whole point, and it is exactly what SetTimer
            // could not do: its window "must be owned by the calling thread".
            g_msgHook = SetWindowsHookExA(WH_GETMESSAGE, GetMsgHook, g_hDll, gameTid);
            if (g_msgHook) {
                MrpgLog::Write("engine: rendezvous armed (WH_GETMESSAGE on tid %lu)", gameTid);

                // One message, to guarantee the pump has something to retrieve.
                // A game window pumps constantly anyway; this removes the case
                // where it happens to be idle at exactly this moment.
                PostMessageA(ctx.found, WM_NULL, 0, 0);

                // ── HOW LONG TO WAIT, AND WHY IT IS THIS LONG ────────────────
                //
                // The first version waited 5 s and gave up, on a real launch, on
                // this machine. The window appears at ~1.7 s but Blockland then
                // spends a long time exec'ing scripts and loading datablocks
                // WITHOUT RETURNING TO ITS MESSAGE PUMP - and a WH_GETMESSAGE
                // hook cannot fire while nothing is retrieving messages. The hook
                // was fine; the deadline was nonsense.
                //
                // Three minutes, because there is no cost to waiting. NOTHING in
                // this module needs to be bound early: no socket opens, no audio
                // plays, and script cannot call us until a server invites us,
                // which cannot happen before the player has reached a menu - by
                // which time the pump is certainly running. Waiting is free and
                // giving up early is not.
                const int  waitMs   = 180000;
                const int  stepMs   = 100;
                const DWORD hookedAt = GetTickCount();

                for (int waited = 0; waited < waitMs; waited += stepMs) {
                    if (InterlockedCompareExchange(&g_bound, 0, 0)) return 0;

                    // One line every 15 s. Without it, a log from a slow-loading
                    // machine is indistinguishable from a hook that is broken.
                    if (waited > 0 && (waited % 15000) == 0)
                        MrpgLog::Write("engine:   still waiting for the game to pump messages"
                                       " (%d s - it is loading add-ons)", waited / 1000);
                    Sleep(stepMs);
                }

                MrpgLog::Write("engine: the hook never fired in %d s (armed at %lu ms); removing it",
                               waitMs / 1000, hookedAt);
                if (g_msgHook) { UnhookWindowsHookEx(g_msgHook); g_msgHook = nullptr; }
                if (InterlockedCompareExchange(&g_bound, 0, 0)) return 0;
            } else {
                MrpgLog::Write("engine: SetWindowsHookEx failed (%lu)", GetLastError());
            }

            // Falling back rather than giving up, but LOUDLY. This path does the
            // registration off the game thread, which races StringTable::insert.
            // It is here because "no audio at all" is a worse outcome than a
            // small race on four strings during startup - but if this line ever
            // appears in a log alongside a crash, this is the first suspect.
            //
            // IT SHOULD NOW BE UNREACHABLE. It was taken on EVERY launch while the
            // rendezvous was SetTimer, which could never have succeeded. If it
            // appears again, the hook is failing and that is the bug to chase.
            MrpgLog::Write("engine: falling back to registering off-thread.");
            MrpgLog::Write("engine:   THIS IS NOT THE SAFE PATH. If this launch crashes, say so:");
            MrpgLog::Write("engine:   it races the engine's string table.");
            if (ScanAndRegister(false)) {
                InterlockedExchange(&g_state, STATE_READY);
                MrpgLog::Write("state: READY (via the unsafe fallback)");
            } else {
                g_inertWhy = "could not bind to the console";
                InterlockedExchange(&g_state, STATE_INERT);
                MrpgLog::Write("state: INERT - %s", g_inertWhy);
            }
            return 0;
        }

        if (GetTickCount() - startMs > timeoutMs) {
            g_inertWhy = "no game window appeared within 60 s";
            InterlockedExchange(&g_state, STATE_INERT);
            MrpgLog::Write("state: INERT - %s", g_inertWhy);
            MrpgLog::Write("state:   Your game is unmodified. If it is running fine, this is a bug");
            MrpgLog::Write("state:   in MonsterRPGAudio's readiness check and worth reporting.");
            return 0;
        }

        Sleep(100);
    }
}

} // namespace

// ── Entry point ──────────────────────────────────────────────────────────────
//
// DllMain does the absolute minimum and returns. It runs on the injector's
// remote thread while the game is still suspended, under loader lock — so no
// LoadLibrary, no thread synchronisation, no waiting on anything. Creating one
// thread is allowed and is all we do.

BOOL APIENTRY DllMain(HINSTANCE hInst, DWORD reason, LPVOID)
{
    switch (reason) {
    case DLL_PROCESS_ATTACH: {
        g_hDll = hInst;
        DisableThreadLibraryCalls(hInst);

        GetModuleFileNameA(hInst, g_dllDir, sizeof(g_dllDir) - 1);
        char* slash = strrchr(g_dllDir, '\\');
        if (slash) *slash = '\0';

        MrpgLog::Init(g_dllDir);
        MrpgLog::Write("dll:  %s, 32-bit, loaded from %s", MRPGAUDIO_VERSION, g_dllDir);
        MrpgLog::Write("dll:  host process: %s", GetCommandLineA());

        MrpgCfg::Load(g_dllDir);
        MrpgNet::Init(g_dllDir);   // asserts the inert state; opens nothing

        // The probe and the wait both happen on this thread, off the loader
        // lock. Doing either here would deadlock: Vulkan's loader calls
        // LoadLibrary, and LoadLibrary inside DllMain is the classic way to hang
        // a process before it has drawn a single frame.
        HANDLE t = CreateThread(nullptr, 0, WatcherMain, nullptr, 0, nullptr);
        if (t) CloseHandle(t);
        else   MrpgLog::Write("dll:  CreateThread failed (%lu) - staying inert", GetLastError());
        break;
    }

    case DLL_PROCESS_DETACH:
        // Deliberately NOT stopping the audio device here. Shutdown joins two
        // threads, and on process exit every other thread has already been
        // killed - the join could never complete. The device dies with the
        // process, which is what process exit is for.
        // Release() joins a thread and calls WSACleanup, neither of which is
        // legal under the loader lock — and on process exit every other thread
        // has already been killed, so the join could never complete anyway.
        //
        // So this deliberately does NOT tear the link down on process exit. The
        // socket dies with the process, and the server's HELLO TTL notices three
        // seconds later, which is exactly the case that TTL exists for. Orderly
        // release happens on DISCONNECT, from script, on the game thread, where
        // it is both safe and useful.
        MrpgLog::Close("process exiting");
        break;
    }
    return TRUE;
}
