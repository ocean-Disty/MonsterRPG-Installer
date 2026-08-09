#pragma once

// =============================================================================
// Cfg — MonsterRPGAudio.cfg, a plain KEY=VALUE text file
// =============================================================================
//
// Deliberately not TorqueScript prefs and deliberately not the client add-on's
// settings. The DLL owns its own mixer and must know how loud the player wants
// things WITHOUT a server being connected — and it has to be able to answer
// "why is this off?" while sitting on the main menu.
//
// It is also the escape hatch. §6.3 of AUDIORT_NATIVE_PLAN.md: the moment a
// player with a perfectly good card is refused by a name-parse bug, the fix must
// be one line in a text file, not a rebuild and a redistribution.

namespace MrpgCfg {

// Reads the file beside the .bat. Missing file is not an error — every key has a
// default and the file is a convenience, not a requirement. A player who deletes
// it gets working audio, not a refusal to start.
void Load(const char* dllDir);

// Both return the default when the key is absent or unparseable. An unparseable
// value is logged: a typo that silently becomes a default is how someone spends
// an evening wondering why their setting does nothing.
int   GetInt(const char* key, int defVal);
float GetFloat(const char* key, float defVal);

// Never null; returns defVal when absent. The returned pointer is owned by this
// module and stays valid until the next Load().
const char* GetStr(const char* key, const char* defVal);

} // namespace MrpgCfg
