#pragma once

// =============================================================================
// Net — the link to the server, and nothing else
// =============================================================================
//
// One UDP socket, one thread. The thread sends a HELLO every second and blocks
// on recvfrom in between; everything that arrives is validated, counted, and
// handed to the (not yet written) audio engine.
//
// WHY UDP AND NOT THE GAME'S OWN CONNECTION. TorqueScript's commandToClient
// rides the same channel as brick and player state, is rate-limited to the
// server's packet rate, and is text. A loop pump at 20 Hz per emitter would be
// competing with the thing it is trying to describe. UDP also lets a dropped
// sound simply be dropped, which is the correct behaviour for audio and is
// exactly what the websocket path could not do — TCP head-of-line blocking makes
// one lost packet delay every sound behind it.
//
// WHY THE CLIENT SPEAKS FIRST. The player is behind NAT and the server cannot
// address them until they have sent something out. The HELLO is that something,
// and repeating it every second keeps the mapping alive. It is the same
// push-with-expiry shape as the existing "WEB <blid> <0|1>" heartbeat, and it
// inherits the property that matters: THE FLAG EXPIRES. A native client that
// crashes must fall back to stock audio within seconds, because the server has
// stopped calling Parent::play3D for it.
//
// NOTHING HERE STARTS AT LOAD. No socket exists until a MonsterRPG server sends
// an invite, and Release() puts it back exactly as it was. The client add-on
// loads on every server, so this module has to be able to prove it is asleep on
// all the others.

namespace MrpgNet {

// Called once from DllMain's thread. Does not touch winsock — just clears state.
void Init(const char* dllDir);

// Opens the socket and starts the thread. Idempotent: a second invite for the
// same endpoint refreshes the token and returns true without churning the
// socket, because a reconnect mid-session would drop every live loop.
//
// `ip` is dotted-quad, already stripped of the port. Note that
// ServerConnection.getAddress() returns "1.2.3.4:28000" with NO "IP:" prefix,
// so the caller must split on ':' — getWord(addr, 1) returns the PORT and has
// already shipped as a bug once in this tree.
// `keyHex` is the 32-character session key the server handed us over the game's
// own connection. It is the trust anchor for the whole UDP link — see
// MrpgCrypto.h — so a key that is not exactly MRPGAUDIO_KEY_HEXLEN clean hex
// characters is refused rather than connected unauthenticated.
bool Connect(const char* ip, int port, const char* keyHex,
             unsigned int blId, unsigned int manifestVer);

// Closes the socket and stops the thread. Sends a BYE first so the server drops
// the routing flag immediately rather than waiting out its TTL — during which
// the player would hear nothing at all.
void Release(const char* why);

bool IsConnected();

// "connected ageMs hellos sfxDgrams sfxRecords mus bad foreign forged"
// APPEND ONLY — read by getWord index on the script side.
//
// `forged` is the one worth watching: it counts packets that came from the
// server's address and FAILED AUTHENTICATION, or that replayed a counter already
// seen. On a healthy link it is zero forever. Anything else means something is
// injecting traffic, or the two sides disagree about the session key.
const char* StatLine();

// Set once the sample bank can actually turn a record into a sound. Until then
// HELLO advertises caps=0 and the server keeps playing that player's audio
// locally. Phase 3 is what calls this with true.
void SetCanPlay(bool canPlay);

} // namespace MrpgNet
