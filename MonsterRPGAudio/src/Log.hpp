#pragma once

// =============================================================================
// Log — MonsterRPGAudio.log, rewritten every launch
// =============================================================================
//
// FILE ONLY, AND MUTEX GUARDED, AND THAT IS NOT A STYLE CHOICE.
//
// Almost nothing in this DLL runs on the game thread. The readiness watcher, the
// UDP receiver and (from Phase 3) the WASAPI callback are all their own threads,
// and BlPrintf / BlCall are not thread safe — the server-side AudioRT worker has
// the same constraint and solves it the same way. So this is the only logging
// available anywhere in this module, and it must be safe to call from any thread
// at any time, including before the engine exists.
//
// It is also the ONLY diagnostic a player can send us. They will not have a
// debugger, they will not have the console open, and Blockland's own console.log
// belongs to the client and is destroyed by a client crash. This file is the
// record.

namespace MrpgLog {

// Truncates the log and writes the header. Safe to call before engine init.
// `dllDir` is the folder bin\MonsterRPGAudio.dll lives in; the log is written
// one level up, next to the .bat the player actually ran.
void Init(const char* dllDir);

void Write(const char* fmt, ...);

// Every launch ends with one of these so a log a player sends us always says
// how it finished, rather than just stopping.
void Close(const char* reason);

} // namespace MrpgLog
