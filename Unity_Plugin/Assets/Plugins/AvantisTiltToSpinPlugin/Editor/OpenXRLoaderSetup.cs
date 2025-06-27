#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

public class OpenXRLoaderSetup : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    private const string CustomAARPath = "Assets/Plugins/AvantisTiltToSpinPlugin/Plugins/Android/custom_openxr_loader.aar";
    private const string UnityAARPath = "Assets/Plugins/UnityOpenXRLoader/android/openxr_loader.aar";

    public void OnPreprocessBuild(BuildReport report)
    {
        EnsureUnityOpenXRAARCopied();

        bool isCustomFeatureEnabled = IsCustomFeatureEnabled(report.summary.platform);
        Debug.Log($"[OpenXRLoaderSetup] AvantisTiltToSpinFeature enabled: {isCustomFeatureEnabled}");

        if (isCustomFeatureEnabled)
        {
            SetPluginIncludeInAndroidBuild(CustomAARPath, true);
            SetPluginIncludeInAndroidBuild(UnityAARPath, false);
        }
        else
        {
            SetPluginIncludeInAndroidBuild(CustomAARPath, false);
            SetPluginIncludeInAndroidBuild(UnityAARPath, true);
        }
    }

    private bool IsCustomFeatureEnabled(BuildTarget target)
    {
        var group = BuildPipeline.GetBuildTargetGroup(target);
        var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
        if (settings == null) return false;

        foreach (var feature in settings.GetFeatures<OpenXRFeature>())
        {
            if (feature != null && feature.GetType().Name == "AvantisTiltToSpinFeature" && feature.enabled)
                return true;
        }
        return false;
    }

    private void SetPluginIncludeInAndroidBuild(string path, bool include)
    {
        var importer = AssetImporter.GetAtPath(path) as PluginImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[OpenXRLoaderSetup] PluginImporter not found at path: {path}");
            return;
        }

        importer.SetCompatibleWithAnyPlatform(false);
        importer.SetCompatibleWithPlatform(BuildTarget.Android, include);
        if (include)
        {
            importer.SetPlatformData(BuildTarget.Android, "CPU", "ARM64");
            importer.SetPlatformData(BuildTarget.Android, "Library", "true");
        }
        importer.SaveAndReimport();

        Debug.Log($"[OpenXRLoaderSetup] {(include ? "Enabled" : "Disabled")} plugin for Android: {path}");
    }

    private void EnsureUnityOpenXRAARCopied()
    {
        string destinationPath = Path.Combine(Application.dataPath, "Plugins/UnityOpenXRLoader/android/openxr_loader.aar");
        string sourceAAR = FindUnityOpenXRAARInPackageCache();
        if (sourceAAR == null || !File.Exists(sourceAAR))
        {
            Debug.LogError("[OpenXRLoaderSetup] Unity openxr_loader.aar not found in PackageCache.");
            return;
        }

        try
        {
            // Make a copy of the original Unity Loader in our project and delete it.
            // We need to do this in order to be able to exclude it from the build if the Feature is enabled.
            // The AssetImporter cannot include/exclude aar's if not in the Assets folder and unity's OpenXR loader is in the FindUnityOpenXRAARInPackageCache().
            // We need to delete it since we will get a clash on openxr_loader.so file (found in both aar) and Gradle doesn't know which to copy or use.
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
                Debug.Log($"[OpenXRLoaderSetup] Deleted existing openxr_loader.aar at: {destinationPath}");
            }

            File.Move(sourceAAR, destinationPath);
            Debug.Log($"[OpenXRLoaderSetup] Moved Unity openxr_loader.aar to: {destinationPath}");

            AssetDatabase.Refresh();
        }
        catch (IOException ex)
        {
            Debug.LogError($"[OpenXRLoaderSetup] Failed to move openxr_loader.aar: {ex.Message}");
        }
    }



    private string FindUnityOpenXRAARInPackageCache()
    {
        string packageCacheDir = Path.Combine("Library", "PackageCache");

        if (!Directory.Exists(packageCacheDir))
        {
            Debug.LogWarning("[OpenXRLoaderSetup] PackageCache directory not found.");
            return null;
        }

        foreach (var dir in Directory.GetDirectories(packageCacheDir))
        {
            if (dir.Contains("com.unity.xr.openxr@"))
            {
                string aarPath = Path.Combine(dir, "RuntimeLoaders", "android", "openxr_loader.aar");
                if (File.Exists(aarPath))
                    return aarPath;
            }
        }

        return null;
    }
}
#endif
