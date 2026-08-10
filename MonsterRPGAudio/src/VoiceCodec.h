#pragma once

// =============================================================================
// VoiceCodec.h — IMA-ADPCM for the voice link
// =============================================================================
//
// SHARED FILE. Byte-identical copies in:
//     Blockland\MonsterRPGAudio\src\VoiceCodec.h        (client)
//     Blockland\Add-Ons\MONSTERRPG\VoiceCodec.h         (server)
//
// ── WHY ADPCM AND NOT OPUS ───────────────────────────────────────────────────
//
// Opus is better by every acoustic measure - roughly 24 kbps against this 66 -
// and it is the right answer eventually. It is not the right answer now:
//
//   * it is not in the 32-bit MSYS2 toolchain here, so it means installing a
//     package, statically linking it, and shipping it inside a DLL that goes to
//     players;
//   * the ADPCM decoder ALREADY EXISTS in Wav.cpp and has been run against 2095
//     real game files without a failure;
//   * 66 kbps per talker is unremarkable on any connection this game is
//     playable on, and voice is gated by a noise gate so a quiet room costs
//     nothing at all.
//
// Revisit this if QUALITY is the complaint. Do not revisit it for bitrate.
//
// ── EVERY FRAME IS INDEPENDENT, AND THAT IS THE IMPORTANT PART ───────────────
//
// ADPCM is a predictive codec: each nibble is a step from the previous sample,
// so a decoder that misses a frame is wrong for every sample after it, forever.
// Over UDP that is not a theoretical risk, it is Tuesday.
//
// So each 20 ms frame carries its own starting predictor and step index, and the
// encoder resets between frames. That costs 4 bytes per frame - about 2% - and
// buys the property that a lost packet costs exactly one lost frame and nothing
// else. The alternative is a talker who becomes permanent noise the first time
// the network hiccups.

#ifdef _MSC_VER
  typedef signed __int16   mv_s16;
  typedef unsigned __int8  mv_u8;
  typedef unsigned __int16 mv_u16;
#else
  #include <stdint.h>
  typedef int16_t  mv_s16;
  typedef uint8_t  mv_u8;
  typedef uint16_t mv_u16;
#endif

// 16 kHz: speech lives below 8 kHz, and the difference between this and 48 kHz
// is inaudible for voice while costing three times the bandwidth.
#define MRPGVOICE_RATE        16000

// 20 ms per frame. Short enough that a lost one is a click rather than a gap,
// long enough that the per-frame header and the packet overhead stay small.
#define MRPGVOICE_FRAME       320

// 4-byte preamble + one nibble per sample.
#define MRPGVOICE_ENC_BYTES   (4 + MRPGVOICE_FRAME / 2)   // 164

// Frames per datagram. Two keeps the packet rate at 25/s per talker rather than
// 50 while adding only 20 ms of buffering.
#define MRPGVOICE_MAX_FRAMES  4

