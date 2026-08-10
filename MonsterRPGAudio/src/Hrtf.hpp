#pragma once

// =============================================================================
// Hrtf.hpp — binaural rendering: the structural head model
// =============================================================================
//
// Turns one mono voice into a left and a right ear signal that carry the three
// cues a person actually localises with:
//
//   ITD    the sound reaches the near ear first. Below about 1.5 kHz this is
//          the dominant azimuth cue and nothing else substitutes for it.
//   ILD    the head shadows the far ear, and it shadows high frequencies far
//          more than low ones. Amplitude panning gets the level difference but
//          makes it frequency-flat, which is why panned audio sits inside the
//          head rather than out in the world.
//   pinna  a moving spectral notch, which is the only cue for elevation and the
//          main one for front-versus-back.
//
// ── WHY THIS IS NOT A CONVOLUTION, YET ───────────────────────────────────────
//
// AUDIORT_NATIVE_PLAN.md §11.1 makes measured HRIRs from SADIE II (Apache 2.0,
// 8802 directions) the destination, loaded from hrtf.bin. This is the model that
// runs until that file exists, and §11.7 says to build it first — not because it
// is a lesser version of the same thing, but because it is the part that has to
// be debugged. It is CONTINUOUS in azimuth and elevation, so it has no direction
// grid, no four-nearest interpolation, and none of the comb-filtering-as-you-
// turn that a badly interpolated HRIR table produces. Everything downstream —
// the head basis, the delay line, the per-ear chain, the block boundaries — is
// identical for both, so hrtf.bin drops into a path that already works.
//
// The structure is Brown and Duda's: a delay, a one-pole head shadow whose
// coefficient tracks the angle to that ear, and a pinna notch. The head-shadow
// filter below is the bilinear transform of
//
//     H(s) = (1 + alpha(theta) * s / (2*w0)) / (1 + s / (2*w0)),   w0 = c / a
//
// worked through rather than copied, so the coefficients are re-derivable:
//
//     b0 = (w0 + alpha*fs) / (w0 + fs)
//     b1 = (w0 - alpha*fs) / (w0 + fs)
//     a1 = (w0 - fs)       / (w0 + fs)
//
// ── WHAT IT DELIBERATELY DOES NOT DO ─────────────────────────────────────────
//
// No near-field correction: below about a metre the ILD grows much faster than
// this model predicts. No head-shadow diffraction detail behind the head. Both
// want measured data, which is what hrtf.bin is for.
//
// ── ONE THING THAT MUST NOT DRIFT ────────────────────────────────────────────
//
// The head basis in ToHeadRelative is the SAME arithmetic as
// Add-Ons/MONSTERRPG/AudioRT.cpp — this is its third caller, after the SFX path
// and the voice path, and that is on purpose (§11.5). A voice that localises
// differently from a footstep at the same spot is worse than one that does not
// localise at all, because it teaches the player to distrust the cue.
// =============================================================================

#include <math.h>
#include <stdio.h>
#include <string.h>
#include <vector>

