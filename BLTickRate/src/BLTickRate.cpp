//////////////////////////////////////////////////
// BLTickRate — variable engine tick rate for Blockland
//
// Blockland runs its simulation at a fixed 32 ms tick (31.25 tps). This DLL
// rewrites the engine's tick constants in memory at startup so it can run at
// 16 ms (62.5 tps) or 8 ms (125 tps) instead. Movement speed, gravity, jump
// height and projectile lifetimes all stay the same — only the granularity of
// the simulation changes.
//
// Original idea and the 2019 proof of concept: CompMix (Blockland forums,
// "BLTickRate"). None of that DLL's addresses survive in the current build —
// zero of its forty matched — so every site here was re-derived from
// Blockland.exe (build dated 2024-11-14) and is verified before it is written.
//
// SAFETY
//
//   Nothing is written to disk. All patches are applied to this process's own
//   memory and vanish when the game closes. Blockland.exe is never modified.
//
//   Every site is checked against the exact instruction bytes it expects
//   BEFORE anything is written, and if a single one disagrees the whole patch
//   set is abandoned. On a Blockland update you get a clean "ABORTED" in the
//   log and a vanilla game, never a half-patched one.
//
// CONFIGURATION — see BLTickRate.cfg next to this DLL. No rebuild needed.
//
// Build: build.bat   (needs MinGW-w64 32-bit; see README.md)

#include <Windows.h>
#include <Psapi.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

// Exported so the DLL can also be attached with Stud_PE import injection,
// which is how CompMix's original was loaded. See README.md.
extern "C" void __declspec(dllexport) hello() {}


//////////////////////////////////////////////////
// Tick configuration (runtime, from BLTickRate.cfg)

// 5 = 32 ms = 31.25 tps  (vanilla — patches become a no-op)
// 4 = 16 ms = 62.5  tps
// 3 =  8 ms = 125   tps
#define TICKSHIFT_DEFAULT 4
#define TICKSHIFT_MIN     3
#define TICKSHIFT_MAX     5

static int gTickShift = TICKSHIFT_DEFAULT;

static int   TickMs()   { return 1 << gTickShift; }
static int   TickMask() { return TickMs() - 1; }
static float TickSec()  { return (float)TickMs() / 1000.0f; }

static char gDllDir[MAX_PATH] = {};   // where this DLL lives (…\BLTickRate\bin\)
static char gCfgDir[MAX_PATH] = {};   // where the player's .cfg and .log live


//////////////////////////////////////////////////
// Logging
//
// DllMain runs long before Blockland's console exists, so there is nothing to
// print to. Everything goes to BLTickRate.log next to the DLL.

static FILE *gLog = NULL;

static void logf(const char *fmt, ...)
{
    if(!gLog)
        return;

    va_list args;
    va_start(args, fmt);
    vfprintf(gLog, fmt, args);
    va_end(args);

    fputc('\n', gLog);
    fflush(gLog);
}

// The DLL ships in BLTickRate\bin\ but the player edits BLTickRate.cfg in
// BLTickRate\, one level up — so look in both. Whichever directory holds the
// config is also where the log goes, so the two stay side by side wherever the
// player put things.
static void locateConfigDir()
{
    char path[MAX_PATH];

    strncpy(gCfgDir, gDllDir, MAX_PATH - 1);

    _snprintf(path, sizeof(path), "%sBLTickRate.cfg", gDllDir);
    if(GetFileAttributesA(path) != INVALID_FILE_ATTRIBUTES)
        return;

    // Try the parent directory.
    char parent[MAX_PATH];
    strncpy(parent, gDllDir, MAX_PATH - 1);
    size_t n = strlen(parent);
    if(n > 1)
    {
        parent[n - 1] = '\0';               // drop the trailing backslash
        char *slash = strrchr(parent, '\\');
        if(slash)
        {
            *(slash + 1) = '\0';
            _snprintf(path, sizeof(path), "%sBLTickRate.cfg", parent);
            if(GetFileAttributesA(path) != INVALID_FILE_ATTRIBUTES)
                strncpy(gCfgDir, parent, MAX_PATH - 1);
        }
    }
}

