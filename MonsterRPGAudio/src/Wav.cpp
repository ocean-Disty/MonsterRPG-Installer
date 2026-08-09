#include <windows.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "Wav.hpp"

namespace MrpgWav {

namespace {

// Format tags, from mmreg.h. Spelled out rather than included so this file has
// no dependency beyond the CRT.
const unsigned short FMT_PCM        = 0x0001;
const unsigned short FMT_ADPCM_MS   = 0x0002;
const unsigned short FMT_IEEE_FLOAT = 0x0003;
const unsigned short FMT_IMA_ADPCM  = 0x0011;
const unsigned short FMT_EXTENSIBLE = 0xFFFE;

// A sound this big is a mistake somewhere, not a sound effect. 64 MB decoded is
// about six minutes of 22 kHz mono - past that we are almost certainly looking
// at a corrupt header, and allocating what it asks for would be the last thing
// this process ever did.
const int MAX_DECODED_BYTES = 64 * 1024 * 1024;

inline unsigned int RdU32(const unsigned char* p)
{
    return (unsigned int)p[0] | ((unsigned int)p[1] << 8)
         | ((unsigned int)p[2] << 16) | ((unsigned int)p[3] << 24);
}
inline unsigned short RdU16(const unsigned char* p)
{
    return (unsigned short)((unsigned int)p[0] | ((unsigned int)p[1] << 8));
}

// ── IMA-ADPCM ────────────────────────────────────────────────────────────────
//
// The IMA/DVI tables, verbatim from the specification. Each nibble is a
// quantised step along a curve whose index walks up and down with the signal, so
// four bits carry roughly sixteen bits of dynamic range.

const int IMA_INDEX[16] = {
    -1, -1, -1, -1, 2, 4, 6, 8,
    -1, -1, -1, -1, 2, 4, 6, 8
};

const int IMA_STEP[89] = {
    7,8,9,10,11,12,13,14,16,17,19,21,23,25,28,31,34,37,41,45,50,55,60,66,73,
    80,88,97,107,118,130,143,157,173,190,209,230,253,279,307,337,371,408,449,
    494,544,598,658,724,796,876,963,1060,1166,1282,1411,1552,1707,1878,2066,
    2272,2499,2749,3024,3327,3660,4026,4428,4871,5358,5894,6484,7132,7845,8630,
    9493,10442,11487,12635,13899,15289,16818,18500,20350,22385,24623,27086,
    29794,32767
};

struct ImaState { int predictor; int index; };

inline int ImaDecodeNibble(ImaState& s, int nibble)
{
    const int step = IMA_STEP[s.index];

    // The reconstruction, written the long way. step/8 is the rounding term the
    // spec adds so the error is centred rather than always negative.
    int diff = step >> 3;
    if (nibble & 1) diff += step >> 2;
    if (nibble & 2) diff += step >> 1;
    if (nibble & 4) diff += step;
    if (nibble & 8) diff = -diff;

    s.predictor += diff;
    if (s.predictor >  32767) s.predictor =  32767;
    if (s.predictor < -32768) s.predictor = -32768;

    s.index += IMA_INDEX[nibble];
    if (s.index < 0)  s.index = 0;
    if (s.index > 88) s.index = 88;

    return s.predictor;
}

} // namespace

Sound Load(const char* path, char* why, int whyLen)
{
    Sound out;
    if (why && whyLen > 0) why[0] = '\0';

    auto fail = [&](const char* msg) -> Sound {
        if (why && whyLen > 0) { lstrcpynA(why, msg, whyLen); }
        Free(out);
        out.ok = false;
        return out;
    };

    FILE* fp = fopen(path, "rb");
    if (!fp) return fail("cannot open the file");

    unsigned char hdr[12];
    if (fread(hdr, 1, 12, fp) != 12
        || memcmp(hdr, "RIFF", 4) != 0 || memcmp(hdr + 8, "WAVE", 4) != 0) {
        fclose(fp);
        return fail("not a RIFF/WAVE file");
    }

    unsigned short fmt = 0, channels = 0, bits = 0, blockAlign = 0;
    unsigned int   rate = 0;
    bool           haveFmt = false;

    unsigned char* raw     = nullptr;
    unsigned int   rawSize = 0;

    // Walk the chunk list rather than assuming fmt is first and data second -
    // plenty of tools emit LIST/fact chunks in between. This loop is the same
    // shape as AudioRT_ProbeWav's on the server, which is the reference.
    for (;;) {
        unsigned char ch[8];
        if (fread(ch, 1, 8, fp) != 8) break;
        unsigned int sz = RdU32(ch + 4);

        if (memcmp(ch, "fmt ", 4) == 0 && sz >= 16) {
            unsigned char f[40];
            unsigned int take = sz < sizeof(f) ? sz : (unsigned int)sizeof(f);
            if (fread(f, 1, take, fp) != take) break;

            fmt        = RdU16(f);
            channels   = RdU16(f + 2);
            rate       = RdU32(f + 4);
            blockAlign = RdU16(f + 12);
            bits       = RdU16(f + 14);

            // WAVE_FORMAT_EXTENSIBLE hides the real tag in the first two bytes
            // of its GUID. Without this every 24-bit file recorded by a modern
            // tool reads as "unsupported format 65534".
            if (fmt == FMT_EXTENSIBLE && take >= 26)
                fmt = RdU16(f + 24);

            haveFmt = true;
            if (sz > take) fseek(fp, (long)(sz - take), SEEK_CUR);
        } else if (memcmp(ch, "data", 4) == 0) {
            if (sz == 0) break;
            if (sz > (unsigned int)MAX_DECODED_BYTES) { fclose(fp); return fail("data chunk implausibly large"); }
            raw = (unsigned char*)malloc(sz);
            if (!raw) { fclose(fp); return fail("out of memory"); }
            rawSize = (unsigned int)fread(raw, 1, sz, fp);
            break;                       // everything we need is behind us
        } else {
            fseek(fp, (long)sz, SEEK_CUR);
        }
        if (sz & 1u) fseek(fp, 1, SEEK_CUR);   // chunks are word-aligned
    }
    fclose(fp);

    if (!haveFmt)                 { free(raw); return fail("no fmt chunk"); }
    if (!raw || rawSize == 0)     { free(raw); return fail("no data chunk"); }
    if (channels < 1 || channels > 2) { free(raw); return fail("only mono and stereo are supported"); }
    if (rate < 4000 || rate > 192000) { free(raw); return fail("implausible sample rate"); }

    out.channels = channels;
    out.rate     = (int)rate;

    // ── IMA-ADPCM ────────────────────────────────────────────────────────────
    if (fmt == FMT_IMA_ADPCM) {
        if (bits != 4)          { free(raw); return fail("IMA-ADPCM must be 4-bit"); }
        if (blockAlign < 4 * channels) { free(raw); return fail("IMA-ADPCM block align too small"); }

        // Each block starts with a 4-byte preamble PER CHANNEL (predictor,
        // index, pad) and then interleaves 4-byte words of nibbles per channel.
        const int hdrBytes    = 4 * channels;
        const int blocks      = (int)(rawSize / blockAlign);
        const int dataPerBlk  = blockAlign - hdrBytes;
        const int samplesPerCh = 1 + (dataPerBlk / channels) * 2;   // preamble sample + 2 per byte

        long long total = (long long)blocks * samplesPerCh * channels;
        if (total <= 0 || total * (long long)sizeof(float) > MAX_DECODED_BYTES) {
            free(raw);
            return fail("IMA-ADPCM decodes to an implausible size");
        }

        out.frames = blocks * samplesPerCh;
        out.data   = (float*)malloc((size_t)total * sizeof(float));
        if (!out.data) { free(raw); return fail("out of memory"); }

        int w = 0;
        for (int b = 0; b < blocks; ++b) {
            const unsigned char* blk = raw + (size_t)b * blockAlign;

            ImaState st[2];
            for (int c = 0; c < channels; ++c) {
                st[c].predictor = (short)RdU16(blk + c * 4);
                st[c].index     = blk[c * 4 + 2];
                if (st[c].index > 88) st[c].index = 88;
            }

            // The preamble's predictor IS the first sample of the block.
            for (int c = 0; c < channels; ++c)
                out.data[w++] = (float)st[c].predictor / 32768.0f;

            const unsigned char* p = blk + hdrBytes;
            const int words = dataPerBlk / (4 * channels);

            for (int wi = 0; wi < words; ++wi) {
                for (int c = 0; c < channels; ++c) {
                    const unsigned char* q = p + (size_t)(wi * channels + c) * 4;
                    for (int k = 0; k < 4; ++k) {
                        // Low nibble first: the format is little-endian all the
                        // way down, including within a byte.
                        int lo = q[k] & 0x0F;
                        int hi = (q[k] >> 4) & 0x0F;
                        int s0 = ImaDecodeNibble(st[c], lo);
                        int s1 = ImaDecodeNibble(st[c], hi);
                        int base = w + (wi * 8 + k * 2) * channels + c;
                        out.data[base]            = (float)s0 / 32768.0f;
                        out.data[base + channels] = (float)s1 / 32768.0f;
                    }
                }
            }
            w += words * 8 * channels;
        }

        free(raw);
        out.frames = w / channels;
        out.ok     = (out.frames > 0);
        if (!out.ok) return fail("IMA-ADPCM produced no samples");
        return out;
    }

    // ── Linear PCM and IEEE float ────────────────────────────────────────────
    if (fmt != FMT_PCM && fmt != FMT_IEEE_FLOAT) {
        free(raw);
        char msg[96];
        _snprintf(msg, sizeof(msg) - 1,
                  fmt == FMT_ADPCM_MS ? "MS-ADPCM (tag %u) is not supported"
                                      : "unsupported format tag %u", (unsigned)fmt);
        msg[sizeof(msg) - 1] = '\0';
        return fail(msg);
    }

    const int bytesPerSample = bits / 8;
    if (bytesPerSample <= 0 || (bits != 8 && bits != 16 && bits != 24 && bits != 32 && bits != 64)) {
        free(raw);
        return fail("unsupported bit depth");
    }

    const int totalSamples = (int)(rawSize / (unsigned int)bytesPerSample);
    const int frames       = totalSamples / channels;
    if (frames <= 0) { free(raw); return fail("no frames"); }
    if ((long long)totalSamples * (long long)sizeof(float) > MAX_DECODED_BYTES) {
        free(raw);
        return fail("decodes to an implausible size");
    }

    out.frames = frames;
    out.data   = (float*)malloc((size_t)frames * channels * sizeof(float));
    if (!out.data) { free(raw); return fail("out of memory"); }

    const int n = frames * channels;
    for (int i = 0; i < n; ++i) {
        const unsigned char* p = raw + (size_t)i * bytesPerSample;
        float v = 0.0f;

        if (fmt == FMT_IEEE_FLOAT) {
            if (bits == 32) {
                unsigned int u = RdU32(p);
                memcpy(&v, &u, 4);
            } else {                       // 64-bit double
                double d;
                memcpy(&d, p, 8);
                v = (float)d;
            }
        } else if (bits == 8) {
            // 8-bit WAV is UNSIGNED, centred on 128. Every other depth is
            // signed. Treating it as signed is the classic way to turn a sound
            // into a loud buzz.
            v = ((float)p[0] - 128.0f) / 128.0f;
        } else if (bits == 16) {
            v = (float)(short)RdU16(p) / 32768.0f;
        } else if (bits == 24) {
            int u = (int)p[0] | ((int)p[1] << 8) | ((int)p[2] << 16);
            if (u & 0x800000) u |= ~0xFFFFFF;      // sign extend
            v = (float)u / 8388608.0f;
        } else {                                   // 32-bit int
            v = (float)(int)RdU32(p) / 2147483648.0f;
        }

        if (v >  1.0f) v =  1.0f;
        if (v < -1.0f) v = -1.0f;
        out.data[i] = v;
    }

    free(raw);
    out.ok = true;
    return out;
}

void Free(Sound& s)
{
    if (s.data) free(s.data);
    s.data   = nullptr;
    s.frames = 0;
    s.ok     = false;
}

} // namespace MrpgWav
