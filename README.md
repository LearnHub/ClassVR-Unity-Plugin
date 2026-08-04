# ClassVR Unity Plugin

The ClassVR Unity Plugin provides utilities and assets to enable development for ClassVR headsets and integration with ClassVR cloud services.

## Installation

Unless you intend to make changes to the Plugin, please install via [OpenUPM](https://openupm.com/packages/com.avantis.classvr/). OpenUPM installation instructions are here - https://openupm.com/packages/com.avantis.classvr/#modal-manualinstallation.

## API Usage

```csharp
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

// Upload a small file to ClassVR Shared Cloud for the current enrolled organisation (on Android)
var url = await FileUploader.UploadToSharedCloud("example.txt", "text/plain", "example file contents");
// Upload a large file to ClassVR Shared Cloud (on Android)
var filePath = Path.Combine(Application.temporaryCachePath, "filename.txt");
... (write data to file)
var url = await FileUploader.UploadToSharedCloud(filePath, "text/plain");
// Upload a file to AVNFS only, without associating it with any organisation (on Android)
var url = await FileUploader.UploadToAvnfs("example.txt", "text/plain", "example file contents");

// Query files in the ClassVR Shared Cloud for the current enrolled organisation (on Android)
var query = new CloudFileQuery { MediaTypes = { "image/png" }, OrderBy = CloudFileOrder.NewestFirst };
await foreach (CloudFile file in CloudFiles.Search(query)) { Debug.Log($"{file.FileName}"); }
// Or to load page-by-page
await foreach (CloudFilePage page in CloudFiles.Search(query).AsPages(pageSize: 10)) {}

// Retrieve a file from an Android ContentProvider
var url = "content://...";
var filePath = await ContentProviderClient.GetFile(url);

// Disable tilt to spin - note this requires the ClassVR Tilt-To-Spin OpenXR feature (see below)
var tiltToSpinFeature = OpenXRSettings.Instance.GetFeature<ClassVrTiltToSpinFeature>();
tiltToSpinFeature.EnableTiltToSpin(false);
```

## OpenXR Integration

### OpenXR Features

This plugin provides the **ClassVR Tilt-To-Spin** feature, which can be enabled via XR Plug-in Management. Further information on OpenXR Features can be found in the [Unity OpenXR Plugin docs](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.3/manual/features.html).

### OpenXR Interaction Profiles

This plugin provides Unity support for the **ClassVR 655 Headset interaction profile**, which can be enabled via XR Plug-in Management. One enabled, interaction paths are available to the Unity Input System. Note that this interaction profile only includes inputs on the headset itself, for controllers please use the Oculus Touch Controller Profile.

Further information on OpenXR Interaction Profiles can be found in the [Unity OpenXR Plugin docs](https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.3/manual/input.html).

## ClassVR File Management

The ClassVR cloud infrastructure stores all files in a flat filesystem called AVNFS. Files in AVNFS can be accessed via their unique URL.

Optionally, files stored in AVNFS can be associated with an organisation (org) in ClassVR. If so, they will appear in the Shared Cloud section on the Portal for that org, and can be browsed by users (usually teachers). Files associated with a ClassVR org can be queried by devices enrolled in the same org.

### Downloads

To download files from ClassVR, use the `ContentProviderClient.GetFile` method with a URL of the form:

> `content://avnfs.com/{hash}?size={bytes}&type={mime}&name={name}` (name is optional)

You can replace `https://` with `content://` in any AVNFS URL to use the ContentProviderClient.

This method will download the file, store it in a temporary cache directory, and return the absolute path to the file which can then be used for normal file operations.

This plugin aims to be agnostic as to which ContentProvider is being accessed, meaning you should be able to use `ContentProviderClient` to download files from other ContentProviders. However, this functionality has only been tested against the ClassVR AVNFS ContentProvider.

### Uploads

To upload files to ClassVR and associate with an organization, use the `FileUploader.UploadToSharedCloud` method. You can use any of the overloads, but for large files it's recommended to write to a temporary file and use the overload which takes a file path.

This method will assign the file to the Shared Cloud library for the organization that the device is currently registered to.

If you only want to upload a file to AVNFS and get its URL — without it appearing in any organization's Shared Cloud library — use `FileUploader.UploadToAvnfs` instead. It accepts the same set of overloads (string, byte array, or file path) and returns the AVNFS URL on success, or `null` on failure.

### Queries

To search files in the Shared Cloud, use `CloudFiles.Search`. It returns a lazily-evaluated, auto-paging sequence — no request is made to the cloud until you start enumerating it. On a ClassVR device the authentication token and organisation are read from the device automatically; off-device (e.g. in the Editor) you must supply a `jwt` and set `OrganizationId` on the query.

Build the search with a `CloudFileQuery`. Every property is an optional filter, so an empty query returns every file the device's organisation can see:

| Property | Description |
| --- | --- |
| `Text` | Free-text search across the files. |
| `MediaTypes` | Restrict to these media (MIME) types. |
| `Tags` + `TagMatch` | Restrict to files matching these tag IDs — `TagMatch.All` (default) or `TagMatch.Any`. |
| `CreatedAfter` / `CreatedBefore` | Restrict to a time range. |
| `OrderBy` | Result ordering, e.g. `CloudFileOrder.NewestFirst` (default is the server's order). |

This example will stream every match — paging is handled for you:

```csharp
var query = new CloudFileQuery { MediaTypes = { "image/png" }, OrderBy = CloudFileOrder.NewestFirst };
await foreach (CloudFile file in CloudFiles.Search(query)) {
  Debug.Log($"{file.FileName} ({file.MediaType}) — {file.FileUrl}");
}
```

Each `CloudFile` exposes `Id`, `FileName`, `FileUrl`, `MediaType`, `SizeBytes`, `IconUrl`, `Updated` and `Tags`.

To drive paging yourself, use `AsPages`. Each `CloudFilePage` has the page's `Files` and a `NextPageToken` that is `null` on the last page:

```csharp
await foreach (CloudFilePage page in CloudFiles.Search(query).AsPages(pageSize: 10)) {
  Debug.Log($"Got a page of {page.Files.Count} file(s)");
  if (page.NextPageToken == null) break;
}
```

Enumeration can be cancelled with a `CancellationToken` (`CloudFiles.Search(query).WithCancellation(token)`), and a failed cloud request throws an `RpcException`.

## Intents and Deep Linking

To receive new intents while the app is running, direct intents to `com.classvr.cvr_unity_java.DeepLinkActivity` via [intent filters](https://developer.android.com/guide/components/intents-filters) in the manifest.

1. In your Unity player settings or build profile, Enable **Custom Main Manifest** under Publishing Settings
1. Open the file `Assets/Plugins/Android/AndroidManifest.xml`
1. Add a new activity element with attribute `android:name="com.classvr.cvr_unity_java.DeepLinkActivity"` and add intent filters to the activity (example below)

### Example Manifest

This example shows the whole AndroidManifest.xml. You must replace the "placeholder" strings in the data elements.

```xml
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