static void readConfig()
{
    char path[MAX_PATH];
    _snprintf(path, sizeof(path), "%sBLTickRate.cfg", gCfgDir);

    FILE *f = fopen(path, "r");
    if(!f)
    {
        logf("no BLTickRate.cfg found - using default TickShift %d",
             TICKSHIFT_DEFAULT);
        logf("(looked in %s and one folder above it)", gDllDir);
        return;
    }

    char line[256];
    while(fgets(line, sizeof(line), f))
    {
        char *p = line;
        while(*p == ' ' || *p == '\t')
            p++;
        if(*p == '#' || *p == ';' || *p == '\r' || *p == '\n' || !*p)
            continue;

        // Accept "TickShift = 4" or a bare "4".
        const char *eq = strchr(p, '=');
        int value = atoi(eq ? eq + 1 : p);

        if(value >= TICKSHIFT_MIN && value <= TICKSHIFT_MAX)
        {
            gTickShift = value;
        }
        else
        {
            logf("BLTickRate.cfg: TickShift %d is out of range %d..%d - "
                 "ignoring it and using %d",
                 value, TICKSHIFT_MIN, TICKSHIFT_MAX, gTickShift);
        }
        break;
    }

    fclose(f);
}


//////////////////////////////////////////////////
// Image base
//
// Blockland.exe is built with DYNAMICBASE and keeps its .reloc section, so
// Windows relocates it. Nothing here may assume 0x400000 — every address is an
// RVA that gets rebased at runtime.

static BYTE *gBase = NULL;
static DWORD gSize = 0;

static bool initImage()
{
    HMODULE module = GetModuleHandle(NULL);
    if(!module)
        return false;

    MODULEINFO info;
    if(!GetModuleInformation(GetCurrentProcess(), module, &info, sizeof(info)))
        return false;

    gBase = (BYTE *)info.lpBaseOfDll;
    gSize = info.SizeOfImage;
    return true;
}

static BYTE *at(DWORD rva)
{
    return gBase + rva;
}


//////////////////////////////////////////////////
// Patch table
//
// RVAs are the address seen in a disassembler minus 0x400000. "sig" covers the
// whole instruction so a changed build is caught before anything is written;
// ?? marks a byte the loader relocates (an absolute address we must not depend
// on).
//
//   field  - offset of the operand inside the instruction
//   size   - operand width in bytes

enum PatchKind
{
    PK_IMM,     // write a tick-derived integer into the operand
    PK_F32,     // write TickSec as a 32-bit float
    PK_F64,     // write TickSec as a double, in (double)(float) form
    PK_PTR      // repoint an absolute operand at our own constant
};

// What to write, computed at runtime from the configured TickShift.
enum ValueKind
{
    V_NONE,
    V_TICKMS,        // 32 -> TickMs
    V_TICKMASK,      // 31 -> TickMs-1
    V_NOT_TICKMASK,  // ~31
    V_TICKSHIFT,     // 5  -> TickShift
    V_PROJ_MAX,      // 32736/TickMs, keeps the same millisecond ceiling
    V_PACKET_RATE    // 1024/TickMs, the engine's own formula
};

struct Patch
{
    const char *name;
    DWORD       rva;
    const char *sig;
    DWORD       field;
    DWORD       size;
    PatchKind   kind;
    ValueKind   vkind;
};

static DWORD computeValue(ValueKind v)
{
    switch(v)
    {
        case V_TICKMS:       return (DWORD)TickMs();
        case V_TICKMASK:     return (DWORD)TickMask();
        case V_NOT_TICKMASK: return (DWORD)(BYTE)~TickMask();
        case V_TICKSHIFT:    return (DWORD)gTickShift;
        case V_PROJ_MAX:     return (DWORD)(32736 / TickMs());
        case V_PACKET_RATE:  return (DWORD)(1024 / TickMs());
        default:             return 0;
    }
}

// 1/TickMs as a double, in memory we allocate. Filled in at runtime.
static DWORD gCaveOneOverTickMs = 0;

