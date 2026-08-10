#pragma once

// =============================================================================
// Devices — enumerate the machine's audio endpoints so a player can pick one
// =============================================================================
//
// Windows' "default device" is right most of the time and wrong exactly when it
// matters: a headset that shows up as the third playback device, a webcam
// microphone that steals the default, a virtual cable left over from streaming
// software. A player who cannot choose has no way out of that.
//
// ── IDENTIFIED BY ENDPOINT ID, NOT BY INDEX ──────────────────────────────────
//
// The saved choice is the endpoint's ID string, not its position in the list.
// Indices move: plugging in a USB headset renumbers everything after it, so an
// index saved today points at a different device tomorrow. The ID survives
// reboots, reorderings and driver updates.
//
// A saved device that is no longer present is not an error - it means the player
// unplugged their headset. That falls back to the system default and says so.

namespace MrpgDevices {

enum Kind { RENDER = 0, CAPTURE = 1 };

// Refreshes the cached list for one direction. Safe to call repeatedly; the
// player may plug something in while the menu is open.
int  Refresh(Kind kind);

int  Count(Kind kind);

// Empty string for an out-of-range index. Names can contain spaces, so anything
// reading these through TorqueScript must take the whole remainder of the line.
const char* Name(Kind kind, int index);
const char* Id(Kind kind, int index);

// True when this entry is what Windows currently considers the default.
bool IsDefault(Kind kind, int index);

} // namespace MrpgDevices
