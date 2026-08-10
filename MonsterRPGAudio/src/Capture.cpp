#define INITGUID
#include <windows.h>
#include <mmdeviceapi.h>
#include <audioclient.h>
#include <functiondiscoverykeys_devpkey.h>
#include <avrt.h>
#include <objbase.h>

#include <stdio.h>
#include <string.h>
#include <math.h>

#include <atomic>
#include <mutex>
#include <thread>
#include <vector>

#include "Capture.hpp"
#include "Cfg.hpp"
#include "Devices.hpp"
#include "Log.hpp"
#include <string>

namespace MrpgCapture {

namespace {

// KSDATAFORMAT_SUBTYPE_IEEE_FLOAT, spelled out for the same reason as in
// Audio.cpp: one 16-byte constant is not worth dragging ksmedia.h in for.
const GUID SUBTYPE_IEEE_FLOAT =
    { 0x00000003, 0x0000, 0x0010, { 0x80, 0x00, 0x00, 0xaa, 0x00, 0x38, 0x9b, 0x71 } };
const GUID SUBTYPE_PCM =
    { 0x00000001, 0x0000, 0x0010, { 0x80, 0x00, 0x00, 0xaa, 0x00, 0x38, 0x9b, 0x71 } };

// ── Gate tuning ──────────────────────────────────────────────────────────────
//
// Hysteresis: it takes more to open than to hold open. A single threshold
// chatters on and off through the quiet parts of a sentence, which sounds far
// worse than either state.
float s_gateOpen  = 0.020f;   // ~ -34 dBFS RMS
float s_gateClose = 0.008f;   // ~ -42 dBFS RMS

// ── THE PUSH-TO-TALK FLOOR IS A DIFFERENT NUMBER, AND IT HAS TO BE ───────────
//
// In open-mic mode the LEVEL is the decision, so it has to sit above room tone
// and a fan - hence 0.020/0.008. In push-to-talk the KEY is the decision, and the
// level is only there to stop a key held in a silent room from sending digital
// silence down the wire. Those are different jobs and they were sharing a number.
//
// Reusing s_gateClose set the bar at 0.008 RMS, which is roughly a raised voice
// into a close-mic headset. MEASURED on this machine, speaking normally into a
// BEHRINGER Line In while holding the key: 0.0007 to 0.0066 - every frame below
// the threshold, so the key was held, the microphone was open, the capture thread
// was running, and not one frame was ever transmitted. Nothing in the system was
// broken and nothing said so.
//
// 0.0015 is comfortably above true silence and far below any real speech at any
// sane input gain. A quiet input is a quiet input; it is not a reason to discard
// a player who is holding the key down and talking.
float s_pttFloor  = 0.0015f;  // ~ -56 dBFS RMS

// Keep sending this long after the level drops. Speech has gaps inside it -
// stops, breaths, the space between words - and cutting at every one of them
// turns a sentence into telegraphy.
int   s_hangoverMs = 400;

// Frames buffered for the net thread. 25 frames is half a second; past that the
// network is not keeping up and the oldest are dropped, because stale voice is
// worse than missing voice.
const int RING_FRAMES = 25;

std::atomic<bool> s_enabled{false};
std::atomic<bool> s_capturing{false};
std::atomic<bool> s_talking{false};
std::atomic<bool> s_ptt{false};
// Open-mic is opt-in on top of voice being on at all: two deliberate steps
// before a microphone transmits without the player holding anything.
std::atomic<bool> s_openMic{false};
std::atomic<bool> s_running{false};
std::thread       s_thread;
HANDLE            s_event = nullptr;

int s_devRate = 48000;
int s_devCh   = 1;

std::atomic<unsigned long long> s_framesMade{0};
std::atomic<unsigned long long> s_framesTaken{0};
std::atomic<unsigned long long> s_dropped{0};

std::mutex s_ringMutex;
mv_u8      s_ring[RING_FRAMES][MRPGVOICE_ENC_BYTES];
int        s_ringHead = 0;   // next write
int        s_ringCount = 0;

// ── INPUT LEVEL, FOR THE SETTINGS METER ──────────────────────────────────────
//
// Written by the capture thread every 20 ms frame, read by the game thread. It is
// NEVER control flow - it only draws a bar - so a torn or stale read costs one
// frame of a meter and nothing else.
//
// It exists because "I hold the key and nothing happens" has exactly two causes -
// the key not arriving, and the microphone being silent - and they are
// indistinguishable without one. The usual culprit is Windows handing us a
// virtual cable (Voicemeeter, a webcam, a streaming device) as the default
// communications endpoint: it opens, it captures, and it captures silence.
std::atomic<float> s_level{0.0f};

std::string s_wantDeviceId;
char        s_curDeviceName[192] = "(none)";
// Set by the settings menu; overrides the cfg once the player has chosen.
int         s_forceEnable = -1;      // -1 unset, 0 off, 1 on

IMMDeviceEnumerator* s_enum   = nullptr;
IMMDevice*           s_device = nullptr;
IAudioClient*        s_client = nullptr;
IAudioCaptureClient* s_capture = nullptr;

void PushFrame(const mv_u8* f)
{
    std::lock_guard<std::mutex> lock(s_ringMutex);
    if (s_ringCount >= RING_FRAMES) {
        // Drop the OLDEST. Voice is only useful while it is current; a backlog
        // played late is worse than a gap, because the talker has moved on.
        s_ringHead = (s_ringHead + 1) % RING_FRAMES;
        --s_ringCount;
        s_dropped.fetch_add(1, std::memory_order_relaxed);
    }
    const int slot = (s_ringHead + s_ringCount) % RING_FRAMES;
    memcpy(s_ring[slot], f, MRPGVOICE_ENC_BYTES);
    ++s_ringCount;
}

// ── The capture thread ───────────────────────────────────────────────────────

void ThreadMain()
{
    CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);

