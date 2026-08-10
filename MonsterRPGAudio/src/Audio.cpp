#define COBJMACROS
#define INITGUID
#include <windows.h>
#include <mmdeviceapi.h>
#include <functiondiscoverykeys_devpkey.h>
#include <audioclient.h>
#include <avrt.h>
#include <objbase.h>

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>

#include <atomic>
#include <mutex>
#include <string>
#include <thread>
#include <unordered_map>
#include <algorithm>
#include <vector>

#include "Audio.hpp"
#include "Wav.hpp"
#include "Cfg.hpp"
#include "Log.hpp"
#include "Devices.hpp"
#include "Hrtf.hpp"
#include "Reverb.hpp"

namespace MrpgAudio {

namespace {

// KSDATAFORMAT_SUBTYPE_IEEE_FLOAT, spelled out rather than pulled in.
//
// It lives in ksmedia.h behind INITGUID and links against ksuser, and dragging
// that whole header in for one 16-byte constant is not worth it. The value is
// fixed by the format spec and has never moved: it is the first four bytes of a
// WAVE format tag (0x0003 = IEEE float) followed by the standard
// KSDATAFORMAT GUID tail.
const GUID MRPG_SUBTYPE_IEEE_FLOAT =
    { 0x00000003, 0x0000, 0x0010, { 0x80, 0x00, 0x00, 0xaa, 0x00, 0x38, 0x9b, 0x71 } };

// ── Tuning ───────────────────────────────────────────────────────────────────

// Simultaneous one-shots. A busy fight is dozens per second; past this the
// quietest is stolen rather than letting the mix turn to mud. Matches
// SFX_MAX_VOICES in sfx.js so both clients behave the same under load.
const int MAX_VOICES = 48;

// Command ring, net thread -> audio thread. Generous because the cost of an
// overflow is a dropped sound and the cost of the memory is nothing.
const int CMD_RING = 512;

// The four bands the tracer reports, and the filter shapes that carry them.
// IDENTICAL to ACOU_BAND_HZ / ACOU_BAND_TYPE in public/acoustics.js, so the two
// clients colour a sound the same way. Do not "tidy" these: band 3 sits at 6 kHz
// rather than its unbounded centre because that is where the content in these
// WAVs actually stops.
const float BAND_HZ[4] = { 125.0f, 500.0f, 2000.0f, 6000.0f };

// Inverse-distance-clamped, the OpenAL / Web Audio default. ref 2 and rolloff
// 0.5 reproduce the curve this game was already tuned to. PORTED, NOT
// RE-DERIVED - see acouDistanceGain and the long note above it about why 0.5 is
// a decision rather than an accident.
const float REF_DISTANCE = 2.0f;
const float ROLLOFF      = 0.5f;

// Past this the tracer's answer is noise and the sound should be out of earshot.
const float MAX_DIST = 150.0f;

// ── AIR ABSORPTION ───────────────────────────────────────────────────────────
//
// Air does not merely make a distant sound quieter, it makes it DULLER: the
// atmosphere's absorption coefficient rises steeply with frequency, so the top
// end is gone long before the fundamental is. Nothing in this chain modelled
// that, and its absence is why a far-off waterfall sounded like a near one with
// the fader down.
//
// Modelled the way every real-time engine models it, Steam Audio included: ONE
// POLE, with the cutoff falling as distance rises. The physical input is a
// single number - attenuation in dB per metre at a reference frequency - and the
// filter is then SOLVED to hit exactly that gain at exactly that frequency
// rather than eyeballed, so changing the constant changes the model rather than
// the tuning.
//
// 0.08 dB/m at 5 kHz is roughly ISO 9613-1 for 20 C and half humidity. At 20 m
// that is -1.6 dB of top end, at 100 m it is -8 dB: audible, never dramatic,
// which is what real air does.
// HRTF is a HEADPHONE technique and it is opt-out for a reason: a player already
// running Windows Sonic, Dolby Atmos for Headphones, or a headset with its own
// spatialiser is having this done to them twice, and two spatialisers stacked
// sound worse than either alone. Speakers want the plain pan too.
bool s_hrtfEnabled = true;

// ── Reverb is OFF by default in this build, and that is deliberate ───────────
//
// The first version of it shipped with an absorption term read out of the wrong
// field, which produced decay times up to 88 seconds - a bus that never decayed
// and simply accumulated. It was loud enough that the game had to be closed.
//
// The cause is fixed and fused in three places, but "I found the bug" is not the
// same claim as "this is safe on your machine", and the difference is somebody's
// hearing. It is opt-in until it has been heard once and confirmed. Set
// Reverb=1 in MonsterRPGAudio.cfg.
bool s_reverbEnabled = false;

// Level match between the two panners, so toggling HRTF changes the IMAGE and
// not the volume.
//
// The constant-power pan sends 0.707 to each ear for a centred source, i.e.
// unit total power. The head model sends roughly unity to each ear - it is
// modelling two ears, not splitting one signal between them - which is +3 dB.
// Without this trim, turning HRTF on sounds "better" mostly because it is
// louder, which is the oldest way there is to fool yourself in an A/B.
const float BINAURAL_TRIM = 0.70710678f;

// How loud the room is against the dry sound.
//
// The FDN is normalised to unit RMS for a unit impulse (see Reverb.hpp), so this
// number means "how much room" and nothing else - which is exactly the property
// ConvolverNode.normalize gave the browser and the reason acoustics.js could
// tune a wet mix at all. Without that normalisation this constant would silently
// mean "how much room times how long its tail is" and every value would be wrong
// for every room but the one it was set in.
const float WET_TRIM = 0.35f;

const float AIR_REF_HZ   = 5000.0f;
float       s_airDbPerM  = 0.08f;
bool        s_airEnabled = true;

// ── THE MEMORY BUDGET, AND WHY IT IS NOT OPTIONAL ────────────────────────────
//
// Measured, not guessed: decoding every .wav under Add-Ons on this machine
// produces 375 MB of float32 (2095 files, all of which decode cleanly). The
// manifest is a subset of that, but a large one.
//
// THIS IS A 32-BIT PROCESS. Its address space is shared with Blockland, which
// is already the biggest thing in it, and running out is not a missing sound -
// it is the player's game crashing. So the bank stops at a hard cap and says so
// in the log.
//
// Sounds are loaded in ASCENDING ID ORDER, which is manifest order, which puts
// stock and early-loading add-on sounds first. That is a weak prioritisation
// rather than a clever one, and it is honest about being weak: the manifest has
// a "hot" flag that would do this properly, and wiring it through the NAMES
// table is a Phase 4 job.
//
// Anything past the cap simply never loads and is counted as `missed`. It is
// visible, bounded, and not a crash.
const long long BANK_BUDGET_BYTES = 128LL * 1024 * 1024;

// Gain smoothing for sustained emitters, in seconds. One-shots are NOT smoothed:
// they know every parameter at the moment they start, and ramping one would ramp
// it across its own duration. That distinction is why acouSetParam takes a
// `ramp` argument, and it is preserved here.
const float LOOP_TAU = 0.05f;

// ── Device state ─────────────────────────────────────────────────────────────

std::atomic<bool> s_running{false};
std::atomic<bool> s_deviceUp{false};
std::thread       s_audioThread;
std::thread       s_loadThread;
HANDLE            s_bufferEvent = nullptr;

int s_rate     = 48000;
int s_channels = 2;

char s_dllDir[MAX_PATH * 2] = {0};

// ── Counters. Diagnostics only, never control flow. ──────────────────────────
std::atomic<unsigned long long> s_played{0};
std::atomic<unsigned long long> s_missed{0};    // no sample loaded yet
std::atomic<unsigned long long> s_dropped{0};   // ring full, or no free voice
std::atomic<unsigned long long> s_underruns{0};

// ── The sample bank ──────────────────────────────────────────────────────────

struct BankEntry {
    std::string        name;
    std::string        path;
    std::atomic<MrpgWav::Sound*> sound{nullptr};   // published with release
    std::atomic<bool>  tried{false};
};

std::mutex                                     s_bankMutex;
// id -> file path, straight from the server. There is no name hop any more:
// getName() is empty on a ghosted datablock, so a name was never a key the
// client could hold. See SfxWire.h.
std::unordered_map<unsigned int, std::string>  s_idPath;        // id   -> path
// Kept for diagnostics only - "how many AudioProfiles does this client have" is
// still worth knowing when a sound fails to load. Nothing resolves through it.
std::unordered_map<std::string, std::string>   s_profilePath;   // path -> path
std::unordered_map<unsigned int, BankEntry*>   s_bank;          // id   -> entry
std::atomic<int>                               s_loadedCount{0};
std::atomic<int>                               s_pendingCount{0};
std::atomic<long long>                         s_bankBytes{0};
std::atomic<int>                               s_skippedBudget{0};

// Resolve id -> entry, creating it the first time. Bank mutex held.
BankEntry* EntryFor(unsigned int id)
{
    auto it = s_bank.find(id);
    if (it != s_bank.end()) return it->second;

    auto n = s_idPath.find(id);
    if (n == s_idPath.end()) return nullptr;

    BankEntry* e = new BankEntry();
    e->name = n->second;      // the path doubles as the human-readable label
    e->path = n->second;
    s_bank[id] = e;
    return e;
}

// ── The command ring ─────────────────────────────────────────────────────────
//
// Single producer (net thread), single consumer (audio thread), so a plain pair
// of atomics is correct and no lock is needed. A mutex here would be a lock on
// the audio thread, which is the thing that must never happen.

struct Cmd {
    unsigned int id;
    unsigned int flags;
    unsigned int loopHandle;
    float        x, y, z;
    float        vol, pitch;
    float        energy[4];
    float        occlusion;
    float        dist;

