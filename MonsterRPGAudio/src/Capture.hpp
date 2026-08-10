#pragma once

#include "VoiceCodec.h"

// =============================================================================
// Capture — the microphone, encoded and ready to send
// =============================================================================
//
// WASAPI capture on the default endpoint, downmixed to mono, resampled to
// 16 kHz, gated, and encoded into 20 ms ADPCM frames. Net drains the finished
// frames; nothing here knows the network exists.
//
// ── A MOD THAT OPENS YOUR MICROPHONE SHOULD SAY SO ───────────────────────────
//
// This is somebody's microphone, in a game modification, and the honest
// defaults follow from that rather than from convenience:
//
//   * OPT-IN. `Voice=0` in MonsterRPGAudio.cfg unless the player sets it.
//   * The device is opened only while actually on a MonsterRPG server, and
//     closed on the way out - not held for the life of the process.
//   * Opening and closing are stated plainly in the log, with the device name.
//   * Windows' own microphone privacy setting can refuse us, and that must be a
//     clear line in the log rather than a silent nothing.
//
// ── THE GATE IS NOT AN OPTIMISATION ──────────────────────────────────────────
//
// A room that is not being spoken in sends NOTHING. That keeps a server with
// twenty idle players at zero voice bandwidth, and it means an open microphone
// is not quietly broadcasting a living room. Hysteresis plus a hangover so a
// sentence is not chopped between words.

namespace MrpgCapture {

// Opens the capture device if the player has enabled voice. False is a normal
// outcome - voice off, no microphone, or Windows refusing - and never fatal.
bool Init(const char* dllDir);
void Shutdown();

// Turn voice on or off while the game is running, from the settings menu.
//
// This is what makes the microphone a UI decision rather than a text-file one.
// Enabling opens the device now; disabling closes it now - not "next launch",
// because a player switching their microphone off means now.
bool SetEnabled(bool on);
bool IsEnabled();

// Reopen capture on a specific endpoint, by ID. Empty means the system default.
bool SetInputDevice(const char* endpointId);
const char* CurrentInputName();

bool IsCapturing();

// Push-to-talk, driven by the player holding a key.
//
// PTT IS THE PRIMARY GATE, not an addition to the noise gate. A player holding
// the key has said "send this"; second-guessing them with a level threshold is
// how push-to-talk gets a reputation for eating the first word. The RMS gate
// stays only as a floor so a held key in a silent room still sends nothing.
//
// The microphone DEVICE is open for the whole visit, but nothing is encoded or
// sent unless the key is down. Opening the device per keypress would cost
// hundreds of milliseconds and clip the start of every sentence.
void SetPushToTalk(bool held);
bool IsPushToTalk();

// True while the gate is open, i.e. the player is actually talking. Cheap.
bool IsTalking();

// Moves up to `maxFrames` finished frames into `out`, which must have room for
// maxFrames * MRPGVOICE_ENC_BYTES. Returns how many were taken. Non-blocking;
// called from the net thread.
int TakeFrames(mv_u8* out, int maxFrames);

// "enabled capturing talking framesMade framesSent dropped rate ch"
// APPEND ONLY.
const char* StatLine();

} // namespace MrpgCapture
