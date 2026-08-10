// =============================================================================
// TestReverb.cpp — does the FDN decay for as long as it was asked to?
// =============================================================================
//
// A reverb is the easiest thing in an audio chain to get plausibly wrong: any
// feedback network makes a wash, and a wash sounds like a room. The questions
// that actually matter are whether the decay time is the one that was requested,
// whether the treble decays faster than the bass, and whether two rooms of very
// different sizes arrive at the SAME LEVEL - which is the normalisation caveat
// in AUDIORT_NATIVE_PLAN.md §5.4, and the one that silently invalidates every
// send gain if it is skipped.
//
// RT60 is measured by Schroeder backward integration, which is how it is
// measured in a real room: integrate the squared IR from the end backwards, and
// the decay curve is the slope of that.

#include <stdio.h>
#include <math.h>
#include <vector>

#include "Reverb.hpp"

namespace {

int g_fail = 0;

void Check(const char* what, bool ok, const char* detail)
{
    printf("  %-46s %s   %s\n", what, ok ? "PASS" : "FAIL", detail);
    if (!ok) ++g_fail;
}

std::vector<float> ImpulseResponse(MrpgReverb::Fdn& f, int n)
{
    std::vector<float> ir(n);
    float l, r;
    f.Process(1.0f, l, r);
    ir[0] = (l + r) * 0.5f;
    for (int i = 1; i < n; ++i) {
        f.Process(0.0f, l, r);
        ir[i] = (l + r) * 0.5f;
    }
    return ir;
}

// Schroeder backward integration -> RT60, fitted over the -5 to -35 dB span and
// extrapolated. Fitting from 0 dB is wrong: the first milliseconds are the
// early reflections, which are not part of the exponential tail.
double MeasureRt60(const std::vector<float>& ir, double fs)
{
    const size_t n = ir.size();
    std::vector<double> sch(n);
    double acc = 0;
    for (size_t i = n; i-- > 0;) {
        acc += (double)ir[i] * ir[i];
        sch[i] = acc;
    }
    if (sch[0] <= 0) return -1;

    const double ref = sch[0];
    double t5 = -1, t35 = -1;
    for (size_t i = 0; i < n; ++i) {
        const double db = 10.0 * log10(sch[i] / ref + 1e-300);
        if (t5  < 0 && db <= -5.0)  t5  = (double)i / fs;
        if (t35 < 0 && db <= -35.0) { t35 = (double)i / fs; break; }
    }
    if (t5 < 0 || t35 < 0) return -1;
    return (t35 - t5) * (60.0 / 30.0);
}

double BandRms(const std::vector<float>& ir, double fs, double hz, size_t from, size_t to)
{
    // One-pole bandpass by difference of two one-poles. Crude, and entirely
    // adequate for "is the top decaying faster than the bottom".
    const double a = 1.0 - exp(-2.0 * 3.14159265 * hz / fs);
    double lo = 0, prev = 0, sum = 0; size_t cnt = 0;
    for (size_t i = 0; i < ir.size(); ++i) {
        lo += (ir[i] - lo) * a;
        const double band = lo - prev;
        prev = lo;
        if (i >= from && i < to) { sum += band * band; ++cnt; }
    }
    return cnt ? sqrt(sum / cnt) : 0.0;
}

} // namespace