    // The room, resolved on the NET thread. The audio thread receives a bus
    // INDEX and a send level and does no room arithmetic at all - building an
    // FDN allocates and then runs itself for up to a second to measure its own
    // level, neither of which may happen on the audio thread.
    int          bus;          // -1 = dry, no enclosure
    float        send;
};

Cmd                     s_ring[CMD_RING];
std::atomic<unsigned>   s_ringHead{0};   // written by producer
std::atomic<unsigned>   s_ringTail{0};   // written by consumer

// ── Listener ─────────────────────────────────────────────────────────────────
//
// Written by the game thread, read by the audio thread. Three floats each, and a
// torn read costs one block of slightly wrong panning - which is inaudible, and
// far cheaper than a lock on the audio thread.
std::atomic<float> s_lx{0}, s_ly{0}, s_lz{0};
std::atomic<float> s_fx{0}, s_fy{1}, s_fz{0};

// Has SetListener ever been called?
//
// THIS IS A FAIL-OPEN FLAG AND THAT IS THE WHOLE POINT. The distance cull below
// compares a world-space source against the listener, and an unset listener sits
// at the origin. On this map the ground is at Z ~ 10000, so every sound measured
// ~10 km away and was culled - the client received all 25 records the server sent
// and played none of them, silently, because the cull did not count what it
// dropped. An unset listener must never be able to silence everything.
std::atomic<bool> s_listenerSet{false};

// 0 master, 1 sfx, 2 music, 3 voice. Read by the audio thread every block, so
// atomics rather than a lock.
std::atomic<float> s_vol[4] = { {1.0f}, {1.0f}, {0.6f}, {1.0f} };

// The endpoint the player asked for, "" for the system default.
std::string s_wantDeviceId;
char        s_curDeviceName[192] = "(default)";

// Culled for distance, and culled because there was no listener to measure from.
// Separate counters: the first is normal and expected, the second is a fault.
std::atomic<unsigned long long> s_culledDist{0};
std::atomic<unsigned long long> s_noListener{0};

// ── Biquad ───────────────────────────────────────────────────────────────────
//
// RBJ cookbook, which is also exactly what Web Audio's BiquadFilterNode
// implements - so the native and browser clients match by construction rather
// than by taste.

struct Biquad {
    float b0 = 1, b1 = 0, b2 = 0, a1 = 0, a2 = 0;
    float x1 = 0, x2 = 0, y1 = 0, y2 = 0;

    void Reset() { x1 = x2 = y1 = y2 = 0; }

    void SetPeaking(float freq, float sr, float dB, float Q)
    {
        const float A     = powf(10.0f, dB / 40.0f);
        const float w0    = 6.283185307f * freq / sr;
        const float alpha = sinf(w0) / (2.0f * Q);
        const float cw    = cosf(w0);

        const float a0 = 1 + alpha / A;
        b0 = (1 + alpha * A) / a0;
        b1 = (-2 * cw) / a0;
        b2 = (1 - alpha * A) / a0;
        a1 = (-2 * cw) / a0;
        a2 = (1 - alpha / A) / a0;
    }

    void SetLowShelf(float freq, float sr, float dB)
    {
        const float A  = powf(10.0f, dB / 40.0f);
        const float w0 = 6.283185307f * freq / sr;
        const float cw = cosf(w0), sw = sinf(w0);
        const float alpha = sw / 2.0f * sqrtf((A + 1 / A) * (1 / 0.707f - 1) + 2);
        const float tsa = 2 * sqrtf(A) * alpha;

        const float a0 = (A + 1) + (A - 1) * cw + tsa;
        b0 = A * ((A + 1) - (A - 1) * cw + tsa) / a0;
        b1 = 2 * A * ((A - 1) - (A + 1) * cw) / a0;
        b2 = A * ((A + 1) - (A - 1) * cw - tsa) / a0;
        a1 = -2 * ((A - 1) + (A + 1) * cw) / a0;
        a2 = ((A + 1) + (A - 1) * cw - tsa) / a0;
    }

    void SetHighShelf(float freq, float sr, float dB)
    {
        const float A  = powf(10.0f, dB / 40.0f);
        const float w0 = 6.283185307f * freq / sr;
        const float cw = cosf(w0), sw = sinf(w0);
        const float alpha = sw / 2.0f * sqrtf((A + 1 / A) * (1 / 0.707f - 1) + 2);
        const float tsa = 2 * sqrtf(A) * alpha;

        const float a0 = (A + 1) - (A - 1) * cw + tsa;
        b0 = A * ((A + 1) + (A - 1) * cw + tsa) / a0;
        b1 = -2 * A * ((A - 1) + (A + 1) * cw) / a0;
        b2 = A * ((A + 1) + (A - 1) * cw - tsa) / a0;
        a1 = 2 * ((A - 1) - (A + 1) * cw) / a0;
        a2 = ((A + 1) - (A - 1) * cw - tsa) / a0;
    }

    inline float Process(float x)
    {
        const float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
        x2 = x1; x1 = x;
        y2 = y1; y1 = y;
        return y;
    }
};

// ── Voices ───────────────────────────────────────────────────────────────────

struct Voice {
    bool               active     = false;
    const MrpgWav::Sound* snd     = nullptr;
    double             pos        = 0;      // fractional frame index
    double             step       = 1;      // rate ratio * pitch
    float              gain       = 0;      // current
    float              gainTarget = 0;
    float              panL = 0.7f, panR = 0.7f;
    float              panLTarget = 0.7f, panRTarget = 0.7f;
    Biquad             eq[4];
    unsigned int       loopHandle = 0;      // 0 = one-shot
    unsigned int       id         = 0;

    // ── WHAT THE VOICE REMEMBERS, AND WHY IT IS THE WORLD POSITION ──────────
    //
    // The server sends WORLD space precisely so the client can re-project it
    // against a live head (decision D2). The first version threw that away: it
    // computed a pan once, when the sound started, and the sound then stayed
    // glued to that direction while the player turned - which is the exact
    // defect the browser path has and the whole reason this path exists.
    //
    // So the world position is kept and the direction is recomputed EVERY
    // BLOCK. Turning your head is then limited by the output buffer, not by
    // when the sound happened to start.
    // Which slider owns this voice. 1 = sfx today; voice and music get their own
    // when those phases land, and the mixer already honours all four.
    int                category   = 1;
    bool               positional = false;
    float              wx = 0, wy = 0, wz = 0;
    float              spectrumPeak = 1.0f;   // level from the tracer's spectrum
    float              vol          = 1.0f;   // the game's own volume

    // Air absorption: one pole, y += (x - y) * a. Coefficient is recomputed once
    // per block from the distance; the state is per sample.
    float              airA = 1.0f;           // 1 = wide open, no filtering
    float              airZ = 0.0f;

    // Binaural rendering. Only used when HRTF is on AND the voice is
    // positional - music and UI must never be pushed through a head model.
    MrpgHrtf::Binaural bin;
    bool               binaural = false;

