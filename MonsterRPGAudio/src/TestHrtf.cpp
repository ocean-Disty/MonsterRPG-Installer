// =============================================================================
// TestHrtf.cpp — does the head model actually produce the cues it claims to?
// =============================================================================
//
// Builds standalone (see build_tests.bat). It exists because "it compiles and I
// can hear something" is not evidence that a spatialiser is correct: a sign
// error in the head basis puts every sound behind you, an ITD of the wrong
// polarity swaps your ears, and BOTH still sound plausible until you try to
// point at something.
//
// Every check below is a NUMBER with a physically-derived expectation, not a
// listening impression.

#include <stdio.h>
#include <math.h>
#include <vector>

#include "Hrtf.hpp"

namespace {

int g_fail = 0;

void Check(const char* what, bool ok, const char* detail)
{
    printf("  %-46s %s   %s\n", what, ok ? "PASS" : "FAIL", detail);
    if (!ok) ++g_fail;
}

// Peak-detected onset: the first sample whose magnitude passes a fraction of
// the eventual peak. Cheap, and exactly what an ITD is.
double Onset(const std::vector<float>& v)
{
    float peak = 0;
    for (float s : v) if (fabsf(s) > peak) peak = fabsf(s);
    if (peak <= 0) return -1;
    for (size_t i = 0; i < v.size(); ++i)
        if (fabsf(v[i]) >= peak * 0.25f) return (double)i;
    return -1;
}

double Rms(const std::vector<float>& v, size_t from)
{
    double sum = 0; size_t n = 0;
    for (size_t i = from; i < v.size(); ++i) { sum += (double)v[i] * v[i]; ++n; }
    return n ? sqrt(sum / n) : 0.0;
}

// Run a signal through the model at one direction and return both ears.
void Render(float az, float el, float fs, const std::vector<float>& in,
            std::vector<float>& outL, std::vector<float>& outR)
{
    MrpgHrtf::Binaural b;
    b.Set(az, el, fs);
    // Seed the delay so the first sample is already at the right offset - the
    // smoothing is for movement, not for note-on.
    b.L.delay = b.L.delayTarget;
    b.R.delay = b.R.delayTarget;

    outL.assign(in.size(), 0.0f);
    outR.assign(in.size(), 0.0f);
    for (size_t i = 0; i < in.size(); ++i)
        b.Process(in[i], 1.0f, outL[i], outR[i]);
}

std::vector<float> Impulse(int n)
{
    std::vector<float> v(n, 0.0f);
    v[8] = 1.0f;
    return v;
}

std::vector<float> Sine(int n, float hz, float fs)
{
    std::vector<float> v(n);
    for (int i = 0; i < n; ++i)
        v[i] = sinf(6.2831853f * hz * (float)i / fs);
    return v;
}

const float DEG = 3.14159265f / 180.0f;

} // namespace

