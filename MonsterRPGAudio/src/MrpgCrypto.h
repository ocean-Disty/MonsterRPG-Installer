#pragma once

// =============================================================================
// MrpgCrypto.h — ChaCha20 + HMAC-SHA256 for the audio link
// =============================================================================
//
// SHARED FILE. Byte-identical copies live in:
//     Blockland\MonsterRPGAudio\src\MrpgCrypto.h        (client)
//     Blockland\Add-Ons\MONSTERRPG\MrpgCrypto.h         (server)
//
// ── WHAT THIS DEFENDS AGAINST, IN ORDER OF HOW MUCH IT MATTERS ───────────────
//
// 1. THE SERVER BECOMING A DDoS AMPLIFIER. This is the real one. A HELLO is 24
//    bytes and makes the server stream audio to whatever address it came from —
//    up to a kilobyte many times a second. Unauthenticated, that is a reflector
//    with a large amplification factor, and the victim is a third party who has
//    nothing to do with this game. The server must not send a single byte to an
//    address until it has seen a HELLO carrying a MAC only the real player could
//    have produced.
//
// 2. HIJACKING A PLAYER'S AUDIO. Spoof a HELLO for someone else's blId and their
//    sound routes to you: they go silent, and you get a machine-readable feed of
//    world positions near them.
//
// 3. INJECTING SOUNDS INTO A PLAYER'S GAME. Less serious, more obvious, and the
//    same fix.
//
// 4. Passive eavesdropping, which is last on purpose — see the honest limits
//    below.
//
// ── THE TRUST ANCHOR IS THE GAME'S OWN CONNECTION ────────────────────────────
//
// We do not need a key exchange, because we already have an authenticated
// channel to every client: Blockland's own TCP game connection. The server
// generates a random 16-byte session key and hands it to exactly one client over
// commandToClient. Nobody else on the UDP path ever sees it, and possession of
// it proves the sender is the player the server already believes them to be.
//
// That is what makes the UDP side safe with ~500 lines instead of a Diffie-
// Hellman implementation nobody here should be hand-rolling.
//
// ── HONEST LIMITS. READ THESE BEFORE CALLING IT "SECURE" ─────────────────────
//
// * Blockland's own connection is NOT encrypted, so somebody positioned to
//   sniff the path can capture the session key as it is delivered and then read
//   and forge our traffic. Closing that would need a real key exchange (X25519),
//   which is a large amount of hand-rolled field arithmetic and a much better
//   opportunity to introduce a subtle, silent bug than anything here.
//
//   That trade is deliberate and defensible: an attacker in that position can
//   already read the entire Blockland protocol in the clear, including every
//   player's position, which is strictly more than our audio stream leaks. The
//   goal is that this channel is NO WEAKER THAN THE GAME'S OWN — and it is now
//   considerably stronger, because it also gets integrity, replay protection and
//   anti-amplification, none of which the game itself has.
//
// * This is hand-written crypto. The mitigation is that both primitives are
//   standard, unmodified, and CHECKED AGAINST THE OFFICIAL TEST VECTORS at
//   startup (SelfTest below, RFC 8439 §2.4.2 and RFC 4231). If SelfTest fails
//   the link refuses to start rather than running with crypto that does not
//   work. Do not "optimise" anything in this file without re-running it.
//
// * Nonces are a per-session counter and MUST NOT repeat under one key. A fresh
//   session key per invite is what guarantees that, so an invite must never
//   reuse a key. See MakeSessionKey.
//
// ── CONSTRUCTION ─────────────────────────────────────────────────────────────
//
//   ENCRYPT-THEN-MAC, which is the order that is safe. The MAC covers the
//   header AND the ciphertext, and it is verified BEFORE anything is decrypted,
//   so a forged packet is discarded without its contents ever being parsed.
//
//   Four keys are derived from the one session key so that the two directions
//   never share a keystream:
//       kC2SEnc = HMAC(session, "mrpg-c2s-enc")   kC2SMac = HMAC(session, "mrpg-c2s-mac")
//       kS2CEnc = HMAC(session, "mrpg-s2c-enc")   kS2CMac = HMAC(session, "mrpg-s2c-mac")
//   HMAC-SHA256 emits exactly 32 bytes, which is exactly a ChaCha20 key.

