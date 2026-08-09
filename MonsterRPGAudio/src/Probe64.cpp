// =============================================================================
// MonsterRPGAudioProbe.exe — a 64-BIT helper that asks Vulkan what this GPU can do
// =============================================================================
//
// WHY THIS IS A SEPARATE PROCESS, AND WHY IT IS 64-BIT
//
// This was originally done inside the DLL, in-process. That was wrong, and it
// was wrong in a way that looked like it worked — measured on the development
// machine, an RX 7900 XTX:
//
//     32-bit process:  211 Vulkan device extensions, ZERO of them ray tracing
//     64-bit process:  220 Vulkan device extensions, including
//                      VK_KHR_ray_query and VK_KHR_acceleration_structure
//
// AMD's 32-bit Vulkan ICD does not expose ray tracing at all. Blockland.exe is a
// 32-bit process, so a probe living inside it would have reported NO_RT for the
// exact card that this project's own server uses to trace acoustics. Not a
// marginal misread — the strongest consumer GPU AMD sells, reported as incapable.
//
// And that answer would have been irrelevant even if it were right: the thing
// this probe exists to gate (Phase 7, client-side tracing) runs in
// MonsterRPG_AudioRT.exe, which is 64-bit. The capability that matters is the
// one visible to a 64-bit process, so the question has to be asked from one.
//
// THE OTHER BUG THIS FILE FIXES: requesting a Vulkan 1.0 instance HIDES
// VK_KHR_acceleration_structure, because it depends on the instance extension
// VK_KHR_get_physical_device_properties2 — core since 1.1 — and the loader does
// not report device extensions whose instance dependencies are unmet. On the
// same 64-bit run: 216 extensions with a 1.0 instance and no acceleration
// structure; 220 and present with a 1.1 one. GpuServer.cpp enables that instance
// extension explicitly at line 1507 and is why it never hit this.
//
// OUTPUT — one line per device, then a terminator, on stdout:
//
//     MRPGGPU <vendorId> <deviceId> <apiVersion> <deviceType> <rq> <as> <dho> <name...>
//     MRPGGPU-END <deviceCount>
//
// The NAME IS LAST because it is the only field that can contain spaces. Any
// field added after it would be unreachable; any field inserted before it
// silently shifts every reading after it.
//
// Anything that goes wrong prints MRPGGPU-ERR <reason...> and exits non-zero.
// Silence is never a valid result: the caller has to be able to tell "no ray
// tracing" apart from "the probe did not run", because those two mean opposite
// things about whether to trust the answer.

#include <windows.h>
#include <stdio.h>
#include <string.h>
#include <stdlib.h>

#define MRPGVK __stdcall

typedef void* VkInstance;
typedef void* VkPhysicalDevice;
typedef int   VkResult;

static const VkResult VK_SUCCESS    = 0;
static const VkResult VK_INCOMPLETE = 5;

// VK_MAKE_API_VERSION(0, 1, 1, 0) — see the header note on why 1.0 is not enough.
static const unsigned int VK_API_1_1 = (1u << 22) | (1u << 12);

struct VkApplicationInfo {
    unsigned int sType;              // VK_STRUCTURE_TYPE_APPLICATION_INFO = 0
    const void*  pNext;
    const char*  pApplicationName;
    unsigned int applicationVersion;
    const char*  pEngineName;
    unsigned int engineVersion;
    unsigned int apiVersion;
};

struct VkInstanceCreateInfo {
    unsigned int             sType;  // VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO = 1
    const void*              pNext;
    unsigned int             flags;
    const VkApplicationInfo* pApplicationInfo;
    unsigned int             enabledLayerCount;
    const char* const*       ppEnabledLayerNames;
    unsigned int             enabledExtensionCount;
    const char* const*       ppEnabledExtensionNames;
};

// Everything past the UUID is slack. The real struct ends with
// VkPhysicalDeviceLimits (504 bytes) and VkPhysicalDeviceSparseProperties (20);
// we read none of it, but vkGetPhysicalDeviceProperties WRITES all of it, so the
// buffer has to be at least that big. Oversized on purpose so a future Vulkan
// that grows the limits struct cannot turn this into stack corruption.
// The leading fields are frozen by the Vulkan ABI and have not moved since 1.0.
struct VkPhysicalDeviceProperties {
    unsigned int  apiVersion;
    unsigned int  driverVersion;
    unsigned int  vendorID;
    unsigned int  deviceID;
    unsigned int  deviceType;
    char          deviceName[256];
    unsigned char pipelineCacheUUID[16];
    unsigned char _tail[1024];
};

struct VkExtensionProperties {
    char         extensionName[256];
    unsigned int specVersion;
};

typedef void*    (MRPGVK *PFN_vkGetInstanceProcAddr)(VkInstance, const char*);
typedef VkResult (MRPGVK *PFN_vkCreateInstance)(const VkInstanceCreateInfo*, const void*, VkInstance*);
typedef void     (MRPGVK *PFN_vkDestroyInstance)(VkInstance, const void*);
typedef VkResult (MRPGVK *PFN_vkEnumeratePhysicalDevices)(VkInstance, unsigned int*, VkPhysicalDevice*);
typedef void     (MRPGVK *PFN_vkGetPhysicalDeviceProperties)(VkPhysicalDevice, VkPhysicalDeviceProperties*);
typedef VkResult (MRPGVK *PFN_vkEnumerateDeviceExtensionProperties)(VkPhysicalDevice, const char*, unsigned int*, VkExtensionProperties*);

