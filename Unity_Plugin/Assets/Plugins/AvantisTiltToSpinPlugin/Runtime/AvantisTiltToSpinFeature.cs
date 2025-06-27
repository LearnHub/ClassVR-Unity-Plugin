using System;
using UnityEngine;
using UnityEngine.XR.OpenXR.Features;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif

#if UNITY_EDITOR
[OpenXRFeatureAttribute(
    BuildTargetGroups = new[] { BuildTargetGroup.Android },
    UiName = "Avantis Tilt-To-Spin",
    Company = "Avantis",
    Desc = "Enables Avantis Tilt-To-Spin OpenXR Api Layer.",
    DocumentationLink = "https://classvr.com",
    Version = "1.0.0",
    FeatureId = "com.avantis.openxr.tilttospin",
    Required = true
)]
#endif
//public class CustomOpenXRFeature : OpenXRFeature
public class AvantisTiltToSpinFeature : OpenXRFeature
{
    protected override void OnSubsystemCreate() => Debug.Log("[CustomOpenXR] Subsystem created");
    protected override void OnSubsystemStart() => Debug.Log("[CustomOpenXR] Subsystem started");
    protected override void OnSubsystemStop() => Debug.Log("[CustomOpenXR] Subsystem stopped");
    protected override void OnSubsystemDestroy() => Debug.Log("[CustomOpenXR] Subsystem destroyed");
}
