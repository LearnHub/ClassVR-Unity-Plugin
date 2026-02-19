# ClassVR Unity Plugin

The ClassVR Unity Plugin provides utilities and assets to enable development for ClassVR headsets and integration with ClassVR cloud services.

## Installation

Unless you intend to make changes to the Plugin, please install via [OpenUPM](https://openupm.com/packages/com.avantis.classvr/). OpenUPM installation instructions are here - https://openupm.com/packages/com.avantis.classvr/#modal-manualinstallation.

## API Usage

```
using ClassVR;
...
// Read ClassVR headset properties
var headsetName = CVRProperties.Instance.DisplayName;

// Subscribe to new Android intents - recommended
// Note that OnIntentReceived will be called immediately if an intent is cached
IntentProvider.IntentReceived += OnIntentReceived;
...
void OnIntentReceived(AndroidIntent intent) {...}
// Read the latest Android intent - not recommended, subscribe to IntentReceived instead
var intent = IntentProvider.LatestIntent;

// Read the system locale
var sysLocale = SystemProperties.GetSysLocale();

// Send an analytics event (fire and forget)
_ = Analytics.SendEvent("example_action", "example_source");
// Send an analytics event (and wait for it to complete)
await Analytics.SendEvent("example_action", "example_source");

// Upload a small file to ClassVR Shared Cloud (on Android)
var url = await FileUploader.UploadToSharedCloud("example.txt", "text/plain", "example file contents");
// Upload a large file to ClassVR Shared Cloud (on Android)
var filePath = Path.Combine(Application.temporaryCachePath, "filename.txt");
... (write data to file)
var url = await FileUploader.UploadToSharedCloud(filePath, "text/plain");

// Retrieve a file from an Android ContentProvider
var url = "content://...";
var filePath = await ContentProviderClient.GetFile(url);

// Disable tilt to spin - note this requires the ClassVR Tilt-To-Spin OpenXR feature (see below)
var tiltToSpinFeature = OpenXRSettings.Instance.GetFeature<ClassVrTiltToSpinFeature>();
tiltToSpinFeature.EnableTiltToSpin(false);
```

## OpenXR Features

This plugin provides the **ClassVR Tilt-To-Spin** feature, which can be enabled via XR Plugin-in Management. Further information on OpenXR Features can be found in the [Unity OpenXR Plugin docs](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.15/manual/features.html).

## ClassVR File Download/Upload

### Downloads

To download files from ClassVR, use the `ContentProviderClient.GetFile` method with a URL of the form:

> `content://avnfs.com/{hash}?size={bytes}&type={mime}&name={name}` (name is optional)

You can replace `https://` with `content://` in any AVNFS URL to use the ContentProviderClient.

This method will download the file, store it in a temporary cache directory, and return the absolute path to the file which can then be used for normal file operations.

This plugin aims to be agnostic as to which ContentProvider is being accessed, meaning you should be able to use `ContentProviderClient` to download files from other ContentProviders. However, this functionality has only been tested against the ClassVR AVNFS ContentProvider.

### Uploads

To upload files to ClassVR, use the `FileUploader.UploadToSharedCloud` method. You can use any of the overloads, but for large files it's recommended to write to a temporary file and use the overload which takes a file path.

This method will assign the file to the Shared Cloud library for the organization that the device is currently registered to.

## Intents and Deep Linking

To receive new intents while the app is running, direct intents to `com.classvr.cvr_unity_java.DeepLinkActivity` via [intent filters](https://developer.android.com/guide/components/intents-filters) in the manifest.

1. In your Unity player settings or build profile, Enable **Custom Main Manifest** under Publishing Settings
1. Open the file `Assets/Plugins/Android/AndroidManifest.xml`
1. Add a new activity element with attribute `android:name="com.classvr.cvr_unity_java.DeepLinkActivity"` and add intent filters to the activity (example below)

### Example Manifest

This example shows the whole AndroidManifest.xml. You must replace the "placeholder" strings in the data elements.

```
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android" xmlns:tools="http://schemas.android.com/tools">
  <application>
    <activity android:name="com.unity3d.player.UnityPlayerActivity" android:theme="@style/UnityThemeSelector">
      <intent-filter>
        <action android:name="android.intent.action.MAIN" />
        <category android:name="android.intent.category.LAUNCHER" />
      </intent-filter>
      <meta-data android:name="unityplayer.UnityActivity" android:value="true" />
    </activity>
    <activity android:name="com.classvr.cvr_unity_java.DeepLinkActivity">
      <intent-filter>
        <action android:name="android.intent.action.VIEW" />
        <category android:name="android.intent.category.DEFAULT" />
        <data android:mimeType="placeholder" />
      </intent-filter>
      <intent-filter>
        <action android:name="android.intent.action.VIEW" />
        <category android:name="android.intent.category.DEFAULT" />
        <category android:name="android.intent.category.BROWSABLE" />
        <data android:scheme="placeholder" android:host="placeholder" />
      </intent-filter>
      <meta-data android:name="unityplayer.UnityActivity" android:value="true" />
    </activity>
  </application>
</manifest>
```
