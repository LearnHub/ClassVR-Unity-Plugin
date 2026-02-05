using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.NativeTypes;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif

namespace ClassVR.OpenXR {
#if UNITY_EDITOR
  [OpenXRFeature(
      UiName = "ClassVR Tilt-To-Spin",
      BuildTargetGroups = new[] { BuildTargetGroup.Android },
      Company = "Avantis Education",
      Desc = "Enables XR_AVN_tilt_to_spin",
      OpenxrExtensionStrings = "XR_AVN_tilt_to_spin",
      Version = "1.0.0",
      FeatureId = "com.avantis.openxr.tilttospin"
  )]
#endif
  /// <summary>
  /// Enables the Avantis Tilt-to-Spin extension for OpenXR.
  /// This feature converts head roll (tilt) into yaw rotation (spin) for VR navigation.
  ///
  /// Usage Example:
  /// <code>
  /// // Get the feature instance
  /// var tiltToSpinFeature = OpenXRSettings.Instance.GetFeature&lt;ClassVrTiltToSpinFeature&gt;();
  ///
  /// if (tiltToSpinFeature != null)
  /// {
  ///     // Enable tilt-to-spin functionality
  ///     if (tiltToSpinFeature.EnableTiltToSpin(true))
  ///     {
  ///         Debug.Log("Tilt-to-spin enabled successfully!");
  ///     }
  ///
  ///     // Enable reset on recenter (modern extension only)
  ///     if (tiltToSpinFeature.IsModernExtensionEnabled)
  ///     {
  ///         tiltToSpinFeature.EnableResetOnRecenter(true);
  ///     }
  /// }
  /// </code>
  /// </summary>
  public class ClassVrTiltToSpinFeature : OpenXRFeature {
    // The extension string constant
    public const string ExtensionName = "XR_AVN_tilt_to_spin";

    //// === Settings that will appear under the cog icon ===
    //[SerializeField, Tooltip("Reset On Recenter")]
    //private bool resetOnRecenter = false;

    // XrBool32 constants
    private const uint XR_TRUE = 1;
    private const uint XR_FALSE = 0;

    // Instance state
    private ulong xrInstance = 0;

    // Function delegates (cached after OnInstanceCreate)
    private xrEnableTiltToSpinAVNDelegate xrEnableTiltToSpinAVN;
    private xrEnableResetOnRecenterTiltToSpinAVNDelegate xrEnableResetOnRecenterTiltToSpinAVN;

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
    /// Delegate for xrEnableTiltToSpinAVN function.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate XrResult xrEnableTiltToSpinAVNDelegate(
        ulong instance,
        uint enable);