static int Fail(const char* fmt, ...)
{
    char msg[512];
    va_list ap;
    va_start(ap, fmt);
    vsnprintf(msg, sizeof(msg), fmt, ap);
    va_end(ap);
    printf("MRPGGPU-ERR %s\n", msg);
    fflush(stdout);
    return 1;
}

int main(void)
{
    // Unbuffered: the parent reads this pipe to EOF, and a probe that crashes
    // after printing should still have delivered what it printed.
    setvbuf(stdout, NULL, _IONBF, 0);

    HMODULE vk = LoadLibraryA("vulkan-1.dll");
    if (!vk) return Fail("no vulkan-1.dll");

    PFN_vkGetInstanceProcAddr gipa =
        (PFN_vkGetInstanceProcAddr)GetProcAddress(vk, "vkGetInstanceProcAddr");
    if (!gipa) return Fail("vulkan-1.dll has no vkGetInstanceProcAddr");

    PFN_vkCreateInstance createInstance = (PFN_vkCreateInstance)gipa(NULL, "vkCreateInstance");
    if (!createInstance) return Fail("no vkCreateInstance");

    VkApplicationInfo app;
    memset(&app, 0, sizeof(app));
    app.sType            = 0;
    app.pApplicationName = "MonsterRPGAudioProbe";
    app.pEngineName      = "MonsterRPGAudioProbe";
    app.apiVersion       = VK_API_1_1;

    VkInstanceCreateInfo ici;
    memset(&ici, 0, sizeof(ici));
    ici.sType            = 1;
    ici.pApplicationInfo = &app;

    VkInstance inst = NULL;
    VkResult   vr   = createInstance(&ici, NULL, &inst);

    // A loader older than 1.1 rejects the 1.1 request outright. Retry at 1.0
    // rather than reporting nothing: such a machine certainly has no ray
    // tracing, but we still want its name and version in the log.
    if (vr != VK_SUCCESS) {
        app.apiVersion = 0;
        vr = createInstance(&ici, NULL, &inst);
        if (vr != VK_SUCCESS)
            return Fail("vkCreateInstance failed (%d)", (int)vr);
        printf("MRPGGPU-NOTE loader rejected 1.1, fell back to 1.0; "
               "acceleration_structure may be hidden\n");
    }

    PFN_vkDestroyInstance                    destroyInstance = (PFN_vkDestroyInstance)gipa(inst, "vkDestroyInstance");
    PFN_vkEnumeratePhysicalDevices           enumDevices     = (PFN_vkEnumeratePhysicalDevices)gipa(inst, "vkEnumeratePhysicalDevices");
    PFN_vkGetPhysicalDeviceProperties        getProps        = (PFN_vkGetPhysicalDeviceProperties)gipa(inst, "vkGetPhysicalDeviceProperties");
    PFN_vkEnumerateDeviceExtensionProperties enumExts        = (PFN_vkEnumerateDeviceExtensionProperties)gipa(inst, "vkEnumerateDeviceExtensionProperties");

    if (!enumDevices || !getProps || !enumExts)
        return Fail("core Vulkan entry points missing");

    unsigned int count = 0;
    enumDevices(inst, &count, NULL);
    if (count == 0) {
        printf("MRPGGPU-END 0\n");
        if (destroyInstance) destroyInstance(inst, NULL);
        return 0;
    }
    if (count > 8) count = 8;

    VkPhysicalDevice devs[8];
    memset(devs, 0, sizeof(devs));
    enumDevices(inst, &count, devs);

    for (unsigned int i = 0; i < count; i++) {
        VkPhysicalDeviceProperties props;
        memset(&props, 0, sizeof(props));
        getProps(devs[i], &props);
        props.deviceName[sizeof(props.deviceName) - 1] = '\0';

        // Spaces in the name are fine — it is the last field — but a newline
        // would forge a second record and a control character would confuse
        // whoever reads the log. Neither has ever been seen in an adapter name;
        // both are one line to rule out.
        for (char* p = props.deviceName; *p; ++p)
            if ((unsigned char)*p < 0x20) *p = ' ';
        if (!props.deviceName[0])
            strcpy(props.deviceName, "(unnamed)");

        int rq = 0, as = 0, dho = 0;

        unsigned int extCount = 0;
        if (enumExts(devs[i], NULL, &extCount, NULL) == VK_SUCCESS && extCount > 0) {
            VkExtensionProperties* exts =
                (VkExtensionProperties*)malloc(sizeof(VkExtensionProperties) * extCount);
            if (exts) {
                VkResult er = enumExts(devs[i], NULL, &extCount, exts);
                if (er == VK_SUCCESS || er == VK_INCOMPLETE) {
                    for (unsigned int e = 0; e < extCount; e++) {
                        exts[e].extensionName[sizeof(exts[e].extensionName) - 1] = '\0';
                        if      (!strcmp(exts[e].extensionName, "VK_KHR_ray_query"))                  rq  = 1;
                        else if (!strcmp(exts[e].extensionName, "VK_KHR_acceleration_structure"))     as  = 1;
                        else if (!strcmp(exts[e].extensionName, "VK_KHR_deferred_host_operations"))   dho = 1;
                    }
                }
                free(exts);
            }
        }

        printf("MRPGGPU %u %u %u %u %d %d %d %s\n",
               props.vendorID, props.deviceID, props.apiVersion, props.deviceType,
               rq, as, dho, props.deviceName);
    }

    printf("MRPGGPU-END %u\n", count);

    if (destroyInstance) destroyInstance(inst, NULL);
    return 0;
}