    int                bus  = -1;     // room bus, or -1 for dry
    float              send = 0.0f;   // how much of this voice goes to it
};

Voice s_voices[MAX_VOICES];

// ── Room buses ───────────────────────────────────────────────────────────────
//
// ONE BUS PER DISTINCT ROOM, NOT ONE PER SOUND. A busy fight is dozens of
// one-shots a second and they are nearly all in the same room; giving each its
// own reverb would be dozens of FDNs for one acoustic answer.
//
// BUILT ONCE AND NEVER REBUILT, which is what makes this safe without a lock.
// The net thread may publish a NEW bus into a free slot; it never mutates one
// the audio thread might be reading. A pointer published with a release store
// and read with an acquire load is the whole synchronisation - see the ring
// above, which works the same way and for the same reason.
//
// Eight is the ladder in acoustics.js (ACOU_MAX_BUSES) and it is plenty: past
// about three simultaneous distinct rooms the player cannot tell them apart
// anyway, and the ninth room takes the nearest match.
const int MAX_BUSES = 8;

struct BusSlot {
    std::atomic<MrpgReverb::Fdn*> fdn{nullptr};
    float rt60 = 0, rt60Hf = 0, mfp = 0;      // written before publish only
};

BusSlot s_buses[MAX_BUSES];
std::atomic<int> s_busCount{0};

// Per-block accumulator, one mono send per bus. Sized at Init.
std::vector<float> s_busIn[MAX_BUSES];

// How different two rooms may be and still share a bus. Generous on purpose:
// this is the quantisation the plan calls a ladder, and a 10% difference in
// decay time is inaudible while a second FDN is not free.
bool RoomMatches(const BusSlot& b, float rt60, float rt60Hf, float mfp)
{
    const float dr = fabsf(b.rt60 - rt60) / (rt60 > 0.01f ? rt60 : 0.01f);
    const float dh = fabsf(b.rt60Hf - rt60Hf) / (rt60Hf > 0.01f ? rt60Hf : 0.01f);
    const float dm = fabsf(b.mfp - mfp) / (mfp > 0.1f ? mfp : 0.1f);
    return dr < 0.25f && dh < 0.35f && dm < 0.35f;
}

// NET THREAD ONLY. Returns a bus index, or -1 if this sound wants no reverb.
int BusFor(float mfp, float rt60, float rt60Hf)
{
    if (!s_reverbEnabled) return -1;
    if (rt60 < 0.08f || mfp <= 0.05f) return -1;      // outdoors: no room at all

    // A mean free path longer than this is not a room, it is the tracer failing
    // to find one. Rendering it as a vast cathedral is how a courtyard ended up
    // sounding like a cave.
    if (mfp > 40.0f) return -1;

    const int n = s_busCount.load(std::memory_order_acquire);

    for (int i = 0; i < n; ++i)
        if (RoomMatches(s_buses[i], rt60, rt60Hf, mfp)) return i;

    if (n < MAX_BUSES) {
        MrpgReverb::Fdn* f = new MrpgReverb::Fdn();
        f->Build(mfp, rt60, rt60Hf, (float)s_rate);

        s_buses[n].rt60   = rt60;
        s_buses[n].rt60Hf = rt60Hf;
        s_buses[n].mfp    = mfp;
        s_buses[n].fdn.store(f, std::memory_order_release);
        s_busCount.store(n + 1, std::memory_order_release);

        MrpgLog::Write("audio: room bus %d built - mfp %.1f m, RT60 %.2f s "
                       "(treble %.2f s)", n, mfp, rt60, rt60Hf);
        return n;
    }

    // Full: nearest match on decay time, which is the dimension the ear is most
    // sensitive to. A slightly wrong room is not noticeable; no room is.
    int best = 0;
    float bestD = 1e30f;
    for (int i = 0; i < n; ++i) {
        const float d = fabsf(s_buses[i].rt60 - rt60);
        if (d < bestD) { bestD = d; best = i; }
    }
    return best;
}

// ── The acoustic model, ported from acoustics.js ─────────────────────────────
//
// PORT THE CONSTANTS, NOT THE INTENT. Every number below was tuned by ear
// against this soundtrack and this geometry, and several carry comments in
// acoustics.js saying so. Copying them verbatim is what makes a difference
// between the two clients attributable to the ENGINE rather than to somebody
// re-deriving a value they thought looked wrong.

// The one-pole coefficient that produces exactly `g` linear gain at AIR_REF_HZ.
//
// SOLVED, NOT APPROXIMATED. The usual shortcut is |H(f)| ~ fc/f, which is only
// true well above the cutoff and is wrong by a lot exactly where these sounds
// live - near the listener, where the filter should be doing almost nothing. For
// H(z) = a / (1 - (1-a) z^-1), writing b = 1-a and squaring the magnitude gives
//
//     b^2 (1-g^2) - 2b (1 - g^2 cos w) + (1-g^2) = 0
//
// which is a quadratic in the pole. The root inside the unit circle is the one
// with the minus sign.
inline float AirCoefFor(float g, float w)
{
    // Nothing to do: g at or above unity means the filter would have to boost,
    // and the degenerate 1-g^2 = 0 would divide by zero on the way there.
    if (g >= 0.999f) return 1.0f;
    if (g <= 0.0f)   return 0.0f;

    const float A = 1.0f - g * g;
    const float k = 1.0f - g * g * cosf(w);
    float disc = k * k - A * A;
    if (disc < 0) disc = 0;

    float b = (k - sqrtf(disc)) / A;
    if (b < 0.0f) b = 0.0f;
    if (b > 0.999f) b = 0.999f;
    return 1.0f - b;
}

inline float DistanceGain(float dist)
{
    const float d = (dist > REF_DISTANCE) ? dist : REF_DISTANCE;
    return REF_DISTANCE / (REF_DISTANCE + ROLLOFF * (d - REF_DISTANCE));
}

// Per-band gain of what survives passing THROUGH a wall rather than round it.
// In step with TRANSMIT_DEFAULT in RayKernels.comp. Bass gets through and treble
// does not, which is the whole difference between "muffled" and "quiet".
const float WALL_TRANSMISSION[4] = { 0.18f, 0.12f, 0.055f, 0.02f };

// THE EQ CARRIES SHAPE. THE GAIN CARRIES LEVEL.
//
// energy[] is a transfer function whose bands are all below 1, so feeding it
// straight into four cascaded filters applies an unbudgeted second helping of
// attenuation on top of the distance gain, on a curve nobody chose. Normalise to
// the LOUDEST band - not the mean - so every filter is a cut and the level lives
// in one place.
//
// TWO GUARDS HERE HAVE BEEN RE-LEARNED THE HARD WAY IN THE BROWSER AND MUST
// SURVIVE THE PORT:
//   1. All four bands at zero means NO SPECTRAL DATA, not silence. Return flat.
//      The CPU fallback, the !gpuUsed fallback and a map with no bricks all emit
//      it, and taking it literally makes everyone inaudible on an empty map.
//   2. Normalising to the mean turns a dark spectrum - which every distant or
//      occluded sound is - into "bass lifted", so walking away from a source
//      makes it boomier. Loudest band, always.
void SpectrumToTilt(const float energy[4], float occ, float outDb[4], float& outPeak)
{
    float shape[4];
    float peak = 0;
    for (int b = 0; b < 4; ++b) if (energy[b] > peak) peak = energy[b];

    if (!(peak > 0.001f)) {
        for (int b = 0; b < 4; ++b) shape[b] = 1.0f;      // no data, not silence
    } else {
        for (int b = 0; b < 4; ++b) {
            float v = energy[b] > 0 ? energy[b] : 0;
            shape[b] = v / peak;
        }
    }

    float band[4];
    float bandPeak = 0;
    for (int b = 0; b < 4; ++b) {
        // Steam Audio's decomposition: the direct path splits into an
        // unoccluded fraction and a fraction TRANSMITTED THROUGH the wall, the
        // second one filtered. A blocked sound goes muffled, not merely quiet.
        const float transmission = (1.0f - occ) + occ * WALL_TRANSMISSION[b];
        band[b] = shape[b] * transmission;
        if (band[b] > bandPeak) bandPeak = band[b];
    }

    outPeak = bandPeak;
    for (int b = 0; b < 4; ++b) {
        if (bandPeak > 0) {
            float ratio = band[b] / bandPeak;
            if (ratio < 0.001f) ratio = 0.001f;           // never log10(0)
            float dB = 20.0f * log10f(ratio);
            if (dB < -30.0f) dB = -30.0f;                 // ACOU_MIN_DB
            if (dB >  0.0f)  dB =  0.0f;                  // every value is a cut
            outDb[b] = dB;
        } else {
            outDb[b] = 0;
        }
    }
}

// ── The test tone ────────────────────────────────────────────────────────────
//
// A sound that depends on NOTHING: no server, no network, no manifest, no WAV on
// disk. That is the whole point of it. When a player says "I hear nothing", the
// first question is which half is broken, and this splits the system exactly
// down the middle - if the tone plays, the device, the mixer, the voice
// allocator and the output format all work, and the fault is upstream in the
// link or the sample bank. If it does not, none of the upstream matters yet.
//
// 0.4 s of 440 Hz with a short fade at each end, because a hard start and stop
// on a sine is a click at both ends and clicks are what a broken mixer sounds
// like - the diagnostic should not imitate the fault.
MrpgWav::Sound s_testTone;

const unsigned int TEST_TONE_ID = 0xFFFFFFFFu;   // wider than a u16 nameId can be

void BuildTestTone()
{
    const int rate   = s_rate;
    const int frames = (int)(rate * 0.4f);
    s_testTone.data     = (float*)malloc((size_t)frames * sizeof(float));
    if (!s_testTone.data) return;
    s_testTone.channels = 1;
    s_testTone.rate     = rate;
    s_testTone.frames   = frames;
    s_testTone.ok       = true;

    const int fade = rate / 100;             // 10 ms
    for (int i = 0; i < frames; ++i) {
        float env = 1.0f;
        if (i < fade)              env = (float)i / (float)fade;
        if (i > frames - fade - 1) env = (float)(frames - 1 - i) / (float)fade;
        s_testTone.data[i] = 0.25f * env * sinf(6.283185307f * 440.0f * (float)i / (float)rate);
    }
}

// Recompute gain and pan for one voice against the CURRENT listener.
//
// Called once per voice per audio block, which at a 10 ms buffer is 100 Hz -
// far faster than the ~20 Hz the browser path manages and, crucially, not tied
// to when the sound started. `seed` snaps rather than smooths, for a one-shot
// that must begin at the right level.
void UpdateSpatial(Voice& v, bool seed)
{
    // Category volume rides on the voice's own gain, so it scales everything
    // downstream including a future reverb send. See the header.
    const float catVol = s_vol[0].load(std::memory_order_relaxed)
                       * s_vol[v.category].load(std::memory_order_relaxed);

    if (!v.positional) {
        // 2D: no geometry, dead centre, full level. A UI click must not swing
        // around the head when the player turns.
        v.gainTarget = v.spectrumPeak * v.vol * catVol;
        v.panLTarget = v.panRTarget = 0.70710678f;
        v.airA       = 1.0f;   // no distance, therefore no air to travel through
        v.binaural   = false;  // a narrator has no position to be filtered from
        if (seed) { v.gain = v.gainTarget; v.panL = v.panLTarget; v.panR = v.panRTarget; }
        return;
    }

    const float lx = s_lx.load(std::memory_order_relaxed);
    const float ly = s_ly.load(std::memory_order_relaxed);
    const float lz = s_lz.load(std::memory_order_relaxed);
    float fx = s_fx.load(std::memory_order_relaxed);
    float fy = s_fy.load(std::memory_order_relaxed);
    const float fz = s_fz.load(std::memory_order_relaxed);

    const float dx = v.wx - lx, dy = v.wy - ly, dz = v.wz - lz;
    const float dist = sqrtf(dx * dx + dy * dy + dz * dz);

    v.gainTarget = DistanceGain(dist) * v.spectrumPeak * v.vol * catVol;

    // Air absorption, from the same distance the gain came from. Recomputed per
    // block rather than per sample: the coefficient changes at walking pace and
    // a transcendental per sample per voice is not a thing an audio thread does.
    if (s_airEnabled) {
        const float hf = powf(10.0f, -s_airDbPerM * dist / 20.0f);
        v.airA = AirCoefFor(hf, 6.2831853f * AIR_REF_HZ / (float)s_rate);
    } else {
        v.airA = 1.0f;
    }

    float flen = sqrtf(fx * fx + fy * fy);
    if (flen > 0.001f) { fx /= flen; fy /= flen; } else { fx = 0; fy = 1; }

    // Right vector on the ground plane: (fy, -fx). Blockland is Z-up.
    const float rx = fy, ry = -fx;
    const float dlen = sqrtf(dx * dx + dy * dy);
    float side = 0;
    if (dlen > 0.001f) side = (dx * rx + dy * ry) / dlen;
    if (side >  1.0f) side =  1.0f;
    if (side < -1.0f) side = -1.0f;

    // Constant power, so a sound sweeping across the front does not dip in the
    // middle.
    const float angle = (side + 1.0f) * 0.25f * 3.14159265f;
    v.panLTarget = cosf(angle);
    v.panRTarget = sinf(angle);

    // ── BINAURAL ─────────────────────────────────────────────────────────────
    //
    // The head basis comes from Hrtf.hpp, a verbatim port of
    // AudioRT::ToHeadRelative - the same arithmetic the server uses for the SFX
    // path and for voices. A footstep and a shout at one spot must localise
    // identically or the cue stops being worth trusting.
    //
    // MIND THE SIGN. h.z is NEGATED forward (a Web Audio convention preserved
    // deliberately, see Hrtf.hpp), so front-positive is -h.z. Getting that
    // backwards puts every sound behind the player, which is a maddening bug to
    // find because left and right go on working perfectly.
    v.binaural = s_hrtfEnabled && (s_channels >= 2);
    if (v.binaural) {
        const MrpgHrtf::Head h =
            MrpgHrtf::ToHeadRelative(lx, ly, lz, fx, fy, fz, v.wx, v.wy, v.wz);

        const float hlen = sqrtf(h.x * h.x + h.y * h.y + h.z * h.z);
        float az = 0, el = 0;
        if (hlen > 0.001f) {
            az = atan2f(h.x, -h.z);            // 0 ahead, + to the right
            float sn = h.y / hlen;
            if (sn >  1.0f) sn =  1.0f;
            if (sn < -1.0f) sn = -1.0f;
            el = asinf(sn);                    // + up
        }
        v.bin.Set(az, el, (float)s_rate);
    }

    if (seed) { v.gain = v.gainTarget; v.panL = v.panLTarget; v.panR = v.panRTarget; }
}

// ── Mixing ───────────────────────────────────────────────────────────────────

int FindFreeVoice(float wantGain)
{
    for (int i = 0; i < MAX_VOICES; ++i)
        if (!s_voices[i].active) return i;

    // All busy: steal the QUIETEST one-shot. Stealing the oldest would cut a
    // sustained sound that is still audible in favour of a new one that might
    // not be; stealing the quietest is the one the ear is least likely to miss.
    int   best = -1;
    float bestGain = wantGain;
    for (int i = 0; i < MAX_VOICES; ++i) {
        if (s_voices[i].loopHandle != 0) continue;        // never steal a loop
        if (s_voices[i].gain < bestGain) { bestGain = s_voices[i].gain; best = i; }
    }
    return best;
}

void ApplyCmd(const Cmd& c)
{
    // A loop update finds its existing voice and moves it, rather than starting
    // a second one. Sending a start every 50 ms would rebuild the source
    // constantly and the loop would stutter rather than sustain.
    int slot = -1;
    if (c.loopHandle != 0) {
        for (int i = 0; i < MAX_VOICES; ++i)
            if (s_voices[i].active && s_voices[i].loopHandle == c.loopHandle) { slot = i; break; }

        if (c.flags & MRPGWIRE_SFX_LOOP_STOP) {
            if (slot >= 0) s_voices[slot].active = false;
            return;
        }
    }

    // Level first, because it decides whether this is worth a voice at all.
    float tiltDb[4], peak;
    SpectrumToTilt(c.energy, c.occlusion, tiltDb, peak);
    const float gain = DistanceGain(c.dist) * peak * (c.vol > 0 ? c.vol : 1.0f);

    if (slot < 0) {
        if (c.flags & MRPGWIRE_SFX_LOOP_STOP) return;      // nothing to stop
        if (gain < 0.0005f) { s_dropped.fetch_add(1, std::memory_order_relaxed); return; }

        slot = FindFreeVoice(gain);
        if (slot < 0) { s_dropped.fetch_add(1, std::memory_order_relaxed); return; }

        Voice& v = s_voices[slot];
        v = Voice();                     // reset filter state and read position
        v.active     = true;
        v.loopHandle = c.loopHandle;
        v.id         = c.id;
        v.snd        = nullptr;          // resolved below
        s_played.fetch_add(1, std::memory_order_relaxed);
    }

    Voice& v = s_voices[slot];

    if (c.id == TEST_TONE_ID) {
        if (!s_testTone.ok) { v.active = false; return; }
        v.snd  = &s_testTone;
        v.step = 1.0;
    }

    // The sample pointer is published by the loader with a release store; this
    // is the matching acquire. Null means "not decoded yet" and the voice simply
    // does not sound - counted, never waited for.
    if (!v.snd) {
        auto it = s_bank.find(c.id);      // read-only lookup; see the note in Submit
        if (it == s_bank.end()) { v.active = false; s_missed.fetch_add(1, std::memory_order_relaxed); return; }
        MrpgWav::Sound* snd = it->second->sound.load(std::memory_order_acquire);
        if (!snd) { v.active = false; s_missed.fetch_add(1, std::memory_order_relaxed); return; }
        v.snd  = snd;
        v.step = ((double)snd->rate / (double)s_rate) * (c.pitch > 0 ? c.pitch : 1.0);
    }

    v.bus  = c.bus;
    v.send = c.send;

    // POSITION IS STORED, NOT RESOLVED. MixBlock turns it into gain and pan
    // against whatever the listener is doing at that moment. See the Voice
    // comment; this used to bake a pan here and never revisit it.
    v.positional   = (c.flags & MRPGWIRE_SFX_2D) == 0;
    v.wx           = c.x;
    v.wy           = c.y;
    v.wz           = c.z;
    v.spectrumPeak = peak;
    v.vol          = (c.vol > 0 ? c.vol : 1.0f);

    // Seed the smoothers so a one-shot starts at its correct level and
    // direction rather than sliding up to them across its own first
    // milliseconds. MixBlock takes over from here.
    UpdateSpatial(v, true);

    for (int b = 0; b < 4; ++b) {
        if (b == 0)      v.eq[b].SetLowShelf (BAND_HZ[b], (float)s_rate, tiltDb[b]);
        else if (b == 3) v.eq[b].SetHighShelf(BAND_HZ[b], (float)s_rate, tiltDb[b]);
        else             v.eq[b].SetPeaking  (BAND_HZ[b], (float)s_rate, tiltDb[b], 1.0f);
    }
}

void DrainCommands()
{
    unsigned tail = s_ringTail.load(std::memory_order_relaxed);
    const unsigned head = s_ringHead.load(std::memory_order_acquire);
    while (tail != head) {
        ApplyCmd(s_ring[tail % CMD_RING]);
        ++tail;
    }
    s_ringTail.store(tail, std::memory_order_release);
}

// Mix one block. NO ALLOCATION, NO LOCKS, NO LOGGING - see the header.
void MixBlock(float* out, int frames)
{
    memset(out, 0, sizeof(float) * frames * s_channels);

    // Front left and right are channels 0 and 1 in every standard layout, and
    // everything else (centre, LFE, surrounds) is left silent. Phase 4's HRTF is
    // a headphone technique and will keep this two-channel front image; a proper
    // surround downmix is a different feature and not one this needs.
    const int stride = s_channels;

    const float smooth = 1.0f - expf(-1.0f / (LOOP_TAU * (float)s_rate));

    const int busN = s_busCount.load(std::memory_order_acquire);
    for (int b = 0; b < busN; ++b) {
        if ((int)s_busIn[b].size() < frames) s_busIn[b].assign((size_t)frames, 0.0f);
        else std::fill(s_busIn[b].begin(), s_busIn[b].begin() + frames, 0.0f);
    }

    for (int i = 0; i < MAX_VOICES; ++i) {
        Voice& v = s_voices[i];
        if (!v.active || !v.snd || !v.snd->data) continue;

        const MrpgWav::Sound& s = *v.snd;
        const bool isLoop = (v.loopHandle != 0);

        // THE HEAD TRANSFORM, once per block, for EVERY voice - one-shots
        // included. A one-shot is exactly the case the old code got wrong: it
        // is short, it is usually the sound you turn towards, and it used to be
        // nailed to the direction it started in.
        UpdateSpatial(v, false);

        for (int f = 0; f < frames; ++f) {
            const int i0 = (int)v.pos;
            if (i0 >= s.frames - 1) {
                if (isLoop) { v.pos -= (double)(s.frames - 1); continue; }
                v.active = false;
                break;
            }

            // Linear interpolation. Cubic is Phase 4's problem: at these rate
            // ratios (22050/48000 and friends) the difference is a fraction of a
            // dB of high-frequency image, and correctness first.
            const float frac = (float)(v.pos - (double)i0);
            float sample;
            if (s.channels == 1) {
                sample = s.data[i0] * (1 - frac) + s.data[i0 + 1] * frac;
            } else {
                // Stereo source, mono voice: fold down. A world sound has one
                // position, so its own stereo image is meaningless once panned.
                const float l = s.data[i0 * 2]     * (1 - frac) + s.data[(i0 + 1) * 2]     * frac;
                const float r = s.data[i0 * 2 + 1] * (1 - frac) + s.data[(i0 + 1) * 2 + 1] * frac;
                sample = (l + r) * 0.5f;
            }

            for (int b = 0; b < 4; ++b) sample = v.eq[b].Process(sample);

            // AFTER the tracer's own EQ, and the order is not arbitrary even
            // though both are linear: the EQ carries what the geometry did to
            // this sound - walls, enclosure - and air absorption is what the
            // remaining distance does to the result. Reading the chain in
            // propagation order is worth more than the zero cycles it saves.
            if (v.airA < 1.0f) {
                v.airZ += (sample - v.airZ) * v.airA;
                sample = v.airZ;
            }

            // Smoothed for EVERY voice now, not just loops. The targets move
            // once per block for all of them, and stepping a gain 100 times a
            // second is zipper noise. The one-shot's opening value is seeded
            // exactly, so this only ever tracks movement after that.
            v.gain += (v.gainTarget - v.gain) * smooth;
            v.panL += (v.panLTarget - v.panL) * smooth;
            v.panR += (v.panRTarget - v.panR) * smooth;

            const float g = sample * v.gain;

            // The reverb send is taken PRE-PAN and POST-EQ: a room is excited
            // by the sound that reaches it, which has already been coloured by
            // the geometry, but a room does not care which ear you heard it in.
            if (v.bus >= 0 && v.bus < busN)
                s_busIn[v.bus][f] += g * v.send;


            if (v.binaural) {
                // ── HRTF ──────────────────────────────────────────────────────
                //
                // The delay smoothing uses the SAME per-sample coefficient as
                // the gain and pan above, so all three settle together when the
                // player turns. A delay that lagged the gain would pull the
                // image apart during exactly the movement this phase exists to
                // make convincing.
                float bl, br;
                v.bin.Process(g * BINAURAL_TRIM, smooth, bl, br);
                out[f * stride]     += bl;
                out[f * stride + 1] += br;
            } else {
                out[f * stride]     += g * v.panL;
                out[f * stride + 1] += g * v.panR;
            }

            v.pos += v.step;
        }
    }

    // ── The rooms, once per block, after every voice has fed them ────────────
    //
    // NOT PER VOICE, and that is the whole design. A reverb is a property of the
    // room, not of the sound, so thirty footsteps in one corridor cost one FDN
    // between them - which is why the sends were accumulated into a buffer above
    // rather than processed inline.
    //
    // The wet path is deliberately NOT spatialised. Late reverberation arrives
    // from every direction at once; running it through the head model would
    // collapse a diffuse field to a point, which is both wrong and the exact
    // opposite of what it is for. The FDN's own decorrelated stereo output is
    // the width.
    for (int b = 0; b < busN; ++b) {
        MrpgReverb::Fdn* fdn = s_buses[b].fdn.load(std::memory_order_acquire);
        if (!fdn) continue;

        const float* in = s_busIn[b].data();
        for (int f = 0; f < frames; ++f) {
            // ── THE INPUT IS BOUNDED ─────────────────────────────────────────
            //
            // Thirty voices can feed one bus at once, and a reverb integrates
            // whatever it is given. A ceiling here means a crowded room gets
            // dense rather than loud, and - more to the point - that no future
            // mistake in the send calculation can drive the network to a level
            // nobody can turn down in time.
            float dry = in[f];
            if (dry >  4.0f) dry =  4.0f;
            if (dry < -4.0f) dry = -4.0f;

            float wl, wr;
            fdn->Process(dry, wl, wr);

            // And so is the output, for the same reason and independently: the
            // two clamps guard different failures.
            wl *= WET_TRIM;
            wr *= WET_TRIM;
            if (wl >  1.0f) wl =  1.0f;
            if (wl < -1.0f) wl = -1.0f;
            if (wr >  1.0f) wr =  1.0f;
            if (wr < -1.0f) wr = -1.0f;

            out[f * stride]     += wl;
            out[f * stride + 1] += wr;
        }
    }

    // Soft clip. A hard clamp on a busy mix sounds like a fault; tanh-ish
    // saturation just sounds loud, which is the honest signal that it is.
    for (int i = 0; i < frames * s_channels; ++i) {
        float x = out[i];
        if (x >  1.0f) x =  1.0f - 1.0f / (1.0f + x);
        if (x < -1.0f) x = -1.0f + 1.0f / (1.0f - x);
        out[i] = x;
    }
}

// ── The audio thread ─────────────────────────────────────────────────────────

IMMDeviceEnumerator* s_enum   = nullptr;
IMMDevice*           s_device = nullptr;
IAudioClient*        s_client = nullptr;
IAudioRenderClient*  s_render = nullptr;

void AudioThreadMain()
{
    // MMCSS. Without it this thread is scheduled like any other and a busy
    // moment in the game produces a dropout; with it Windows guarantees it a
    // share. "Pro Audio" is the documented task name for exactly this.
    DWORD taskIndex = 0;
    HANDLE mmcss = AvSetMmThreadCharacteristicsA("Pro Audio", &taskIndex);

    UINT32 bufferFrames = 0;
    s_client->GetBufferSize(&bufferFrames);

    std::vector<float> mixBuf((size_t)bufferFrames * s_channels);

    while (s_running.load(std::memory_order_relaxed)) {
        // Event driven, not polled: WASAPI signals when it wants more.
        if (WaitForSingleObject(s_bufferEvent, 200) != WAIT_OBJECT_0) continue;
        if (!s_running.load(std::memory_order_relaxed)) break;

        UINT32 padding = 0;
        if (FAILED(s_client->GetCurrentPadding(&padding))) continue;

        const UINT32 avail = bufferFrames - padding;
        if (avail == 0) continue;

        BYTE* buf = nullptr;
        if (FAILED(s_render->GetBuffer(avail, &buf)) || !buf) {
            s_underruns.fetch_add(1, std::memory_order_relaxed);
            continue;
        }

        DrainCommands();
        MixBlock(mixBuf.data(), (int)avail);
        memcpy(buf, mixBuf.data(), sizeof(float) * avail * s_channels);

        s_render->ReleaseBuffer(avail, 0);
    }

    if (mmcss) AvRevertMmThreadCharacteristics(mmcss);
}

// ── The loader thread ────────────────────────────────────────────────────────

std::atomic<bool> s_preloadWanted{false};

void LoadThreadMain()
{
    while (s_running.load(std::memory_order_relaxed)) {
        if (!s_preloadWanted.exchange(false)) { Sleep(100); continue; }

        // Snapshot the ids to load, then decode WITHOUT the lock held. Decoding
        // a WAV takes milliseconds and the net thread must not wait on it.
        std::vector<unsigned int> todo;
        {
            std::lock_guard<std::mutex> lock(s_bankMutex);
            for (const auto& kv : s_idPath) {
                BankEntry* e = EntryFor(kv.first);
                if (e && !e->tried.load(std::memory_order_relaxed)) todo.push_back(kv.first);
            }
        }

        // Ascending id = manifest order. See BANK_BUDGET_BYTES.
        std::sort(todo.begin(), todo.end());

        s_pendingCount.store((int)todo.size(), std::memory_order_relaxed);
        int ok = 0, failed = 0, skipped = 0;
        char firstWhy[128] = {0};
        char firstName[96] = {0};

        for (unsigned int id : todo) {
            if (!s_running.load(std::memory_order_relaxed)) break;

            BankEntry* e = nullptr;
            {
                std::lock_guard<std::mutex> lock(s_bankMutex);
                auto it = s_bank.find(id);
                if (it != s_bank.end()) e = it->second;
            }
            if (!e || e->tried.exchange(true)) continue;

            if (s_bankBytes.load(std::memory_order_relaxed) >= BANK_BUDGET_BYTES) {
                ++skipped;
                s_skippedBudget.fetch_add(1, std::memory_order_relaxed);
                s_pendingCount.fetch_sub(1, std::memory_order_relaxed);
                continue;
            }

            char why[128] = {0};
            MrpgWav::Sound s = MrpgWav::Load(e->path.c_str(), why, sizeof(why));
            if (s.ok) {
                MrpgWav::Sound* heap = new MrpgWav::Sound(s);
                // RELEASE, matching the acquire in ApplyCmd. Without the pairing
                // the mixer could see the pointer before the samples it points at.
                e->sound.store(heap, std::memory_order_release);
                s_bankBytes.fetch_add((long long)s.frames * s.channels * 4LL,
                                      std::memory_order_relaxed);
                s_loadedCount.fetch_add(1, std::memory_order_relaxed);
                ++ok;
            } else {
                ++failed;
                if (!firstWhy[0]) {
                    lstrcpynA(firstWhy, why[0] ? why : "unknown", sizeof(firstWhy));
                    lstrcpynA(firstName, e->name.c_str(), sizeof(firstName));
                }
            }
            s_pendingCount.fetch_sub(1, std::memory_order_relaxed);
        }

        if (ok || failed || skipped) {
            MrpgLog::Write("audio: preload done - %d loaded (%.1f MB), %d failed, %d skipped",
                           ok, (double)s_bankBytes.load(std::memory_order_relaxed) / 1048576.0,
                           failed, skipped);
            if (failed && firstWhy[0])
                MrpgLog::Write("audio:   first failure: %s (%s)", firstName, firstWhy);
            if (skipped)
                MrpgLog::Write("audio:   %d sound(s) skipped: the %lld MB bank budget is full."
                               " Those will not play.",
                               skipped, BANK_BUDGET_BYTES / (1024 * 1024));
        }
        s_pendingCount.store(0, std::memory_order_relaxed);
    }
}

} // namespace

// ── Lifecycle ────────────────────────────────────────────────────────────────

bool Init(const char* dllDir)
{
    if (s_running.exchange(true)) return true;
    lstrcpynA(s_dllDir, dllDir ? dllDir : "", sizeof(s_dllDir));

    // Read here rather than at file scope: MrpgCfg::Load runs after the DLL is
    // mapped, so a file-scope initialiser would capture the defaults and nothing
    // in the cfg would ever take effect.
    s_hrtfEnabled = MrpgCfg::GetInt("HRTF", s_hrtfEnabled ? 1 : 0) != 0;
    s_reverbEnabled = MrpgCfg::GetInt("Reverb", s_reverbEnabled ? 1 : 0) != 0;
    s_airEnabled = MrpgCfg::GetInt("AirAbsorb", s_airEnabled ? 1 : 0) != 0;
    s_airDbPerM  = MrpgCfg::GetFloat("AirAbsorbDbPerM", s_airDbPerM);
    if (s_airDbPerM < 0) s_airDbPerM = 0;

    MrpgLog::Write("audio: air absorption %s (%.3f dB/m at %.0f Hz)",
                   s_airEnabled ? "on" : "off", s_airDbPerM, AIR_REF_HZ);
    MrpgLog::Write("audio: HRTF %s", s_hrtfEnabled ? "on" : "off");
    MrpgLog::Write("audio: room reverb %s", s_reverbEnabled ? "ON" : "off (set Reverb=1 to try it)");

    // Apartment-threaded, and this is the thread that will call Shutdown too.
    // RPC_E_CHANGED_MODE means the game already initialised COM differently -
    // not an error for us, because we only use the device enumerator and it is
    // happy either way.
    HRESULT hr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    const bool weInitedCom = SUCCEEDED(hr);

    hr = CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_ALL,
                          __uuidof(IMMDeviceEnumerator), (void**)&s_enum);
    if (FAILED(hr) || !s_enum) {
        MrpgLog::Write("audio: no device enumerator (hr 0x%08lX) - native audio is off", (unsigned long)hr);
        s_running.store(false);
        if (weInitedCom) CoUninitialize();
        return false;
    }

