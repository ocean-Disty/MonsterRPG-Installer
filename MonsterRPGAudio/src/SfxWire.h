#pragma once

// =============================================================================
// SfxWire.h — the datagram format between the server DLL and the client DLL
//
// SHARED FILE. Must be byte-identical in:
//     Blockland\MonsterRPGAudio\src\SfxWire.h          (client)
//     Blockland\Add-Ons\MONSTERRPG\SfxWire.h           (server)
//
// Two copies of a wire format that drift are the single most expensive bug this
// system can have, because the failure is not a crash — it is plausible numbers
// read from the wrong offsets. The sibling ASCII path already taught this: the
// RES message shipped for a long time with 24 printf specifiers for 21
// arguments, and the three orphans appended whatever was on the stack.
//
// WHICH IS WHY THERE IS A VERSION FIELD, CHECKED BEFORE ANYTHING ELSE. Bump
// MRPGAUDIO_WIRE_VERSION on ANY change to the layout or to the meaning of a
// field.
//
// ── EVERY PACKET IS ENCRYPTED AND AUTHENTICATED ──────────────────────────────
//
//     [ header, 24 bytes, plaintext but authenticated ]
//     [ body, ChaCha20 ciphertext                     ]
//     [ MAC, 16 bytes                                 ]
//
// ENCRYPT-THEN-MAC. The MAC covers the header AND the ciphertext and is verified
// BEFORE anything is decrypted, so a forged packet is discarded without its
// contents ever being parsed.
//
// THE POINT IS NOT PRIVACY, IT IS THAT AN UNAUTHENTICATED HELLO MAKES THE SERVER
// A DDoS AMPLIFIER. A HELLO is small and makes the server stream audio to
// whatever address it came from, many times a second, indefinitely. Without a
// MAC the server is a reflector pointed at any victim an attacker names — and
// that victim is a third party with nothing to do with this game. The server
// must not send one byte to an address until it has verified a MAC only the real
// player could produce.
//
// Second: without it, spoofing a HELLO for someone else's blId hijacks their
// audio — they go silent, and the attacker gets a machine-readable feed of world
// positions around them.
//
// See MrpgCrypto.h for the construction, the key derivation and, importantly,
// the honest limits of what this does and does not protect against.
//
// blId IS IN THE PLAINTEXT HEADER ON PURPOSE. The server has to know whose key
// to verify with before it can verify anything, and blId is not a secret — it is
// public in the player list. A forged blId simply fails the MAC.
// =============================================================================

#ifdef _MSC_VER
  typedef unsigned __int8  mrpg_u8;
  typedef unsigned __int16 mrpg_u16;
  typedef unsigned __int32 mrpg_u32;
  typedef unsigned __int64 mrpg_u64;
#else
  #include <stdint.h>
  typedef uint8_t  mrpg_u8;
  typedef uint16_t mrpg_u16;
  typedef uint32_t mrpg_u32;
  typedef uint64_t mrpg_u64;
#endif

// 'MRPA', little-endian. Not a security measure — it exists so a stray datagram
// from something else is discarded before we spend a HMAC on it.
#define MRPGAUDIO_WIRE_MAGIC   0x4150524Du

// 3: adds MRPGWIRE_OP_NAMES, so a client can learn what the 16-bit sound ids in
//    an SFX record actually refer to.
// 2: the whole packet is now encrypted and authenticated. Version 1 was
//    plaintext with a cleartext token and never left the test bench.
#define MRPGAUDIO_WIRE_VERSION 3

// Session key size, in bytes. 16 is 128 bits, which is beyond reach for a key
// that lives for one visit to one server. Carried to the client as 32 hex
// characters over commandToClient — see MrpgCrypto.h on why that channel is the
// trust anchor.
#define MRPGAUDIO_KEY_BYTES    16
#define MRPGAUDIO_KEY_HEXLEN   (MRPGAUDIO_KEY_BYTES * 2)