namespace MrpgHrtf {

// Speed of sound and head radius. 0.0875 m is the standard fit for the
// Woodworth model and gives a maximum ITD of about 0.66 ms, which is what
// human heads actually measure.
const float SOUND_C     = 343.0f;
const float HEAD_RADIUS = 0.0875f;

// Enough for the maximum ITD at any sample rate this will ever see: 0.66 ms at
// 192 kHz is 127 samples. The device on this machine runs at 96 kHz.
const int   ITD_MAX     = 192;

// ── Head basis ───────────────────────────────────────────────────────────────
//
// PORTED VERBATIM from AudioRT::ToHeadRelative. Right-handed basis from the
// listener's eye vector and world up, Blockland being Z-up; the forward
// component is NEGATED because that function was written against Web Audio,
// which looks down -Z. Keeping the negation - rather than "fixing" it here -
// is what guarantees this agrees with the server's own answer, and the sign is
// undone at the one place that needs a front-positive number.
struct Head {
    float x;   // + right
    float y;   // + up
    float z;   // + BEHIND  (negated forward, per the note above)
};

inline Head ToHeadRelative(float lx, float ly, float lz,
                           float fx, float fy, float fz,
                           float sx, float sy, float sz)
{
    float flen = sqrtf(fx * fx + fy * fy + fz * fz);
    if (flen > 0.001f) { fx /= flen; fy /= flen; fz /= flen; }
    else               { fx = 0; fy = 1; fz = 0; }

    // right = forward x worldUp, worldUp = (0,0,1)
    float rx = fy * 1.0f - fz * 0.0f;
    float ry = fz * 0.0f - fx * 1.0f;
    float rz = fx * 0.0f - fy * 0.0f;
    float rlen = sqrtf(rx * rx + ry * ry + rz * rz);
    if (rlen > 0.001f) { rx /= rlen; ry /= rlen; rz /= rlen; }
    else               { rx = 1; ry = 0; rz = 0; }

    // up = right x forward
    const float ux = ry * fz - rz * fy;
    const float uy = rz * fx - rx * fz;
    const float uz = rx * fy - ry * fx;

    const float dx = sx - lx, dy = sy - ly, dz = sz - lz;

    Head h;
    h.x =  dx * rx + dy * ry + dz * rz;
    h.y =  dx * ux + dy * uy + dz * uz;
    h.z = -(dx * fx + dy * fy + dz * fz);
    return h;
}

// ── The measured table (hrtf.bin) ────────────────────────────────────────────
//
// Built by Add-Ons/MONSTERRPG/tools/make_hrtf.py from SADIE II D1 (Neumann
// KU100, Apache 2.0, DOI 10.3390/app8112029) and verified by check_hrtf.py.
// 1152 directions on a 5 x 10 degree grid, 64 minimum-phase taps, ITD stored
// separately. 293 KB on disk.
//
// RESAMPLED ONCE AT LOAD, not per voice. The file is 48 kHz and the device is
// whatever WASAPI hands us (96 kHz on the machine this was written on). Doing it
// here costs one pass over 1152 impulses at startup; doing it per voice per
// block would cost it forty-eight times a second forever.
struct Table {
    bool  loaded = false;
    int   taps   = 0;
    int   azSteps = 0, elSteps = 0;
    float elMin = 0, elStep = 0, azStep = 0;

    std::vector<float> itd;    // dirCount
    std::vector<float> ir;     // dirCount * 2 * taps, ear-interleaved per dir

    const float* At(int dir, int ear) const
    {
        return &ir[((size_t)dir * 2 + ear) * (size_t)taps];
    }
};

inline Table& Global()
{
    static Table t;
    return t;
}

// Returns false and leaves the table unloaded on ANY problem. The caller falls
// back to the structural model, which is the whole reason that model still
// exists - a missing or corrupt hrtf.bin must cost realism, never silence.
inline bool LoadTable(const char* path, float deviceRate, char* why, int whyLen)
{
    Table& t = Global();
    t.loaded = false;

    FILE* f = fopen(path, "rb");
    if (!f) { _snprintf(why, whyLen, "not found"); return false; }

    char magic[8] = {0};
    unsigned int ver = 0, fileRate = 0, taps = 0, dirs = 0, azS = 0, elS = 0;
    float elMin = 0, elStep = 0, azStep = 0, scale = 1.0f;

    bool ok = fread(magic, 1, 8, f) == 8 && memcmp(magic, "MRPGHRTF", 8) == 0;
    ok = ok && fread(&ver, 4, 1, f) == 1 && ver == 1;
    ok = ok && fread(&fileRate, 4, 1, f) == 1;
    ok = ok && fread(&taps, 4, 1, f) == 1;
    ok = ok && fread(&dirs, 4, 1, f) == 1;
    ok = ok && fread(&azS, 4, 1, f) == 1;
    ok = ok && fread(&elS, 4, 1, f) == 1;
    ok = ok && fread(&elMin, 4, 1, f) == 1;
    ok = ok && fread(&elStep, 4, 1, f) == 1;
    ok = ok && fread(&azStep, 4, 1, f) == 1;
    ok = ok && fread(&scale, 4, 1, f) == 1;

    if (!ok) { fclose(f); _snprintf(why, whyLen, "bad header"); return false; }
    if (dirs != azS * elS || taps == 0 || taps > 512 || dirs == 0 || dirs > 65536) {
        fclose(f);
        _snprintf(why, whyLen, "implausible dimensions");
        return false;
    }

    std::vector<float> itd((size_t)dirs);
    std::vector<short> q((size_t)dirs * 2 * taps);
    ok = fread(itd.data(), 4, dirs, f) == dirs;
    ok = ok && fread(q.data(), 2, q.size(), f) == q.size();
    fclose(f);
    if (!ok) { _snprintf(why, whyLen, "truncated"); return false; }

    // Resample to the device rate. Linear on a 64-tap minimum-phase impulse is
    // fine: it is smooth and already band-limited by the measurement.
    const double ratio = (double)deviceRate / (double)fileRate;
    int outTaps = (int)(taps * ratio + 0.5);
    if (outTaps < 8) outTaps = 8;
    if (outTaps > 512) outTaps = 512;

    t.ir.assign((size_t)dirs * 2 * outTaps, 0.0f);
    const float q15 = scale / 32767.0f;

    for (unsigned int d = 0; d < dirs; ++d) {
        for (int e = 0; e < 2; ++e) {
            const short* src = &q[((size_t)d * 2 + e) * taps];
            float* dst = &t.ir[((size_t)d * 2 + e) * outTaps];
            for (int i = 0; i < outTaps; ++i) {
                const double sp = i / ratio;
                const int    i0 = (int)sp;
                if (i0 >= (int)taps - 1) { dst[i] = 0; continue; }
                const float fr = (float)(sp - i0);
                dst[i] = (src[i0] * (1.0f - fr) + src[i0 + 1] * fr) * q15;
            }
        }
    }

    t.itd = itd;
    t.taps = outTaps;
    t.azSteps = (int)azS;
    t.elSteps = (int)elS;
    t.elMin = elMin;
    t.elStep = elStep;
    t.azStep = azStep;
    t.loaded = true;

    _snprintf(why, whyLen, "%u directions, %d taps at %.0f Hz (file %u Hz)",
              dirs, outTaps, deviceRate, fileRate);
    return true;
}

// ── One ear ──────────────────────────────────────────────────────────────────

struct Ear {
    // Head shadow: one zero, one pole.
    float b0 = 1, b1 = 0, a1 = 0;
    float x1 = 0, y1 = 0;