namespace MrpgVoice {

// The IMA/DVI tables, verbatim from the specification. Same values as the WAV
// decoder in Wav.cpp - deliberately duplicated rather than shared, because that
// file is about reading files and this one is about a wire format, and coupling
// them would mean a change for one silently altering the other.
static const int kIndexAdjust[16] = {
    -1, -1, -1, -1, 2, 4, 6, 8,
    -1, -1, -1, -1, 2, 4, 6, 8
};

static const int kStepTable[89] = {
    7,8,9,10,11,12,13,14,16,17,19,21,23,25,28,31,34,37,41,45,50,55,60,66,73,
    80,88,97,107,118,130,143,157,173,190,209,230,253,279,307,337,371,408,449,
    494,544,598,658,724,796,876,963,1060,1166,1282,1411,1552,1707,1878,2066,
    2272,2499,2749,3024,3327,3660,4026,4428,4871,5358,5894,6484,7132,7845,8630,
    9493,10442,11487,12635,13899,15289,16818,18500,20350,22385,24623,27086,
    29794,32767
};

struct State { int predictor; int index; };

inline int Clamp16(int v)
{
    if (v >  32767) return  32767;
    if (v < -32768) return -32768;
    return v;
}

inline mv_u8 EncodeNibble(State& s, int sample)
{
    const int step = kStepTable[s.index];
    int diff = sample - s.predictor;

    mv_u8 nibble = 0;
    if (diff < 0) { nibble = 8; diff = -diff; }

    // Successive halving: each bit says "is the remainder at least this much".
    int delta = step >> 3;
    if (diff >= step)      { nibble |= 4; diff -= step;      delta += step;      }
    if (diff >= (step>>1)) { nibble |= 2; diff -= step >> 1; delta += step >> 1; }
    if (diff >= (step>>2)) { nibble |= 1;                    delta += step >> 2; }

    // The encoder must track the DECODER's reconstruction, not the input.
    // Predicting from the original signal instead is the classic ADPCM bug: it
    // sounds fine on a sine and turns speech into gravel, because the two ends
    // drift apart sample by sample.
    s.predictor = Clamp16(s.predictor + ((nibble & 8) ? -delta : delta));

    s.index += kIndexAdjust[nibble];
    if (s.index < 0)  s.index = 0;
    if (s.index > 88) s.index = 88;

    return nibble;
}

inline int DecodeNibble(State& s, int nibble)
{
    const int step = kStepTable[s.index];

    int diff = step >> 3;
    if (nibble & 1) diff += step >> 2;
    if (nibble & 2) diff += step >> 1;
    if (nibble & 4) diff += step;
    if (nibble & 8) diff = -diff;

    s.predictor = Clamp16(s.predictor + diff);

    s.index += kIndexAdjust[nibble];
    if (s.index < 0)  s.index = 0;
    if (s.index > 88) s.index = 88;

    return s.predictor;
}

// One 20 ms frame -> MRPGVOICE_ENC_BYTES. Self-contained: the preamble carries
// the state the decoder needs, so frames may be lost without consequence to
// their neighbours.
inline void EncodeFrame(const mv_s16* pcm, mv_u8* out)
{
    State s;
    // Seed the PREDICTOR from the first sample so the frame opens at the right
    // level rather than sliding up to it from zero.
    s.predictor = pcm[0];

    // Seed the STEP from this frame's own amplitude, not a fixed mid-table
    // guess.
    //
    // A fixed index of 24 (step 73) meant an all-silent frame still moved the
    // predictor by step/8 every sample - the minimum ADPCM increment - so
    // silence decoded as a quiet dither instead of silence. It also made a loud
    // frame spend its first samples climbing to the right step size.
    //
    // Choosing the step from the frame's peak fixes both ends: quiet frames get
    // a small step and stay quiet, loud ones start at the right scale. The index
    // travels in the preamble, so the decoder needs to know nothing about this.
    int peak = 0;
    for (int i = 0; i < MRPGVOICE_FRAME; ++i) {
        int a = pcm[i] < 0 ? -(int)pcm[i] : (int)pcm[i];
        if (a > peak) peak = a;
    }
    // A quarter of the peak is roughly the largest step a frame needs, and the
    // index adapts from there within a few samples either way.
    const int wantStep = peak / 4;
    s.index = 0;
    while (s.index < 88 && kStepTable[s.index] < wantStep) ++s.index;

    out[0] = (mv_u8)(s.predictor & 0xFF);
    out[1] = (mv_u8)((s.predictor >> 8) & 0xFF);
    out[2] = (mv_u8)s.index;
    out[3] = 0;

    for (int i = 0; i < MRPGVOICE_FRAME; i += 2) {
        const mv_u8 lo = EncodeNibble(s, pcm[i]);
        const mv_u8 hi = EncodeNibble(s, pcm[i + 1]);
        out[4 + i / 2] = (mv_u8)(lo | (hi << 4));
    }
}

inline void DecodeFrame(const mv_u8* in, mv_s16* pcm)
{
    State s;
    s.predictor = (mv_s16)((mv_u16)in[0] | ((mv_u16)in[1] << 8));
    s.index     = in[2];
    if (s.index > 88) s.index = 88;

    for (int i = 0; i < MRPGVOICE_FRAME; i += 2) {
        const mv_u8 b = in[4 + i / 2];
        pcm[i]     = (mv_s16)DecodeNibble(s, b & 0x0F);
        pcm[i + 1] = (mv_s16)DecodeNibble(s, (b >> 4) & 0x0F);
    }
}

} // namespace MrpgVoice
