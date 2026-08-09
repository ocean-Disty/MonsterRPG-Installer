#define COBJMACROS
#define INITGUID
#include <windows.h>
#include <mmdeviceapi.h>
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
std::unordered_map<std::string, std::string>   s_profilePath;   // name -> path
std::unordered_map<unsigned int, std::string>  s_idName;        // id   -> name
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

    auto n = s_idName.find(id);
    if (n == s_idName.end()) return nullptr;

    auto p = s_profilePath.find(n->second);
    if (p == s_profilePath.end()) return nullptr;   // the server knows a sound we do not have

    BankEntry* e = new BankEntry();
    e->name = n->second;
    e->path = p->second;
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
};

Voice s_voices[MAX_VOICES];

// ── The acoustic model, ported from acoustics.js ─────────────────────────────
//
// PORT THE CONSTANTS, NOT THE INTENT. Every number below was tuned by ear
// against this soundtrack and this geometry, and several carry comments in
// acoustics.js saying so. Copying them verbatim is what makes a difference
// between the two clients attributable to the ENGINE rather than to somebody
// re-deriving a value they thought looked wrong.

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

    // Pan, from the source's direction relative to the listener's facing.
    // Constant-power, so a sound sweeping across the front does not dip in the
    // middle. Phase 4 replaces this whole block with an HRTF and a per-block
    // head transform; this is the honest stereo version that proves the rest.
    const float lx = s_lx.load(std::memory_order_relaxed);
    const float ly = s_ly.load(std::memory_order_relaxed);
    const float lz = s_lz.load(std::memory_order_relaxed);
    float fx = s_fx.load(std::memory_order_relaxed);
    float fy = s_fy.load(std::memory_order_relaxed);

    float dx = c.x - lx, dy = c.y - ly, dz = c.z - lz;
    (void)dz;

    float flen = sqrtf(fx * fx + fy * fy);
    if (flen > 0.001f) { fx /= flen; fy /= flen; } else { fx = 0; fy = 1; }

    // Right vector on the ground plane: (fy, -fx). Blockland is Z-up.
    const float rx = fy, ry = -fx;
    const float dlen = sqrtf(dx * dx + dy * dy);
    float side = 0;
    if (dlen > 0.001f) side = (dx * rx + dy * ry) / dlen;
    if (side >  1.0f) side =  1.0f;
    if (side < -1.0f) side = -1.0f;

    const float angle = (side + 1.0f) * 0.25f * 3.14159265f;   // 0..pi/2
    v.panLTarget = cosf(angle);
    v.panRTarget = sinf(angle);

    v.gainTarget = gain;

    const bool isLoop = (c.loopHandle != 0);
    if (!isLoop) {
        // One-shots take their level instantly. See LOOP_TAU.
        v.gain = gain;
        v.panL = v.panLTarget;
        v.panR = v.panRTarget;
    }

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

    for (int i = 0; i < MAX_VOICES; ++i) {
        Voice& v = s_voices[i];
        if (!v.active || !v.snd || !v.snd->data) continue;

        const MrpgWav::Sound& s = *v.snd;
        const bool isLoop = (v.loopHandle != 0);

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

            if (isLoop) {
                v.gain += (v.gainTarget - v.gain) * smooth;
                v.panL += (v.panLTarget - v.panL) * smooth;
                v.panR += (v.panRTarget - v.panR) * smooth;
            }

            const float g = sample * v.gain;
            out[f * stride]     += g * v.panL;
            out[f * stride + 1] += g * v.panR;

            v.pos += v.step;
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
            for (const auto& kv : s_idName) {
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

    hr = s_enum->GetDefaultAudioEndpoint(eRender, eConsole, &s_device);
    if (FAILED(hr) || !s_device) {
        MrpgLog::Write("audio: no default output device (hr 0x%08lX)", (unsigned long)hr);
        s_running.store(false);
        return false;
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

    // 20 ms. Small enough that a sound is not perceptibly late, large enough
    // that a scheduling hiccup on a loaded machine does not tear a hole in it.
    const REFERENCE_TIME dur = 200000;

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
    s_loadThread  = std::thread(LoadThreadMain);

    MrpgLog::Write("audio: device up - %d Hz, %d ch, float32, shared mode, MMCSS",
                   s_rate, s_channels);
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
        s_idName.clear();
        s_profilePath.clear();
    }
    s_loadedCount.store(0);
    s_deviceUp.store(false);

    MrpgLog::Write("audio: device closed");
}

bool IsRunning() { return s_running.load(std::memory_order_relaxed); }

// ── Mappings ─────────────────────────────────────────────────────────────────

void MapProfile(const char* name, const char* path)
{
    if (!name || !*name || !path || !*path) return;
    std::lock_guard<std::mutex> lock(s_bankMutex);
    s_profilePath[name] = path;
}

void MapId(unsigned int id, const char* name)
{
    if (!name || !*name) return;
    std::lock_guard<std::mutex> lock(s_bankMutex);
    s_idName[id] = name;
}

void ClearMappings()
{
    std::lock_guard<std::mutex> lock(s_bankMutex);
    s_idName.clear();
    // Deliberately NOT clearing s_bank or the decoded samples: the mixer may be
    // reading them right now. Stale entries are unreachable once the ids that
    // pointed at them are gone, and everything is freed at Shutdown.
}

int MappedProfiles() { std::lock_guard<std::mutex> l(s_bankMutex); return (int)s_profilePath.size(); }
int MappedIds()      { std::lock_guard<std::mutex> l(s_bankMutex); return (int)s_idName.size(); }

void BeginPreload() { s_preloadWanted.store(true, std::memory_order_relaxed); }

bool CanPlay()
{
    return s_deviceUp.load(std::memory_order_relaxed)
        && s_loadedCount.load(std::memory_order_relaxed) > 0;
}

void SetListener(float x, float y, float z, float fwdX, float fwdY, float fwdZ)
{
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

    // The straight-line distance from the listener, computed HERE on the net
    // thread rather than in the mixer, so the audio thread does no geometry.
    const float dx = rec.x - s_lx.load(std::memory_order_relaxed);
    const float dy = rec.y - s_ly.load(std::memory_order_relaxed);
    const float dz = rec.z - s_lz.load(std::memory_order_relaxed);
    c.dist = sqrtf(dx * dx + dy * dy + dz * dz);
    if (c.dist > MAX_DIST && !(rec.flags & MRPGWIRE_SFX_LOOP_STOP)) return;

    s_ringHead.store(head + 1, std::memory_order_release);
}

const char* StatLine()
{
    static char out[224];
    int active = 0;
    for (int i = 0; i < MAX_VOICES; ++i) if (s_voices[i].active) ++active;

    // APPEND ONLY. ids and profiles are last because they were added last, and
    // because a reader that does not know about them keeps working.
    _snprintf(out, sizeof(out) - 1, "%d %d %d %d %d %llu %llu %llu %llu %d %d %d %d",
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
              MappedProfiles());
    out[sizeof(out) - 1] = '\0';
    return out;
}

} // namespace MrpgAudio
