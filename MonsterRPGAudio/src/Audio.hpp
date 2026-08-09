#pragma once

#include "SfxWire.h"

// =============================================================================
// Audio — the WASAPI device, the mixer, and the sample bank
// =============================================================================
//
// Phase 3 of AUDIORT_NATIVE_PLAN.md. A sound record arrives from the server, and
// this turns it into something you can hear.
//
// ── THREE THREADS, AND THE RULES BETWEEN THEM ────────────────────────────────
//
//   NET THREAD     calls Submit(). Never blocks, never allocates, never touches
//                  the disk. It writes one command into a lock-free ring and
//                  returns.
//
//   AUDIO THREAD   WASAPI's. Drains the ring and mixes. MUST NOT allocate, lock,
//                  log, or touch the filesystem - every one of those can block
//                  for longer than a 5 ms buffer and the result is an audible
//                  dropout. This is the one thread in the project with a hard
//                  real-time deadline.
//
//   LOADER THREAD  decodes WAVs and publishes them into the bank with a release
//                  store. The audio thread reads that pointer with an acquire
//                  load and skips the voice if it is still null.
//
// That split is why the bank hands out raw pointers that are never freed while
// the device is running: freeing a sample the mixer might be reading is the one
// race that cannot be made safe cheaply, so samples live until Shutdown.
//
// ── WHAT IS DELIBERATELY NOT HERE YET ────────────────────────────────────────
//
// HRTF and the client-side head transform are PHASE 4. This phase pans in plain
// stereo from the server's world-space vector, and the plan says explicitly not
// to skip ahead: getting a sound to arrive, at the right time, at the right
// level, from the right file is four things that can each fail, and debugging
// them through an HRTF convolver is how a week disappears.
//
// Reverb is PHASE 5. Music is PHASE 6.

namespace MrpgAudio {

// Starts the device and the threads. False if there is no usable output device,
// which is not fatal to anything else - the player simply keeps stock audio.
bool Init(const char* dllDir);
void Shutdown();

bool IsRunning();

// ── The sample bank ──────────────────────────────────────────────────────────
//
// Two halves that meet in the middle:
//
//   MapProfile(name, path)   from the CLIENT's own TorqueScript, at invite time.
//                            The client enumerates its own AudioProfile
//                            datablocks, so the path is the one that exists on
//                            THIS machine rather than one the server guessed.
//
//   MapId(id, name)          from the SERVER, over the link. The wire carries a
//                            16-bit id, and this is what it means.
//
// id -> name -> path -> samples. Splitting it that way is what lets the server
// stay ignorant of where a client keeps its add-ons, and the client stay
// ignorant of how the server numbered its manifest.
void MapProfile(const char* name, const char* path);
void MapId(unsigned int id, const char* name);
void ClearMappings();
int  MappedProfiles();
int  MappedIds();

// Kicks off background loading of everything mapped so far, and reports true
// once enough is loaded to be worth announcing. See CanPlay below.
void BeginPreload();

// True when the device is up AND at least one sound is decoded and ready.
//
// THIS IS THE INTERLOCK THE WHOLE PHASE HANGS ON. It becomes the CAN_PLAY bit in
// the HELLO, and the server routes audio away from the engine only for clients
// that set it. Announcing readiness early does not delay sounds, it LOSES them -
// sfx.js learned exactly this and its comment says so.
bool CanPlay();

// One record from the server. Called on the net thread.
void Submit(const MrpgWireSfx& rec);

// The listener's own position, so a world-space source can be made relative.
// Phase 4 replaces this with a per-block read of the live camera; for now the
// client add-on pushes it and it is enough to pan by.
void SetListener(float x, float y, float z, float fwdX, float fwdY, float fwdZ);

// Plays a synthesised tone 2 m to the listener's left (-1), centre (0) or
// right (+1). Depends on no server, no network and no sample bank, so it splits
// "I hear nothing" cleanly in half: if this plays, everything downstream of the
// link works and the fault is upstream.
void PlayTestTone(float pan);

// "running device voices loaded pending played missed dropped underruns bankMB skipped"
// APPEND ONLY - read by getWord index on the script side.
const char* StatLine();

} // namespace MrpgAudio