    // Pinna notch: a peaking EQ with negative gain. A true notch is too narrow
    // to be heard as anything but a hole; the cue is a broad dip that MOVES.
    float n_b0 = 1, n_b1 = 0, n_b2 = 0, n_a1 = 0, n_a2 = 0;
    float n_x1 = 0, n_x2 = 0, n_y1 = 0, n_y2 = 0;

    // ── The measured path ────────────────────────────────────────────────────
    //
    // When hrtf.bin is loaded these replace the shadow and pinna filters: one
    // FIR carrying the whole magnitude response of a real head at this
    // direction, blended from the four grid points around it.
    //
    // CROSS-FADED BETWEEN BLOCKS, not switched. Swapping an FIR outright clicks
    // at every block boundary the player turns through - a hundred a second -
    // which is the artefact the ITD/magnitude split was chosen to avoid,
    // reintroduced at the very last step.
    std::vector<float> fir, firPrev;
    std::vector<float> hist;
    int   histPos = 0;
    float blend = 1.0f;          // 0 = all previous, 1 = all current

    // Fractional delay line for the ITD.
    //
    // THE `= {}` IS LOAD-BEARING. Without it this array is default-initialized,
    // i.e. indeterminate, and a delay line seeded with garbage does not fail
    // quietly - it feeds junk straight into the head-shadow filter and the
    // output runs away. Audio.cpp happens to be safe because it assigns
    // `v = Voice()`, which value-initializes, but relying on every future caller
    // to know that is exactly the kind of assumption that breaks six months
    // later. Caught by TestHrtf.cpp, which produced a peak of 6.4e33.
    float buf[ITD_MAX] = {};
    int   w = 0;
    float delay = 0, delayTarget = 0;

    void Reset()
    {
        memset(buf, 0, sizeof(buf));
        w = 0; x1 = y1 = 0;
        n_x1 = n_x2 = n_y1 = n_y2 = 0;
        delay = delayTarget;
        std::fill(hist.begin(), hist.end(), 0.0f);
        histPos = 0;
        blend = 1.0f;
    }

