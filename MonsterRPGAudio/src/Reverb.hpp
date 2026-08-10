#pragma once

// =============================================================================
// Reverb.hpp — an 8-line feedback delay network, parameterised from the trace
// =============================================================================
//
// The tracer already tells us the two things a room reverb needs: the mean free
// path, and how much energy survives a bounce in each of four bands. This turns
// those into a decay, rather than into a lookup table somebody tuned by ear.
//
// ── WHY AN FDN AND NOT A CONVOLUTION ─────────────────────────────────────────
//
// The browser built an impulse response parametrically and then fed it to a
// ConvolverNode, because Web Audio hands you a convolver and nothing else.
// Natively that is pure cost: a partitioned FFT convolution against a 3-second
// stereo IR, per room, to reproduce a response we synthesised from three
// numbers in the first place. An FDN is the same room description in its native
// vocabulary — see AUDIORT_NATIVE_PLAN.md §5.4.
//
// ── RT60 IS DERIVED, NOT TUNED (§11.3) ───────────────────────────────────────
//
// Sound crosses one mean free path per bounce, and each bounce keeps (1-alpha)
// of the energy. Sixty dB is a factor of 10^-6, so
//
//     n bounces to decay 60 dB = 6 ln(10) / -ln(1 - alpha) = 13.8155 / -ln(1-a)
//     RT60                     = (mfp / c) * 13.8155 / -ln(1 - alpha)
//
// which is Eyring's equation written in terms of the mean free path. It agrees
// with Sabine as a sanity check — 13.8155/343 = 0.0403 against Sabine's
// 0.161 * V/S = 0.161 * mfp/4 = 0.0403 * mfp — and it is EYRING rather than
// Sabine because Sabine diverges as alpha approaches 1: it predicts a
// reverberant tail in an anechoic chamber, and this game is mostly open field,
// where alpha is effectively 1.
//
// ── THE NORMALISATION CAVEAT, WHICH IS THE EASY THING TO SKIP ────────────────
//
// ConvolverNode.normalize = true was load-bearing in the browser: it made the
// send gain mean "how much room" instead of "how much room times how long the
// tail is", and every wet-mix constant in acoustics.js was tuned against that.
// An FDN has no such normalisation — its output level scales with feedback
// gain, i.e. with RT60 — so a 3-second room would arrive roughly an order of
// magnitude louder than a 0.35-second one and every send value would have to be
// re-derived. Build() therefore MEASURES its own output and scales it, which is
// what keeps the existing send gains meaningful.
// =============================================================================

#include <math.h>
#include <string.h>
#include <vector>
#include <algorithm>

namespace MrpgReverb {

const int   LINES     = 8;
const float SOUND_C   = 343.0f;

// Longest line we will ever need: a 40 m mean free path at 96 kHz is about
// 11 200 samples, and the longest line is a small multiple of that. Used only
// as a CLAMP - the buffers are sized to what each room actually needs.
const int   MAX_DELAY = 32768;
const int   MAX_PRE   = 8192;

// ── RT60 from what the tracer actually reports ───────────────────────────────
//
// `alpha` is absorption per bounce: 0 is a perfect mirror, 1 swallows
// everything. Both ends are clamped, and not arbitrarily - at alpha = 0 the
// formula divides by zero (a room that never decays), and at alpha = 1 it
// returns zero (no reverb at all, which is the correct answer outdoors and is
// handled by the caller not building a bus).
// The longest decay this will ever return. A cathedral is about 8 seconds and a
// large stone hall about 3; nothing in this game is either.
//
// THIS CLAMP IS NOT TIDINESS, IT IS A FUSE, and it exists because the absence of
// one did real damage. An alpha floor of 0.005 permits RT60 = mfp * 8 seconds,
// so an 11 m room came out at EIGHTY-EIGHT SECONDS: a bus that never decays,
// accumulating every sound fed into it for as long as the player stood there.
// The symptom was a rising wall of noise loud enough that the game had to be
// closed. An unphysical decay time is always a bug upstream, and the right
// response is to refuse it here rather than to render it faithfully.
const float RT60_MAX = 2.5f;
const float RT60_MIN = 0.12f;

inline float Rt60FromMfp(float mfp, float alpha)
{
    if (mfp <= 0.01f) return 0.0f;          // no bounce recorded: no enclosure

    // The floor is 0.06, not 0.005. Real surfaces absorb a few percent at worst -
    // polished stone is about 0.02, and a room made entirely of it does not
    // exist. Below this the formula is describing a resonator, not a room.
    if (alpha < 0.06f)  alpha = 0.06f;
    if (alpha > 0.995f) alpha = 0.995f;

    float rt = (mfp / SOUND_C) * 13.8155f / -logf(1.0f - alpha);

    if (rt > RT60_MAX) rt = RT60_MAX;
    if (rt < RT60_MIN) rt = 0.0f;           // too short to be a room at all
    return rt;
}

// ── One room ─────────────────────────────────────────────────────────────────

struct Fdn {
    // Delay lines. Lengths are mutually prime so their echoes do not pile up
    // into a periodic flutter - the classic FDN failure, and it sounds like a
    // metallic ring rather than like a room.
    //
    // HEAP, SIZED TO THE ROOM, AND THAT IS NOT A MICRO-OPTIMISATION. Fixed
    // [LINES][MAX_DELAY] arrays make this struct about a megabyte, which
    // overflows the stack the moment anybody writes `Fdn f;` in a function -
    // TestReverb.cpp did exactly that and died with no output at all, which is
    // the least diagnosable failure there is. One allocation per room, in
    // Build(), which never runs on the audio thread.
    std::vector<float> buf[LINES];
    int   len[LINES]  = {};
    int   pos[LINES]  = {};
    float g[LINES]    = {};        // feedback gain, from RT60
    float damp[LINES] = {};        // one-pole coefficient, from the HF ratio
    float lp[LINES]   = {};        // damping filter state