static Patch gPatches[] =
{
    // ---- TickSec ----------------------------------------------------------
    // There are THREE forms of TickSec in this build and all three matter.
    //
    // 1. A float 0.032f, reached by 12 x87 loads. MSVC's /OPT:ICF folded what
    //    were 11 separate literals in the 2019 build into this single constant.
    //
    // Do NOT go looking for 0.032f in .rdata generally — there is a 128-entry
    // ascending float table at 0x6FAD94-0x6FAF90 (0.001 .. 1.0) that contains
    // 0.032, 0.016 and 0.32 and has no references at all. Writing into it
    // corrupts unrelated data silently.
    { "TickSec (float, 12 refs)", 0x35E9D8, "6F 12 03 3D", 0, 4, PK_F32, V_NONE },

    // 2. A parallel *double*, 16 refs — the double-precision physics paths.
    //    It stores (double)(float)0.032 = 0.03200000151991844, NOT
    //    double(0.032), so searching for the obvious bit pattern finds nothing
    //    and it looks like this build has no double TickSec at all.
    //
    //    Missing this one leaves gravity and runForce integrating 0.032 per
    //    tick at the new tick rate — exactly 2x acceleration at 16 ms — while
    //    top speed still looks right, because that is a clamp not an integral.
    { "TickSec (double, 16 refs)", 0x35EB70, "00 00 00 E0 4D 62 A0 3F", 0, 8, PK_F64, V_NONE },

    // 3. One inlined literal: "mov [esp+38h], 3D03126Fh", the else-branch of a
    //    partial-tick timestep. It is an instruction operand rather than data,
    //    so a disassembler reports no references to it and a 4-byte-aligned
    //    constant scan misses it entirely.
    { "TickSec (inline literal)", 0x0DD410, "C7 44 24 38 6F 12 03 3D", 4, 4, PK_F32, V_NONE },

    // ---- tick quantisation ------------------------------------------------
    // The two ProcessList::advanceTime variants (server and client).
    // "lea esi,[eax+1Fh]" then "and esi,0FFFFFFE0h" is (time + TickMask) & ~TickMask.
    { "advanceTime A: and ~TickMask", 0x0BD0DA, "83 E6 E0", 2, 1, PK_IMM, V_NOT_TICKMASK },
    { "advanceTime A: shr TickShift", 0x0BD0E5, "C1 E9 05", 2, 1, PK_IMM, V_TICKSHIFT },
    { "advanceTime B: lea +TickMask", 0x0BD3F5, "8D 70 1F", 2, 1, PK_IMM, V_TICKMASK },
    { "advanceTime B: and ~TickMask", 0x0BD3FC, "83 E6 E0", 2, 1, PK_IMM, V_NOT_TICKMASK },
    { "advanceTime B: and TickMask",  0x0BD4A5, "83 E0 1F", 2, 1, PK_IMM, V_TICKMASK },

    // The step inside the drain loop. Both lists run:
    //
    //     loop: advanceObjects();
    //           mLastTick += TickMs;
    //           if(mLastTick != targetTick) goto loop;
    //
    // The exit test is equality, not >=. Rounding targetTick to 16 while this
    // still adds 32 steps straight past it (1024 -> 1056 -> 1088 never equals
    // 1040) and the engine spins forever at 100% CPU with sim time frozen.
    { "advanceTime A: mLastTick += TickMs", 0x0BD10F, "83 C0 20", 2, 1, PK_IMM, V_TICKMS },
    { "advanceTime B: mLastTick += TickMs", 0x0BD713, "83 C0 20", 2, 1, PK_IMM, V_TICKMS },

    // List B decides "did a tick elapse?" from a raw millisecond difference
    // rather than a tick count like list A does:
    //
    //     targetTick = (mLastTime + delta + TickMask) & ~TickMask
    //     elapsed    = targetTick - mLastTick
    //     if(elapsed >= TickMs) ...                  // these three
    //
    // The last one feeds the function's return value. Left at 32, a single
    // 16 ms tick fails the test and list B takes the "nothing happened" path
    // every tick — interpolation and collision degrade without anything
    // obviously crashing. CompMix's DLL patched no equivalent, which may be
    // the unexplained stair/collision bug reported alongside it in 2019.
    { "advanceTime B: elapsed >= TickMs (1)", 0x0BD53D, "83 7C 24 28 20", 4, 1, PK_IMM, V_TICKMS },
    { "advanceTime B: elapsed >= TickMs (2)", 0x0BD57F, "83 7C 24 28 20", 4, 1, PK_IMM, V_TICKMS },
    { "advanceTime B: elapsed >= TickMs (3)", 0x0BD87D, "83 7C 24 28 20", 4, 1, PK_IMM, V_TICKMS },

    // ---- milliseconds -> ticks --------------------------------------------
    // Four "fmul qword ptr ds:0x75EB68" sites, the shared double 0.03125.
    //
    // That constant is NOT ours to overwrite: it is also used by avatar face
    // and decal code and by pixel-format code, where 1/32 means something else
    // entirely. CompMix patched the shared constant and repointed the
    // exceptions back; with /OPT:ICF folding that is the wrong way round, so
    // this inverts it — allocate a fresh 1/TickMs and repoint only the four
    // confirmed tick sites. Nothing shared is modified.
    //
    // The last two are the RENDER side. Missing them does not affect the
    // simulation at all: the world steps correctly while what you SEE
    // interpolates at half rate. The visible symptom is a first-person
    // mounted image jittering as the view sways, while third person looks
    // fine — because a first-person image is transformed against the
    // interpolated eye rather than a world node.
    { "advanceTime A: fmul 1/TickMs",   0x0BD1D7, "DC 0D ?? ?? ?? ??", 2, 4, PK_PTR, V_NONE },
    { "advanceTime B: fmul 1/TickMs",   0x0BD4C8, "DC 0D ?? ?? ?? ??", 2, 4, PK_PTR, V_NONE },
    { "render: elapsed ticks",          0x0883F9, "DC 0D ?? ?? ?? ??", 2, 4, PK_PTR, V_NONE },
    { "render: interpolation fraction", 0x0B99BD, "DC 0D ?? ?? ?? ??", 2, 4, PK_PTR, V_NONE },

    // ---- projectile field limits ------------------------------------------
    // ProjectileData::initPersistFields builds three IRangeValidatorScaled
    // objects (lifetime, armingDelay, fadeDelay), each as:
    //   [+8]=0 min, [+0Ch]=3FFh max ticks, [+10h]=20h scale (ms per tick).
    // 32736/TickMs keeps the same ceiling in milliseconds.
    { "lifetime max ticks",    0x0E8901, "C7 40 0C FF 03 00 00", 3, 4, PK_IMM, V_PROJ_MAX },
    { "lifetime ms scale",     0x0E8908, "C7 40 10 20 00 00 00", 3, 4, PK_IMM, V_TICKMS },
    { "armingDelay max ticks", 0x0E894B, "C7 40 0C FF 03 00 00", 3, 4, PK_IMM, V_PROJ_MAX },
    { "armingDelay ms scale",  0x0E8952, "C7 40 10 20 00 00 00", 3, 4, PK_IMM, V_TICKMS },
    { "fadeDelay max ticks",   0x0E8995, "C7 40 0C FF 03 00 00", 3, 4, PK_IMM, V_PROJ_MAX },
    { "fadeDelay ms scale",    0x0E899C, "C7 40 10 20 00 00 00", 3, 4, PK_IMM, V_TICKMS },

    // ---- projectile tick counts ON THE WIRE --------------------------------
    // Raising the caps above is only half the job, and getting it wrong is
    // silent. Those same three fields are range-coded onto the network as
    //
    //     writeRangedU32(value, 0, 1023)      1023 == 32736/32, the VANILLA cap
    //
    // so a cap of 32736/TickMs (2046 at 16 ms, 4092 at 8 ms) writes values the
    // encoder cannot represent. A value outside a ranged write does not fail
    // loudly - it corrupts the stream or trips an assert, and an assert aborts
    // the process. That matches the 0x40000015 aborts seen while testing.
    //
    // The pack/unpack pair MUST move together or the two sides disagree about
    // how many bits the field occupies and every following field shifts.
    // Identified by the operands: +0A0h/+0A4h/+0A8h are exactly the lifetime /
    // armingDelay / fadeDelay offsets used by initPersistFields above.
    //
    // This does change the packet layout - 0..4092 needs 12 bits where 0..1023
    // needed 10 - so BOTH ends must run BLTickRate at the SAME TickShift. They
    // already must, for movement to work at all.
    //
    // CompMix's 2019 DLL scaled the caps without touching these, so this
    // overflow is inherited from the original rather than new.
    // ---- DISABLED 2026-08-08 ------------------------------------------
    // Enabling these crashed the CLIENT with 0xC0000005 inside
    // Blockland.exe the moment it loaded datablocks from the server -
    // i.e. exactly where ProjectileData::unpackData runs. Both ends were
    // on the same build, so a simple pack/unpack mismatch is not the
    // whole story and the change is not safe as written.
    //
    // The UNDERLYING PROBLEM IS STILL REAL: the caps above are raised to
    // 32736/TickMs while these encoders still say 0..1023, so a long-lived
    // projectile can still be handed a value the stream cannot represent.
    // Re-enable one pair at a time and test datablock load after each.
//    { "lifetime pack range",    0x0E92BC, "68 FF 03 00 00 6A 00", 1, 4, PK_IMM, V_PROJ_MAX },
//    { "armingDelay pack range", 0x0E92D0, "68 FF 03 00 00 6A 00", 1, 4, PK_IMM, V_PROJ_MAX },
//    { "fadeDelay pack range",   0x0E92E4, "68 FF 03 00 00 6A 00", 1, 4, PK_IMM, V_PROJ_MAX },
//    { "lifetime unpack range",    0x0E980C, "68 FF 03 00 00 6A 00", 1, 4, PK_IMM, V_PROJ_MAX },
//    { "armingDelay unpack range", 0x0E981A, "68 FF 03 00 00 6A 00", 1, 4, PK_IMM, V_PROJ_MAX },
//    { "fadeDelay unpack range",   0x0E982E, "68 FF 03 00 00 6A 00", 1, 4, PK_IMM, V_PROJ_MAX },

    // The live projectile's own tick counter, same 0..1023 encoding. Confirmed
    // tick-derived: sub_487F50 computes (now - lastUpdate) * 1/TickMs, i.e.
    // elapsed IN TICKS, and compares it against this very field.
//    { "projectile tick pack range",   0x0ECD6B, "68 FF 03 00 00 6A 00", 1, 4, PK_IMM, V_PROJ_MAX },
//    { "projectile tick unpack range", 0x0ED19D, "68 FF 03 00 00 6A 00", 1, 4, PK_IMM, V_PROJ_MAX },

    // ---- packet rate clamp -------------------------------------------------
    // NetConnection::checkMaxRate has two "mov <global>, 20h" that clamp
    // $Pref::Net::PacketRateToServer / ToClient to 32/sec. The C7 05 opcode is
    // followed by a relocated global address, hence the wildcards.
    //
    // The compiled default scripts clamp these as well, so the prefs still
    // have to be re-set after the game finishes loading — the engine patch
    // alone is not enough. See README.md.
    { "clamp PacketRateToServer", 0x1919DD, "C7 05 ?? ?? ?? ?? 20 00 00 00", 6, 4, PK_IMM, V_PACKET_RATE },
    { "clamp PacketRateToClient", 0x191A02, "C7 05 ?? ?? ?? ?? 20 00 00 00", 6, 4, PK_IMM, V_PACKET_RATE },
};