    // A specific endpoint if the player picked one, otherwise the system
    // default. An ID that no longer resolves - an unplugged headset - falls back
    // rather than failing, and says so.
    if (!s_wantDeviceId.empty()) {
        int n = (int)MultiByteToWideChar(CP_UTF8, 0, s_wantDeviceId.c_str(), -1, nullptr, 0);
        if (n > 0) {
            std::vector<wchar_t> w((size_t)n);
            MultiByteToWideChar(CP_UTF8, 0, s_wantDeviceId.c_str(), -1, w.data(), n);
            if (FAILED(s_enum->GetDevice(w.data(), &s_device)) || !s_device) {
                MrpgLog::Write("audio: the chosen output device is not present; using the default");
                s_device = nullptr;
            }
        }
    }

    if (!s_device) hr = s_enum->GetDefaultAudioEndpoint(eRender, eConsole, &s_device);
    if (FAILED(hr) || !s_device) {
        MrpgLog::Write("audio: no default output device (hr 0x%08lX)", (unsigned long)hr);
        s_running.store(false);
        return false;
    }

    {
        IPropertyStore* props = nullptr;
        if (SUCCEEDED(s_device->OpenPropertyStore(STGM_READ, &props)) && props) {
            PROPVARIANT v; PropVariantInit(&v);
            if (SUCCEEDED(props->GetValue(PKEY_Device_FriendlyName, &v)) && v.vt == VT_LPWSTR)
                WideCharToMultiByte(CP_UTF8, 0, v.pwszVal, -1,
                                    s_curDeviceName, sizeof(s_curDeviceName), nullptr, nullptr);
            PropVariantClear(&v);
            props->Release();
        }
    }

