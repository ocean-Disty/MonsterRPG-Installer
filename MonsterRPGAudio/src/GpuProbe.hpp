#pragma once

// =============================================================================
// GpuProbe — can this machine trace rays, and is it worth asking it to?
// =============================================================================
//
// READ THIS FIRST, BECAUSE THE NAME OVERSELLS IT.
//
// This does NOT decide whether the player gets ray-traced audio. Everyone gets
// ray-traced audio: the acoustic trace runs on the SERVER's GPU (decision D1 in
// AUDIORT_NATIVE_PLAN.md) and arrives over the wire as numbers. A GTX 1060 hears
// exactly what a 7900 XTX hears.
//
// What this decides is whether the tracing can eventually be OFFLOADED to the
// player's own machine — Phase 7, optional, not built yet. Until then this
// module only reports, so that by the time Phase 7 arrives there is real data
// about what the player base actually has instead of a guess.
//
// A WRONG ANSWER HERE COSTS NOTHING. Every path falls back to server-side
// tracing, which is the default anyway. That is why it is safe to be opinionated
// below.
//
// ---------------------------------------------------------------------------
// TWO GATES, AND THEY ANSWER DIFFERENT QUESTIONS
// ---------------------------------------------------------------------------
//
// GATE A — CAPABILITY. Can this GPU trace rays at all? Objective, and answered
// by asking Vulkan for VK_KHR_ray_query + VK_KHR_acceleration_structure. Vulkan
// and not D3D12 because Phase 7 reuses MonsterRPG_AudioRT.exe and RayKernels.spv
// verbatim, and those are Vulkan ray query. Asking DXR would be measuring
// something we are not going to use.
//
// Gate A alone excludes GTX 10xx and older, RX 5000 and older, and every pre-Arc
// Intel iGPU. It ADMITS the RTX 20xx series — Turing does support ray query.
//
// GATE A IS NOT ASKED FROM THIS PROCESS. It cannot be. Measured on an RX 7900
// XTX: a 32-bit process sees 211 Vulkan device extensions and NONE of them are
// ray tracing; a 64-bit process on the same machine sees 220 including
// VK_KHR_ray_query and VK_KHR_acceleration_structure. AMD's 32-bit ICD does not
// expose ray tracing, and Blockland.exe is 32-bit — so asking in-process would
// report NO_RT for the strongest card AMD sells, which is the very card this
// project's server traces on.
//
// So the Vulkan half lives in MonsterRPGAudioProbe.exe (src/Probe64.cpp, 64-bit)
// and this file runs it and reads its answer. That is also the right shape for
// Phase 7, whose tracer is a 64-bit process for the same reason.
//
// COROLLARY, AND IT IS LOAD-BEARING: if the helper cannot be run, the verdict is
// UNKNOWN and never NO_RT. This process has no basis for a capability answer of
// its own, and a confident wrong answer is worse than an honest missing one.
//
// GATE B — PERFORMANCE POLICY. Is it fast enough to be worth the trouble? This
// is the "NVIDIA 3000 or later, AMD 6000 or later" rule, and it is a judgement
// rather than a capability. Note what it actually means on each vendor:
//
//   AMD      RX 6000 (RDNA2) IS AMD's first ray-tracing generation. "6000 or
//            later" and "has ray tracing at all" are the same set, so on AMD
//            Gate A has already done the whole job.
//
//   NVIDIA   RTX 3000 (Ampere) is the SECOND RT generation. "3000 or later"
//            deliberately excludes RTX 20xx, which can trace but at roughly half
//            the throughput per tier.
//
// So Gate B is precisely: exclude Turing, and decide about Intel Arc. That is a
// small, explicit, arguable exclusion and it lives in one function below where
// it can be argued with.
//
// ---------------------------------------------------------------------------
// WHY A NAME STRING AND NOT A DEVICE-ID TABLE
// ---------------------------------------------------------------------------
//
// A table of PCI device-ID ranges looks precise and is not. NVIDIA's IDs are
// only roughly ordered by architecture and AMD's are not ordered at all — Navi21
// is 0x73BF and Navi31 is 0x744C, but Navi24 is 0x7422, and nothing about those
// numbers tells you which generation they belong to without a lookup table that
// is wrong the day a new card ships.
//
// The adapter name says "RTX 3060 Ti" and "RX 6800 XT" and a human can check it.
// It is also editable: when this refuses a card it should not, the answer is one
// line in MonsterRPGAudio.cfg, not a rebuild.
//
// PARSE IT CAREFULLY. Adapter names have inconsistent spacing and this tree has
// already lost months to getWord returning empty on a double space in a
// column-aligned string. Tokenise on RUNS of whitespace; never index fixed word
// positions.

namespace MrpgGpu {

enum Verdict {
    VERDICT_UNKNOWN = 0,   // no Vulkan, or a name nothing recognises
    VERDICT_NO_RT,         // Gate A said no. Hard, objective, final.
    VERDICT_TOO_OLD,       // Gate A yes, Gate B no — e.g. an RTX 2070
    VERDICT_ELIGIBLE,      // both gates passed
    VERDICT_FORCED_ON,     // cfg GpuMode=on
    VERDICT_FORCED_OFF     // cfg GpuMode=off
};

struct Result {
    Verdict verdict;
    bool    hasRayQuery;        // Gate A
    bool    policyPass;         // Gate B, only meaningful when hasRayQuery
    unsigned int vendorId;      // 0x10DE NVIDIA, 0x1002 AMD, 0x8086 Intel
    unsigned int deviceId;
    unsigned int apiVersion;    // the DEVICE's Vulkan version, not the loader's
    int          deviceType;    // 1 integrated, 2 discrete, 3 virtual, 4 cpu
    char         name[256];
    char         why[192];      // one sentence, for the log and for /audiort
};

// Runs the probe and caches the result. Costs one short-lived child process, so
// it runs exactly once per launch. Safe to call before the engine is up: it
// touches a helper process and the registry, never Blockland.
//
// `dllDir` is the folder MonsterRPGAudio.dll lives in, i.e. where the helper
// sits beside it. Ignored after the first call, which is why every later caller
// may pass whatever it has.
const Result& Probe(const char* dllDir);

// The one-line summary the log and the server-side report both use.
const char* VerdictName(Verdict v);

} // namespace MrpgGpu