    DWORD taskIndex = 0;
    HANDLE mmcss = AvSetMmThreadCharacteristicsA("Audio", &taskIndex);

    // Resampler state. The device rate is whatever the endpoint runs at (48 k
    // usually, but it is not ours to choose), and we need exactly 16 k.
    const double step = (double)s_devRate / (double)MRPGVOICE_RATE;
    double  readPos   = 0.0;
    float   lp        = 0.0f;

    // One-pole lowpass before decimation. Without it, everything above 8 kHz
    // folds back into the speech band as aliasing - which sounds like a robot
    // and cannot be removed later. Crude, but it is the difference between
    // "telephone" and "broken".
    const float lpCoef = (float)(1.0 - exp(-2.0 * 3.14159265 * 3400.0 / s_devRate));

    std::vector<float> mono;        // device-rate mono, awaiting resample
    mono.reserve(s_devRate);

    mv_s16 frame[MRPGVOICE_FRAME];
    int    frameFill = 0;

    DWORD lastLoudMs = 0;
    bool  gateOpen   = false;

    while (s_running.load(std::memory_order_relaxed)) {
        if (WaitForSingleObject(s_event, 200) != WAIT_OBJECT_0) continue;
        if (!s_running.load(std::memory_order_relaxed)) break;

        for (;;) {
            UINT32 packet = 0;
            if (FAILED(s_capture->GetNextPacketSize(&packet)) || packet == 0) break;

            BYTE*  data  = nullptr;
            UINT32 frames = 0;
            DWORD  flags = 0;
            if (FAILED(s_capture->GetBuffer(&data, &frames, &flags, nullptr, nullptr))) break;

            const float* f = (const float*)data;
            for (UINT32 i = 0; i < frames; ++i) {
                float v = 0;
                if (!(flags & AUDCLNT_BUFFERFLAGS_SILENT) && data) {
                    // Downmix: a headset is usually mono already, but a stereo
                    // or array endpoint must be folded or one ear is discarded.
                    for (int c = 0; c < s_devCh; ++c) v += f[(size_t)i * s_devCh + c];
                    v /= (float)s_devCh;
                }
                lp += (v - lp) * lpCoef;
                mono.push_back(lp);
            }
            s_capture->ReleaseBuffer(frames);
        }

        // Resample what we have into 16 kHz frames.
        while (readPos + 1.0 < (double)mono.size()) {
            const int   i0   = (int)readPos;
            const float frac = (float)(readPos - i0);
            const float s    = mono[i0] * (1 - frac) + mono[i0 + 1] * frac;

            int v = (int)(s * 32767.0f);
            if (v >  32767) v =  32767;
            if (v < -32768) v = -32768;
            frame[frameFill++] = (mv_s16)v;

            readPos += step;

            if (frameFill == MRPGVOICE_FRAME) {
                frameFill = 0;

                double sum = 0;
                for (int k = 0; k < MRPGVOICE_FRAME; ++k) {
                    const double x = frame[k] / 32768.0;
                    sum += x * x;
                }
                const float rms = (float)sqrt(sum / MRPGVOICE_FRAME);
                s_level.store(rms, std::memory_order_relaxed);
                const DWORD now = GetTickCount();

                const bool ptt = s_ptt.load(std::memory_order_relaxed);

                if (!s_openMic.load(std::memory_order_relaxed)) {
                    // PUSH TO TALK. The key decides; the level only stops a
                    // held key in a silent room from sending digital silence.
                    gateOpen = ptt && (rms >= s_pttFloor);
                    if (gateOpen) lastLoudMs = now;
                } else {
                    if (rms >= s_gateOpen) { gateOpen = true;  lastLoudMs = now; }
                    else if (gateOpen && rms < s_gateClose
                             && (now - lastLoudMs) > (DWORD)s_hangoverMs) gateOpen = false;
                }

                s_talking.store(gateOpen, std::memory_order_relaxed);

                if (gateOpen) {
                    mv_u8 enc[MRPGVOICE_ENC_BYTES];
                    MrpgVoice::EncodeFrame(frame, enc);
                    PushFrame(enc);
                    s_framesMade.fetch_add(1, std::memory_order_relaxed);
                }
            }
        }

        // Retire consumed input. Keeping one sample back preserves the
        // interpolator's left neighbour across the splice; dropping it puts a
        // click at every buffer boundary.
        const int consumed = (int)readPos;
        if (consumed > 0) {
            mono.erase(mono.begin(), mono.begin() + consumed);
            readPos -= consumed;
        }
    }