    // Straight time-domain convolution. At 64 to 128 taps a partitioned FFT
    // would cost more in bookkeeping than it saves, and this stays trivially
    // correct - which matters more in the part that runs on the audio thread.
    inline float Convolve(float in)
    {
        const int n = (int)fir.size();
        if (n <= 0) return in;

        hist[histPos] = in;

        float acc = 0;
        int   h = histPos;

        if (blend >= 0.999f) {
            for (int i = 0; i < n; ++i) {
                acc += fir[i] * hist[h];
                if (--h < 0) h = n - 1;
            }
        } else {
            const float a = blend, b = 1.0f - blend;
            for (int i = 0; i < n; ++i) {
                acc += (fir[i] * a + firPrev[i] * b) * hist[h];
                if (--h < 0) h = n - 1;
            }
        }

        if (++histPos >= n) histPos = 0;
        return acc;
    }

    // Delay first, then the measured FIR - the same order and the same delay
    // line as the structural path. Only what sits after the delay changes.
    inline float ProcessMeasured(float in, float delaySmooth)
    {
        delay += (delayTarget - delay) * delaySmooth;

        buf[w] = in;
        float rp = (float)w - delay;
        while (rp < 0) rp += (float)ITD_MAX;
        const int   i0 = (int)rp;
        const float fr = rp - (float)i0;
        const int   i1 = (i0 + 1) % ITD_MAX;
        const float d = buf[i0] * (1.0f - fr) + buf[i1] * fr;
        w = (w + 1) % ITD_MAX;

        return Convolve(d);
    }

    inline float Process(float in, float delaySmooth)
    {
        // ── delay ────────────────────────────────────────────────────────────
        // SMOOTHED TOWARDS THE TARGET rather than snapped. The ITD changes every
        // block as the player turns, and stepping a delay line by whole samples
        // is a click; sliding it is the same thing a Doppler shift does, which
        // is both inaudible here and physically what happens when a source
        // moves relative to your head.
        delay += (delayTarget - delay) * delaySmooth;

        buf[w] = in;

        float rp = (float)w - delay;
        while (rp < 0) rp += (float)ITD_MAX;

        const int   i0 = (int)rp;
        const float fr = rp - (float)i0;
        const int   i1 = (i0 + 1) % ITD_MAX;
        float out = buf[i0] * (1.0f - fr) + buf[i1] * fr;

        w = (w + 1) % ITD_MAX;

        // ── head shadow ──────────────────────────────────────────────────────
        const float y = b0 * out + b1 * x1 - a1 * y1;
        x1 = out;
        y1 = y;
        out = y;

        // ── pinna ────────────────────────────────────────────────────────────
        const float ny = n_b0 * out + n_b1 * n_x1 + n_b2 * n_x2
                       - n_a1 * n_y1 - n_a2 * n_y2;
        n_x2 = n_x1; n_x1 = out;
        n_y2 = n_y1; n_y1 = ny;

        return ny;
    }
};

// ── A binaural voice ─────────────────────────────────────────────────────────

struct Binaural {
    Ear  L, R;
    bool seeded = false;

    void Reset() { L.Reset(); R.Reset(); seeded = false; }

    // az: radians, 0 = straight ahead, positive to the RIGHT.
    // el: radians, 0 = ear level, positive UP.
    // True when the measured table is driving this voice.
    bool measured = false;

