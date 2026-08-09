#pragma once

// =============================================================================
// Wav — decode a .wav from disk into float32 samples
// =============================================================================
//
// THE FILES ARE ALREADY ON THIS MACHINE. That is the quiet advantage the native
// path has over the browser: a browser cannot read
// Add-Ons\Script_PeggFootsteps\Sounds\jumpSoundPlayer.wav, so sfx.js had to
// download every sound over HTTP and keep an IndexedDB cache of the encoded
// bytes. This DLL is inside the game, in the folder that already contains that
// file, because the player must have the add-on to have the datablock at all.
// Zero download, and the whole cache layer disappears.
//
// FORMATS. PCM 8/16/24/32-bit, IEEE float 32/64, and IMA-ADPCM.
//
// IMA-ADPCM IS NOT OPTIONAL AND IS NOT PADDING. AudioRT_ProbeWav exists on the
// server for one reason: decodeAudioData refuses IMA-ADPCM, so those profiles
// are marked rtClass="local" in the manifest and left on the engine, unable to
// ever be ray traced. Several add-ons ship them. Decoding them here is about
// sixty lines and deletes that whole carve-out - see §2.1 of
// AUDIORT_NATIVE_PLAN.md.
//
// EVERYTHING BECOMES float32, and the mixer never asks what it used to be.
// Decoded size is 4 bytes per sample per channel: a 22 kHz mono second is 88 KB,
// which is why the bank loads on demand rather than eagerly.

namespace MrpgWav {

struct Sound {
    float* data     = nullptr;   // interleaved, channels * frames floats
    int    channels = 0;
    int    rate     = 0;         // as authored; the mixer resamples
    int    frames   = 0;
    bool   ok       = false;
};

// Reads and decodes. On failure `ok` is false and `why` says something a person
// can act on. Never throws, never partially fills: a Sound is usable or it is
// empty.
Sound Load(const char* path, char* why, int whyLen);

void Free(Sound& s);

} // namespace MrpgWav
