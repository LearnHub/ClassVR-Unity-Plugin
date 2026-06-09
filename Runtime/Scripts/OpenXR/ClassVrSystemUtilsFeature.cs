using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.NativeTypes;
using UnityEngine.XR.OpenXR;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif

namespace ClassVR.OpenXR {
#if UNITY_EDITOR
  [OpenXRFeature(
    UiName = "ClassVR System Utils",
    BuildTargetGroups = new[] { BuildTargetGroup.Android },
    Company = "Avantis Education",
    Desc = "Enables XR_AVN_system_utils",
    OpenxrExtensionStrings = "XR_AVN_system_utils",
    Version = "1.0.0",
    FeatureId = "com.avantis.openxr.systemutils"
  )]
#endif
  /// <summary>
  /// Enables the Avantis System Utils extension for OpenXR.
  /// This contains functions to query the system, including the time since last recenter.
  /// It is not recommended to use this class directly, use XR Plug-in Management in Project Settings instead.
  ///
  /// Usage Example:
  /// <code>
  /// // Get the feature instance
  /// var systemUtilsFeature = OpenXRSettings.Instance.GetFeature&lt;ClassVrSystemUtilsFeature&gt;();
  ///
  /// if (systemUtilsFeature != null) {
  ///   // Get time since last recenter
  ///   var timeSinceRecenter = systemUtilsFeature.GetTimeSinceLastRecenter();
  ///   if (timeSinceRecenter != -1) {
  ///     Debug.Log($"Time since recenter = {timeSinceRecenter}");
  ///   }
  /// }
  /// </code>
  /// </summary>
  public class ClassVrSystemUtilsFeature : OpenXRFeature {
    // The extension string constant
    public const string ExtensionName = "XR_AVN_system_utils";

    // Instance state
    private ulong xrInstance = 0;
    // Pre-allocated unmanaged buffer for output pointer parameters, to avoid per-call allocation
    private IntPtr timeSinceRecenterPtr = IntPtr.Zero;

    // Function delegates (cached after OnInstanceCreate)
    private xrGetTimeSinceLastRecenterAVNDelegate xrGetTimeSinceLastRecenterAVN;

    #region Native Delegates
    /// <summary>
    /// Delegate for xrGetInstanceProcAddr function.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate XrResult GetInstanceProcAddrDelegate(
      ulong instance,
      string name,
      ref IntPtr function);

    /// <summary>
    /// Delegate for xrGetTimeSinceLastRecenterAVN function.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate XrResult xrGetTimeSinceLastRecenterAVNDelegate(ulong instance, IntPtr timeSinceRecenter);
    #endregion

    #region Lifecycle Methods
    /// <summary>
    /// Called when the OpenXR instance is created.
    /// Retrieves function pointers for the extension functions.
    /// </summary>
    protected override bool OnInstanceCreate(ulong xrInstanceHandle) {
      // Check the extension is enabled
      if (!OpenXRRuntime.IsExtensionEnabled(ExtensionName)) {
        Debug.LogError($"[AvantisSystemUtils] {ExtensionName} extension is not enabled.");
        return false;
      }

      xrInstance = xrInstanceHandle;

      // Get xrGetInstanceProcAddr function
      var xrGetInstanceProcAddrPtr = xrGetInstanceProcAddr;
      if (xrGetInstanceProcAddrPtr == IntPtr.Zero) {
        Debug.LogError("[AvantisSystemUtils] Failed to get xrGetInstanceProcAddr");
        return false;
      }

      var getInstanceProcAddr = Marshal.GetDelegateForFunctionPointer<GetInstanceProcAddrDelegate>(xrGetInstanceProcAddrPtr);

      // Retrieve function pointers
      var funcPtr = IntPtr.Zero;
      XrResult result;

      // Get xrGetTimeSinceLastRecenterAVN
      result = getInstanceProcAddr(xrInstance, "xrGetTimeSinceLastRecenterAVN", ref funcPtr);
      if (result.IsSuccess() && funcPtr != IntPtr.Zero) {
        xrGetTimeSinceLastRecenterAVN = Marshal.GetDelegateForFunctionPointer<xrGetTimeSinceLastRecenterAVNDelegate>(funcPtr);
      } else {
        Debug.LogError("[AvantisSystemUtils] Failed to get xrGetTimeSinceLastRecenterAVN function pointer");
        return false;
      }

      timeSinceRecenterPtr = Marshal.AllocHGlobal(sizeof(long));

      return xrGetTimeSinceLastRecenterAVN != null;
    }

    /// <summary>
    /// Called when the OpenXR instance is destroyed.
    /// Clears cached function pointers and state.
    /// </summary>
    protected override void OnInstanceDestroy(ulong xrInstanceHandle) {
      xrInstance = 0;
      xrGetTimeSinceLastRecenterAVN = null;
      if (timeSinceRecenterPtr != IntPtr.Zero) {
        Marshal.FreeHGlobal(timeSinceRecenterPtr);
        timeSinceRecenterPtr = IntPtr.Zero;
      }
    }
    #endregion

    #region Public API
    /// <summary>
    /// Gets the time in nanoseconds since the last recenter event.
    /// Note that 0 will be returned if there have been no recenter events since the app started.
    /// </summary>
    /// <returns>Time in nanoseconds since last recenter if successful, -1 if unsuccessful, 0 if no recenter events yet.</returns>
    public long GetTimeSinceLastRecenter() {
      if (xrGetTimeSinceLastRecenterAVN == null) {
        Debug.LogError("[AvantisSystemUtils] Cannot call GetTimeSinceLastRecenter - function not loaded. Ensure the feature is enabled in OpenXR settings.");
        return -1;
      }

      Marshal.WriteInt64(timeSinceRecenterPtr, 0);
      XrResult result = xrGetTimeSinceLastRecenterAVN(xrInstance, timeSinceRecenterPtr);

      if (!result.IsSuccess()) {
        Debug.LogError($"[AvantisSystemUtils] xrGetTimeSinceLastRecenterAVN failed with result: {result}");
        return -1;
      }

      return Marshal.ReadInt64(timeSinceRecenterPtr);
    }
    #endregion
  }
}