    if (mmcss) AvRevertMmThreadCharacteristics(mmcss);
    CoUninitialize();
}

} // namespace

bool Init(const char* dllDir)
{
    (void)dllDir;
    if (s_running.load(std::memory_order_relaxed)) return true;

    // OPT-IN. Absent or 0 means the microphone is never opened at all - unless
    // the player has since turned it on in the settings menu, which is a more
    // recent and more deliberate statement than a file they may never have read.
    const int wantOn = (s_forceEnable >= 0) ? s_forceEnable : MrpgCfg::GetInt("Voice", 0);
    if (wantOn == 0) {
        MrpgLog::Write("voice: capture is off (Voice=0 in MonsterRPGAudio.cfg). "
                       "The microphone is not opened.");
        s_enabled.store(false);
        return false;
    }
    s_enabled.store(true);

    s_gateOpen   = MrpgCfg::GetFloat("VoiceGateOpen",  s_gateOpen);
    s_gateClose  = MrpgCfg::GetFloat("VoiceGateClose", s_gateClose);
    s_pttFloor   = MrpgCfg::GetFloat("VoicePttFloor",  s_pttFloor);
    s_hangoverMs = MrpgCfg::GetInt("VoiceHangoverMs",  s_hangoverMs);
    s_openMic.store(MrpgCfg::GetInt("VoiceOpenMic", 0) != 0);

    HRESULT hr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    const bool weInited = SUCCEEDED(hr);

    hr = CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_ALL,
                          __uuidof(IMMDeviceEnumerator), (void**)&s_enum);
    if (FAILED(hr) || !s_enum) {
        MrpgLog::Write("voice: no device enumerator (hr 0x%08lX)", (unsigned long)hr);
        if (weInited) CoUninitialize();
        return false;
    }

    if (!s_wantDeviceId.empty()) {
        int n = (int)MultiByteToWideChar(CP_UTF8, 0, s_wantDeviceId.c_str(), -1, nullptr, 0);
        if (n > 0) {
            std::vector<wchar_t> w((size_t)n);
            MultiByteToWideChar(CP_UTF8, 0, s_wantDeviceId.c_str(), -1, w.data(), n);
            if (FAILED(s_enum->GetDevice(w.data(), &s_device)) || !s_device) {
                MrpgLog::Write("voice: the chosen microphone is not present; using the default");
                s_device = nullptr;
            }
        }
    }

    if (!s_device) hr = s_enum->GetDefaultAudioEndpoint(eCapture, eCommunications, &s_device);
    if (FAILED(hr) || !s_device) {
        MrpgLog::Write("voice: no default microphone (hr 0x%08lX) - voice is off",
                       (unsigned long)hr);
        return false;
    }

    // Name the device in the log. If somebody is going to have a microphone
    // opened on their behalf, the record should say which one.
    {
        IPropertyStore* props = nullptr;
        if (SUCCEEDED(s_device->OpenPropertyStore(STGM_READ, &props)) && props) {
            PROPVARIANT v; PropVariantInit(&v);
            if (SUCCEEDED(props->GetValue(PKEY_Device_FriendlyName, &v)) && v.vt == VT_LPWSTR) {
                WideCharToMultiByte(CP_UTF8, 0, v.pwszVal, -1,
                                    s_curDeviceName, sizeof(s_curDeviceName), nullptr, nullptr);
                MrpgLog::Write("voice: microphone is \"%s\"", s_curDeviceName);
            }
            PropVariantClear(&v);
            props->Release();
        }
    }

    hr = s_device->Activate(__uuidof(IAudioClient), CLSCTX_ALL, nullptr, (void**)&s_client);
    if (FAILED(hr) || !s_client) {
        // AUDCLNT_E_DEVICE_IN_USE or an access denial from Windows' microphone
        // privacy setting both land here, and both deserve naming rather than a
        // silent absence of voice.
        MrpgLog::Write("voice: could not open the microphone (hr 0x%08lX).", (unsigned long)hr);
        MrpgLog::Write("voice:   If this is 0x80070005, Windows privacy settings are refusing");
        MrpgLog::Write("voice:   microphone access to this application.");
        return false;
    }

    WAVEFORMATEX* mix = nullptr;
    if (FAILED(s_client->GetMixFormat(&mix)) || !mix) {
        MrpgLog::Write("voice: GetMixFormat failed");
        return false;
    }

    // Same lesson as the output device: in shared mode, take the endpoint's own
    // format rather than inventing one. Inventing 2-channel 48 k float was
    // exactly what AUDCLNT_E_UNSUPPORTED_FORMAT rejected on the render side.
    s_devRate = (int)mix->nSamplesPerSec;
    s_devCh   = (int)mix->nChannels;

    bool isFloat = (mix->wFormatTag == WAVE_FORMAT_IEEE_FLOAT);
    if (mix->wFormatTag == WAVE_FORMAT_EXTENSIBLE
        && mix->cbSize >= sizeof(WAVEFORMATEXTENSIBLE) - sizeof(WAVEFORMATEX)) {
        const WAVEFORMATEXTENSIBLE* ext = (const WAVEFORMATEXTENSIBLE*)mix;
        isFloat = (memcmp(&ext->SubFormat, &SUBTYPE_IEEE_FLOAT, sizeof(GUID)) == 0);
    }
    if (!isFloat || mix->wBitsPerSample != 32) {
        MrpgLog::Write("voice: microphone mix format is not 32-bit float "
                       "(tag %u, %u bits) - voice is off",
                       (unsigned)mix->wFormatTag, (unsigned)mix->wBitsPerSample);
        CoTaskMemFree(mix);
        return false;
    }

    const REFERENCE_TIME dur = 200000;   // 20 ms; capture latency is not critical
    hr = s_client->Initialize(AUDCLNT_SHAREMODE_SHARED,
                              AUDCLNT_STREAMFLAGS_EVENTCALLBACK, dur, 0, mix, nullptr);
    CoTaskMemFree(mix);
    if (FAILED(hr)) {
        MrpgLog::Write("voice: capture Initialize failed (hr 0x%08lX)", (unsigned long)hr);
        return false;
    }

    s_event = CreateEventA(nullptr, FALSE, FALSE, nullptr);
    s_client->SetEventHandle(s_event);

    if (FAILED(s_client->GetService(__uuidof(IAudioCaptureClient), (void**)&s_capture))
        || !s_capture) {
        MrpgLog::Write("voice: no capture client");
        return false;
    }

    s_client->Start();
    s_running.store(true);
    s_capturing.store(true);
    s_thread = std::thread(ThreadMain);

    MrpgLog::Write("voice: MICROPHONE OPEN - %d Hz, %d ch, mode %s, gate %.3f/%.3f",
                   s_devRate, s_devCh,
                   s_openMic.load() ? "OPEN MIC" : "push-to-talk",
                   s_gateOpen, s_gateClose);
    if (!s_openMic.load())
        MrpgLog::Write("voice:   nothing is transmitted unless the push-to-talk key is held.");
    return true;
}