    void Set(float az, float el, float sampleRate)
    {
        const float fs = sampleRate;

        // Named tbl, not t: the pinna section below already owns t.
        const Table& tbl = Global();
        if (tbl.loaded) { SetMeasured(tbl, az, el, fs); return; }
        measured = false;

        // Direction cosine along the interaural axis. +1 is hard right.
        const float sinAz = sinf(az);
        const float cosEl = cosf(el);
        float lat = sinAz * cosEl;                 // lateral component
        if (lat >  1.0f) lat =  1.0f;
        if (lat < -1.0f) lat = -1.0f;

        // ── ITD, Woodworth ───────────────────────────────────────────────────
        //
        // theta is the angle from straight ahead, projected onto the horizontal
        // plane: a sound directly overhead has no ITD however far round it is,
        // which is exactly why the cone of confusion exists.
        //
        // Known limit, and it is documented rather than hidden: this is a RAY
        // model, the high-frequency limit of the exact diffraction solution, and
        // it under-predicts ITD between roughly 500 Hz and 1.5 kHz where
        // diffraction lengthens the path around the head. Measured per-direction
        // ITD is one of the things hrtf.bin brings.
        const float th = asinf(lat);               // -pi/2 .. +pi/2
        const float ath = fabsf(th);
        float itd = (HEAD_RADIUS / SOUND_C) * (sinf(ath) + ath);   // seconds, >= 0

        const float itdSamples = itd * fs;

        // The FAR ear is delayed. Both lines carry a baseline delay so neither
        // ever needs a negative one - a delay line cannot look into the future,
        // and the absolute offset is inaudible while the difference is the cue.
        const float half = itdSamples * 0.5f;
        const float base = (float)(ITD_MAX / 4);
        L.delayTarget = base + (lat > 0 ?  half : -half);
        R.delayTarget = base + (lat > 0 ? -half :  half);

        // ── Head shadow, Brown and Duda ──────────────────────────────────────
        //
        // alpha runs from 2.0 when the source is at that ear (a slight boost -
        // the head reflects sound into the near ear) down to 0.1 at 150 degrees
        // away, which is the shadow. Frequency-dependent by construction: the
        // pole sits at w0 = c/a, about 624 Hz, so the level difference grows
        // with frequency exactly as a real head's does.
        const float w0 = SOUND_C / HEAD_RADIUS;

        // Angle from each ear's axis. Left ear points to -x, right to +x.
        const float thL = acosf(-lat);
        const float thR = acosf( lat);

        SetShadow(L, thL, w0, fs);
        SetShadow(R, thR, w0, fs);

        // ── Pinna ────────────────────────────────────────────────────────────
        //
        // One notch, sliding with elevation: about 10 kHz for a source below,
        // 6 kHz for one above. Crude next to a real pinna, which has several
        // notches and peaks - but it is the difference between elevation doing
        // nothing at all and elevation doing something.
        float t = (el + 1.5707963f) / 3.14159265f;      // 0 below .. 1 above
        if (t < 0) t = 0;
        if (t > 1) t = 1;
        const float notchHz = 10000.0f - 4000.0f * t;

        // Deeper at the front, where the pinna actually faces the source.
        const float front = cosf(az) * cosEl;            // +1 ahead, -1 behind
        const float depth = -6.0f - 4.0f * (front > 0 ? front : 0);   // dB

        SetNotch(L, notchHz, depth, fs);
        SetNotch(R, notchHz, depth, fs);

        if (!seeded) { L.delay = L.delayTarget; R.delay = R.delayTarget; seeded = true; }
    }

    inline void Process(float in, float delaySmooth, float& outL, float& outR)
    {
        if (measured) {
            outL = L.ProcessMeasured(in, delaySmooth);
            outR = R.ProcessMeasured(in, delaySmooth);

            // Retire the previous FIR over about 5 ms: long enough that no step
            // is audible, short enough that the image is never noticeably stale.
            const float step = 1.0f / (0.005f * 48000.0f);
            if (L.blend < 1.0f) { L.blend += step; if (L.blend > 1) L.blend = 1; }
            if (R.blend < 1.0f) { R.blend += step; if (R.blend > 1) R.blend = 1; }
            return;
        }
        outL = L.Process(in, delaySmooth);
        outR = R.Process(in, delaySmooth);
    }

