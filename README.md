# A Unity 6.1 (6000.1.8f1) Plugin that adds a new OpenXR Feature to enable the Tilt-To-Spin Explicit OpenXR API Layer

Folder structure:

### 1."Custom_OpenXR_Loader" folder can be used as a template to:
- Enable Explicit OpenXR Layers (Unity does not have a mechanism to enable/disable Explicit OpenXR API Layers as they are passed in xrCreateInstance).
- Hook different OpenXR functions.
- Creates a shim openxr_loader.so library that internally loads the openxr_loader_real.so which is suppolied by Unity Package.
- Can be used to change initialization behaviour or hook OpenXR function calls which cannot be controller from Unity IDE.
- Builds an .aar file which can be used in the "Unity_Plugin".

### 2."Unity_Plugin" folder is a template for:
- A simple OpenXR Feature that allows the "Custom_OpenXR_Loader" - which enables Tilt-To-Spin OpenXR API layer - to be included in the Build for Android.
![preview](https://github.com/user-attachments/assets/a549c7aa-1ef1-4ec7-89c8-e1a53d4e4c21)

- If enabled: bundles our "Custom_OpenXR_Loader" in the APK generation & moves Unity's openxr_loader.aar file in the Project's Assets/Plugins folder & excludes it from build.
- If fisabled: excludes our "Custom_OpenXR_Loader" from build & includes Unity's openxr_loader.aar (Standard Khronos Loader).

## Installation:
- Grab the latest Release from the "Release" page.
- Unzip-it to your Unity's Project folder.