void Shutdown()
{
    if (!s_running.exchange(false)) return;

    if (s_event) SetEvent(s_event);
    if (s_thread.joinable()) s_thread.join();

    if (s_client)  s_client->Stop();
    if (s_capture) { s_capture->Release(); s_capture = nullptr; }
    if (s_client)  { s_client->Release();  s_client  = nullptr; }
    if (s_device)  { s_device->Release();  s_device  = nullptr; }
    if (s_enum)    { s_enum->Release();    s_enum    = nullptr; }
    if (s_event)   { CloseHandle(s_event); s_event   = nullptr; }

    {
        std::lock_guard<std::mutex> lock(s_ringMutex);
        s_ringHead = s_ringCount = 0;
    }
    s_capturing.store(false);
    s_talking.store(false);

    MrpgLog::Write("voice: MICROPHONE CLOSED");
}

bool IsCapturing() { return s_capturing.load(std::memory_order_relaxed); }

void SetPushToTalk(bool held) { s_ptt.store(held, std::memory_order_relaxed); }

bool IsEnabled() { return s_enabled.load(std::memory_order_relaxed); }
const char* CurrentInputName() { return s_curDeviceName; }

bool SetEnabled(bool on)
{
    s_forceEnable = on ? 1 : 0;
    if (on) {
        if (s_running.load(std::memory_order_relaxed)) return true;
        return Init(nullptr);
    }
    Shutdown();
    s_enabled.store(false);
    return true;
}