#include <string.h>

#ifdef _MSC_VER
  typedef unsigned __int8  mc_u8;
  typedef unsigned __int32 mc_u32;
  typedef unsigned __int64 mc_u64;
#else
  #include <stdint.h>
  typedef uint8_t  mc_u8;
  typedef uint32_t mc_u32;
  typedef uint64_t mc_u64;
#endif

namespace MrpgCrypto {

// ── SHA-256 ──────────────────────────────────────────────────────────────────

struct Sha256Ctx {
    mc_u32 h[8];
    mc_u64 len;
    mc_u8  buf[64];
    unsigned int have;
};

inline mc_u32 Ror32(mc_u32 x, int n) { return (x >> n) | (x << (32 - n)); }

inline void Sha256Block(Sha256Ctx& c, const mc_u8* p)
{
    static const mc_u32 K[64] = {
        0x428a2f98,0x71374491,0xb5c0fbcf,0xe9b5dba5,0x3956c25b,0x59f111f1,0x923f82a4,0xab1c5ed5,
        0xd807aa98,0x12835b01,0x243185be,0x550c7dc3,0x72be5d74,0x80deb1fe,0x9bdc06a7,0xc19bf174,
        0xe49b69c1,0xefbe4786,0x0fc19dc6,0x240ca1cc,0x2de92c6f,0x4a7484aa,0x5cb0a9dc,0x76f988da,
        0x983e5152,0xa831c66d,0xb00327c8,0xbf597fc7,0xc6e00bf3,0xd5a79147,0x06ca6351,0x14292967,
        0x27b70a85,0x2e1b2138,0x4d2c6dfc,0x53380d13,0x650a7354,0x766a0abb,0x81c2c92e,0x92722c85,
        0xa2bfe8a1,0xa81a664b,0xc24b8b70,0xc76c51a3,0xd192e819,0xd6990624,0xf40e3585,0x106aa070,
        0x19a4c116,0x1e376c08,0x2748774c,0x34b0bcb5,0x391c0cb3,0x4ed8aa4a,0x5b9cca4f,0x682e6ff3,
        0x748f82ee,0x78a5636f,0x84c87814,0x8cc70208,0x90befffa,0xa4506ceb,0xbef9a3f7,0xc67178f2
    };

    mc_u32 w[64];
    for (int i = 0; i < 16; i++)
        w[i] = ((mc_u32)p[i*4] << 24) | ((mc_u32)p[i*4+1] << 16) |
               ((mc_u32)p[i*4+2] << 8) | (mc_u32)p[i*4+3];
    for (int i = 16; i < 64; i++) {
        mc_u32 s0 = Ror32(w[i-15], 7) ^ Ror32(w[i-15], 18) ^ (w[i-15] >> 3);
        mc_u32 s1 = Ror32(w[i-2], 17) ^ Ror32(w[i-2], 19) ^ (w[i-2] >> 10);
        w[i] = w[i-16] + s0 + w[i-7] + s1;
    }

    mc_u32 a=c.h[0],b=c.h[1],cc=c.h[2],d=c.h[3],e=c.h[4],f=c.h[5],g=c.h[6],h=c.h[7];
    for (int i = 0; i < 64; i++) {
        mc_u32 S1 = Ror32(e,6) ^ Ror32(e,11) ^ Ror32(e,25);
        mc_u32 ch = (e & f) ^ ((~e) & g);
        mc_u32 t1 = h + S1 + ch + K[i] + w[i];
        mc_u32 S0 = Ror32(a,2) ^ Ror32(a,13) ^ Ror32(a,22);
        mc_u32 mj = (a & b) ^ (a & cc) ^ (b & cc);
        mc_u32 t2 = S0 + mj;
        h=g; g=f; f=e; e=d+t1; d=cc; cc=b; b=a; a=t1+t2;
    }
    c.h[0]+=a; c.h[1]+=b; c.h[2]+=cc; c.h[3]+=d;
    c.h[4]+=e; c.h[5]+=f; c.h[6]+=g;  c.h[7]+=h;
}

inline void Sha256Init(Sha256Ctx& c)
{
    c.h[0]=0x6a09e667; c.h[1]=0xbb67ae85; c.h[2]=0x3c6ef372; c.h[3]=0xa54ff53a;
    c.h[4]=0x510e527f; c.h[5]=0x9b05688c; c.h[6]=0x1f83d9ab; c.h[7]=0x5be0cd19;
    c.len = 0; c.have = 0;
}

inline void Sha256Update(Sha256Ctx& c, const void* data, unsigned int n)
{
    const mc_u8* p = (const mc_u8*)data;
    c.len += n;
    while (n) {
        unsigned int take = 64 - c.have;
        if (take > n) take = n;
        memcpy(c.buf + c.have, p, take);
        c.have += take; p += take; n -= take;
        if (c.have == 64) { Sha256Block(c, c.buf); c.have = 0; }
    }
}

inline void Sha256Final(Sha256Ctx& c, mc_u8 out[32])
{
    mc_u64 bits = c.len * 8;
    mc_u8 pad = 0x80;
    Sha256Update(c, &pad, 1);
    mc_u8 zero = 0;
    while (c.have != 56) Sha256Update(c, &zero, 1);
    mc_u8 lenb[8];
    for (int i = 0; i < 8; i++) lenb[i] = (mc_u8)(bits >> (56 - i*8));
    Sha256Update(c, lenb, 8);
    for (int i = 0; i < 8; i++) {
        out[i*4]   = (mc_u8)(c.h[i] >> 24);
        out[i*4+1] = (mc_u8)(c.h[i] >> 16);
        out[i*4+2] = (mc_u8)(c.h[i] >> 8);
        out[i*4+3] = (mc_u8)(c.h[i]);
    }
}

inline void Sha256(const void* data, unsigned int n, mc_u8 out[32])
{
    Sha256Ctx c; Sha256Init(c); Sha256Update(c, data, n); Sha256Final(c, out);
}

// ── HMAC-SHA256 ──────────────────────────────────────────────────────────────

inline void HmacSha256(const mc_u8* key, unsigned int keyLen,
                       const void* msg, unsigned int msgLen, mc_u8 out[32])
{
    mc_u8 k[64];
    memset(k, 0, sizeof(k));
    if (keyLen > 64) Sha256(key, keyLen, k);
    else             memcpy(k, key, keyLen);

    mc_u8 ipad[64], opad[64];
    for (int i = 0; i < 64; i++) { ipad[i] = k[i] ^ 0x36; opad[i] = k[i] ^ 0x5c; }

    mc_u8 inner[32];
    Sha256Ctx c;
    Sha256Init(c); Sha256Update(c, ipad, 64); Sha256Update(c, msg, msgLen); Sha256Final(c, inner);
    Sha256Init(c); Sha256Update(c, opad, 64); Sha256Update(c, inner, 32);   Sha256Final(c, out);
}

// Two-part variant, so a header and a body can be MAC'd without first being
// copied into one buffer. Same result as concatenating them.
inline void HmacSha256_2(const mc_u8* key, unsigned int keyLen,
                         const void* a, unsigned int aLen,
                         const void* b, unsigned int bLen, mc_u8 out[32])
{
    mc_u8 k[64];
    memset(k, 0, sizeof(k));
    if (keyLen > 64) Sha256(key, keyLen, k);
    else             memcpy(k, key, keyLen);

    mc_u8 ipad[64], opad[64];
    for (int i = 0; i < 64; i++) { ipad[i] = k[i] ^ 0x36; opad[i] = k[i] ^ 0x5c; }

    mc_u8 inner[32];
    Sha256Ctx c;
    Sha256Init(c);
    Sha256Update(c, ipad, 64);
    Sha256Update(c, a, aLen);
    Sha256Update(c, b, bLen);
    Sha256Final(c, inner);

    Sha256Init(c); Sha256Update(c, opad, 64); Sha256Update(c, inner, 32); Sha256Final(c, out);
}

// ── ChaCha20 (RFC 8439) ──────────────────────────────────────────────────────

#define MC_QR(a,b,c,d) \
    a += b; d ^= a; d = (d << 16) | (d >> 16); \
    c += d; b ^= c; b = (b << 12) | (b >> 20); \
    a += b; d ^= a; d = (d << 8)  | (d >> 24); \
    c += d; b ^= c; b = (b << 7)  | (b >> 25);

inline void ChaCha20Block(const mc_u8 key[32], mc_u32 counter,
                          const mc_u8 nonce[12], mc_u8 out[64])
{
    mc_u32 s[16];
    s[0]=0x61707865; s[1]=0x3320646e; s[2]=0x79622d32; s[3]=0x6b206574;
    for (int i = 0; i < 8; i++)
        s[4+i] = (mc_u32)key[i*4] | ((mc_u32)key[i*4+1] << 8) |
                 ((mc_u32)key[i*4+2] << 16) | ((mc_u32)key[i*4+3] << 24);
    s[12] = counter;
    for (int i = 0; i < 3; i++)
        s[13+i] = (mc_u32)nonce[i*4] | ((mc_u32)nonce[i*4+1] << 8) |
                  ((mc_u32)nonce[i*4+2] << 16) | ((mc_u32)nonce[i*4+3] << 24);

    mc_u32 x[16];
    memcpy(x, s, sizeof(x));
    for (int i = 0; i < 10; i++) {
        MC_QR(x[0], x[4], x[ 8], x[12])
        MC_QR(x[1], x[5], x[ 9], x[13])
        MC_QR(x[2], x[6], x[10], x[14])
        MC_QR(x[3], x[7], x[11], x[15])
        MC_QR(x[0], x[5], x[10], x[15])
        MC_QR(x[1], x[6], x[11], x[12])
        MC_QR(x[2], x[7], x[ 8], x[13])
        MC_QR(x[3], x[4], x[ 9], x[14])
    }
    for (int i = 0; i < 16; i++) {
        mc_u32 v = x[i] + s[i];
        out[i*4]   = (mc_u8)(v);
        out[i*4+1] = (mc_u8)(v >> 8);
        out[i*4+2] = (mc_u8)(v >> 16);
        out[i*4+3] = (mc_u8)(v >> 24);
    }
}

// In-place stream cipher. Encryption and decryption are the same operation.
inline void ChaCha20Xor(const mc_u8 key[32], mc_u32 counter,
                        const mc_u8 nonce[12], mc_u8* data, unsigned int len)
{
    mc_u8 ks[64];
    unsigned int off = 0;
    while (off < len) {
        ChaCha20Block(key, counter++, nonce, ks);
        unsigned int n = len - off;
        if (n > 64) n = 64;
        for (unsigned int i = 0; i < n; i++) data[off + i] ^= ks[i];
        off += n;
    }
}

// ── Session keys ─────────────────────────────────────────────────────────────

struct SessionKeys {
    mc_u8 c2sEnc[32], c2sMac[32];
    mc_u8 s2cEnc[32], s2cMac[32];
    bool  valid;
};

inline void DeriveKeys(const mc_u8* sessionKey, unsigned int len, SessionKeys& out)
{
    HmacSha256(sessionKey, len, "mrpg-c2s-enc", 12, out.c2sEnc);
    HmacSha256(sessionKey, len, "mrpg-c2s-mac", 12, out.c2sMac);
    HmacSha256(sessionKey, len, "mrpg-s2c-enc", 12, out.s2cEnc);
    HmacSha256(sessionKey, len, "mrpg-s2c-mac", 12, out.s2cMac);
    out.valid = true;
}

// Nonce from the packet counter. The top four bytes are zero and the counter is
// little-endian in the low eight, so a counter that never repeats within a
// session gives a nonce that never repeats — which is the one property ChaCha20
// cannot survive losing.
inline void NonceFromCounter(mc_u64 counter, mc_u8 nonce[12])
{
    memset(nonce, 0, 12);
    for (int i = 0; i < 8; i++) nonce[4 + i] = (mc_u8)(counter >> (i * 8));
}

// Constant time. A byte-at-a-time memcmp that returns early leaks, through
// timing, how many leading bytes of a forged MAC were right — which is enough to
// forge one byte at a time.
inline bool MacEqual(const mc_u8* a, const mc_u8* b, unsigned int n)
{
    mc_u8 diff = 0;
    for (unsigned int i = 0; i < n; i++) diff |= (mc_u8)(a[i] ^ b[i]);
    return diff == 0;
}

// ── Replay window ────────────────────────────────────────────────────────────
//
// A 64-packet sliding window, the same shape IPsec uses. A plain "must be
// greater than the last one" rule would work on a perfect network and drop
// perfectly good audio on a real one, because UDP reorders.

struct ReplayWindow {
    mc_u64 highest;
    mc_u64 bitmap;
};

inline void ReplayInit(ReplayWindow& w) { w.highest = 0; w.bitmap = 0; }

// True if `counter` is fresh; marks it seen. False means replayed or too old.
inline bool ReplayCheck(ReplayWindow& w, mc_u64 counter)
{
    if (counter == 0) return false;                 // counters start at 1
    if (counter > w.highest) {
        // Bit 0 always means "the highest counter seen". Shifting left by the
        // jump keeps every older packet at its correct age, and the | 1 marks
        // this one as seen in the same step.
        mc_u64 shift = counter - w.highest;
        w.bitmap = (shift >= 64) ? 1 : ((w.bitmap << shift) | 1);
        w.highest = counter;
        return true;
    }
    mc_u64 age = w.highest - counter;
    if (age >= 64) return false;                    // too old to judge: refuse
    mc_u64 bit = (mc_u64)1 << age;
    if (w.bitmap & bit) return false;               // already seen
    w.bitmap |= bit;
    return true;
}

// ── Hex, for carrying the key through TorqueScript ───────────────────────────

inline void ToHex(const mc_u8* in, unsigned int n, char* out)
{
    static const char* d = "0123456789abcdef";
    for (unsigned int i = 0; i < n; i++) {
        out[i*2]   = d[in[i] >> 4];
        out[i*2+1] = d[in[i] & 15];
    }
    out[n*2] = '\0';
}

inline int HexVal(char c)
{
    if (c >= '0' && c <= '9') return c - '0';
    if (c >= 'a' && c <= 'f') return c - 'a' + 10;
    if (c >= 'A' && c <= 'F') return c - 'A' + 10;
    return -1;
}

// Returns the number of bytes written, or 0 if the string is not clean hex of
// the expected length. Strict on purpose: a key that silently half-parses gives
// a link that fails to authenticate for reasons nobody can see.
inline unsigned int FromHex(const char* hex, mc_u8* out, unsigned int outLen)
{
    if (!hex) return 0;
    unsigned int n = (unsigned int)strlen(hex);
    if (n != outLen * 2) return 0;
    for (unsigned int i = 0; i < outLen; i++) {
        int hi = HexVal(hex[i*2]), lo = HexVal(hex[i*2+1]);
        if (hi < 0 || lo < 0) return 0;
        out[i] = (mc_u8)((hi << 4) | lo);
    }
    return outLen;
}

// ── Self test, against the published vectors ─────────────────────────────────
//
// RFC 8439 §2.4.2 (ChaCha20) and RFC 4231 §4.2 (HMAC-SHA256), plus SHA256("abc")
// from FIPS 180-4. Called before the link starts; a failure refuses the link
// rather than running with crypto that does not work.
inline bool SelfTest()
{
    // SHA256("abc")
    {
        mc_u8 h[32];
        Sha256("abc", 3, h);
        static const mc_u8 want[32] = {
            0xba,0x78,0x16,0xbf,0x8f,0x01,0xcf,0xea,0x41,0x41,0x40,0xde,0x5d,0xae,0x22,0x23,
            0xb0,0x03,0x61,0xa3,0x96,0x17,0x7a,0x9c,0xb4,0x10,0xff,0x61,0xf2,0x00,0x15,0xad };
        if (memcmp(h, want, 32) != 0) return false;
    }

    // RFC 4231 test case 1: key = 0x0b x20, data = "Hi There"
    {
        mc_u8 key[20];
        memset(key, 0x0b, sizeof(key));
        mc_u8 mac[32];
        HmacSha256(key, 20, "Hi There", 8, mac);
        static const mc_u8 want[32] = {
            0xb0,0x34,0x4c,0x61,0xd8,0xdb,0x38,0x53,0x5c,0xa8,0xaf,0xce,0xaf,0x0b,0xf1,0x2b,
            0x88,0x1d,0xc2,0x00,0xc9,0x83,0x3d,0xa7,0x26,0xe9,0x37,0x6c,0x2e,0x32,0xcf,0xf7 };
        if (memcmp(mac, want, 32) != 0) return false;
    }

    // The two-part HMAC must equal the one-part HMAC over the concatenation.
    // This is the property the packet path actually relies on.
    {
        mc_u8 key[16];
        memset(key, 0x5a, sizeof(key));
        mc_u8 a[32];
        HmacSha256(key, 16, "HelloWorld", 10, a);
        mc_u8 b[32];
        HmacSha256_2(key, 16, "Hello", 5, "World", 5, b);
        if (memcmp(a, b, 32) != 0) return false;
    }

    // RFC 8439 §2.4.2: key 00..1f, nonce 00 00 00 00 00 00 00 4a 00 00 00 00,
    // counter 1, over the "Ladies and Gentlemen" plaintext.
    {
        mc_u8 key[32];
        for (int i = 0; i < 32; i++) key[i] = (mc_u8)i;
        mc_u8 nonce[12] = {0,0,0,0, 0,0,0,0x4a, 0,0,0,0};

        // "could", not "can". A stream cipher's first 16 output bytes depend
        // only on the first 16 plaintext bytes, so an earlier draft of this test
        // had the wrong plaintext AND only checked 16 bytes — and passed. The
        // whole 114-byte vector is checked now, which is the only version of
        // this test that can fail for the right reason.
        const char* pt = "Ladies and Gentlemen of the class of '99: If I could offer you "
                         "only one tip for the future, sunscreen would be it.";
        unsigned int n = (unsigned int)strlen(pt);
        if (n != 114) return false;

        mc_u8 buf[128];
        memcpy(buf, pt, n);
        ChaCha20Xor(key, 1, nonce, buf, n);

        static const mc_u8 want[114] = {
            0x6e,0x2e,0x35,0x9a,0x25,0x68,0xf9,0x80,0x41,0xba,0x07,0x28,0xdd,0x0d,0x69,0x81,
            0xe9,0x7e,0x7a,0xec,0x1d,0x43,0x60,0xc2,0x0a,0x27,0xaf,0xcc,0xfd,0x9f,0xae,0x0b,
            0xf9,0x1b,0x65,0xc5,0x52,0x47,0x33,0xab,0x8f,0x59,0x3d,0xab,0xcd,0x62,0xb3,0x57,
            0x16,0x39,0xd6,0x24,0xe6,0x51,0x52,0xab,0x8f,0x53,0x0c,0x35,0x9f,0x08,0x61,0xd8,
            0x07,0xca,0x0d,0xbf,0x50,0x0d,0x6a,0x61,0x56,0xa3,0x8e,0x08,0x8a,0x22,0xb6,0x5e,
            0x52,0xbc,0x51,0x4d,0x16,0xcc,0xf8,0x06,0x81,0x8c,0xe9,0x1a,0xb7,0x79,0x37,0x36,
            0x5a,0xf9,0x0b,0xbf,0x74,0xa3,0x5b,0xe6,0xb4,0x0b,0x8e,0xed,0xf2,0x78,0x5e,0x42,
            0x87,0x4d };
        if (memcmp(buf, want, 114) != 0) return false;

        // And it must round trip.
        ChaCha20Xor(key, 1, nonce, buf, n);
        if (memcmp(buf, pt, n) != 0) return false;
    }

    // Replay window behaviour, which is logic rather than crypto but is just as
    // able to silently drop every packet if it is wrong.
    {
        ReplayWindow w; ReplayInit(w);
        if (!ReplayCheck(w, 1))  return false;
        if ( ReplayCheck(w, 1))  return false;      // replay
        if (!ReplayCheck(w, 2))  return false;
        if (!ReplayCheck(w, 10)) return false;
        if (!ReplayCheck(w, 5))  return false;      // reordered, still fresh
        if ( ReplayCheck(w, 5))  return false;      // now a replay
        if (!ReplayCheck(w, 200))return false;
        if ( ReplayCheck(w, 10)) return false;      // fell out of the window
        if ( ReplayCheck(w, 0))  return false;      // zero is never valid
    }

    return true;
}

} // namespace MrpgCrypto