    hr = s_device->Activate(__uuidof(IAudioClient), CLSCTX_ALL, nullptr, (void**)&s_client);
    if (FAILED(hr) || !s_client) {
        MrpgLog::Write("audio: could not activate the audio client (hr 0x%08lX)", (unsigned long)hr);
        s_running.store(false);
        return false;
    }

    // SHARED MODE, DELIBERATELY. Exclusive mode would take the device away from
    // the game's own OpenAL, which is still open for every sound we do not
    // intercept - and for every OTHER application the player is running.
    WAVEFORMATEX* mix = nullptr;
    hr = s_client->GetMixFormat(&mix);
    if (FAILED(hr) || !mix) {
        MrpgLog::Write("audio: GetMixFormat failed (hr 0x%08lX)", (unsigned long)hr);
        s_running.store(false);
        return false;
    }

    // ── USE THE DEVICE'S OWN MIX FORMAT. DO NOT INVENT ONE. ─────────────────
    //
    // The first version built a WAVEFORMATEXTENSIBLE describing 2-channel
    // float32 at the device's rate, and real Blockland answered
    // AUDCLNT_E_UNSUPPORTED_FORMAT (0x88890008) on this machine. In SHARED mode
    // the endpoint does not have to accept an invented format at all - the one
    // format it is guaranteed to accept is the one GetMixFormat just handed us,
    // because that is what its mixer is already running.
    //
    // So take it whole: rate, channel count and layout. The channel count in
    // particular is not ours to choose - a 5.1 or 7.1 endpoint reports 6 or 8,
    // and asking it for 2 is exactly the request that was refused.
    s_rate     = (int)mix->nSamplesPerSec;
    s_channels = (int)mix->nChannels;