// The chosen device's friendly name, WITHOUT opening it.
//
// Needed because a player can pick a microphone while voice is switched off, and
// nothing is opened in that case. Reporting the last-opened device then would show
// them a name that is not the one they just chose - which reads as the setting
// having been ignored.
static bool LookUpName(const std::string& id)
{
    const int n = MrpgDevices::Count(MrpgDevices::CAPTURE);
    for (int i = 0; i < n; ++i) {
        if (id == MrpgDevices::Id(MrpgDevices::CAPTURE, i)) {
            _snprintf(s_curDeviceName, sizeof(s_curDeviceName) - 1, "%s",
                      MrpgDevices::Name(MrpgDevices::CAPTURE, i));
            s_curDeviceName[sizeof(s_curDeviceName) - 1] = '\0';
            return true;
        }
    }
    return false;
}

static void NameChosenDevice(const std::string& id)
{
    if (id.empty()) { strcpy(s_curDeviceName, "(system default)"); return; }

    if (LookUpName(id)) return;

    // A MISS IS NOT AN ANSWER UNTIL THE LIST HAS BEEN REFRESHED. The cached list
    // is empty until something asks for it, and the first caller here is the join
    // that restores a saved device - so a bare lookup would report every player's
    // remembered microphone as missing on the first frame of every session.
    MrpgDevices::Refresh(MrpgDevices::CAPTURE);
    if (LookUpName(id)) return;

    // Genuinely absent: unplugged since it was chosen. Init falls back to the
    // system default, and this name is what tells the player why.
    strcpy(s_curDeviceName, "(not connected)");
}

