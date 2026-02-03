using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.XR;
using UnityEngine.Scripting;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Input;
using UnityEngine.XR.OpenXR.Features;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if USE_INPUT_SYSTEM_POSE_CONTROL
using PoseControl = UnityEngine.InputSystem.XR.PoseControl;
#else
using PoseControl = UnityEngine.XR.OpenXR.Input.PoseControl;
#endif

namespace ClassVR.Device.Headset655 {
  /// <summary>
  /// This <see cref="OpenXRInteractionFeature"/> enables the use of ClassVR 655 headset interaction profiles in OpenXR.
  /// </summary>
#if UNITY_EDITOR
  [UnityEditor.XR.OpenXR.Features.OpenXRFeature(
      UiName = "ClassVR 655 Headset Profile",
      BuildTargetGroups = new[] { BuildTargetGroup.Android },
      Company = "Avantis",
      Desc = "Allows for mapping input to the ClassVR 655 headset interaction profile.",
      DocumentationLink = "",
      OpenxrExtensionStrings = "",
      Version = "0.0.1",
      Category = UnityEditor.XR.OpenXR.Features.FeatureCategory.Interaction,
      FeatureId = featureId)]
#endif
  public class ClassVrHeadset655InteractionProfile : OpenXRInteractionFeature {
    /// <summary>
    /// The feature id string. This is used to give the feature a well known id for reference.
    /// </summary>
    public const string featureId = "com.avantis.openxr.feature.input.headset655";

    /// <summary>
    /// An Input System device based on the ClassVrHeadset655 interaction profile defined by Avantis.
    /// </summary>
    [Preserve, InputControlLayout(displayName = "ClassVR Headset 655 (OpenXR)")]
    public class ClassVrHeadset655 : TrackedDevice {
      /// <summary>
      /// A <see cref="PoseControl"/> that represents the /user/head/input/gaze/pose OpenXR binding.
      /// </summary>
      [Preserve, InputControl(offset = 0, alias = "aimPose", usages = new[] { "Pointer", "Device", "gaze" })]
      public PoseControl gazePose { get; private set; }

      /// <summary>
      /// A [ButtonControl](xref:UnityEngine.InputSystem.Controls.ButtonControl) that represents the /user/head/input/select/click OpenXR binding.
      /// </summary>
      [Preserve, InputControl(aliases = new[] { "select", "action" }, usage = "SelectButton")]
      public ButtonControl selectButton { get; private set; }

      /// <summary>
      /// A [ButtonControl](xref:UnityEngine.InputSystem.Controls.ButtonControl) that represents the /user/head/input/back/click OpenXR binding.
      /// </summary>
      [Preserve, InputControl(aliases = new[] { "back", "cancel" }, usage = "Cancel")]
      public ButtonControl backButton { get; private set; }

      /// <summary>
      /// A [ButtonControl](xref:UnityEngine.InputSystem.Controls.ButtonControl) that represents the /user/head/input/volume_up/click OpenXR binding.
      /// </summary>
      [Preserve, InputControl(alias = "volumeUp", usage = "VolumeUp")]
      public ButtonControl volumeUpButton { get; private set; }

      /// <summary>
      /// A [ButtonControl](xref:UnityEngine.InputSystem.Controls.ButtonControl) that represents the /user/head/input/volume_down/click OpenXR binding.
      /// </summary>
      [Preserve, InputControl(alias = "volumeDown", usage = "VolumeDown")]
      public ButtonControl volumeDownButton { get; private set; }

      // Backwards compatibility

      /// <summary>
      /// A [ButtonControl](xref:UnityEngine.InputSystem.Controls.ButtonControl) required for backwards compatibility with the XRSDK layouts. This represents the overall tracking state of the device. This value is equivalent to mapping devicePose/isTracked.
      /// </summary>
      [Preserve, InputControl(offset = 28, usage = "IsTracked")]
      new public ButtonControl isTracked { get; private set; }

      /// <summary>
      /// A [IntegerControl](xref:UnityEngine.InputSystem.Controls.IntegerControl) required for backwards compatibility with the XRSDK layouts. This represents the bit flag set to indicate what data is valid. This value is equivalent to mapping devicePose/trackingState.
      /// </summary>
      [Preserve, InputControl(offset = 32, usage = "TrackingState")]
      new public IntegerControl trackingState { get; private set; }

      protected override void FinishSetup() {
        base.FinishSetup();
        gazePose = GetChildControl<PoseControl>("gazePose");
        selectButton = GetChildControl<ButtonControl>("selectButton");
        backButton = GetChildControl<ButtonControl>("backButton");
        volumeUpButton = GetChildControl<ButtonControl>("volumeUpButton");
        volumeDownButton = GetChildControl<ButtonControl>("volumeDownButton");
        isTracked = GetChildControl<ButtonControl>("isTracked");
        trackingState = GetChildControl<IntegerControl>("trackingState");
      }
    }

    /// <summary>
    /// The OpenXR constant that is used to reference a head-worn device. See <see href="https://www.khronos.org/registry/OpenXR/specs/1.0/html/xrspec.html#semantic-path-user">OpenXR Specification 6.3.1</see> for more information on user paths.
    /// </summary>
    private const string userPath = "/user/head";