static const int gPatchCount = sizeof(gPatches) / sizeof(gPatches[0]);

// RVA of the shared 0.03125 double, used to sanity check the repoint sites.
#define RVA_ONE_OVER_32_DBL 0x35EB68


//////////////////////////////////////////////////
// Signature matching

// Parse "83 E6 E0" / "DC 0D ?? ?? ?? ??" into bytes + mask. Returns length.
static int parseSig(const char *sig, BYTE *bytes, char *mask, int max)
{
    int n = 0;

    for(const char *p = sig; *p && n < max; )
    {
        while(*p == ' ')
            p++;
        if(!*p)
            break;

        if(p[0] == '?')
        {
            bytes[n] = 0;
            mask[n]  = '?';
            p += (p[1] == '?') ? 2 : 1;
        }
        else
        {
            unsigned int v = 0;
            sscanf(p, "%2x", &v);
            bytes[n] = (BYTE)v;
            mask[n]  = 'x';
            p += 2;
        }
        n++;
    }

    return n;
}

static bool verify(const Patch &p, char *why, size_t whyLen)
{
    BYTE bytes[24];
    char mask[24];
    int len = parseSig(p.sig, bytes, mask, 24);

    if(p.rva + len > gSize)
    {
        _snprintf(why, whyLen, "RVA past end of image");
        return false;
    }

    BYTE *target = at(p.rva);

    for(int i = 0; i < len; i++)
    {
        if(mask[i] == 'x' && target[i] != bytes[i])
        {
            _snprintf(why, whyLen,
                      "byte %d: found %02X, expected %02X", i, target[i], bytes[i]);
            return false;
        }
    }

    // For a repoint, also confirm the operand really points at the constant we
    // think it does, after relocation.
    if(p.kind == PK_PTR)
    {
        DWORD cur  = *(DWORD *)(target + p.field);
        DWORD want = (DWORD)at(RVA_ONE_OVER_32_DBL);

        if(cur != want)
        {
            _snprintf(why, whyLen,
                      "operand points at %08X, expected %08X", cur, want);
            return false;
        }
    }

    return true;
}