    /// <summary>
    /// Delegate for xrEnableResetOnRecenterTiltToSpinAVN function.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate XrResult xrEnableResetOnRecenterTiltToSpinAVNDelegate(
        ulong instance,
        uint isResetEnabled);
    #endregion

    #region Lifecycle Methods
    /// <summary>
    /// Called when the OpenXR instance is created.
    /// Retrieves function pointers for the extension functions.
    /// </summary>
    protected override bool OnInstanceCreate(ulong xrInstanceHandle) {
      // Check the extension is enabled
      if (!OpenXRRuntime.IsExtensionEnabled(ExtensionName)) {
        Debug.LogWarning($"[AvantisTiltToSpin] {ExtensionName} extension is not enabled.");
        return false;
      }

      xrInstance = xrInstanceHandle;

      // Get xrGetInstanceProcAddr function
      var xrGetInstanceProcAddrPtr = xrGetInstanceProcAddr;
      if (xrGetInstanceProcAddrPtr == IntPtr.Zero) {
        Debug.LogError("[AvantisTiltToSpin] Failed to get xrGetInstanceProcAddr");
        return false;
      }

      var getInstanceProcAddr = Marshal.GetDelegateForFunctionPointer<GetInstanceProcAddrDelegate>(xrGetInstanceProcAddrPtr);

      // Retrieve function pointers
      var funcPtr = IntPtr.Zero;
      XrResult result;

      // Get xrEnableTiltToSpinAVN
      result = getInstanceProcAddr(xrInstance, "xrEnableTiltToSpinAVN", ref funcPtr);
      if (result.IsSuccess() && funcPtr != IntPtr.Zero) {
        xrEnableTiltToSpinAVN = Marshal.GetDelegateForFunctionPointer<xrEnableTiltToSpinAVNDelegate>(funcPtr);
        Debug.Log("[AvantisTiltToSpin] Successfully loaded xrEnableTiltToSpinAVN");
      } else {
        Debug.LogWarning("[AvantisTiltToSpin] Failed to get xrEnableTiltToSpinAVN function pointer");
      }

      // Get xrEnableResetOnRecenterTiltToSpinAVN
      funcPtr = IntPtr.Zero;
      result = getInstanceProcAddr(xrInstance, "xrEnableResetOnRecenterTiltToSpinAVN", ref funcPtr);
      if (result.IsSuccess() && funcPtr != IntPtr.Zero) {
        xrEnableResetOnRecenterTiltToSpinAVN =
            Marshal.GetDelegateForFunctionPointer<xrEnableResetOnRecenterTiltToSpinAVNDelegate>(funcPtr);
        Debug.Log("[AvantisTiltToSpin] Successfully loaded xrEnableResetOnRecenterTiltToSpinAVN");
      } else {
        Debug.LogWarning("[AvantisTiltToSpin] Failed to get xrEnableResetOnRecenterTiltToSpinAVN function pointer");
      }

      return xrEnableTiltToSpinAVN != null;
    }

    /// <summary>
    /// Called when the OpenXR instance is destroyed.
    /// Clears cached function pointers and state.
    /// </summary>
    protected override void OnInstanceDestroy(ulong xrInstanceHandle) {
      xrInstance = 0;
      xrEnableTiltToSpinAVN = null;
      xrEnableResetOnRecenterTiltToSpinAVN = null;
    }
    #endregion

    #region Public API
    /// <summary>
    /// Enable or disable the tilt-to-spin functionality.
    /// When enabled, head roll is converted to yaw rotation.
    /// </summary>
    /// <param name="enable">True to enable tilt-to-spin, false to disable.</param>
    /// <returns>True if the operation succeeded, false otherwise.</returns>
    public bool EnableTiltToSpin(bool enable) {
      if (xrEnableTiltToSpinAVN == null) {
        Debug.LogError("[AvantisTiltToSpin] Cannot call EnableTiltToSpin - function not loaded. Ensure the feature is enabled in OpenXR settings.");
        return false;
      }

      uint xrBoolValue = enable ? XR_TRUE : XR_FALSE;
      XrResult result = xrEnableTiltToSpinAVN(xrInstance, xrBoolValue);

      if (!result.IsSuccess()) {
        Debug.LogError($"[AvantisTiltToSpin] xrEnableTiltToSpinAVN failed with result: {result}");
        return false;
      }

      Debug.Log($"[AvantisTiltToSpin] Tilt-to-spin {(enable ? "enabled" : "disabled")}");
      return true;
    }

    /// <summary>
    /// Enable or disable automatic reset of tilt-to-spin state when tracking recenters.
    /// When enabled, the accumulated yaw rotation is reset to identity when a recenter event occurs.
    /// </summary>
    /// <param name="enable">True to enable reset on recenter, false to disable.</param>
    /// <returns>True if the operation succeeded, false otherwise.</returns>
    public bool EnableResetOnRecenter(bool enable) {
      if (xrEnableResetOnRecenterTiltToSpinAVN == null) {
        Debug.LogError("[AvantisTiltToSpin] Cannot call EnableResetOnRecenter - function not loaded.");
        return false;
      }

      uint xrBoolValue = enable ? XR_TRUE : XR_FALSE;
      XrResult result = xrEnableResetOnRecenterTiltToSpinAVN(xrInstance, xrBoolValue);

      if (!result.IsSuccess()) {
        Debug.LogError($"[AvantisTiltToSpin] xrEnableResetOnRecenterTiltToSpinAVN failed with result: {result}");
        return false;
      }

      Debug.Log($"[AvantisTiltToSpin] Reset on recenter {(enable ? "enabled" : "disabled")}");
      return true;
    }
    #endregion
  }
}