    /// <summary>
    /// The interaction profile string used to reference the ClassVR Headset 655.
    /// </summary>
    public const string profile = "/interaction_profiles/avantis/headset655_avn";

    /// <summary>
    /// The OpenXR Extension string. This is used by OpenXR to check if this extension is available or enabled.
    /// </summary>
    public const string extensionString = "XR_AVN_headset_input";

    // Available Bindings
    /// <summary>
    /// Constant for a pose interaction binding '.../input/gaze/pose' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
    /// </summary>
    public const string gaze = "/input/gaze/pose";
    /// <summary>
    /// Constant for a boolean interaction binding '.../input/select/click' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs. This binding is only available for the <see cref="OpenXRInteractionFeature.UserPaths.head"/> user path.
    /// </summary>
    public const string select = "/input/select/click";
    /// <summary>
    /// Constant for a boolean interaction binding '.../input/back/click' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs. This binding is only available for the <see cref="OpenXRInteractionFeature.UserPaths.head"/> user path.
    /// </summary>
    public const string back = "/input/back/click";
    /// <summary>
    /// Constant for a boolean interaction binding '.../input/volume_up/click' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs. This binding is only available for the <see cref="OpenXRInteractionFeature.UserPaths.head"/> user path.
    /// </summary>
    public const string volume_up = "/input/volume_up/click";
    /// <summary>
    /// Constant for a boolean interaction binding '.../input/volume_down/click' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs. This binding is only available for the <see cref="OpenXRInteractionFeature.UserPaths.head"/> user path.
    /// </summary>
    public const string volume_down = "/input/volume_down/click";

    private const string kDeviceLocalizedName = "ClassVR Headset 655 OpenXR";

    /// <inheritdoc/>
    protected override bool OnInstanceCreate(ulong instance) {
      // Requires the Avantis headset input extension
      if (!OpenXRRuntime.IsExtensionEnabled(extensionString)) {
        return false;
      }

      return base.OnInstanceCreate(instance);
    }

    /// <summary>
    /// Registers the <see cref="ClassVrHeadset655"/> layout with the Input System.
    /// </summary>
    protected override void RegisterDeviceLayout() {
      InputSystem.RegisterLayout(typeof(ClassVrHeadset655),
        matches: new InputDeviceMatcher()
          .WithInterface(XRUtilities.InterfaceMatchAnyVersion)
          .WithProduct(kDeviceLocalizedName));
    }

    /// <summary>
    /// Removes the <see cref="ClassVrHeadset655"/> layout from the Input System.
    /// </summary>
    protected override void UnregisterDeviceLayout() {
      InputSystem.RemoveLayout(nameof(ClassVrHeadset655));
    }

    /// <summary>
    /// Return device layout string that used for registering device for the Input System.
    /// </summary>
    /// <returns>Device layout string.</returns>
    protected override string GetDeviceLayoutName() {
      return nameof(ClassVrHeadset655);
    }

    /// <inheritdoc/>
    protected override void RegisterActionMapsWithRuntime() {
      ActionMapConfig actionMap = new ActionMapConfig() {
        name = "classvrheadset655",
        localizedName = kDeviceLocalizedName,
        desiredInteractionProfile = profile,
        manufacturer = "Avantis",
        serialNumber = "",
        deviceInfos = new List<DeviceConfig>() {
          new DeviceConfig() {
            characteristics = InputDeviceCharacteristics.TrackedDevice | InputDeviceCharacteristics.HeadMounted,
            userPath = userPath
          }
        },
        actions = new List<ActionConfig>() {
          new ActionConfig() {
            name = "gazePose",
            localizedName = "Gaze Pose",
            type = ActionType.Pose,
            usages = new List<string>() { "Pointer", "Device", "gaze" },
            bindings = new List<ActionBinding>() {
              new ActionBinding()
              {
                interactionPath = gaze,
                interactionProfileName = profile,
              }
            }
          },
          new ActionConfig() {
            name = "select",
            localizedName = "Select",
            type = ActionType.Binary,
            usages = new List<string>() { "SelectButton" },
            bindings = new List<ActionBinding>() {
              new ActionBinding() {
                interactionPath = select,
                interactionProfileName = profile,
              }
            }
          },
          new ActionConfig() {
            name = "back",
            localizedName = "Back",
            type = ActionType.Binary,
            usages = new List<string>() { "Cancel" },
            bindings = new List<ActionBinding>() {
              new ActionBinding() {
                interactionPath = back,
                interactionProfileName = profile,
              }
            }
          },
          new ActionConfig() {
            name = "volumeUp",
            localizedName = "Volume Up",
            type = ActionType.Binary,
            usages = new List<string>() { "VolumeUp" },
            bindings = new List<ActionBinding>() {
              new ActionBinding() {
                interactionPath = volume_up,
                interactionProfileName = profile,
              }
            }
          },
          new ActionConfig() {
            name = "volumeDown",
            localizedName = "Volume Down",
            type = ActionType.Binary,
            usages = new List<string>() { "VolumeDown" },
            bindings = new List<ActionBinding>() {
              new ActionBinding() {
                interactionPath = volume_down,
                interactionProfileName = profile,
              }
            }
          }
        }
      };

      AddActionMap(actionMap);
    }

  }
}