int main()
{
    const float fs = 48000.0f;
    printf("\nMonsterRPGAudio - head model checks at %.0f Hz\n\n", fs);

    std::vector<float> l, r;

    // ── 1. ITD polarity and magnitude ────────────────────────────────────────
    //
    // Woodworth: ITD = (a/c)(sin th + th). At 90 degrees that is
    // (0.0875/343)(1 + pi/2) = 0.655 ms = 31.4 samples at 48 kHz.
    {
        const double expect = (MrpgHrtf::HEAD_RADIUS / MrpgHrtf::SOUND_C)
                            * (1.0 + 1.5707963) * fs;

        Render(90 * DEG, 0, fs, Impulse(512), l, r);
        const double dL = Onset(l), dR = Onset(r);
        char buf[160];

        snprintf(buf, sizeof(buf), "L-R = %.1f samples, expected %.1f", dL - dR, expect);
        Check("hard right: left ear is later, by Woodworth",
              dR >= 0 && dL >= 0 && fabs((dL - dR) - expect) < 1.5, buf);

        Render(-90 * DEG, 0, fs, Impulse(512), l, r);
        const double dL2 = Onset(l), dR2 = Onset(r);
        snprintf(buf, sizeof(buf), "R-L = %.1f samples", dR2 - dL2);
        Check("hard left: mirror image of hard right",
              fabs((dR2 - dL2) - expect) < 1.5, buf);

        Render(0, 0, fs, Impulse(512), l, r);
        snprintf(buf, sizeof(buf), "L-R = %.2f samples", Onset(l) - Onset(r));
        Check("dead ahead: no interaural delay",
              fabs(Onset(l) - Onset(r)) < 0.5, buf);
    }

    // ── 2. Head shadow is frequency dependent ────────────────────────────────
    //
    // This is the cue amplitude panning cannot produce, and the whole reason a
    // panned sound sits inside your head. The ILD at 6 kHz must be much larger
    // than at 300 Hz for the same direction.
    {
        char buf[160];
        Render(90 * DEG, 0, fs, Sine(8192, 300.0f, fs), l, r);
        const double ildLow = 20.0 * log10(Rms(r, 2048) / (Rms(l, 2048) + 1e-9));

        Render(90 * DEG, 0, fs, Sine(8192, 6000.0f, fs), l, r);
        const double ildHigh = 20.0 * log10(Rms(r, 2048) / (Rms(l, 2048) + 1e-9));

        snprintf(buf, sizeof(buf), "300 Hz %+.1f dB, 6 kHz %+.1f dB", ildLow, ildHigh);
        Check("shadow grows with frequency", ildHigh > ildLow + 6.0, buf);
        Check("near ear is the louder one at both", ildLow > 0 && ildHigh > 0, buf);

        snprintf(buf, sizeof(buf), "%+.1f dB at 6 kHz", ildHigh);
        Check("high-frequency ILD is in the human range (6-25 dB)",
              ildHigh > 6.0 && ildHigh < 25.0, buf);
    }

    // ── 3. Front and back differ ─────────────────────────────────────────────
    //
    // With no pinna cue at all these two are identical, which is what makes a
    // plain panner unable to put anything behind you.
    {
        char buf[160];
        Render(0, 0, fs, Sine(8192, 8000.0f, fs), l, r);
        const double front = Rms(l, 2048);
        Render(180 * DEG, 0, fs, Sine(8192, 8000.0f, fs), l, r);
        const double back = Rms(l, 2048);

        const double d = 20.0 * log10(front / (back + 1e-9));
        snprintf(buf, sizeof(buf), "front vs back at 8 kHz: %+.1f dB", d);
        Check("front and back are distinguishable", fabs(d) > 1.0, buf);
    }

    // ── 4. Elevation moves the notch ─────────────────────────────────────────
    {
        char buf[160];
        Render(0,  60 * DEG, fs, Sine(8192, 6000.0f, fs), l, r);
        const double above = Rms(l, 2048);
        Render(0, -60 * DEG, fs, Sine(8192, 6000.0f, fs), l, r);
        const double below = Rms(l, 2048);

        const double d = 20.0 * log10(below / (above + 1e-9));
        snprintf(buf, sizeof(buf), "6 kHz: below is %+.1f dB vs above", d);
        Check("elevation changes the spectrum", fabs(d) > 1.5, buf);
    }

    // ── 5. The head basis agrees with AudioRT::ToHeadRelative ────────────────
    //
    // Hand-checked directions. If this drifts, a footstep and a voice at one
    // spot stop localising the same way, which is worse than neither working.
    {
        char buf[160];
        // Listener at the origin looking north (+Y). Source due east (+X).
        MrpgHrtf::Head h = MrpgHrtf::ToHeadRelative(0,0,0,  0,1,0,  10,0,0);
        snprintf(buf, sizeof(buf), "x=%.2f y=%.2f z=%.2f", h.x, h.y, h.z);
        Check("east of a north-facing listener is to the RIGHT", h.x > 9.0, buf);

        // Source due north = straight ahead. z is NEGATED forward, so ahead is
        // negative z. Getting this backwards is the every-sound-behind-you bug.
        h = MrpgHrtf::ToHeadRelative(0,0,0,  0,1,0,  0,10,0);
        snprintf(buf, sizeof(buf), "x=%.2f y=%.2f z=%.2f", h.x, h.y, h.z);
        Check("straight ahead is NEGATIVE z (Web Audio convention kept)",
              h.z < -9.0 && fabsf(h.x) < 0.01f, buf);

        // Straight up.
        h = MrpgHrtf::ToHeadRelative(0,0,0,  0,1,0,  0,0,10);
        snprintf(buf, sizeof(buf), "x=%.2f y=%.2f z=%.2f", h.x, h.y, h.z);
        Check("overhead is +y and nothing else", h.y > 9.0 && fabsf(h.x) < 0.01f, buf);

        // And the azimuth the mixer derives from it.
        h = MrpgHrtf::ToHeadRelative(0,0,0,  0,1,0,  10,0,0);
        const float az = atan2f(h.x, -h.z) / DEG;
        snprintf(buf, sizeof(buf), "azimuth = %+.1f degrees", az);
        Check("...and that reads as +90 degrees azimuth", fabsf(az - 90.0f) < 0.5f, buf);
    }

    // ── 6. Nothing blows up ──────────────────────────────────────────────────
    {
        char buf[160];
        bool finite = true; float worst = 0;
        for (int a = -180; a <= 180; a += 5) {
            for (int e = -90; e <= 90; e += 15) {
                Render(a * DEG, e * DEG, fs, Impulse(256), l, r);
                for (size_t i = 0; i < l.size(); ++i) {
                    if (!isfinite(l[i]) || !isfinite(r[i])) finite = false;
                    if (fabsf(l[i]) > worst) worst = fabsf(l[i]);
                    if (fabsf(r[i]) > worst) worst = fabsf(r[i]);
                }
            }
        }
        snprintf(buf, sizeof(buf), "peak %.2f over 73x13 directions", worst);
        Check("finite and bounded everywhere on the sphere",
              finite && worst < 4.0f, buf);
    }

    // ── 7. THE MEASURED TABLE, through the real code path ────────────────────
    //
    // check_hrtf.py already verified the FILE. This verifies the DLL's own
    // loader, its resampler and its bilinear interpolation, by asking the same
    // physical questions of the thing that will actually run in the game. The
    // two are not the same claim: a correct file read with a transposed index,
    // or with the ITD sign flipped on the way in, would pass one and fail the
    // other.
    //
    // Skipped, not failed, when hrtf.bin is absent - it is an optional asset and
    // a developer without it should still be able to run the suite.
    {
        char why[192] = {0};
        const char* path = "hrtf.bin";
        if (!MrpgHrtf::LoadTable(path, fs, why, sizeof(why)))
            path = "../hrtf.bin";
        if (!MrpgHrtf::Global().loaded)
            MrpgHrtf::LoadTable(path, fs, why, sizeof(why));

        if (!MrpgHrtf::Global().loaded) {
            printf("\n  (measured table not found - %s - structural checks only)\n", why);
        } else {
            printf("\n  measured table: %s\n", why);
            char buf[160];

            // Same three questions as the structural model, same expectations.
            Render(90 * DEG, 0, fs, Impulse(1024), l, r);
            const double dL = Onset(l), dR = Onset(r);
            snprintf(buf, sizeof(buf), "L-R = %.1f samples", dL - dR);
            Check("measured: hard right arrives at the right ear first",
                  dR >= 0 && dL > dR, buf);

            const double eL = Rms(l, 0), eR = Rms(r, 0);
            snprintf(buf, sizeof(buf), "R/L = %+.1f dB", 20.0 * log10(eR / (eL + 1e-12)));
            Check("measured: hard right is louder in the right ear", eR > eL * 1.5, buf);

            Render(-90 * DEG, 0, fs, Impulse(1024), l, r);
            const double mL = Rms(l, 0), mR = Rms(r, 0);
            snprintf(buf, sizeof(buf), "L/R = %+.1f dB", 20.0 * log10(mL / (mR + 1e-12)));
            Check("measured: hard left mirrors it", mL > mR * 1.5, buf);

            Render(0, 0, fs, Impulse(1024), l, r);
            snprintf(buf, sizeof(buf), "L-R = %.2f samples", Onset(l) - Onset(r));
            Check("measured: dead ahead is symmetric",
                  fabs(Onset(l) - Onset(r)) < 1.5, buf);

            // Interpolation must not blow up between grid points, and 2.5
            // degrees is exactly half a cell - the worst case for a bilinear
            // blend.
            bool ok = true; float worst = 0;
            for (float a = -180; a <= 180; a += 2.5f) {
                for (float e = -55; e <= 85; e += 7.5f) {
                    Render(a * DEG, e * DEG, fs, Impulse(256), l, r);
                    for (size_t i = 0; i < l.size(); ++i) {
                        if (!isfinite(l[i]) || !isfinite(r[i])) ok = false;
                        if (fabsf(l[i]) > worst) worst = fabsf(l[i]);
                        if (fabsf(r[i]) > worst) worst = fabsf(r[i]);
                    }
                }
            }
            snprintf(buf, sizeof(buf), "peak %.2f over 145x20 interpolated directions", worst);
            Check("measured: interpolation is finite and bounded",
                  ok && worst < 4.0f, buf);
        }
    }

    printf("\n%s  (%d failure%s)\n\n",
           g_fail ? "SOME CHECKS FAILED" : "ALL CHECKS PASSED",
           g_fail, g_fail == 1 ? "" : "s");
    return g_fail ? 1 : 0;
}