    // We can only write float samples. Shared-mode mix formats are float32 in
    // every case anyone has met, but "virtually always" is not "always", so this
    // is checked rather than assumed - and a machine that says otherwise gets a
    // log line naming the format instead of silence.
    bool isFloat = false;
    if (mix->wFormatTag == WAVE_FORMAT_IEEE_FLOAT) {
        isFloat = true;
    } else if (mix->wFormatTag == WAVE_FORMAT_EXTENSIBLE
               && mix->cbSize >= sizeof(WAVEFORMATEXTENSIBLE) - sizeof(WAVEFORMATEX)) {
        const WAVEFORMATEXTENSIBLE* ext = (const WAVEFORMATEXTENSIBLE*)mix;
        isFloat = (memcmp(&ext->SubFormat, &MRPG_SUBTYPE_IEEE_FLOAT, sizeof(GUID)) == 0);
    }

    if (!isFloat || mix->wBitsPerSample != 32) {
        MrpgLog::Write("audio: the output device's mix format is not 32-bit float"
                       " (tag %u, %u bits) - native audio is off",
                       (unsigned)mix->wFormatTag, (unsigned)mix->wBitsPerSample);
        CoTaskMemFree(mix);
        s_running.store(false);
        return false;
    }

    if (s_channels < 2) {
        MrpgLog::Write("audio: the output device reports %d channel(s); stereo panning"
                       " needs at least 2 - native audio is off", s_channels);
        CoTaskMemFree(mix);
        s_running.store(false);
        return false;
    }

    // 10 ms, halved from 20.
    //
    // This is the largest fixed latency the client adds, and the goal is that a
    // player's ping is the only thing they wait on. Shared mode still mixes on
    // the endpoint's own period underneath, so this is our contribution to the
    // total rather than the total itself. MMCSS is what makes 10 ms safe on a
    // machine that is also running the game; without it this would underrun.
    const REFERENCE_TIME dur = 100000;

