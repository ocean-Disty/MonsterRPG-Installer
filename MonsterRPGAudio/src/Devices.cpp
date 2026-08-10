#define INITGUID
#include <windows.h>
#include <mmdeviceapi.h>
#include <functiondiscoverykeys_devpkey.h>
#include <objbase.h>

#include <stdio.h>
#include <string.h>

#include <mutex>
#include <string>
#include <vector>

#include "Devices.hpp"
#include "Log.hpp"

namespace MrpgDevices {

namespace {

struct Entry {
    std::string id;
    std::string name;
    bool        isDefault = false;
};

std::mutex         s_mutex;
std::vector<Entry> s_list[2];

// Endpoint names arrive as UTF-16 and leave through TorqueScript, which is byte
// oriented. UTF-8 keeps a name like "Réaltek" intact on the way through rather
// than truncating at the first non-ASCII byte.
std::string Narrow(const wchar_t* w)
{
    if (!w) return std::string();
    int n = WideCharToMultiByte(CP_UTF8, 0, w, -1, nullptr, 0, nullptr, nullptr);
    if (n <= 1) return std::string();
    std::string out((size_t)n - 1, '\0');
    WideCharToMultiByte(CP_UTF8, 0, w, -1, &out[0], n, nullptr, nullptr);
    return out;
}

// A device name with a newline or a tab in it would forge a row in whatever list
// this ends up in. Nobody has ever shipped one, and it costs a line to be sure.
void Sanitise(std::string& s)
{
    for (size_t i = 0; i < s.size(); ++i)
        if ((unsigned char)s[i] < 0x20) s[i] = ' ';
}

} // namespace

int Refresh(Kind kind)
{
    const EDataFlow flow = (kind == RENDER) ? eRender : eCapture;

    HRESULT hr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    const bool weInited = SUCCEEDED(hr);

    IMMDeviceEnumerator* en = nullptr;
    hr = CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_ALL,
                          __uuidof(IMMDeviceEnumerator), (void**)&en);
    if (FAILED(hr) || !en) {
        if (weInited) CoUninitialize();
        return 0;
    }

    // The current default, so the list can mark it. A player looking for "the
    // one it would have picked anyway" should not have to guess.
    std::string defaultId;
    {
        IMMDevice* def = nullptr;
        if (SUCCEEDED(en->GetDefaultAudioEndpoint(flow,
                (kind == CAPTURE) ? eCommunications : eConsole, &def)) && def) {
            LPWSTR id = nullptr;
            if (SUCCEEDED(def->GetId(&id)) && id) { defaultId = Narrow(id); CoTaskMemFree(id); }
            def->Release();
        }
    }

    std::vector<Entry> found;

    IMMDeviceCollection* col = nullptr;
    if (SUCCEEDED(en->EnumAudioEndpoints(flow, DEVICE_STATE_ACTIVE, &col)) && col) {
        UINT count = 0;
        col->GetCount(&count);
        for (UINT i = 0; i < count; ++i) {
            IMMDevice* dev = nullptr;
            if (FAILED(col->Item(i, &dev)) || !dev) continue;

            Entry e;
            LPWSTR id = nullptr;
            if (SUCCEEDED(dev->GetId(&id)) && id) { e.id = Narrow(id); CoTaskMemFree(id); }

            IPropertyStore* props = nullptr;
            if (SUCCEEDED(dev->OpenPropertyStore(STGM_READ, &props)) && props) {
                PROPVARIANT v; PropVariantInit(&v);
                if (SUCCEEDED(props->GetValue(PKEY_Device_FriendlyName, &v)) && v.vt == VT_LPWSTR)
                    e.name = Narrow(v.pwszVal);
                PropVariantClear(&v);
                props->Release();
            }
            dev->Release();

            if (e.id.empty()) continue;
            if (e.name.empty()) e.name = "(unnamed device)";
            Sanitise(e.name);
            e.isDefault = (!defaultId.empty() && e.id == defaultId);
            found.push_back(e);
        }
        col->Release();
    }
    en->Release();
    if (weInited) CoUninitialize();

    {
        std::lock_guard<std::mutex> lock(s_mutex);
        s_list[(int)kind].swap(found);
        return (int)s_list[(int)kind].size();
    }
}

int Count(Kind kind)
{
    std::lock_guard<std::mutex> lock(s_mutex);
    return (int)s_list[(int)kind].size();
}

const char* Name(Kind kind, int index)
{
    std::lock_guard<std::mutex> lock(s_mutex);
    const auto& v = s_list[(int)kind];
    if (index < 0 || index >= (int)v.size()) return "";
    return v[(size_t)index].name.c_str();
}

const char* Id(Kind kind, int index)
{
    std::lock_guard<std::mutex> lock(s_mutex);
    const auto& v = s_list[(int)kind];
    if (index < 0 || index >= (int)v.size()) return "";
    return v[(size_t)index].id.c_str();
}

bool IsDefault(Kind kind, int index)
{
    std::lock_guard<std::mutex> lock(s_mutex);
    const auto& v = s_list[(int)kind];
    if (index < 0 || index >= (int)v.size()) return false;
    return v[(size_t)index].isDefault;
}

} // namespace MrpgDevices
