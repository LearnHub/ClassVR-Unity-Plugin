#include <openxr/openxr.h>
#include <dlfcn.h>
#include <android/log.h>
#include <string.h>
#include <vector>

#define LOG_TAG "OpenXRAvanitsLoader"
#define LOG(...) __android_log_print(ANDROID_LOG_INFO, LOG_TAG, __VA_ARGS__)

static void* real_loader_handle = nullptr;
static PFN_xrGetInstanceProcAddr real_xrGetInstanceProcAddr = nullptr;
static PFN_xrCreateInstance original_xrCreateInstance = nullptr;

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
    std::vector<const char*> requiredExtensionNames(
            modifiedInfo.enabledExtensionNames,
            modifiedInfo.enabledExtensionNames + modifiedInfo.enabledExtensionCount
    );
    requiredExtensionNames.push_back("XR_EXT_Avantis_tilt_to_spin");

    modifiedInfo.enabledExtensionCount = static_cast<uint32_t>(requiredExtensionNames.size());
    modifiedInfo.enabledExtensionNames = requiredExtensionNames.data();

    XrResult res = original_xrCreateInstance(&modifiedInfo, instance);

    if (res != XR_SUCCESS)
        LOG("xrCreateInstance Failed with %d", res);
    else
        LOG("xrCreateInstance Created");


    // Define our function in OpenXR format.
    typedef XrResult (XRAPI_PTR *PFN_xrEnableTiltToSpinEXT)(XrInstance instance, XrBool32 enable);
    PFN_xrEnableTiltToSpinEXT xrEnableTiltToSpinEXT = nullptr;
    xrGetInstanceProcAddr(*instance, "xrEnableTiltToSpinEXT",(PFN_xrVoidFunction *) (&xrEnableTiltToSpinEXT));

    if (xrEnableTiltToSpinEXT) {
        xrEnableTiltToSpinEXT(*instance, XR_TRUE);
        LOG("Tilt to Spin Enabled");
    }
    else
    {
        LOG("Tilt to Spin Failed");
    }
    return res;
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