    hr = s_client->Initialize(AUDCLNT_SHAREMODE_SHARED,
                              AUDCLNT_STREAMFLAGS_EVENTCALLBACK,
                              dur, 0, mix, nullptr);
    CoTaskMemFree(mix);

    if (FAILED(hr)) {
        MrpgLog::Write("audio: IAudioClient::Initialize failed (hr 0x%08lX) even with the"
                       " device's own mix format - native audio is off", (unsigned long)hr);
        s_running.store(false);
        return false;
    }

    s_bufferEvent = CreateEventA(nullptr, FALSE, FALSE, nullptr);
    s_client->SetEventHandle(s_bufferEvent);

    hr = s_client->GetService(__uuidof(IAudioRenderClient), (void**)&s_render);
    if (FAILED(hr) || !s_render) {
        MrpgLog::Write("audio: no render client (hr 0x%08lX)", (unsigned long)hr);
        s_running.store(false);
        return false;
    }

    s_client->Start();
    s_deviceUp.store(true);

    BuildTestTone();

    s_audioThread = std::thread(AudioThreadMain);
    // ── The measured head, if it is there ────────────────────────────────────
    //
    // AFTER the device is open, because the table is resampled to the device
    // rate once at load rather than per voice per block. A missing or unreadable
    // hrtf.bin is NOT an error: the structural model in Hrtf.hpp takes over, and
    // the log says which one is running so "does this sound right" is answerable
    // without guessing.
    if (s_hrtfEnabled) {
        char path[MAX_PATH * 2];
        // ONE FOLDER UP FROM THE DLL, which is where MonsterRPGAudio.cfg lives.
        // s_dllDir is bin\, i.e. build output; hrtf.bin is an asset and belongs
        // beside the cfg where a player can find it. Cfg::Load does this same
        // walk for this same reason.
        char dir[MAX_PATH * 2];
        lstrcpynA(dir, s_dllDir, sizeof(dir));
        size_t dn = strlen(dir);
        while (dn > 0 && (dir[dn - 1] == '\\' || dir[dn - 1] == '/')) dir[--dn] = 0;
        char* sl = strrchr(dir, '\\');
        if (!sl) sl = strrchr(dir, '/');
        if (sl) *sl = 0;

        _snprintf(path, sizeof(path) - 1, "%s\\hrtf.bin", dir);
        path[sizeof(path) - 1] = 0;

        char why[192] = {0};
        if (MrpgHrtf::LoadTable(path, (float)s_rate, why, sizeof(why)))
            MrpgLog::Write("audio: HRTF using MEASURED head (SADIE II KU100) - %s", why);
        else
            MrpgLog::Write("audio: HRTF using the structural model (hrtf.bin: %s)", why);
    }

    s_loadThread  = std::thread(LoadThreadMain);

    MrpgLog::Write("audio: device up - \"%s\" %d Hz, %d ch, float32, shared mode, MMCSS",
                   s_curDeviceName, s_rate, s_channels);
    return true;
}

void Shutdown()
{
    if (!s_running.exchange(false)) return;

    if (s_bufferEvent) SetEvent(s_bufferEvent);
    if (s_audioThread.joinable()) s_audioThread.join();
    if (s_loadThread.joinable())  s_loadThread.join();

    if (s_client) s_client->Stop();
    if (s_render) { s_render->Release(); s_render = nullptr; }
    if (s_client) { s_client->Release(); s_client = nullptr; }
    if (s_device) { s_device->Release(); s_device = nullptr; }
    if (s_enum)   { s_enum->Release();   s_enum   = nullptr; }
    if (s_bufferEvent) { CloseHandle(s_bufferEvent); s_bufferEvent = nullptr; }

    // Samples are freed only HERE, with both threads joined. Freeing one while
    // the mixer might be reading it is the one race that cannot be made cheap.
    {
        std::lock_guard<std::mutex> lock(s_bankMutex);
        s_bankBytes.store(0);
        for (auto& kv : s_bank) {
            MrpgWav::Sound* s = kv.second->sound.exchange(nullptr);
            if (s) { MrpgWav::Free(*s); delete s; }
            delete kv.second;
        }
        s_bank.clear();
        s_idPath.clear();
        s_profilePath.clear();
    }
    s_loadedCount.store(0);
    s_deviceUp.store(false);

    MrpgLog::Write("audio: device closed");
}

bool IsRunning() { return s_running.load(std::memory_order_relaxed); }

// ── Mappings ─────────────────────────────────────────────────────────────────

bool MapProfile(const char* name, const char* path)
{
    // INSTRUMENTED. Script reported 467 successful calls while this map stayed
    // empty - which is possible because TS_MapProfile returns "1" as soon as it
    // has enough arguments, BEFORE knowing whether anything was stored. So the
    // script's count only ever proved the function ran. These lines show what
    // actually arrives.
    static int s_logged = 0;
    if (s_logged < 5) {
        ++s_logged;
        MrpgLog::Write("audio: MapProfile #%d name='%s' path='%s'",
                       s_logged,
                       name ? name : "(null)",
                       path ? path : "(null)");
    }

    // KEYED ON THE PATH, NOT THE NAME. `name` is empty for every ghosted
    // datablock and rejecting on it threw away all 467 profiles. This map is
    // diagnostic now - nothing resolves through it - so the name is recorded
    // only when the engine happens to have one.
    if (!path || !*path) {
        static int s_rejected = 0;
        if (++s_rejected <= 3)
            MrpgLog::Write("audio: MapProfile REJECTED - empty path");
        return false;
    }

    std::lock_guard<std::mutex> lock(s_bankMutex);
    s_profilePath[path] = (name && *name) ? name : path;

    // s_logged CAPS at 5 and stops incrementing, so "<= 5" stayed true forever
    // and this logged all 443 stores instead of the first few. Compare against
    // the map size instead, which actually advances.
    if (s_profilePath.size() <= 5)
        MrpgLog::Write("audio:   stored; profile map now holds %d",
                       (int)s_profilePath.size());
    return true;
}

void MapId(unsigned int id, const char* path)
{
    if (!path || !*path) return;
    std::lock_guard<std::mutex> lock(s_bankMutex);
    s_idPath[id] = path;
}

void ClearMappings()
{
    std::lock_guard<std::mutex> lock(s_bankMutex);
    s_idPath.clear();
    // Deliberately NOT clearing s_bank or the decoded samples: the mixer may be
    // reading them right now. Stale entries are unreachable once the ids that
    // pointed at them are gone, and everything is freed at Shutdown.
}

int MappedProfiles() { std::lock_guard<std::mutex> l(s_bankMutex); return (int)s_profilePath.size(); }
int MappedIds()      { std::lock_guard<std::mutex> l(s_bankMutex); return (int)s_idPath.size(); }

void BeginPreload() { s_preloadWanted.store(true, std::memory_order_relaxed); }

bool CanPlay()
{
    return s_deviceUp.load(std::memory_order_relaxed)
        && s_loadedCount.load(std::memory_order_relaxed) > 0;
}

void SetVolume(int category, float value)
{
    if (category < 0 || category > 3) return;
    if (!(value >= 0.0f)) value = 0.0f;     // also catches NaN
    if (value > 2.0f)     value = 2.0f;
    s_vol[category].store(value, std::memory_order_relaxed);
}

float GetVolume(int category)
{
    if (category < 0 || category > 3) return 0.0f;
    return s_vol[category].load(std::memory_order_relaxed);
}

const char* CurrentOutputName() { return s_curDeviceName; }

bool SetOutputDevice(const char* endpointId)
{
    const std::string want = endpointId ? endpointId : "";
    if (want == s_wantDeviceId && s_deviceUp.load(std::memory_order_relaxed))
        return true;                         // already there

    // The whole device is torn down and rebuilt. Swapping an endpoint under a
    // running WASAPI stream is not a thing you can do.
    //
    // ── THE MAPPINGS HAVE TO BE CARRIED ACROSS BY HAND ───────────────────────
    //
    // Shutdown() clears s_idPath and s_profilePath along with the bank, which is
    // right for a real shutdown and catastrophic here: the server sends the
    // id -> path table ONCE, eight entries per heartbeat, and never resends it.
    // A device swap that dropped it would leave the player permanently deaf to
    // every sound the server had already named - silently, with the link still
    // up and the counters still healthy. Nothing in the log would say why.
    //
    // So they are snapshotted and put back. The samples themselves are not: the
    // loader refills the bank from these paths on its own once poked, which is a
    // background reload rather than a stall on the game thread.
    std::unordered_map<unsigned int, std::string> keepId;
    std::unordered_map<std::string, std::string>  keepProfile;
    {
        std::lock_guard<std::mutex> lock(s_bankMutex);
        keepId      = s_idPath;
        keepProfile = s_profilePath;
    }

    const std::string prev = s_wantDeviceId;
    Shutdown();
    s_wantDeviceId = want;

    const bool ok = Init(s_dllDir);
    if (!ok) {
        // Put it back. A player who picks a device that will not open must end up
        // where they started rather than in silence.
        MrpgLog::Write("audio: could not open the chosen device; reverting");
        s_wantDeviceId = prev;
        Init(s_dllDir);
    }

    {
        std::lock_guard<std::mutex> lock(s_bankMutex);
        s_idPath      = keepId;
        s_profilePath = keepProfile;
    }
    s_preloadWanted.store(true, std::memory_order_relaxed);

    MrpgLog::Write("audio: device swap kept %d sound mappings; reloading the bank",
                   (int)keepId.size());
    return ok;
}