static void apply(const Patch &p)
{
    BYTE *target = at(p.rva) + p.field;
    DWORD old;

    VirtualProtect(target, p.size, PAGE_EXECUTE_READWRITE, &old);

    switch(p.kind)
    {
        case PK_F32:
        {
            float v = TickSec();
            memcpy(target, &v, 4);
            break;
        }

        case PK_F64:
        {
            // Match how the compiler produced it: a float widened to double.
            double v = (double)(float)TickSec();
            memcpy(target, &v, 8);
            break;
        }

        case PK_PTR:
        {
            DWORD v = gCaveOneOverTickMs;
            memcpy(target, &v, 4);
            break;
        }

        case PK_IMM:
        default:
        {
            DWORD value = computeValue(p.vkind);
            if(p.size == 1)
            {
                BYTE v = (BYTE)value;
                memcpy(target, &v, 1);
            }
            else
            {
                memcpy(target, &value, 4);
            }
            break;
        }
    }

    DWORD ignored;
    VirtualProtect(target, p.size, old, &ignored);
}


//////////////////////////////////////////////////
// Entry

static void run()
{
    char logPath[MAX_PATH];
    locateConfigDir();

    _snprintf(logPath, sizeof(logPath), "%sBLTickRate.log", gCfgDir);
    gLog = fopen(logPath, "w");

    readConfig();

    logf("BLTickRate - TickShift %d, %d ms/tick, %.2f tps",
         gTickShift, TickMs(), 1000.0 / (double)TickMs());

    if(gTickShift == 5)
    {
        logf("");
        logf("TickShift 5 is Blockland's normal speed, so this changes nothing.");
        logf("Set TickShift to 4 or 3 in BLTickRate.cfg to actually speed it up.");
    }

    if(!initImage())
    {
        logf("FAILED: could not resolve the image base");
        return;
    }

    logf("image base %08X, size %08X", (DWORD)gBase, gSize);

    // Our own copy of 1/TickMs for the repointed sites. Allocating is cleaner
    // than writing into an unused corner of the exe — nothing else can be
    // relying on this page.
    void *cave = VirtualAlloc(NULL, 4096, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    if(!cave)
    {
        logf("FAILED: could not allocate the constant page");
        return;
    }

    double oneOverTickMs = 1.0 / (double)TickMs();
    memcpy(cave, &oneOverTickMs, sizeof(double));
    gCaveOneOverTickMs = (DWORD)cave;

    logf("1/TickMs = %.8f at %08X", oneOverTickMs, gCaveOneOverTickMs);
    logf("");

    // Two phases on purpose. A half-patched engine is far worse than an
    // unpatched one, so nothing is written until every site has been verified.
    int bad = 0;

    for(int i = 0; i < gPatchCount; i++)
    {
        char why[128] = "";

        if(verify(gPatches[i], why, sizeof(why)))
        {
            logf("  ok       %-34s rva %06X", gPatches[i].name, gPatches[i].rva);
        }
        else
        {
            logf("  MISMATCH %-34s rva %06X : %s",
                 gPatches[i].name, gPatches[i].rva, why);
            bad++;
        }
    }

    logf("");

    if(bad)
    {
        logf("ABORTED: %d of %d sites did not match, so NOTHING was written and",
             bad, gPatchCount);
        logf("the game is running completely unmodified.");
        logf("");
        logf("This almost always means Blockland has been updated. The addresses");
        logf("above are specific to one build of Blockland.exe and have to be");
        logf("re-derived when it changes. Until then, play as normal.");
        return;
    }

    for(int i = 0; i < gPatchCount; i++)
        apply(gPatches[i]);

    logf("applied %d/%d patches - the game is now running at %.2f tps",
         gPatchCount, gPatchCount, 1000.0 / (double)TickMs());
    logf("");
    logf("Reminder: $Pref::Net::PacketRateToClient / ToServer are also clamped");
    logf("to 32 by the compiled default scripts. Raise them to %d after the game",
         1024 / TickMs());
    logf("has finished loading or the extra ticks never reach the network.");
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID reserved)
{
    if(reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(module);

        // Config and log live next to the DLL, wherever the player put it.
        GetModuleFileNameA(module, gDllDir, MAX_PATH);
        char *slash = strrchr(gDllDir, '\\');
        if(slash)
            *(slash + 1) = '\0';
        else
            gDllDir[0] = '\0';

        run();
    }

    return TRUE;
}
