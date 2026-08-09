#include <windows.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <ctype.h>

#include "GpuProbe.hpp"
#include "Cfg.hpp"
#include "Log.hpp"

// =============================================================================
// GpuProbe — asks a 64-bit helper what the GPU can do, then applies policy
//
// The Vulkan work is NOT here. It is in MonsterRPGAudioProbe.exe, which is
// 64-bit, and the header of src/Probe64.cpp explains at length why that is not
// an implementation detail: AMD's 32-bit ICD exposes no ray-tracing extensions
// at all, so a probe running inside 32-bit Blockland.exe reports NO_RT for a
// 7900 XTX. Measured, not assumed.
//
// What lives here is everything that is not a hardware question: launching the
// helper, parsing its lines, applying the generation policy, and the cfg
// override. Splitting it that way keeps the helper reporting FACTS and this file
// making JUDGEMENTS, which is the only division that stays honest as the policy
// changes.
//
// Every log line in this file is plain ASCII. It is read in Notepad by players,
// and a UTF-8 em dash renders there as three bytes of noise — which is exactly
// what the first version of this file did.
// =============================================================================

namespace MrpgGpu {

namespace {

const unsigned int VENDOR_NVIDIA = 0x10DE;
const unsigned int VENDOR_AMD    = 0x1002;

Result s_result;
bool   s_probed = false;

// ── Name parsing ─────────────────────────────────────────────────────────────

// Tokenise on RUNS of whitespace, skipping empties.
//
// This is the getWord lesson transplanted: a double-space-separated string
// parses into zeroed fields if you assume one separator means one boundary, and
// it painted every point of interest in this game the wrong colour for months.
// Adapter names are exactly that shape - "Radeon(TM)  RX 6600M", "NVIDIA
// GeForce RTX 3060 Ti" - so nothing here may index a fixed word position.
int Tokenise(const char* s, char tok[16][64])
{
    int n = 0;
    const char* p = s;
    while (*p && n < 16) {
        while (*p == ' ' || *p == '\t' || *p == '(' || *p == ')') ++p;
        if (!*p) break;
        int k = 0;
        while (*p && *p != ' ' && *p != '\t' && *p != '(' && *p != ')' && k < 63)
            tok[n][k++] = (char)toupper((unsigned char)*p++);
        tok[n][k] = '\0';
        if (k) ++n;
        while (*p && *p != ' ' && *p != '\t' && *p != '(' && *p != ')') ++p;
    }
    return n;
}

// "3060" -> 3060; "3060TI" -> 3060; "TI" -> -1. Leading digits only, because
// suffixes are marketing and never change the generation.
int LeadingNumber(const char* tok)
{
    if (!isdigit((unsigned char)tok[0])) return -1;
    return atoi(tok);
}

// GATE B. Returns 1 pass, 0 fail, -1 unknown.
//
// UNKNOWN IS A REAL ANSWER AND MUST STAY ONE. Folding it into "fail" would
// quietly exclude every card whose name this function has not been taught, which
// is every card released after it was written.
int PolicyForName(unsigned int vendorId, const char* name)
{
    char tok[16][64];
    int  n = Tokenise(name, tok);

    if (vendorId == VENDOR_NVIDIA) {
        for (int i = 0; i < n; ++i) {
            // Pre-RT consumer parts, named explicitly rather than left to fall
            // through as unknown - a GTX 1080 reaching a benchmark would waste a
            // second proving what its name already said.
            if (!strcmp(tok[i], "GTX") || !strcmp(tok[i], "GT") || !strcmp(tok[i], "MX"))
                return 0;

            if (!strcmp(tok[i], "RTX") && i + 1 < n) {
                // "RTX A4000" / "RTX A6000" are the Ampere professional line -
                // Ampere or later by construction, so the letter alone passes.
                if (tok[i + 1][0] == 'A' && isdigit((unsigned char)tok[i + 1][1]))
                    return 1;

                int model = LeadingNumber(tok[i + 1]);
                if (model < 0) continue;

                // Laptop parts share the desktop numbering (a 3060 Laptop GPU is
                // still Ampere), so the same threshold is correct for both. It is
                // slower, and that is a benchmark question, not a name question.
                return (model >= 3000) ? 1 : 0;
            }
        }
        return -1;
    }

    if (vendorId == VENDOR_AMD) {
        for (int i = 0; i < n; ++i) {
            if (!strcmp(tok[i], "RX") && i + 1 < n) {
                int model = LeadingNumber(tok[i + 1]);
                if (model < 0) continue;
                // RX 6000 is RDNA2, AMD's first RT generation. Below it there is
                // no ray query at all and Gate A has already said so - this line
                // only ever fires on a driver reporting something odd.
                return (model >= 6000) ? 1 : 0;
            }
            // Radeon Pro W6800 / W7900 - same generation numbering as RX.
            if (tok[i][0] == 'W' && isdigit((unsigned char)tok[i][1])) {
                int model = LeadingNumber(tok[i] + 1);
                if (model >= 6000) return 1;
            }
        }
        // Integrated RDNA2/RDNA3 ("Radeon 780M", "Radeon Graphics") lands here.
        // Those DO have ray query and are genuinely weak, which is a benchmark
        // question and not a name question. Unknown on purpose.
        return -1;
    }

    // Intel Arc has ray tracing and is not covered by the rule we were given.
    // Open item 2 in AUDIORT_NATIVE_PLAN.md - Gate A plus a benchmark decides,
    // and it gets logged so the decision is eventually made on data.
    return -1;
}

// ── Fallback identification ──────────────────────────────────────────────────
//
// Worth the twenty lines. "The helper did not run" is the answer on the most
// broken machines, which are exactly the ones we most want to be able to name
// when a player reports a problem.
void ReadAdapterNameFromRegistry(char* out, int outLen)
{
    out[0] = '\0';
    HKEY hKey;
    const char* path = "SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\0000";
    if (RegOpenKeyExA(HKEY_LOCAL_MACHINE, path, 0, KEY_READ, &hKey) != ERROR_SUCCESS)
        return;

    DWORD type = 0, cb = (DWORD)outLen;
    if (RegQueryValueExA(hKey, "DriverDesc", nullptr, &type, (LPBYTE)out, &cb) != ERROR_SUCCESS
        || type != REG_SZ)
        out[0] = '\0';
    else
        out[outLen - 1] = '\0';

    RegCloseKey(hKey);
}

// ── Running the helper ───────────────────────────────────────────────────────

// Runs bin\MonsterRPGAudioProbe.exe and returns its stdout. False if it could
// not be run at all, which is a DIFFERENT answer from it running and reporting
// no ray tracing - see the note at the call site.
bool RunHelper(const char* dllDir, char* out, int outLen)
{
    out[0] = '\0';

    char exe[MAX_PATH * 2];
    _snprintf(exe, sizeof(exe) - 1, "%s\\MonsterRPGAudioProbe.exe", dllDir);
    exe[sizeof(exe) - 1] = '\0';

    if (GetFileAttributesA(exe) == INVALID_FILE_ATTRIBUTES) {
        MrpgLog::Write("gpu: helper not found at %s", exe);
        return false;
    }

    SECURITY_ATTRIBUTES sa;
    sa.nLength              = sizeof(sa);
    sa.lpSecurityDescriptor = nullptr;
    sa.bInheritHandle       = TRUE;

    HANDLE rd = nullptr, wr = nullptr;
    if (!CreatePipe(&rd, &wr, &sa, 0)) {
        MrpgLog::Write("gpu: CreatePipe failed (%lu)", GetLastError());
        return false;
    }
    // The READ end must not be inheritable, or the child holds a copy of it and
    // the pipe never reports EOF - the read below would block until the timeout
    // on every single launch.
    SetHandleInformation(rd, HANDLE_FLAG_INHERIT, 0);

    STARTUPINFOA si;
    ZeroMemory(&si, sizeof(si));
    si.cb          = sizeof(si);
    si.dwFlags     = STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW;
    si.wShowWindow = SW_HIDE;               // never flash a console at the player
    si.hStdOutput  = wr;
    si.hStdError   = wr;
    si.hStdInput   = nullptr;

    PROCESS_INFORMATION pi;
    ZeroMemory(&pi, sizeof(pi));

    char cmd[MAX_PATH * 2 + 4];
    _snprintf(cmd, sizeof(cmd) - 1, "\"%s\"", exe);
    cmd[sizeof(cmd) - 1] = '\0';

    if (!CreateProcessA(exe, cmd, nullptr, nullptr, TRUE,
                        CREATE_NO_WINDOW, nullptr, dllDir, &si, &pi)) {
        MrpgLog::Write("gpu: could not start the helper (%lu)", GetLastError());
        CloseHandle(rd);
        CloseHandle(wr);
        return false;
    }

    // Ours must be closed or the pipe has a live writer forever and never EOFs.
    CloseHandle(wr);

    int total = 0;
    for (;;) {
        DWORD got = 0;
        char  buf[512];
        if (!ReadFile(rd, buf, sizeof(buf), &got, nullptr) || got == 0)
            break;                          // EOF: the child exited
        if (total + (int)got >= outLen - 1) got = (DWORD)(outLen - 1 - total);
        if (got == 0) break;
        memcpy(out + total, buf, got);
        total += (int)got;
    }
    out[total] = '\0';
    CloseHandle(rd);

    // The read above already returned, so the child is finished or wedged. A
    // short wait catches the normal case; the kill catches a driver that hung
    // inside vkCreateInstance, which does happen on broken installs and must not
    // leave an orphan process behind every launch.
    if (WaitForSingleObject(pi.hProcess, 5000) == WAIT_TIMEOUT) {
        MrpgLog::Write("gpu: helper did not exit within 5 s; terminating it");
        TerminateProcess(pi.hProcess, 1);
    }
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);