void SetListener(float x, float y, float z, float fwdX, float fwdY, float fwdZ)
{
    if (!s_listenerSet.exchange(true))
        MrpgLog::Write("audio: listener position received for the first time (%.1f %.1f %.1f)",
                       x, y, z);

    s_lx.store(x, std::memory_order_relaxed);
    s_ly.store(y, std::memory_order_relaxed);
    s_lz.store(z, std::memory_order_relaxed);
    s_fx.store(fwdX, std::memory_order_relaxed);
    s_fy.store(fwdY, std::memory_order_relaxed);
    s_fz.store(fwdZ, std::memory_order_relaxed);
}

void PlayTestTone(float pan)
{
    if (!s_deviceUp.load(std::memory_order_relaxed)) return;

    MrpgWireSfx r;
    memset(&r, 0, sizeof(r));
    r.nameId = 0;
    r.vol    = 1.0f;
    r.pitch  = 1.0f;
    for (int b = 0; b < 4; ++b) r.energy[b] = 255;    // flat, no colouring

    // Two metres away, on the side asked for. Close enough that the distance
    // model does not make it quiet, far enough that it is not treated as being
    // inside the listener's head.
    const float lx = s_lx.load(std::memory_order_relaxed);
    const float ly = s_ly.load(std::memory_order_relaxed);
    const float lz = s_lz.load(std::memory_order_relaxed);
    float fx = s_fx.load(std::memory_order_relaxed);
    float fy = s_fy.load(std::memory_order_relaxed);
    float fl = sqrtf(fx*fx + fy*fy);
    if (fl > 0.001f) { fx /= fl; fy /= fl; } else { fx = 0; fy = 1; }

    r.x = lx + (fy * pan * 2.0f) + fx * 0.5f;
    r.y = ly + (-fx * pan * 2.0f) + fy * 0.5f;
    r.z = lz;

    Submit(r);

    // Submit only knows nameId, so the reserved id is written straight into the
    // command it just queued. Safe because this runs on the same thread that
    // produces into the ring, and the audio thread has not been told about the
    // entry yet - the head store in Submit is what publishes it.
    const unsigned head = s_ringHead.load(std::memory_order_relaxed);
    s_ring[(head - 1) % CMD_RING].id = TEST_TONE_ID;
}

void Submit(const MrpgWireSfx& rec)
{
    if (!s_deviceUp.load(std::memory_order_relaxed)) return;

    const unsigned head = s_ringHead.load(std::memory_order_relaxed);
    const unsigned tail = s_ringTail.load(std::memory_order_acquire);
    if (head - tail >= (unsigned)CMD_RING) {
        s_dropped.fetch_add(1, std::memory_order_relaxed);
        return;                            // ring full: drop, never block
    }

    Cmd& c = s_ring[head % CMD_RING];
    c.id         = rec.nameId;
    c.flags      = rec.flags;
    c.loopHandle = rec.loopHandle;
    c.x          = rec.x;
    c.y          = rec.y;
    c.z          = rec.z;
    c.vol        = rec.vol;
    c.pitch      = rec.pitch;
    for (int b = 0; b < 4; ++b) c.energy[b] = rec.energy[b] / 255.0f;
    c.occlusion  = rec.occlusion / 255.0f;

    // ── The room, from the trace ─────────────────────────────────────────────
    //
    // reflEnergy is the fraction of energy that SURVIVED the reflections in each
    // band, so absorption per bounce is one minus it. That plus the mean free
    // path is everything Eyring needs - see Reverb.hpp and §11.3. No table, no
    // tuning, and it responds to the geometry the tracer actually found.
    //
    // Two bands, not four: the FDN damps between a low and a high decay time, so
    // the middle two would only be averaged away. Band 0 is 125 Hz and band 3 is
    // 6 kHz, which is the widest true span available.
    {
        const float mfp = rec.meanFreePathCm / 100.0f;

        // ── ABSORPTION DOES NOT COME FROM reflEnergy, AND THAT WAS THE BUG ────
        //
        // reflEnergy is "hemisphere reflections" (BHSupport.hpp): a per-ray MEAN
        // OF THE REFLECTED ENERGY REACHING THE LISTENER, clamped to 0..1. It is a
        // level, not a per-bounce survival fraction. Reading it as one meant that
        // the MORE reflective a spot was, the closer alpha went to zero and the
        // longer the tail grew - the exact opposite of the physics, and unbounded
        // at the top.
        //
        // Nothing currently traced measures per-bounce absorption. So this is a
        // BOUNDED HEURISTIC on enclosure, and it is labelled as one rather than
        // dressed up as Eyring: enclosure 1 is a space barely closed in, which
        // behaves as though it absorbs most of what hits it; enclosure 6 is fully
        // enclosed hard surfaces. Everything between interpolates.
        //
        // The maths downstream is still Eyring - only the input is a stand-in,
        // and it is a stand-in that cannot produce an 88-second room.
        const float encl01 = (rec.enclosure > 6 ? 6 : rec.enclosure) / 6.0f;

        const float aLo = 0.55f - 0.40f * encl01;      // 0.55 open .. 0.15 sealed
        const float aHi = aLo + 0.18f;                 // treble is always absorbed
                                                       // faster - carpet, cloth,
                                                       // air, and people

        const float rtLo = MrpgReverb::Rt60FromMfp(mfp, aLo);
        float       rtHi = MrpgReverb::Rt60FromMfp(mfp, aHi);
        if (rtHi > rtLo) rtHi = rtLo;      // treble never outlasts bass

        // An enclosure of 0 is open sky. The tracer says so directly, and
        // trusting it is cheaper and more correct than inferring it from a
        // decay time that a lucky bounce could make non-zero outdoors.
        c.bus = (rec.enclosure > 0) ? BusFor(mfp, rtLo, rtHi) : -1;

        if (c.bus >= 0) {
            // reflGain is the tracer's own answer to "how much reflected energy
            // reaches the listener", so it IS the send. Occlusion raises it: a
            // sound you cannot see directly arrives mostly as reflections, which
            // is why a talker round a corner sounds like the corridor and not
            // like themselves.
            const float occl = c.occlusion;
            c.send = (rec.reflGain / 255.0f) * (1.0f + 0.6f * occl);
            if (c.send > 1.5f) c.send = 1.5f;
        } else {
            c.send = 0.0f;
        }
    }

    // The straight-line distance from the listener, computed HERE on the net
    // thread rather than in the mixer, so the audio thread does no geometry.
    if (!s_listenerSet.load(std::memory_order_relaxed)) {
        // No listener yet. Play it at the reference distance rather than
        // measuring against the origin and culling everything - the server has
        // already decided this player can hear it, and a sound at slightly the
        // wrong level is enormously better than silence with no explanation.
        s_noListener.fetch_add(1, std::memory_order_relaxed);
        c.dist = REF_DISTANCE;
    } else {
        const float dx = rec.x - s_lx.load(std::memory_order_relaxed);
        const float dy = rec.y - s_ly.load(std::memory_order_relaxed);
        const float dz = rec.z - s_lz.load(std::memory_order_relaxed);
        c.dist = sqrtf(dx * dx + dy * dy + dz * dz);

        // COUNTED, not silent. The server has already applied the profile's own
        // maxDistance, so this only catches a source the tracer should not have
        // sent - and if it ever starts firing in bulk, that is the signal.
        if (c.dist > MAX_DIST && !(rec.flags & MRPGWIRE_SFX_LOOP_STOP)) {
            s_culledDist.fetch_add(1, std::memory_order_relaxed);
            return;
        }
    }

    s_ringHead.store(head + 1, std::memory_order_release);
}

const char* StatLine()
{
    static char out[320];
    int active = 0;
    for (int i = 0; i < MAX_VOICES; ++i) if (s_voices[i].active) ++active;

    // APPEND ONLY. ids and profiles are last because they were added last, and
    // because a reader that does not know about them keeps working.
    _snprintf(out, sizeof(out) - 1,
              "%d %d %d %d %d %llu %llu %llu %llu %d %d %d %d %llu %llu %d %.1f %.1f %.1f",
              s_running.load(std::memory_order_relaxed) ? 1 : 0,
              s_deviceUp.load(std::memory_order_relaxed) ? 1 : 0,
              active,
              s_loadedCount.load(std::memory_order_relaxed),
              s_pendingCount.load(std::memory_order_relaxed),
              s_played.load(std::memory_order_relaxed),
              s_missed.load(std::memory_order_relaxed),
              s_dropped.load(std::memory_order_relaxed),
              s_underruns.load(std::memory_order_relaxed),
              (int)(s_bankBytes.load(std::memory_order_relaxed) / 1048576),
              s_skippedBudget.load(std::memory_order_relaxed),
              MappedIds(),
              MappedProfiles(),
              s_culledDist.load(std::memory_order_relaxed),
              s_noListener.load(std::memory_order_relaxed),
              s_listenerSet.load(std::memory_order_relaxed) ? 1 : 0,
              s_lx.load(std::memory_order_relaxed),
              s_ly.load(std::memory_order_relaxed),
              s_lz.load(std::memory_order_relaxed));
    out[sizeof(out) - 1] = '\0';
    return out;
}

} // namespace MrpgAudio
