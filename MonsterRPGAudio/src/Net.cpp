#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#include <stdio.h>
#include <string.h>

#include "Net.hpp"
#include "SfxWire.h"
#include "MrpgCrypto.h"
#include "Log.hpp"
#include "Cfg.hpp"
#include "Audio.hpp"
#include "Capture.hpp"

namespace MrpgNet {

namespace {

// One second between HELLOs, against a server-side TTL of three. Three missed
// beats before a player is dropped back to stock audio is the same ratio the
// existing WEB heartbeat uses, and for the same reason: the cost of a false drop
// is a second of stock audio, and the cost of a missed drop is silence.
const DWORD HELLO_MS = 1000;

// recvfrom timeout. Short enough that Release() never waits long, long enough
// that an idle client is not spinning.
const DWORD RECV_TIMEOUT_MS = 250;

// Per-sound logging is a debugging tool, not a feature: a busy fight is dozens
// of records a second and would bury everything else in the file.
const unsigned int LOG_FIRST_N_RECORDS = 12;

volatile LONG s_running   = 0;
volatile LONG s_connected = 0;
volatile LONG s_canPlay   = 0;

HANDLE      s_thread   = nullptr;
SOCKET      s_sock     = INVALID_SOCKET;
bool        s_wsaUp    = false;
sockaddr_in s_server;
char        s_serverIp[64] = {0};
int         s_serverPort   = 0;

volatile LONG s_blId        = 0;
volatile LONG s_manifestVer = 0;

// ── Crypto state ─────────────────────────────────────────────────────────────
//
// Touched only by Connect (before the thread starts), by Release (after it
// stops) and by the thread itself. No lock, because no two of those overlap —
// which is a property to preserve, not a lucky accident.
MrpgCrypto::SessionKeys  s_keys;
MrpgCrypto::ReplayWindow s_replay;
mrpg_u64                 s_sendCounter = 0;

// Counters. Relaxed by nature — diagnostics, never control flow.
volatile LONG s_helloSent  = 0;
volatile LONG s_sfxDgrams  = 0;
volatile LONG s_sfxRecords = 0;
volatile LONG s_musDgrams  = 0;
volatile LONG s_badDgrams  = 0;   // malformed, wrong magic/version, bad length
volatile LONG s_forged     = 0;   // MAC failed or replayed: someone is lying
volatile LONG s_foreign    = 0;   // right port, wrong sender address
volatile LONG s_welcomes   = 0;
volatile LONG s_namesRecv  = 0;
volatile LONG s_lastRecvMs = 0;

unsigned int s_loggedRecords = 0;

// ── Packet building ──────────────────────────────────────────────────────────
//
// Header in the clear, body encrypted, MAC over both. Returns total length, or
// 0 if it would not fit.
int BuildPacket(mrpg_u8 op, mrpg_u8 count, const void* body, int bodyLen,
                char* out, int outMax)
{
    const int total = (int)sizeof(MrpgWireHeader) + bodyLen + MRPGAUDIO_MAC_BYTES;
    if (total > outMax) return 0;

    MrpgWireHeader h;
    memset(&h, 0, sizeof(h));
    h.magic   = MRPGAUDIO_WIRE_MAGIC;
    h.version = MRPGAUDIO_WIRE_VERSION;
    h.op      = op;
    h.count   = count;
    h.blId    = (mrpg_u32)InterlockedCompareExchange(&s_blId, 0, 0);

    // Counters start at 1: zero is reserved as "never seen" by the replay
    // window, so a packet numbered 0 is always rejected.
    h.counter = ++s_sendCounter;

    memcpy(out, &h, sizeof(h));
    if (bodyLen > 0) memcpy(out + sizeof(h), body, bodyLen);

    mc_u8 nonce[12];
    MrpgCrypto::NonceFromCounter(h.counter, nonce);
    if (bodyLen > 0)
        MrpgCrypto::ChaCha20Xor(s_keys.c2sEnc, 0, nonce,
                                (mc_u8*)(out + sizeof(h)), (unsigned int)bodyLen);

    mc_u8 mac[32];
    MrpgCrypto::HmacSha256_2(s_keys.c2sMac, 32,
                             out, sizeof(h),
                             out + sizeof(h), (unsigned int)bodyLen, mac);
    memcpy(out + sizeof(h) + bodyLen, mac, MRPGAUDIO_MAC_BYTES);

    return total;
}

void SendHello()
{
    if (s_sock == INVALID_SOCKET || !s_keys.valid) return;

    MrpgWireHello b;
    memset(&b, 0, sizeof(b));
    b.manifestVer = (mrpg_u32)InterlockedCompareExchange(&s_manifestVer, 0, 0);

    // The interlock. Phase 2 always reports 0 here, so a server that has already
    // learned to route natively keeps this player on stock audio rather than
    // sending sounds to a client that cannot make a noise. See SfxWire.h.
    // PHASE 3: this is no longer hardcoded to 0. It is the live answer from the
    // mixer - device open AND at least one sound decoded. The server routes
    // audio away from the engine only for clients that say yes, so saying yes
    // early does not delay sounds, it LOSES them.
    b.caps = MrpgAudio::CanPlay() ? (mrpg_u8)MRPGWIRE_CAP_CAN_PLAY : (mrpg_u8)0;

    char pkt[MRPGWIRE_MAX_PACKET];
    int n = BuildPacket(MRPGWIRE_OP_HELLO, 0, &b, sizeof(b), pkt, sizeof(pkt));
    if (n <= 0) return;

    sendto(s_sock, pkt, n, 0, (sockaddr*)&s_server, sizeof(s_server));
    InterlockedIncrement(&s_helloSent);
}

void SendBye()
{
    if (s_sock == INVALID_SOCKET || !s_keys.valid) return;

    MrpgWireHello b;
    memset(&b, 0, sizeof(b));

    char pkt[MRPGWIRE_MAX_PACKET];
    int n = BuildPacket(MRPGWIRE_OP_BYE, 0, &b, sizeof(b), pkt, sizeof(pkt));
    if (n <= 0) return;

    // Best effort, once. If it is lost the server's TTL catches it three seconds
    // later; the only cost of the loss is those three seconds of silence, which
    // is exactly what the TTL exists to bound.
    sendto(s_sock, pkt, n, 0, (sockaddr*)&s_server, sizeof(s_server));
}

// ── Receiving ────────────────────────────────────────────────────────────────

void HandleSfx(const MrpgWireHeader& h, const char* body, int bodyLen)
{
    const int n = (int)h.count;
    if (n <= 0 || n > MRPGWIRE_MAX_RECORDS) {
        InterlockedIncrement(&s_badDgrams);
        return;
    }
    // Length must match the count EXACTLY. A short buffer read as N records is
    // the class of bug the ASCII path shipped for months, reading whatever was
    // past the end and presenting it as acoustics.
    if (bodyLen != n * (int)sizeof(MrpgWireSfx)) {
        InterlockedIncrement(&s_badDgrams);
        MrpgLog::Write("net: SFX datagram says %d records but carries %d bytes (expected %d)",
                       n, bodyLen, n * (int)sizeof(MrpgWireSfx));
        return;
    }

    InterlockedIncrement(&s_sfxDgrams);
    InterlockedExchangeAdd(&s_sfxRecords, n);

    for (int i = 0; i < n; ++i) {
        MrpgWireSfx r;
        memcpy(&r, body + i * (int)sizeof(MrpgWireSfx), sizeof(r));

        // PHASE 3: this is where a datagram becomes a sound. Submit never
        // blocks and never allocates - it writes one command into a lock-free
        // ring that the audio thread drains.
        MrpgAudio::Submit(r);

        if (s_loggedRecords < LOG_FIRST_N_RECORDS) {
            ++s_loggedRecords;
            MrpgLog::Write("net:   sfx seq=%u id=%u flags=%02X pos=(%.1f %.1f %.1f) "
                           "vol=%.2f pitch=%.2f occ=%.2f encl=%u mfp=%.1f loop=%u",
                           (unsigned)r.seq, (unsigned)r.nameId, (unsigned)r.flags,
                           r.x, r.y, r.z, r.vol, r.pitch,
                           r.occlusion / 255.0f, (unsigned)r.enclosure,
                           r.meanFreePathCm / 100.0f, (unsigned)r.loopHandle);
            if (s_loggedRecords == LOG_FIRST_N_RECORDS)
                MrpgLog::Write("net:   (further sound records will not be logged individually)");
        }
    }
}

// id -> name, walked rather than indexed because the records are variable
// length. Every bound is checked against the ACTUAL body length: a malformed
// length byte must run off the end of the loop, not off the end of the buffer.
void HandleNames(const MrpgWireHeader& h, const char* body, int bodyLen)
{
    int off = 0, taken = 0;
    for (int i = 0; i < (int)h.count; ++i) {
        if (off + (int)sizeof(MrpgWireNameHdr) > bodyLen) break;

        MrpgWireNameHdr nh;
        memcpy(&nh, body + off, sizeof(nh));
        off += (int)sizeof(nh);

        if (nh.len == 0 || nh.len > MRPGWIRE_NAMES_MAX_LEN) break;
        if (off + nh.len > bodyLen) break;

        char name[MRPGWIRE_NAMES_MAX_LEN + 1];
        memcpy(name, body + off, nh.len);
        name[nh.len] = '\0';
        off += nh.len;

        MrpgAudio::MapId(nh.id, name);
        ++taken;
    }

    if (taken > 0) {
        InterlockedExchangeAdd(&s_namesRecv, (LONG)taken);
        // Kick the loader after every batch rather than waiting for the last
        // one: the earlier the common sounds are decoded, the earlier CAN_PLAY
        // can go true, and a batch that never arrives must not strand the rest.
        MrpgAudio::BeginPreload();
    }
}

void HandleWelcome(const char* body, int bodyLen)
{
    if (bodyLen < (int)sizeof(MrpgWireWelcome)) {
        InterlockedIncrement(&s_badDgrams);
        return;
    }
    MrpgWireWelcome w;
    memcpy(&w, body, sizeof(w));

    InterlockedIncrement(&s_welcomes);

    if (InterlockedCompareExchange(&s_connected, 1, 0) == 0)
        MrpgLog::Write("net: server answered and its MAC verified - secure link is up"
                       " (its manifest version %u)", (unsigned)w.manifestVer);

    const unsigned int mine = (unsigned int)InterlockedCompareExchange(&s_manifestVer, 0, 0);
    if (w.manifestVer != mine) {
        // Not fatal in Phase 2 — there is no sample bank to be wrong yet. It is
        // logged because in Phase 3 a nameId resolved against the wrong manifest
        // plays the WRONG SOUND, which is far harder to notice than silence.
        MrpgLog::Write("net: manifest mismatch - server %u, we have %u. Sound ids cannot"
                       " be trusted until this agrees.", (unsigned)w.manifestVer, mine);
    }
}

DWORD WINAPI ThreadMain(LPVOID)
{
    MrpgLog::Write("net: thread up (tid %lu), talking to %s:%d",
                   GetCurrentThreadId(), s_serverIp, s_serverPort);

    DWORD lastHello = 0;
    DWORD lastSummary = GetTickCount();
    LONG  lastSummaryRecords = 0;

    while (InterlockedCompareExchange(&s_running, 1, 1)) {
        DWORD now = GetTickCount();

        if (now - lastHello >= HELLO_MS || lastHello == 0) {
            lastHello = now;
            SendHello();
        }

        char        buf[MRPGWIRE_MAX_PACKET];
        sockaddr_in from;
        int         fromLen = sizeof(from);

        int got = recvfrom(s_sock, buf, sizeof(buf), 0, (sockaddr*)&from, &fromLen);
        if (got == SOCKET_ERROR) {
            int err = WSAGetLastError();
            if (err == WSAETIMEDOUT || err == WSAEWOULDBLOCK) continue;
            if (!InterlockedCompareExchange(&s_running, 1, 1)) break;
            if (err == WSAECONNRESET) {
                MrpgLog::Write("net: server port unreachable (still starting up?)");
                Sleep(200);
                continue;
            }
            MrpgLog::Write("net: recvfrom failed (WSA %d); stopping", err);
            break;
        }

        // THE SOURCE ADDRESS IS NOT CHECKED HERE, AND THAT IS A FIX.
        //
        // This used to drop anything whose source did not match the address we
        // sent to. The comment beside it already said the right thing - source
        // addresses are forgeable, the MAC is the security boundary, this is only
        // a cost filter - and then the cost filter threw away every legitimate
        // reply on a real network:
        //
        //   the client joins via the PUBLIC address and sends to 108.203.43.243
        //   the server sits on the SAME LAN and its replies arrive from
        //   192.168.1.110, so every one was counted foreign and discarded
        //
        // The server saw the HELLOs (its counter kept climbing) and answered
        // every one; the client rejected all of them and sat there with a link
        // that looked up and delivered nothing. The same break happens on any
        // multi-homed server, and on hairpin NAT generally.
        //
        // So the MAC decides, exactly as the old comment claimed. A forged packet
        // costs one HMAC and is discarded; that is what the HMAC is for.

        const int minLen = (int)sizeof(MrpgWireHeader) + MRPGAUDIO_MAC_BYTES;
        if (got < minLen) { InterlockedIncrement(&s_badDgrams); continue; }

        MrpgWireHeader h;
        memcpy(&h, buf, sizeof(h));

        if (h.magic != MRPGAUDIO_WIRE_MAGIC) { InterlockedIncrement(&s_badDgrams); continue; }

        if (h.version != MRPGAUDIO_WIRE_VERSION) {
            InterlockedIncrement(&s_badDgrams);
            static bool once = false;
            if (!once) {
                once = true;
                MrpgLog::Write("net: WIRE VERSION MISMATCH - server speaks %u, this build speaks %u.",
                               (unsigned)h.version, (unsigned)MRPGAUDIO_WIRE_VERSION);
                MrpgLog::Write("net:   Nothing will be played. Update MonsterRPGAudio.");
            }
            continue;
        }

        const int cipherLen = got - (int)sizeof(MrpgWireHeader) - MRPGAUDIO_MAC_BYTES;
        if (cipherLen < 0) { InterlockedIncrement(&s_badDgrams); continue; }

        // ── VERIFY BEFORE DECRYPT ────────────────────────────────────────────
        //
        // This order is the whole point of encrypt-then-MAC. A forged or
        // tampered packet is discarded here, having never been decrypted and
        // never been parsed as anything.
        mc_u8 want[32];
        MrpgCrypto::HmacSha256_2(s_keys.s2cMac, 32,
                                 buf, sizeof(MrpgWireHeader),
                                 buf + sizeof(MrpgWireHeader), (unsigned int)cipherLen,
                                 want);
        const mc_u8* givenMac = (const mc_u8*)(buf + sizeof(MrpgWireHeader) + cipherLen);
        if (!MrpgCrypto::MacEqual(want, givenMac, MRPGAUDIO_MAC_BYTES)) {
            InterlockedIncrement(&s_forged);
            static bool once = false;
            if (!once) {
                once = true;
                MrpgLog::Write("net: a packet from the server address FAILED AUTHENTICATION.");
                MrpgLog::Write("net:   Either something is forging traffic to this port, or the");
                MrpgLog::Write("net:   session key disagrees. Dropping it and every one like it.");
            }
            continue;
        }

        // The address is LEARNED from an authenticated packet, never from an
        // unauthenticated one - the same rule the server applies to us. This is
        // what makes hairpin NAT, multi-homed servers and a roaming route all
        // work without any of them being special cases.
        if (from.sin_addr.s_addr != s_server.sin_addr.s_addr
            || from.sin_port != s_server.sin_port) {
            static bool once = false;
            if (!once) {
                once = true;
                char oldIp[32], newIp[32];
                lstrcpynA(oldIp, inet_ntoa(s_server.sin_addr), sizeof(oldIp));
                lstrcpynA(newIp, inet_ntoa(from.sin_addr), sizeof(newIp));
                MrpgLog::Write("net: server answers from %s:%d though we send to %s:%d"
                               " - adopting it (authenticated).",
                               newIp, (int)ntohs(from.sin_port),
                               oldIp, (int)ntohs(s_server.sin_port));
            }
            InterlockedIncrement(&s_foreign);   // still counted, no longer fatal
            s_server.sin_addr = from.sin_addr;
            s_server.sin_port = from.sin_port;
        }

        // Replay, checked after the MAC so a flood of replayed-but-valid packets
        // is the only thing that can reach it.
        if (!MrpgCrypto::ReplayCheck(s_replay, h.counter)) {
            InterlockedIncrement(&s_forged);
            continue;
        }

        char* body = buf + sizeof(MrpgWireHeader);
        if (cipherLen > 0) {
            mc_u8 nonce[12];
            MrpgCrypto::NonceFromCounter(h.counter, nonce);
            MrpgCrypto::ChaCha20Xor(s_keys.s2cEnc, 0, nonce,
                                    (mc_u8*)body, (unsigned int)cipherLen);
        }

        InterlockedExchange(&s_lastRecvMs, (LONG)now);

        switch (h.op) {
            case MRPGWIRE_OP_WELCOME: HandleWelcome(body, cipherLen); break;
            case MRPGWIRE_OP_SFX:     HandleSfx(h, body, cipherLen);  break;
            case MRPGWIRE_OP_NAMES:   HandleNames(h, body, cipherLen); break;
            case MRPGWIRE_OP_MUS:
                InterlockedIncrement(&s_musDgrams);
                break;                                     // Phase 6
            default:
                InterlockedIncrement(&s_badDgrams);
                break;
        }

        if (now - lastSummary >= 30000) {
            LONG recs = InterlockedCompareExchange(&s_sfxRecords, 0, 0);
            MrpgLog::Write("net: 30 s summary - %ld sound records (%ld new), %ld hellos, "
                           "%ld bad, %ld forged, %ld foreign",
                           recs, recs - lastSummaryRecords,
                           InterlockedCompareExchange(&s_helloSent, 0, 0),
                           InterlockedCompareExchange(&s_badDgrams, 0, 0),
                           InterlockedCompareExchange(&s_forged, 0, 0),
                           InterlockedCompareExchange(&s_foreign, 0, 0));
            lastSummary = now;
            lastSummaryRecords = recs;
        }
    }

    MrpgLog::Write("net: thread down");
    return 0;
}

} // namespace

// ── Lifecycle ────────────────────────────────────────────────────────────────

void Init(const char*)
{
    memset(&s_keys, 0, sizeof(s_keys));
    MrpgCrypto::ReplayInit(s_replay);
    // Deliberately nothing observable: no winsock, no socket, no thread. A
    // player sitting on a vanilla server must be able to see this module has
    // done nothing at all.
}

bool Connect(const char* ip, int port, const char* keyHex,
             unsigned int blId, unsigned int manifestVer)
{
    if (!ip || !*ip || port <= 0 || port > 65535) {
        MrpgLog::Write("net: refusing a bad endpoint (%s:%d)", ip ? ip : "(null)", port);
        return false;
    }

    // THE CRYPTO IS CHECKED BEFORE IT IS TRUSTED. If the primitives do not
    // reproduce the published test vectors on this machine, the link does not
    // start — running with crypto that does not work is worse than not running,
    // because it looks identical from the outside.
    if (!MrpgCrypto::SelfTest()) {
        MrpgLog::Write("net: CRYPTO SELF TEST FAILED. Refusing to open the link.");
        MrpgLog::Write("net:   This build is broken; please report it.");
        return false;
    }

    mc_u8 sessionKey[MRPGAUDIO_KEY_BYTES];
    if (MrpgCrypto::FromHex(keyHex, sessionKey, MRPGAUDIO_KEY_BYTES) != MRPGAUDIO_KEY_BYTES) {
        MrpgLog::Write("net: the server sent a session key that is not %d hex characters.",
                       MRPGAUDIO_KEY_HEXLEN);
        MrpgLog::Write("net:   Refusing to connect unauthenticated.");
        return false;
    }

    // A re-invite for the same endpoint refreshes the key. Tearing the socket
    // down and rebuilding it would drop every live loop, and a server that
    // re-sends its invite (on respawn, on a mission change) has not asked for
    // that.
    //
    // NOTE the counters and the replay window reset with the key, because they
    // must: a new key means a new nonce space, and carrying a stale replay
    // window into it would reject the new session's first 64 packets.
    if (InterlockedCompareExchange(&s_running, 1, 1)
        && !strcmp(s_serverIp, ip) && s_serverPort == port) {
        MrpgCrypto::DeriveKeys(sessionKey, MRPGAUDIO_KEY_BYTES, s_keys);
        MrpgCrypto::ReplayInit(s_replay);
        s_sendCounter = 0;
        InterlockedExchange(&s_blId, (LONG)blId);
        InterlockedExchange(&s_manifestVer, (LONG)manifestVer);
        MrpgLog::Write("net: re-invited by the same server; session key rolled");
        return true;
    }

    if (InterlockedCompareExchange(&s_running, 1, 1))
        Release("a different server invited us");

    WSADATA wsa;
    if (!s_wsaUp) {
        int rc = WSAStartup(MAKEWORD(2, 2), &wsa);
        if (rc != 0) {
            MrpgLog::Write("net: WSAStartup failed (%d)", rc);
            return false;
        }
        s_wsaUp = true;
    }

    memset(&s_server, 0, sizeof(s_server));
    s_server.sin_family = AF_INET;
    s_server.sin_port   = htons((unsigned short)port);
    s_server.sin_addr.s_addr = inet_addr(ip);
    if (s_server.sin_addr.s_addr == INADDR_NONE) {
        MrpgLog::Write("net: '%s' is not a dotted-quad address.", ip);
        MrpgLog::Write("net:   getAddress() returns \"1.2.3.4:28000\" with NO 'IP:' prefix -"
                       " split on ':', do not use getWord.");
        return false;
    }

    s_sock = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
    if (s_sock == INVALID_SOCKET) {
        MrpgLog::Write("net: could not create a socket (WSA %d)", WSAGetLastError());
        return false;
    }

    // Bind to an EPHEMERAL port, not a fixed one.
    //
    // Several Blockland installs run on this machine at once and a fixed port
    // would have them stealing it from each other — which is precisely how the
    // voice socket's 28003 has already misbehaved. It is also why the player
    // never sees a firewall prompt: a client that speaks first and is replied to
    // on the same socket is solicited traffic under stateful filtering, and the
    // 1 Hz HELLO keeps that mapping alive.
    sockaddr_in local;
    memset(&local, 0, sizeof(local));
    local.sin_family      = AF_INET;
    local.sin_addr.s_addr = INADDR_ANY;
    local.sin_port        = 0;
    if (bind(s_sock, (sockaddr*)&local, sizeof(local)) == SOCKET_ERROR) {
        MrpgLog::Write("net: bind failed (WSA %d)", WSAGetLastError());
        closesocket(s_sock);
        s_sock = INVALID_SOCKET;
        return false;
    }

    DWORD tv = RECV_TIMEOUT_MS;
    setsockopt(s_sock, SOL_SOCKET, SO_RCVTIMEO, (const char*)&tv, sizeof(tv));

    // Stop WSAECONNRESET from a returned ICMP unreachable being reported on this
    // socket. Without it, a server that is not listening yet makes every
    // subsequent recvfrom fail rather than time out, and the link never
    // establishes even once the server does come up.
    {
        DWORD off = 0, ret = 0;
        const DWORD SIO_UDP_CONNRESET = 0x9800000C;
        WSAIoctl(s_sock, SIO_UDP_CONNRESET, &off, sizeof(off), nullptr, 0, &ret, nullptr, nullptr);
    }

    lstrcpynA(s_serverIp, ip, sizeof(s_serverIp));
    s_serverPort = port;

    MrpgCrypto::DeriveKeys(sessionKey, MRPGAUDIO_KEY_BYTES, s_keys);
    MrpgCrypto::ReplayInit(s_replay);
    s_sendCounter = 0;

    InterlockedExchange(&s_blId, (LONG)blId);
    InterlockedExchange(&s_manifestVer, (LONG)manifestVer);
    InterlockedExchange(&s_connected, 0);
    InterlockedExchange(&s_helloSent, 0);
    InterlockedExchange(&s_sfxDgrams, 0);
    InterlockedExchange(&s_sfxRecords, 0);
    InterlockedExchange(&s_musDgrams, 0);
    InterlockedExchange(&s_badDgrams, 0);
    InterlockedExchange(&s_forged, 0);
    InterlockedExchange(&s_foreign, 0);
    InterlockedExchange(&s_welcomes, 0);
    InterlockedExchange(&s_namesRecv, 0);
    s_loggedRecords = 0;

    InterlockedExchange(&s_running, 1);
    s_thread = CreateThread(nullptr, 0, ThreadMain, nullptr, 0, nullptr);
    if (!s_thread) {
        MrpgLog::Write("net: CreateThread failed (%lu)", GetLastError());
        InterlockedExchange(&s_running, 0);
        closesocket(s_sock);
        s_sock = INVALID_SOCKET;
        return false;
    }

    // The key is NEVER logged, not even truncated. A log is the one artefact a
    // player is asked to paste into a chat window when something goes wrong.
    // The microphone opens HERE, on joining a MonsterRPG server, and closes in
    // Release. Not at load, not for the life of the process.
    MrpgCapture::Init(nullptr);

    MrpgLog::Write("net: invited by %s:%d (blid %u, manifest %u, can_play %d), "
                   "session key accepted, ChaCha20 + HMAC-SHA256 armed",
                   ip, port, blId, manifestVer,
                   (int)InterlockedCompareExchange(&s_canPlay, 0, 0));
    return true;
}

void Release(const char* why)
{
    if (!InterlockedCompareExchange(&s_running, 1, 1)) return;

    MrpgLog::Write("net: releasing - %s", why ? why : "(no reason given)");

    SendBye();

    // The microphone goes first: whatever else fails below, it must not be left
    // open after the player has left.
    MrpgCapture::Shutdown();

    InterlockedExchange(&s_running, 0);

    if (s_thread) {
        if (WaitForSingleObject(s_thread, 3000) == WAIT_TIMEOUT)
            MrpgLog::Write("net: thread did not stop in 3 s; abandoning it");
        CloseHandle(s_thread);
        s_thread = nullptr;
    }

    if (s_sock != INVALID_SOCKET) {
        closesocket(s_sock);
        s_sock = INVALID_SOCKET;
    }

    if (s_wsaUp) {
        WSACleanup();
        s_wsaUp = false;
    }

    // Wipe the keys. They are dead the moment the session is, and a key sitting
    // in a process image is a key that can end up in a crash dump the player
    // uploads to a bug tracker.
    SecureZeroMemory(&s_keys, sizeof(s_keys));
    MrpgCrypto::ReplayInit(s_replay);
    s_sendCounter = 0;

    InterlockedExchange(&s_connected, 0);
    s_serverIp[0] = '\0';
    s_serverPort  = 0;
}

bool IsConnected()
{
    return InterlockedCompareExchange(&s_connected, 0, 0) != 0;
}

void SetCanPlay(bool canPlay)
{
    InterlockedExchange(&s_canPlay, canPlay ? 1 : 0);
}

const char* StatLine()
{
    static char out[256];

    LONG last = InterlockedCompareExchange(&s_lastRecvMs, 0, 0);
    LONG age  = last ? (LONG)(GetTickCount() - (DWORD)last) : -1;

    _snprintf(out, sizeof(out) - 1, "%d %ld %ld %ld %ld %ld %ld %ld %ld %ld",
              IsConnected() ? 1 : 0,
              (long)age,
              (long)InterlockedCompareExchange(&s_helloSent, 0, 0),
              (long)InterlockedCompareExchange(&s_sfxDgrams, 0, 0),
              (long)InterlockedCompareExchange(&s_sfxRecords, 0, 0),
              (long)InterlockedCompareExchange(&s_musDgrams, 0, 0),
              (long)InterlockedCompareExchange(&s_badDgrams, 0, 0),
              (long)InterlockedCompareExchange(&s_foreign, 0, 0),
              (long)InterlockedCompareExchange(&s_forged, 0, 0),
              (long)InterlockedCompareExchange(&s_namesRecv, 0, 0));
    out[sizeof(out) - 1] = '\0';
    return out;
}

} // namespace MrpgNet
