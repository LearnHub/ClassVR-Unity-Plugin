#include <openxr/openxr.h>
#include <dlfcn.h>
#include <android/log.h>
#include <string.h>

#define LOG_TAG "OpenXRAvanitsLoader"
#define LOG(...) __android_log_print(ANDROID_LOG_INFO, LOG_TAG, __VA_ARGS__)

static void* real_loader_handle = nullptr;
static PFN_xrGetInstanceProcAddr real_xrGetInstanceProcAddr = nullptr;
static PFN_xrCreateInstance original_xrCreateInstance = nullptr;
static PFN_xrCreateReferenceSpace original_xrCreateReferenceSpace = nullptr;
static PFN_xrLocateSpace real_xrLocateSpace = nullptr;

// Load the real OpenXR loader
static bool load_real_loader() {
    if (real_loader_handle)
        return true;

    real_loader_handle = dlopen("libopenxr_loader_real.so", RTLD_NOW | RTLD_LOCAL);
    if (!real_loader_handle) {
        LOG("Failed to dlopen real loader: %s", dlerror());
        return false;
    }

    real_xrGetInstanceProcAddr = (PFN_xrGetInstanceProcAddr)dlsym(real_loader_handle, "xrGetInstanceProcAddr");
    if (!real_xrGetInstanceProcAddr) {
        LOG("Failed to find xrGetInstanceProcAddr in real loader");
        dlclose(real_loader_handle);
        real_loader_handle = nullptr;
        return false;
    }

    LOG("Loaded real OpenXR loader and xrGetInstanceProcAddr: %p", real_xrGetInstanceProcAddr);
    return true;
}

// Hooked xrCreateInstance to inject API layer
extern "C" XrResult xrCreateInstance(const XrInstanceCreateInfo* createInfo, XrInstance* instance) {
    LOG("shim xrCreateInstance called");

    if (!original_xrCreateInstance)
        return XR_ERROR_INITIALIZATION_FAILED;

    XrInstanceCreateInfo modifiedInfo = *createInfo;

    static const char* layers[] = {
            "XR_APILAYER_TILTTOSPIN_AVANTIS"
    };

    modifiedInfo.enabledApiLayerCount = 1;
    modifiedInfo.enabledApiLayerNames = layers;

    LOG("Injecting API layer: %s", layers[0]);

    return original_xrCreateInstance(&modifiedInfo, instance);
}

// Hooked xrCreateReferenceSpace to log space type
extern "C" XrResult xrCreateReferenceSpace(XrSession session, const XrReferenceSpaceCreateInfo* createInfo, XrSpace* space) {
    const char* typeName = "UNKNOWN";
    switch (createInfo->referenceSpaceType) {
        case XR_REFERENCE_SPACE_TYPE_VIEW: typeName = "VIEW"; break;
        case XR_REFERENCE_SPACE_TYPE_LOCAL: typeName = "LOCAL"; break;
        case XR_REFERENCE_SPACE_TYPE_STAGE: typeName = "STAGE"; break;
        case XR_REFERENCE_SPACE_TYPE_UNBOUNDED_MSFT: typeName = "UNBOUNDED_MSFT"; break;
        case XR_REFERENCE_SPACE_TYPE_COMBINED_EYE_VARJO: typeName = "COMBINED_EYE_VARJO"; break;
        case XR_REFERENCE_SPACE_TYPE_LOCAL_FLOOR_EXT: typeName = "LOCAL_FLOOR_EXT"; break;
    }

    LOG("xrCreateReferenceSpace called with type: %s (%d)", typeName, createInfo->referenceSpaceType);

    if (!original_xrCreateReferenceSpace) {
        LOG("Original xrCreateReferenceSpace is null!");
        return XR_ERROR_INITIALIZATION_FAILED;
    }

    XrResult res = original_xrCreateReferenceSpace(session, createInfo, space);
    if (res != XR_SUCCESS) {
        LOG("xrCreateReferenceSpace returned error: %d", res);
    }
    return res;
}