    std::vector<float> pre;
    int   preLen = 0, prePos = 0;

    float outScale = 1.0f;
    bool  built    = false;

    float rt60 = 0, rt60Hf = 0, mfp = 0;

    // Build a room. rt60Low and rt60High are the decay times for the bottom and
    // top of the spectrum; passing them equal gives a flat, unnatural tail.
    // NOT AUDIO-THREAD SAFE: it allocates, and it runs the network for up to a
    // second to measure its own level. Build rooms on the net or game thread and
    // hand the finished object over.
    void Build(float meanFreePath, float rt60Low, float rt60High, float fs)
    {
        memset(lp, 0, sizeof(lp));
        prePos = 0;

        mfp    = meanFreePath;
        rt60   = rt60Low;
        rt60Hf = rt60High;

        if (rt60Low  < 0.05f) rt60Low  = 0.05f;
        if (rt60High < 0.02f) rt60High = 0.02f;
        if (rt60High > rt60Low) rt60High = rt60Low;   // treble never outlasts bass

        // ── Line lengths ─────────────────────────────────────────────────────
        //
        // Centred on the mean free path expressed in samples, because that IS
        // the average time between reflections in this room - the one number
        // that makes a corridor's echo density differ from a cathedral's. The
        // spread is deliberately wide and the multipliers are chosen to land on
        // primes after rounding.
        float mfpSamples = (meanFreePath / SOUND_C) * fs;
        if (mfpSamples < 32.0f)    mfpSamples = 32.0f;
        if (mfpSamples > 12000.0f) mfpSamples = 12000.0f;

        static const float spread[LINES] =
            { 0.61f, 0.74f, 0.87f, 1.00f, 1.13f, 1.28f, 1.45f, 1.63f };

        for (int i = 0; i < LINES; ++i) {
            int n = (int)(mfpSamples * spread[i]);
            if (n < 17) n = 17;
            if (n > MAX_DELAY - 1) n = MAX_DELAY - 1;
            len[i] = NextPrime(n);
            pos[i] = 0;
            buf[i].assign((size_t)len[i], 0.0f);
        }

        // Pre-delay: the gap before the first reflection arrives. One mean free
        // path is the physically right answer and costs nothing to be right
        // about - it is the time sound takes to reach the nearest surface and
        // come back.
        preLen = (int)(mfpSamples * 0.5f);
        if (preLen < 1) preLen = 1;
        if (preLen > MAX_PRE - 1) preLen = MAX_PRE - 1;
        pre.assign((size_t)preLen, 0.0f);

        // ── Feedback gains ───────────────────────────────────────────────────
        //
        // A line of length L is traversed fs/L times a second, so to lose 60 dB
        // in rt60 seconds each pass must keep 10^(-3L / (fs*rt60)).
        for (int i = 0; i < LINES; ++i) {
            const float L = (float)len[i];
            g[i] = powf(10.0f, -3.0f * L / (fs * rt60Low));
            if (g[i] > 0.9999f) g[i] = 0.9999f;   // never let a room ring forever

            // ── Damping ──────────────────────────────────────────────────────
            //
            // A one-pole lowpass inside the loop, chosen so the TOP of the
            // spectrum decays at rt60High while DC still decays at rt60Low. The
            // filter is unity at DC and (1-d)/(1+d) at Nyquist, so setting that
            // ratio to the ratio of the two per-pass gains gives d directly.
            const float gLo = g[i];
            const float gHi = powf(10.0f, -3.0f * L / (fs * rt60High));
            float ratio = (gLo > 1e-9f) ? (gHi / gLo) : 1.0f;
            if (ratio > 0.9999f) ratio = 0.9999f;
            if (ratio < 0.0001f) ratio = 0.0001f;
            damp[i] = (1.0f - ratio) / (1.0f + ratio);
            if (damp[i] < 0) damp[i] = 0;
            if (damp[i] > 0.95f) damp[i] = 0.95f;
        }

        built    = true;
        outScale = 1.0f;
        Normalise(fs);
    }