// Truncated HMAC-SHA256. 128 bits of authentication tag is the usual floor and
// is far more than a forger gets to attempt against a link that rekeys every
// session.
#define MRPGAUDIO_MAC_BYTES    16

// Opcodes.
#define MRPGWIRE_OP_HELLO      1   // client -> server, every second
#define MRPGWIRE_OP_BYE        2   // client -> server, on leave
#define MRPGWIRE_OP_WELCOME    3   // server -> client, acknowledges a HELLO
#define MRPGWIRE_OP_SFX        4   // server -> client, 1..N sound records
#define MRPGWIRE_OP_MUS        5   // server -> client, music state
#define MRPGWIRE_OP_NAMES      6   // server -> client, id -> sound name

// ── Why NAMES exists, and why it carries names rather than file paths ────────
//
// An SFX record identifies its sound with a 16-bit manifest id. The client has
// to turn that back into a file on ITS OWN disk, and the two halves of that come
// from opposite directions:
//
//   the SERVER knows   id -> datablock name     (it numbered the manifest)
//   the CLIENT knows   datablock name -> path   (it has the AudioProfile loaded,
//                                                or it could not be playing here)
//
// So the server sends names, never paths. A path is meaningless across machines:
// the same add-on can sit anywhere, and the server has no business guessing
// where. This also means a client missing an add-on simply has no path for that
// name and skips it, instead of being told to open a file that is not there.
//
// Sent once per session, right after WELCOME, and cheap enough not to bother
// compressing: ~1000 sounds at ~28 bytes is about 28 KB, or roughly 35 datagrams
// on a link that is idle at that moment anyway.

// Each NAMES record: id, length, then that many bytes of name. Not
// null-terminated and not fixed-width - a fixed 32-byte name field would waste
// most of a datagram, and a terminator would be one more thing to get wrong.
#define MRPGWIRE_NAMES_MAX_LEN 63

// HELLO capability bits.
//
// CAN_PLAY IS THE SAFETY INTERLOCK AND IT IS NOT OPTIONAL. The server stops
// calling Parent::play3D for a routed client, so if it routes to a client that
// cannot yet turn a datagram into a sound, that player goes silent — and the
// symptom is "MonsterRPG broke my audio", not "a phase is incomplete".
//
// Phase 2 sets this to 0 on purpose: the client receives, counts and logs, and
// plays nothing. The server must treat 0 as "keep playing this player's audio
// locally". Phase 3 sets it once the sample bank is loaded, which is also when
// sfx.js learned to announce readiness rather than presence.
#define MRPGWIRE_CAP_CAN_PLAY  0x01u

// Sound flags. Deliberately the same bit values as FLAG_* in AudioRT.cpp and
// SFX_FLAG_* in public/sfx.js, so all three can be read against each other.
#define MRPGWIRE_SFX_2D          0x01u
#define MRPGWIRE_SFX_LOOP_START  0x02u
#define MRPGWIRE_SFX_LOOP_UPDATE 0x04u
#define MRPGWIRE_SFX_LOOP_STOP   0x08u

// Records per datagram. 24 + 20*48 + 16 = 1000 bytes, comfortably inside any
// real path MTU, so a batched update can never be the thing that fragments.
#define MRPGWIRE_MAX_RECORDS   20

#pragma pack(push, 1)

// Every datagram starts with this, in the clear, and every byte of it is covered
// by the MAC.
struct MrpgWireHeader {
    mrpg_u32 magic;
    mrpg_u16 version;
    mrpg_u8  op;
    mrpg_u8  count;      // records following; 0 for HELLO/BYE/WELCOME
    mrpg_u32 blId;       // whose session key authenticates this packet
    mrpg_u32 _reserved;
    // Nonce source, and the one thing that must never repeat under one key.
    // Monotonic per direction per session; the receiver also runs it through a
    // 64-packet replay window. A fresh session key per invite is what guarantees
    // no reuse across sessions.
    mrpg_u64 counter;
};   // 24 bytes