extern "C" XrResult xrLocateSpace(
        XrSpace space,
        XrSpace baseSpace,
        XrTime time,
        XrSpaceLocation* location)
{
    if (!load_real_loader()) {
        return XR_ERROR_INITIALIZATION_FAILED;
    }

    if (!real_xrLocateSpace) {
        // Resolve original function pointer on first use
        XrResult res = real_xrGetInstanceProcAddr(XR_NULL_HANDLE, "xrLocateSpace", (PFN_xrVoidFunction*)&real_xrLocateSpace);
        if (res != XR_SUCCESS) {
            LOG("Failed to get original xrLocateSpace");
            return res;
        }
    }

    XrResult result = real_xrLocateSpace(space, baseSpace, time, location);

    if (result == XR_ERROR_HANDLE_INVALID)
    {
        //LOG("[xrLocateSpace] Suppressed XR_ERROR_HANDLE_INVALID error");
        return XR_SUCCESS;
    }

    return result;
}

// Intercept key function pointers
extern "C" XrResult xrGetInstanceProcAddr(XrInstance instance, const char* name, PFN_xrVoidFunction* function) {
    if (!load_real_loader())
        return XR_ERROR_INITIALIZATION_FAILED;

    if (strcmp(name, "xrCreateInstance") == 0) {
        *function = (PFN_xrVoidFunction)xrCreateInstance;
        if (!original_xrCreateInstance) {
            XrResult res = real_xrGetInstanceProcAddr(instance, name, (PFN_xrVoidFunction*)&original_xrCreateInstance);
            if (res != XR_SUCCESS) {
                LOG("Failed to get original xrCreateInstance");
                return res;
            }
        }
        return XR_SUCCESS;
    }

    if (strcmp(name, "xrCreateReferenceSpace") == 0) {
        *function = (PFN_xrVoidFunction)xrCreateReferenceSpace;
        if (!original_xrCreateReferenceSpace) {
            XrResult res = real_xrGetInstanceProcAddr(instance, name, (PFN_xrVoidFunction*)&original_xrCreateReferenceSpace);
            if (res != XR_SUCCESS) {
                LOG("Failed to get original xrCreateReferenceSpace");
                return res;
            }
        }
        return XR_SUCCESS;
    }
    if (strcmp(name, "xrLocateSpace") == 0) {
        *function = (PFN_xrVoidFunction)xrLocateSpace;
        if (!real_xrLocateSpace) {
            XrResult res = real_xrGetInstanceProcAddr(instance, name, (PFN_xrVoidFunction*)&real_xrLocateSpace);
            if (res != XR_SUCCESS) {
                LOG("Failed to get original xrLocateSpace");
                return res;
            }
        }
        return XR_SUCCESS;
    }
    return real_xrGetInstanceProcAddr(instance, name, function);
}

// Forward xrEnumerateApiLayerProperties
extern "C" XrResult xrEnumerateApiLayerProperties(
        uint32_t propertyCapacityInput,
        uint32_t* propertyCountOutput,
        XrApiLayerProperties* properties)
{
    if (!load_real_loader())
        return XR_ERROR_INITIALIZATION_FAILED;

    typedef XrResult (*PFN_xrEnumerateApiLayerProperties)(uint32_t, uint32_t*, XrApiLayerProperties*);
    static PFN_xrEnumerateApiLayerProperties func = nullptr;

    if (!func) {
        func = (PFN_xrEnumerateApiLayerProperties)dlsym(real_loader_handle, "xrEnumerateApiLayerProperties");
        if (!func) {
            LOG("Failed to find xrEnumerateApiLayerProperties");
            return XR_ERROR_FUNCTION_UNSUPPORTED;
        }
    }

    return func(propertyCapacityInput, propertyCountOutput, properties);
}