    // One sample in, stereo out. The input is the send; the output is pure wet.
    inline void Process(float in, float& outL, float& outR)
    {
        // Pre-delay.
        const float delayed = pre[prePos];
        pre[prePos] = in;
        prePos = (prePos + 1) % preLen;

        float taps[LINES];
        for (int i = 0; i < LINES; ++i) taps[i] = buf[i][pos[i]];

        // ── Householder mixing ───────────────────────────────────────────────
        //
        // y = x - (2/N) * sum(x). Orthogonal, so it conserves energy exactly -
        // which is what lets the feedback gains above mean what they say - and
        // it costs one sum and N subtractions instead of an N x N multiply.
        float sum = 0;
        for (int i = 0; i < LINES; ++i) sum += taps[i];
        const float corr = 2.0f * sum / (float)LINES;

        for (int i = 0; i < LINES; ++i) {
            float v = (taps[i] - corr) * g[i] + delayed;

            // Damping, in the loop so it compounds once per pass.
            lp[i] += (v - lp[i]) * (1.0f - damp[i]);
            v = lp[i];

            buf[i][pos[i]] = v;
            pos[i] = (pos[i] + 1) % len[i];
        }

        // Stereo out from alternating lines with opposite signs on one side, so
        // the two ears get decorrelated tails. A mono reverb collapses to the
        // centre of the head and undoes the work the HRTF just did.
        float l = 0, r = 0;
        for (int i = 0; i < LINES; i += 2) l += taps[i];
        for (int i = 1; i < LINES; i += 2) r += taps[i];

        outL = l * outScale;
        outR = r * outScale;
    }

private:
    static bool IsPrime(int n)
    {
        if (n < 2) return false;
        if (n % 2 == 0) return n == 2;
        for (int d = 3; d * d <= n; d += 2)
            if (n % d == 0) return false;
        return true;
    }

    static int NextPrime(int n)
    {
        while (!IsPrime(n)) ++n;
        return n;
    }

    // ── The normalisation the plan warns about ───────────────────────────────
    //
    // Measured rather than derived, because the analytic energy of an FDN
    // depends on the mixing matrix as well as the gains and getting it wrong is
    // silent. Run an impulse through and scale so the result has unit RMS.
    //
    // The measurement window is capped at one second: a 3-second tail is
    // already 40 dB down by then, so the extra samples move the RMS by a
    // fraction of a dB and cost three times as long at bus construction.
    void Normalise(float fs)
    {
        const int n = (int)(fs * (rt60 < 1.0f ? rt60 : 1.0f));
        double sum = 0, peak = 0;
        float l, r;

        Process(1.0f, l, r);
        sum += (double)l * l + (double)r * r;
        for (int i = 1; i < n; ++i) {
            Process(0.0f, l, r);
            sum += (double)l * l + (double)r * r;
            const double m = (fabs(l) > fabs(r)) ? fabs(l) : fabs(r);
            if (m > peak) peak = m;
        }

        const double rms = (n > 0) ? sqrt(sum / (2.0 * n)) : 0.0;
        outScale = (rms > 1e-9) ? (float)(1.0 / rms) : 1.0f;

        // ── AND A PEAK CEILING ON TOP OF THE RMS MATCH ───────────────────────
        //
        // Unit RMS is the right target for LEVEL, but it is the wrong target on
        // its own for a very small room: the impulse response there is sparse
        // and spiky, so a modest RMS is reached by a few large samples and the
        // normalisation then multiplies those up. Measured: a 0.5 m room came
        // out with a peak of 58x the input, which the send gain would not have
        // hidden and which would clip on any transient.
        //
        // So the scale is whichever is smaller. Ordinary rooms are unaffected -
        // their peak-to-RMS ratio is nowhere near 4 - and the pathological ones
        // are bounded instead of loud.
        const double PEAK_CEIL = 4.0;
        if (peak > 1e-9) {
            const float capped = (float)(PEAK_CEIL / peak);
            if (capped < outScale) outScale = capped;
        }

        // The measurement filled the delay lines; clear them so the first real
        // sample does not arrive into the tail of a test impulse.
        for (int i = 0; i < LINES; ++i) {
            std::fill(buf[i].begin(), buf[i].end(), 0.0f);
            pos[i] = 0;
        }
        std::fill(pre.begin(), pre.end(), 0.0f);
        memset(lp, 0, sizeof(lp));
        prePos = 0;
    }
};

} // namespace MrpgReverb