bool SetInputDevice(const char* endpointId)
{
    const std::string want = endpointId ? endpointId : "";
    if (want == s_wantDeviceId && s_running.load(std::memory_order_relaxed)) return true;

    const bool wasOn = s_running.load(std::memory_order_relaxed);
    const std::string prev = s_wantDeviceId;
    if (wasOn) Shutdown();
    s_wantDeviceId = want;

    if (!wasOn) {                     // will be used next time it is opened
        NameChosenDevice(want);
        return true;
    }
    if (Init(nullptr)) return true;

    MrpgLog::Write("voice: could not open the chosen microphone; reverting");
    s_wantDeviceId = prev;
    Init(nullptr);
    return false;
}
bool IsPushToTalk()           { return s_ptt.load(std::memory_order_relaxed); }
bool IsTalking()   { return s_talking.load(std::memory_order_relaxed); }

int TakeFrames(mv_u8* out, int maxFrames)
{
    if (maxFrames <= 0) return 0;
    std::lock_guard<std::mutex> lock(s_ringMutex);
    int n = 0;
    while (n < maxFrames && s_ringCount > 0) {
        memcpy(out + (size_t)n * MRPGVOICE_ENC_BYTES, s_ring[s_ringHead], MRPGVOICE_ENC_BYTES);
        s_ringHead = (s_ringHead + 1) % RING_FRAMES;
        --s_ringCount;
        ++n;
    }
    s_framesTaken.fetch_add((unsigned long long)n, std::memory_order_relaxed);
    return n;
}

const char* StatLine()
{
    // FIELDS ARE READ BY INDEX AT THE FAR END, so anything new goes on the END.
    // Inserting in the middle silently re-points every existing reader at the
    // wrong number - which has already cost this project one debugging session.
    //
    //  0 enabled   1 capturing  2 talking  3 made   4 taken  5 dropped
    //  6 rate      7 channels   8 ptt      9 openMic
    // 10 level (0..1 RMS)      11 gate threshold the level must beat
    static char out[192];
    _snprintf(out, sizeof(out) - 1, "%d %d %d %llu %llu %llu %d %d %d %d %.4f %.4f",
              s_enabled.load(std::memory_order_relaxed) ? 1 : 0,
              s_capturing.load(std::memory_order_relaxed) ? 1 : 0,
              s_talking.load(std::memory_order_relaxed) ? 1 : 0,
              s_framesMade.load(std::memory_order_relaxed),
              s_framesTaken.load(std::memory_order_relaxed),
              s_dropped.load(std::memory_order_relaxed),
              s_devRate, s_devCh,
              s_ptt.load(std::memory_order_relaxed) ? 1 : 0,
              s_openMic.load(std::memory_order_relaxed) ? 1 : 0,
              s_capturing.load(std::memory_order_relaxed)
                  ? s_level.load(std::memory_order_relaxed) : 0.0f,
              s_openMic.load(std::memory_order_relaxed) ? s_gateOpen : s_pttFloor);
    out[sizeof(out) - 1] = '\0';
    return out;
}

} // namespace MrpgCapture