    // ── Bilinear blend of the four surrounding measurements ──────────────────
    //
    // The magnitude parts are averaged - they are delay-free, so this is safe,
    // which is the entire reason make_hrtf.py strips the ITD out - and the ITD
    // is averaged separately as a scalar. Doing it the other way round, on raw
    // HRIRs, produces a comb filter that sweeps as the player turns.
    void SetMeasured(const Table& t, float az, float el, float fs)
    {
        measured = true;

        float azDeg = az * 57.29578f;
        while (azDeg < 0)       azDeg += 360.0f;
        while (azDeg >= 360.0f) azDeg -= 360.0f;

        float elDeg = el * 57.29578f;
        const float elMax = t.elMin + t.elStep * (float)(t.elSteps - 1);
        if (elDeg < t.elMin) elDeg = t.elMin;
        if (elDeg > elMax)   elDeg = elMax;

        const float af = azDeg / t.azStep;
        const float ef = (elDeg - t.elMin) / t.elStep;

        int   a0 = (int)af;      const float aFrac = af - (float)a0;
        int   e0 = (int)ef;      float       eFrac = ef - (float)e0;

        if (e0 >= t.elSteps - 1) { e0 = t.elSteps - 2; eFrac = 1.0f; }
        if (e0 < 0)              { e0 = 0;             eFrac = 0.0f; }

        a0 %= t.azSteps;
        const int a1 = (a0 + 1) % t.azSteps;   // azimuth wraps, elevation does not
        const int e1 = e0 + 1;

        const int d00 = e0 * t.azSteps + a0, d10 = e0 * t.azSteps + a1;
        const int d01 = e1 * t.azSteps + a0, d11 = e1 * t.azSteps + a1;

        const float w00 = (1 - aFrac) * (1 - eFrac), w10 = aFrac * (1 - eFrac);
        const float w01 = (1 - aFrac) * eFrac,       w11 = aFrac * eFrac;

        const float itd = t.itd[d00] * w00 + t.itd[d10] * w10
                        + t.itd[d01] * w01 + t.itd[d11] * w11;

        // Positive ITD means the RIGHT ear is later - the contract check_hrtf.py
        // verifies the converter against, and the one that decides whether the
        // world comes out mirrored.
        const float itdSamples = itd * fs;
        const float base = (float)(ITD_MAX / 4);
        L.delayTarget = base - itdSamples * 0.5f;
        R.delayTarget = base + itdSamples * 0.5f;

        BlendFir(L, t, 0, d00, d10, d01, d11, w00, w10, w01, w11);
        BlendFir(R, t, 1, d00, d10, d01, d11, w00, w10, w01, w11);

        if (!seeded) {
            L.delay = L.delayTarget; R.delay = R.delayTarget;
            L.blend = R.blend = 1.0f;
            seeded = true;
        }
    }

    static void BlendFir(Ear& e, const Table& t, int ear,
                         int d00, int d10, int d01, int d11,
                         float w00, float w10, float w01, float w11)
    {
        if ((int)e.fir.size() != t.taps) {
            e.fir.assign((size_t)t.taps, 0.0f);
            e.firPrev.assign((size_t)t.taps, 0.0f);
            e.hist.assign((size_t)t.taps, 0.0f);
            e.histPos = 0;
            e.blend = 1.0f;          // nothing to fade from on the first block
        } else {
            e.firPrev = e.fir;
            e.blend = 0.0f;
        }

        const float* h00 = t.At(d00, ear);
        const float* h10 = t.At(d10, ear);
        const float* h01 = t.At(d01, ear);
        const float* h11 = t.At(d11, ear);

        for (int i = 0; i < t.taps; ++i)
            e.fir[i] = h00[i] * w00 + h10[i] * w10 + h01[i] * w01 + h11[i] * w11;
    }

private:
    static void SetShadow(Ear& e, float theta, float w0, float fs)
    {
        // alpha(theta) = (1 + amin/2) + (1 - amin/2) * cos(theta * 180 / thmin)
        const float amin  = 0.1f;
        const float thmin = 150.0f * 3.14159265f / 180.0f;
        const float a = (1.0f + amin * 0.5f)
                      + (1.0f - amin * 0.5f) * cosf(theta * 3.14159265f / thmin);

        const float den = w0 + fs;
        e.b0 = (w0 + a * fs) / den;
        e.b1 = (w0 - a * fs) / den;
        e.a1 = (w0 - fs)     / den;
    }

    static void SetNotch(Ear& e, float hz, float gainDb, float fs)
    {
        // RBJ peaking EQ. Guarded against a centre frequency at or above
        // Nyquist: at 44.1 kHz a 10 kHz notch is fine, but the clamp costs
        // nothing and an unclamped bilinear warp folds the notch to a garbage
        // frequency rather than failing visibly.
        float f = hz;
        const float nyq = fs * 0.45f;
        if (f > nyq) f = nyq;
        if (f < 200.0f) f = 200.0f;

        const float A  = powf(10.0f, gainDb / 40.0f);
        const float w  = 6.2831853f * f / fs;
        const float cw = cosf(w), sw = sinf(w);
        const float Q  = 1.6f;
        const float al = sw / (2.0f * Q);

        const float b0 =  1 + al * A;
        const float b1 = -2 * cw;
        const float b2 =  1 - al * A;
        const float a0 =  1 + al / A;
        const float a1 = -2 * cw;
        const float a2 =  1 - al / A;

        e.n_b0 = b0 / a0;  e.n_b1 = b1 / a0;  e.n_b2 = b2 / a0;
        e.n_a1 = a1 / a0;  e.n_a2 = a2 / a0;
    }
};

} // namespace MrpgHrtf