struct MrpgWireHello {
    mrpg_u32 manifestVer;
    mrpg_u8  caps;       // MRPGWIRE_CAP_*
    mrpg_u8  _pad[7];
};   // 12 bytes

struct MrpgWireWelcome {
    mrpg_u32 manifestVer;   // what the server expects the client to have
    mrpg_u32 serverMs;      // the server's tick count, for a one-way delay estimate
    mrpg_u8  _pad[4];
};   // 12 bytes

// One sound, for one listener.
//
// POSITION IS WORLD SPACE, NOT HEAD-RELATIVE, and that is the whole point of the
// native path (decision D2 in AUDIORT_NATIVE_PLAN.md). The browser is sent a
// head-relative vector computed server-side from a 20 Hz pose plus network
// latency, so every sound swims for 60-120 ms when the listener turns. Here the
// client re-projects into head space per audio block from its own live camera,
// and direction becomes instant. Do not "simplify" this back to head-relative to
// match the ASCII path — the ASCII path is the one with the defect.
//
// The 8-bit quantisation of energy[] and occlusion is deliberate and safe.
// energy[] feeds acouSpectrumShape, which immediately normalises to its own peak
// and converts to dB against a -30 dB floor: 1/255 is about 0.03 dB at the top of
// that range and far under the floor at the bottom. occlusion is a crossfade
// coefficient, not a level.
struct MrpgWireSfx {
    mrpg_u8  flags;
    mrpg_u8  enclosure;        // 0..6
    mrpg_u16 nameId;           // manifest sound id. NOT the datablock string.
    mrpg_u32 seq;
    mrpg_u32 loopHandle;
    float    x, y, z;          // WORLD space
    float    vol;
    float    pitch;
    mrpg_u8  energy[4];        // /255 -> the 0..1 transfer function
    mrpg_u8  reflEnergy[4];
    mrpg_u8  occlusion;        // /255
    mrpg_u8  reflGain;         // /255
    mrpg_u8  reflCoverage;     // /255
    mrpg_u8  _pad;
    mrpg_u16 meanFreePathCm;   // centimetres, so 655 m fits in 16 bits
    mrpg_u16 effectiveDistCm;
};   // 48 bytes

// One id -> name pair. Variable length, so these are walked rather than indexed:
//     [u16 id][u8 len][len bytes of name]
// `count` in the header says how many follow.
struct MrpgWireNameHdr {
    mrpg_u16 id;
    mrpg_u8  len;
};   // 3 bytes, then `len` bytes of name

struct MrpgWireMus {
    mrpg_u16 trackId;          // 0xFFFF = stop
    mrpg_u8  intensity;        // /255
    mrpg_u8  combat;
    mrpg_u16 fadeMs;
    mrpg_u8  _pad[6];
};   // 12 bytes

#pragma pack(pop)

// Largest datagram either side ever builds or accepts.
#define MRPGWIRE_MAX_PACKET \
    (24 + MRPGWIRE_MAX_RECORDS * 48 + MRPGAUDIO_MAC_BYTES)

// Compile-time layout assertions. These are the whole reason the version field
// can be trusted: a struct that silently changed size on one side would
// otherwise be discovered by ear, months later, as "the acoustics went strange".
#if defined(__cplusplus) && __cplusplus >= 201103L
  static_assert(sizeof(MrpgWireHeader)  == 24, "MrpgWireHeader must be 24 bytes");
  static_assert(sizeof(MrpgWireHello)   == 12, "MrpgWireHello must be 12 bytes");
  static_assert(sizeof(MrpgWireWelcome) == 12, "MrpgWireWelcome must be 12 bytes");
  static_assert(sizeof(MrpgWireSfx)     == 48, "MrpgWireSfx must be 48 bytes");
  static_assert(sizeof(MrpgWireMus)     == 12, "MrpgWireMus must be 12 bytes");
  static_assert(sizeof(MrpgWireNameHdr) == 3,  "MrpgWireNameHdr must be 3 bytes");
  static_assert(MRPGWIRE_MAX_PACKET     == 1000, "packet budget moved; check the MTU note");
#endif