    return total > 0;
}

// ── Scoring, for machines with more than one GPU ─────────────────────────────
//
// A gaming laptop enumerates the iGPU and the dGPU, usually in that order, and
// taking the first would gate a 4070 machine on its Iris Xe.
int ScoreDevice(bool rayQuery, int policy, int deviceType)
{
    int s = 0;
    if (rayQuery)        s += 100;
    if (policy == 1)     s += 50;
    if (policy == -1)    s += 10;    // unknown beats a known failure
    if (deviceType == 2) s += 5;     // VK_PHYSICAL_DEVICE_TYPE_DISCRETE_GPU
    return s;
}

void ApplyVerdict(bool gateA, int policy)
{
    if (!gateA) {
        s_result.verdict = VERDICT_NO_RT;
        lstrcpynA(s_result.why, "no Vulkan ray query on this GPU", sizeof(s_result.why));
    } else if (policy == 1) {
        s_result.verdict = VERDICT_ELIGIBLE;
        lstrcpynA(s_result.why, "ray query present and the generation rule passes",
                  sizeof(s_result.why));
    } else if (policy == 0) {
        s_result.verdict = VERDICT_TOO_OLD;
        lstrcpynA(s_result.why,
                  "can trace, but older than the NVIDIA 3000 / AMD 6000 policy floor",
                  sizeof(s_result.why));
    } else {
        s_result.verdict = VERDICT_UNKNOWN;
        lstrcpynA(s_result.why,
                  "can trace; name not recognised, so a benchmark would decide",
                  sizeof(s_result.why));
    }
}

} // namespace