int main()
{
    const float fs = 48000.0f;
    printf("\nMonsterRPGAudio - FDN reverb checks at %.0f Hz\n\n", fs);
    char buf[200];

    // ── 1. Eyring, against a hand-worked number ──────────────────────────────
    //
    // A 10 m mean free path with 20% absorbed per bounce:
    //   RT60 = (10/343) * 13.8155 / -ln(0.8) = 0.02915 * 13.8155 / 0.22314
    //        = 1.805 s
    {
        const float rt = MrpgReverb::Rt60FromMfp(10.0f, 0.20f);
        snprintf(buf, sizeof(buf), "%.3f s, hand-worked 1.805 s", rt);
        Check("Eyring: 10 m mfp, alpha 0.20", fabsf(rt - 1.805f) < 0.01f, buf);

        const float open = MrpgReverb::Rt60FromMfp(0.0f, 0.5f);
        snprintf(buf, sizeof(buf), "%.3f s", open);
        Check("no bounce recorded means no reverb", open == 0.0f, buf);

        // Sabine cross-check, INSIDE the physical range. The two formulas agree
        // only for small absorption - which is Sabine's whole limitation - so
        // this is deliberately a small room at the absorption floor. Asking for
        // agreement at alpha 0.02 as an earlier version did now measures the
        // safety clamp instead of the formula.
        const float small  = MrpgReverb::Rt60FromMfp(2.0f, 0.06f);
        const float sabine = 0.161f * (2.0f / 4.0f) / 0.06f;
        snprintf(buf, sizeof(buf), "Eyring %.2f s vs Sabine %.2f s", small, sabine);
        Check("agrees with Sabine where Sabine is valid",
              fabsf(small - sabine) / sabine < 0.05f, buf);

        // ── THE FUSE ─────────────────────────────────────────────────────────
        //
        // This is the check that would have caught the reverb that had to be
        // switched off mid-session. A near-mirror-finish room, read out of a
        // field that was never an absorption coefficient, produced 88 seconds of
        // decay: a bus that never emptied and simply accumulated every sound put
        // into it. No decay time this engine returns may exceed a cathedral's.
        const float absurd = MrpgReverb::Rt60FromMfp(11.0f, 0.0f);
        snprintf(buf, sizeof(buf), "%.2f s (fuse at %.1f s)", absurd, MrpgReverb::RT60_MAX);
        Check("a mirror-finish room is refused, not rendered",
              absurd <= MrpgReverb::RT60_MAX + 0.001f, buf);

        float worstRt = 0;
        for (float m = 0.1f; m <= 60.0f; m *= 1.3f)
            for (float a = 0.0f; a <= 1.0f; a += 0.01f) {
                const float rt = MrpgReverb::Rt60FromMfp(m, a);
                if (rt > worstRt) worstRt = rt;
            }
        snprintf(buf, sizeof(buf), "worst over every mfp and alpha: %.2f s", worstRt);
        Check("no input at all can produce a runaway tail",
              worstRt <= MrpgReverb::RT60_MAX + 0.001f, buf);
    }

    // ── 2. The decay is the one that was asked for ───────────────────────────
    {
        const float want[] = { 0.35f, 0.8f, 1.8f, 3.0f };
        for (float w : want) {
            MrpgReverb::Fdn f;
            f.Build(8.0f, w, w, fs);
            std::vector<float> ir = ImpulseResponse(f, (int)(fs * (w * 1.6f + 0.3f)));
            const double got = MeasureRt60(ir, fs);

            snprintf(buf, sizeof(buf), "asked %.2f s, measured %.2f s", w, got);
            // 15% is the honest tolerance for a Schroeder fit on a synthetic
            // tail of this length; anything tighter is measuring the fit.
            Check("RT60 matches the request", got > 0 && fabs(got - w) / w < 0.15, buf);
        }
    }

    // ── 3. Treble decays faster than bass ────────────────────────────────────
    //
    // The single thing that makes a stone corridor sound unlike a curtained
    // room. Flat damping is the tell-tale of a reverb that was never given the
    // tracer's per-band energy.
    {
        MrpgReverb::Fdn f;
        f.Build(8.0f, 2.0f, 0.5f, fs);          // bass 2 s, treble 0.5 s
        std::vector<float> ir = ImpulseResponse(f, (int)(fs * 2.5f));

        const size_t early = (size_t)(fs * 0.10);
        const size_t late0 = (size_t)(fs * 0.80);
        const size_t late1 = (size_t)(fs * 1.20);

        const double loEarly = BandRms(ir, fs, 200.0,  0, early);
        const double hiEarly = BandRms(ir, fs, 6000.0, 0, early);
        const double loLate  = BandRms(ir, fs, 200.0,  late0, late1);
        const double hiLate  = BandRms(ir, fs, 6000.0, late0, late1);

        const double loDrop = 20.0 * log10((loLate + 1e-12) / (loEarly + 1e-12));
        const double hiDrop = 20.0 * log10((hiLate + 1e-12) / (hiEarly + 1e-12));

        snprintf(buf, sizeof(buf), "low %.1f dB, high %.1f dB over the same span",
                 loDrop, hiDrop);
        Check("high frequencies die first", hiDrop < loDrop - 3.0, buf);
    }

    // ── 4. THE NORMALISATION CAVEAT (§5.4) ───────────────────────────────────
    //
    // Without it a 3 s room arrives roughly an order of magnitude louder than a
    // 0.35 s one, every send gain has to be re-derived, and the symptom is
    // "big rooms are too loud" - which is easy to mistake for a taste problem
    // and to fix in the wrong place.
    {
        MrpgReverb::Fdn a, b;
        a.Build(4.0f,  0.35f, 0.25f, fs);
        b.Build(20.0f, 3.00f, 1.20f, fs);

        std::vector<float> ia = ImpulseResponse(a, (int)(fs * 0.5f));
        std::vector<float> ib = ImpulseResponse(b, (int)(fs * 0.5f));

        double sa = 0, sb = 0;
        for (float v : ia) sa += (double)v * v;
        for (float v : ib) sb += (double)v * v;
        sa = sqrt(sa / ia.size());
        sb = sqrt(sb / ib.size());

        const double d = 20.0 * log10((sb + 1e-12) / (sa + 1e-12));
        snprintf(buf, sizeof(buf), "3.0 s room is %+.1f dB vs the 0.35 s room", d);
        Check("small and large rooms arrive at the same level", fabs(d) < 6.0, buf);
    }

    // ── 5. Stability and sanity across every room we can ask for ─────────────
    {
        bool ok = true; float worst = 0;
        for (float mfp = 0.5f; mfp <= 40.0f; mfp *= 1.7f) {
            for (float rt = 0.1f; rt <= 4.0f; rt *= 1.8f) {
                MrpgReverb::Fdn f;
                f.Build(mfp, rt, rt * 0.4f, fs);
                std::vector<float> ir = ImpulseResponse(f, (int)(fs * 1.0f));
                for (float v : ir) {
                    if (!isfinite(v)) ok = false;
                    if (fabsf(v) > worst) worst = fabsf(v);
                }
                // A tail that is still at full strength after a second when it
                // was asked for 0.1 s is a runaway, not a room.
                const double tailEnd = fabs(ir[ir.size() - 1]);
                if (tailEnd > 0.5) ok = false;
            }
        }
        snprintf(buf, sizeof(buf), "peak %.2f across %s", worst, "8 x 5 room sizes");
        Check("finite, bounded and decaying for every room", ok && worst < 8.0f, buf);
    }

    // ── 6. Left and right are not the same signal ────────────────────────────
    {
        MrpgReverb::Fdn f;
        f.Build(10.0f, 1.2f, 0.6f, fs);
        double dot = 0, el = 0, er = 0;
        float l, r;
        f.Process(1.0f, l, r);
        for (int i = 1; i < (int)(fs * 0.5f); ++i) {
            f.Process(0.0f, l, r);
            dot += (double)l * r; el += (double)l * l; er += (double)r * r;
        }
        const double corr = dot / (sqrt(el * er) + 1e-12);
        snprintf(buf, sizeof(buf), "interaural correlation %.3f", corr);
        Check("the two channels are decorrelated", fabs(corr) < 0.5, buf);
    }

    printf("\n%s  (%d failure%s)\n\n",
           g_fail ? "SOME CHECKS FAILED" : "ALL CHECKS PASSED",
           g_fail, g_fail == 1 ? "" : "s");
    return g_fail ? 1 : 0;
}
