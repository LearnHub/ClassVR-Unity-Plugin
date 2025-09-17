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
static PFN_xrEnumerateInstanceExtensionProperties real_xrEnumerateInstanceExtensionProperties = nullptr;

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
//--------------------------------------------------------------------------------------------------

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

    bool isAvantisSupported_v1 = false;
    bool isAvantisSupported_v2 = false;
    const char* TILT_TO_SPIN_V1 = "XR_EXT_Avantis_tilt_to_spin";
    const char* TILT_TO_SPIN_V2 = "XR_AVN_tilt_to_spin";
    uint32_t extCount = 0;

    // get count
    if (xrEnumerateInstanceExtensionProperties(nullptr, 0, &extCount, nullptr) == XR_SUCCESS) {
        std::vector<XrExtensionProperties> extProps(extCount, {XR_TYPE_EXTENSION_PROPERTIES});

        // Second call: get properties
        if (xrEnumerateInstanceExtensionProperties(nullptr, extCount, &extCount, extProps.data()) == XR_SUCCESS)
        {
            for (const auto& ext : extProps)
            {
                if (strcmp(ext.extensionName, TILT_TO_SPIN_V1) == 0)
                {
                    isAvantisSupported_v1 = true;
                }
                else if (strcmp(ext.extensionName, TILT_TO_SPIN_V2) == 0)
                {
                    isAvantisSupported_v2 = true;
                }
            }
        }
    }

    // Push it once after the enum finishes
    if (isAvantisSupported_v1)
        requiredExtensionNames.push_back(TILT_TO_SPIN_V1);
    else if (isAvantisSupported_v2)
        requiredExtensionNames.push_back(TILT_TO_SPIN_V2);


    modifiedInfo.enabledExtensionCount = static_cast<uint32_t>(requiredExtensionNames.size());
    modifiedInfo.enabledExtensionNames = requiredExtensionNames.data();

    XrResult res = original_xrCreateInstance(&modifiedInfo, instance);

    if (res != XR_SUCCESS)
        LOG("xrCreateInstance Failed with %d", res);
    else
        LOG("xrCreateInstance Created");


    // Define our function in OpenXR format.
    if (isAvantisSupported_v1)
    {
        typedef XrResult (XRAPI_PTR *PFN_xrEnableTiltToSpinEXT)(XrInstance instance, XrBool32 enable);
        PFN_xrEnableTiltToSpinEXT xrEnableTiltToSpinEXT = nullptr;
        xrGetInstanceProcAddr(*instance, "xrEnableTiltToSpinEXT",(PFN_xrVoidFunction *) (&xrEnableTiltToSpinEXT));

        if (xrEnableTiltToSpinEXT){
            xrEnableTiltToSpinEXT(*instance, XR_TRUE);
            LOG("Tilt to Spin Enabled");
        } else {
            LOG("Tilt to Spin Failed");
        }
    }
    if (isAvantisSupported_v2)
    {
        typedef XrResult (XRAPI_PTR *PFN_xrEnableTiltToSpinAVN)(XrInstance instance, XrBool32 enable);
        PFN_xrEnableTiltToSpinAVN xrEnableTiltToSpinAVN = nullptr;
        xrGetInstanceProcAddr(*instance, "xrEnableTiltToSpinAVN",(PFN_xrVoidFunction *) (&xrEnableTiltToSpinAVN));

        if (xrEnableTiltToSpinAVN)
        {
            xrEnableTiltToSpinAVN(*instance, XR_TRUE);
            LOG("Tilt to Spin Enabled");
        } else {
            LOG("Tilt to Spin Failed");
        }
    }
    return res;
}
//--------------------------------------------------------------------------------------------------

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
//--------------------------------------------------------------------------------------------------

extern "C" XrResult XRAPI_PTR xrEnumerateInstanceExtensionProperties(const char* layerName, uint32_t propertyCapacityInput, uint32_t* propertyCountOutput, XrExtensionProperties* properties)
{
    if (!load_real_loader())
        return XR_ERROR_INITIALIZATION_FAILED;

    // Resolve the real function once
    if (!real_xrEnumerateInstanceExtensionProperties)
    {
        real_xrEnumerateInstanceExtensionProperties = (PFN_xrEnumerateInstanceExtensionProperties)dlsym(real_loader_handle,"xrEnumerateInstanceExtensionProperties");
        if (!real_xrEnumerateInstanceExtensionProperties)
        {
            LOG("Failed to find real xrEnumerateInstanceExtensionProperties");
            return XR_ERROR_FUNCTION_UNSUPPORTED;
        }
    }

    XrResult res = real_xrEnumerateInstanceExtensionProperties(
            layerName,
            propertyCapacityInput,
            propertyCountOutput,
            properties
    );


    if (XR_SUCCEEDED(res) && layerName == nullptr && properties && *propertyCountOutput < propertyCapacityInput)
    {
        XrExtensionProperties customExt{XR_TYPE_EXTENSION_PROPERTIES};
        strncpy(customExt.extensionName, "XR_EXT_Avantis_tilt_to_spin", XR_MAX_EXTENSION_NAME_SIZE);
        customExt.extensionVersion = 1;
        properties[(*propertyCountOutput)++] = customExt;
        LOG("Injected XR_EXT_Avantis_tilt_to_spin into extension list");
    }

    return res;
}
//--------------------------------------------------------------------------------------------------

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
//--------------------------------------------------------------------------------------------------