const char* VerdictName(Verdict v)
{
    switch (v) {
        case VERDICT_NO_RT:      return "NO_RT";
        case VERDICT_TOO_OLD:    return "TOO_OLD";
        case VERDICT_ELIGIBLE:   return "ELIGIBLE";
        case VERDICT_FORCED_ON:  return "FORCED_ON";
        case VERDICT_FORCED_OFF: return "FORCED_OFF";
        default:                 return "UNKNOWN";
    }
}

const Result& Probe(const char* dllDir)
{
    if (s_probed) return s_result;
    s_probed = true;

    memset(&s_result, 0, sizeof(s_result));
    s_result.verdict = VERDICT_UNKNOWN;
    lstrcpynA(s_result.name, "(unidentified)", sizeof(s_result.name));
    lstrcpynA(s_result.why,  "probe did not run", sizeof(s_result.why));

    // The override is checked FIRST and short-circuits everything, including
    // launching the helper. Someone who has set GpuMode=off has said they do not
    // want this subsystem touching their machine, and starting a process anyway
    // "just to log it" would not honour that.
    const char* mode = MrpgCfg::GetStr("GpuMode", "auto");
    if (!lstrcmpiA(mode, "off")) {
        s_result.verdict = VERDICT_FORCED_OFF;
        lstrcpynA(s_result.why, "GpuMode=off in MonsterRPGAudio.cfg", sizeof(s_result.why));
        ReadAdapterNameFromRegistry(s_result.name, sizeof(s_result.name));
        MrpgLog::Write("gpu: FORCED_OFF by cfg (%s)",
                       s_result.name[0] ? s_result.name : "unidentified");
        return s_result;
    }

    char output[8192];
    if (!RunHelper(dllDir, output, sizeof(output))) {
        // NOT NO_RT. The 32-bit process this code runs in cannot see ray tracing
        // even when the machine has it, so we have no basis whatsoever for a
        // capability answer here - and reporting NO_RT would be a confident
        // wrong answer rather than an honest missing one. UNKNOWN is correct and
        // is what Phase 7 must treat as "do not offload".
        s_result.verdict = VERDICT_UNKNOWN;
        lstrcpynA(s_result.why, "the 64-bit GPU probe could not be run", sizeof(s_result.why));
        ReadAdapterNameFromRegistry(s_result.name, sizeof(s_result.name));
        MrpgLog::Write("gpu: UNKNOWN - helper did not run. adapter per registry: %s",
                       s_result.name[0] ? s_result.name : "(could not read)");
        MrpgLog::Write("gpu:   This changes nothing about what you hear.");
        return s_result;
    }

    int  bestScore = -1;
    int  devices   = 0;
    char line[512];
    int  li = 0;

    for (const char* p = output; ; ++p) {
        if (*p && *p != '\n' && *p != '\r') {
            if (li < (int)sizeof(line) - 1) line[li++] = *p;
            continue;
        }
        line[li] = '\0';
        int len = li;
        li = 0;

        if (len > 0) {
            if (!strncmp(line, "MRPGGPU-ERR", 11)) {
                MrpgLog::Write("gpu: helper error:%s", line + 11);
            } else if (!strncmp(line, "MRPGGPU-NOTE", 12)) {
                MrpgLog::Write("gpu: helper note:%s", line + 12);
            } else if (!strncmp(line, "MRPGGPU-END", 11)) {
                // nothing to do; the count is already known from the records
            } else if (!strncmp(line, "MRPGGPU ", 8)) {
                unsigned int vendorId = 0, deviceId = 0, apiVersion = 0, deviceType = 0;
                int rq = 0, as = 0, dho = 0;
                int consumed = 0;

                // %n gives the offset where the name starts, which is how a
                // final field containing spaces is read without splitting it.
                if (sscanf(line + 8, "%u %u %u %u %d %d %d %n",
                           &vendorId, &deviceId, &apiVersion, &deviceType,
                           &rq, &as, &dho, &consumed) == 7 && consumed > 0) {
                    const char* name = line + 8 + consumed;
                    ++devices;

                    const unsigned int major = apiVersion >> 22;
                    const unsigned int minor = (apiVersion >> 12) & 0x3FF;

                    // VK_KHR_acceleration_structure requires Vulkan 1.1. A device
                    // advertising the extension on a 1.0 API version is not one
                    // to trust with this.
                    const bool apiOk  = (major > 1) || (major == 1 && minor >= 1);
                    const bool gateA  = rq && as && dho && apiOk;
                    const int  policy = PolicyForName(vendorId, name);

                    MrpgLog::Write("gpu:   %s", name);
                    MrpgLog::Write("gpu:     vendor %04X  device %04X  vulkan %u.%u  type %u",
                                   vendorId, deviceId, major, minor, deviceType);
                    MrpgLog::Write("gpu:     ray_query %d  accel_struct %d  deferred_ops %d"
                                   "  ->  gateA %d, gateB %s",
                                   rq, as, dho, (int)gateA,
                                   policy == 1 ? "pass" : (policy == 0 ? "fail" : "unknown"));

                    int score = ScoreDevice(gateA, policy, (int)deviceType);
                    if (score > bestScore) {
                        bestScore = score;
                        s_result.hasRayQuery = gateA;
                        s_result.policyPass  = (policy == 1);
                        s_result.vendorId    = vendorId;
                        s_result.deviceId    = deviceId;
                        s_result.apiVersion  = apiVersion;
                        s_result.deviceType  = (int)deviceType;
                        lstrcpynA(s_result.name, name, sizeof(s_result.name));
                        ApplyVerdict(gateA, policy);
                    }
                }
            }
        }

        if (!*p) break;
    }

    if (devices == 0) {
        s_result.verdict = VERDICT_UNKNOWN;
        lstrcpynA(s_result.why, "the GPU probe reported no devices", sizeof(s_result.why));
        ReadAdapterNameFromRegistry(s_result.name, sizeof(s_result.name));
        MrpgLog::Write("gpu: helper ran but reported no devices");
    }

    if (!lstrcmpiA(mode, "on")) {
        MrpgLog::Write("gpu: cfg GpuMode=on overrides the verdict (%s -> FORCED_ON)",
                       VerdictName(s_result.verdict));
        s_result.verdict = VERDICT_FORCED_ON;
        lstrcpynA(s_result.why, "GpuMode=on in MonsterRPGAudio.cfg", sizeof(s_result.why));
    }

    MrpgLog::Write("gpu: verdict %s - %s", VerdictName(s_result.verdict), s_result.why);
    MrpgLog::Write("gpu:   chosen adapter: %s", s_result.name);
    MrpgLog::Write("gpu:   THIS DOES NOT AFFECT WHAT YOU HEAR. Acoustics are traced on the");
    MrpgLog::Write("gpu:   server's GPU regardless; this only decides whether that work can");
    MrpgLog::Write("gpu:   later be offloaded to this machine (not built yet).");

    return s_result;
}

} // namespace MrpgGpu
