using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ClassVR {
  /// <summary>
  /// WARNING: Do not use this class directly from Unity, use <see cref="FileUploader"/>.
  /// Uploads files to AVNFS via the on-device AVNFS ContentProvider (content://avnfs.com).
  /// Supports uploading byte arrays directly or streaming from file/content URIs.
  /// Note: This only uploads to AVNFS, but doesn't assign it to an organization.
  /// </summary>
  public static class AvnfsUploader {
    private const string JavaClassName = "com.classvr.cvr_unity_java.AvnfsUploadHelper";

#if !UNITY_EDITOR && UNITY_ANDROID
    private static readonly AndroidJavaClass _javaClass = new AndroidJavaClass(JavaClassName);
#endif

    /// <summary>
    /// Result of an AVNFS upload operation.
    /// </summary>
    [Serializable]
    public class UploadResult {
      public bool Success;
      public string AvnfsUrl;
      public bool AlreadyExisted;
      public string Error;
    }

#region Upload Byte Array

    /// <summary>
    /// Uploads a byte array to AVNFS synchronously.
    /// WARNING: This blocks the calling thread. Use <see cref="UploadAsync(byte[], string, string, string)"/> for large files.
    /// Note: Bundle has a ~1MB Binder transaction limit. For larger files, use the URI overload instead.
    /// </summary>
    /// <param name="data">The file contents as a byte array.</param>
    /// <param name="mediaType">The MIME type of the file (e.g. "image/png").</param>
    /// <param name="jwt">JWT token for authentication.</param>
    /// <param name="name">Optional filename (e.g. "photo.png").</param>
    /// <returns>An UploadResult with the AVNFS URL on success, or error details on failure.</returns>
    public static UploadResult Upload(byte[] data, string mediaType, string jwt, string name = null) {
      if (data == null || data.Length == 0) {
        Debug.LogError("AvnfsUploader: data is null or empty.");
        return new UploadResult { Error = "data is null or empty" };
      }
      if (string.IsNullOrEmpty(mediaType)) {
        Debug.LogError("AvnfsUploader: mediaType is null or empty.");
        return new UploadResult { Error = "mediaType is null or empty" };
      }
      if (string.IsNullOrEmpty(jwt)) {
        Debug.LogError("AvnfsUploader: jwt is null or empty.");
        return new UploadResult { Error = "jwt is null or empty" };
      }

#if !UNITY_EDITOR && UNITY_ANDROID
      try {
        var json = _javaClass.CallStatic<string>("uploadByteArray", ToSignedBytes(data), mediaType, name, jwt);
        return ParseResult(json);
      } catch (Exception ex) {
        Debug.LogError($"AvnfsUploader: Exception during byte array upload: {ex.Message}");
        Debug.LogException(ex);
        return new UploadResult { Error = ex.Message };
      }
#else
      Debug.LogWarning("AvnfsUploader.Upload is only supported on Android.");
      return new UploadResult { Error = "Not supported on this platform" };
#endif
    }

    /// <summary>
    /// Uploads a byte array to AVNFS asynchronously on a background thread.
    /// Note: Bundle has a ~1MB Binder transaction limit. For larger files, use the URI overload instead.
    /// </summary>
    /// <param name="data">The file contents as a byte array.</param>
    /// <param name="mediaType">The MIME type of the file (e.g. "image/png").</param>
    /// <param name="jwt">JWT token for authentication.</param>
    /// <param name="name">Optional filename (e.g. "photo.png").</param>
    /// <returns>A Task resolving to an UploadResult.</returns>
    public static Task<UploadResult> UploadAsync(byte[] data, string mediaType, string jwt, string name = null) {
      if (data == null || data.Length == 0) {
        Debug.LogError("AvnfsUploader: data is null or empty.");
        return Task.FromResult(new UploadResult { Error = "data is null or empty" });
      }
      if (string.IsNullOrEmpty(mediaType)) {
        Debug.LogError("AvnfsUploader: mediaType is null or empty.");
        return Task.FromResult(new UploadResult { Error = "mediaType is null or empty" });
      }
      if (string.IsNullOrEmpty(jwt)) {
        Debug.LogError("AvnfsUploader: jwt is null or empty.");
        return Task.FromResult(new UploadResult { Error = "jwt is null or empty" });
      }

#if !UNITY_EDITOR && UNITY_ANDROID
      var tcs = new TaskCompletionSource<UploadResult>();

      try {
        var callback = new UploadCallbackProxy(tcs);
        _javaClass.CallStatic("uploadByteArrayAsync", ToSignedBytes(data), mediaType, name, jwt, callback);
      } catch (Exception ex) {
        Debug.LogError($"AvnfsUploader: Exception starting async byte array upload: {ex.Message}");
        Debug.LogException(ex);
        tcs.TrySetResult(new UploadResult { Error = ex.Message });
      }

      return tcs.Task;
#else
      Debug.LogWarning("AvnfsUploader.UploadAsync is only supported on Android.");
      return Task.FromResult(new UploadResult { Error = "Not supported on this platform" });
#endif
    }

#endregion
#region Upload from URI

    /// <summary>
    /// Uploads a file from a URI to AVNFS synchronously.
    /// More memory-efficient than the byte array overload — the ContentProvider streams the file.
    /// WARNING: This blocks the calling thread. Use <see cref="UploadAsync(string, string, string, string)"/> for large files.
    /// </summary>
    /// <param name="sourceUri">The file:// or content:// URI of the source file.</param>
    /// <param name="mediaType">The MIME type of the file (e.g. "video/mp4").</param>
    /// <param name="jwt">JWT token for authentication.</param>
    /// <param name="name">Optional filename (e.g. "video.mp4").</param>
    /// <returns>An UploadResult with the AVNFS URL on success, or error details on failure.</returns>
    public static UploadResult Upload(string sourceUri, string mediaType, string jwt, string name = null) {
      if (string.IsNullOrEmpty(sourceUri)) {
        Debug.LogError("AvnfsUploader: sourceUri is null or empty.");
        return new UploadResult { Error = "sourceUri is null or empty" };
      }
      if (string.IsNullOrEmpty(mediaType)) {
        Debug.LogError("AvnfsUploader: mediaType is null or empty.");
        return new UploadResult { Error = "mediaType is null or empty" };
      }
      if (string.IsNullOrEmpty(jwt)) {
        Debug.LogError("AvnfsUploader: jwt is null or empty.");
        return new UploadResult { Error = "jwt is null or empty" };
      }

#if !UNITY_EDITOR && UNITY_ANDROID
      try {
        var json = _javaClass.CallStatic<string>("uploadFromUri", sourceUri, mediaType, name, jwt);
        return ParseResult(json);
      } catch (Exception ex) {
        Debug.LogError($"AvnfsUploader: Exception during URI upload: {ex.Message}");
        Debug.LogException(ex);
        return new UploadResult { Error = ex.Message };
      }
#else
      Debug.LogWarning("AvnfsUploader.Upload is only supported on Android.");
      return new UploadResult { Error = "Not supported on this platform" };
#endif
    }

    /// <summary>
    /// Uploads a file from a URI to AVNFS asynchronously on a background thread.
    /// More memory-efficient than the byte array overload — the ContentProvider streams the file.
    /// </summary>
    /// <param name="sourceUri">The file:// or content:// URI of the source file.</param>
    /// <param name="mediaType">The MIME type of the file (e.g. "video/mp4").</param>
    /// <param name="jwt">JWT token for authentication.</param>
    /// <param name="name">Optional filename (e.g. "video.mp4").</param>
    /// <returns>A Task resolving to an UploadResult.</returns>
    public static Task<UploadResult> UploadAsync(string sourceUri, string mediaType, string jwt, string name = null) {
      if (string.IsNullOrEmpty(sourceUri)) {
        Debug.LogError("AvnfsUploader: sourceUri is null or empty.");
        return Task.FromResult(new UploadResult { Error = "sourceUri is null or empty" });
      }
      if (string.IsNullOrEmpty(mediaType)) {
        Debug.LogError("AvnfsUploader: mediaType is null or empty.");
        return Task.FromResult(new UploadResult { Error = "mediaType is null or empty" });
      }
      if (string.IsNullOrEmpty(jwt)) {
        Debug.LogError("AvnfsUploader: jwt is null or empty.");
        return Task.FromResult(new UploadResult { Error = "jwt is null or empty" });
      }

#if !UNITY_EDITOR && UNITY_ANDROID
      var tcs = new TaskCompletionSource<UploadResult>();

      try {
        var callback = new UploadCallbackProxy(tcs);
        _javaClass.CallStatic("uploadFromUriAsync", sourceUri, mediaType, name, jwt, callback);
      } catch (Exception ex) {
        Debug.LogError($"AvnfsUploader: Exception starting async URI upload: {ex.Message}");
        Debug.LogException(ex);
        tcs.TrySetResult(new UploadResult { Error = ex.Message });
      }

      return tcs.Task;
#else
      Debug.LogWarning("AvnfsUploader.UploadAsync is only supported on Android.");
      return Task.FromResult(new UploadResult { Error = "Not supported on this platform" });
#endif
    }

#endregion

    // Unity's JNI bridge requires sbyte[] (not byte[]) to map to Java's signed byte[].
    // The bit patterns are identical — this is a type annotation, not a data conversion.
    private static sbyte[] ToSignedBytes(byte[] data) {
      var signed = new sbyte[data.Length];
      Buffer.BlockCopy(data, 0, signed, 0, data.Length);
      return signed;
    }

    private static UploadResult ParseResult(string json) {
      if (string.IsNullOrEmpty(json)) {
        Debug.LogError("AvnfsUploader: Received null or empty result from Java.");
        return new UploadResult { Error = "Received null or empty result from Java" };
      }

      var result = JsonUtility.FromJson<UploadResult>(json);
      if (!result.Success && !string.IsNullOrEmpty(result.Error)) {
        Debug.LogError($"AvnfsUploader: Upload failed: {result.Error}");
      }
      return result;
    }

    // AndroidJavaProxy callback implementation
    private class UploadCallbackProxy : AndroidJavaProxy {
      private readonly TaskCompletionSource<UploadResult> _tcs;

      public UploadCallbackProxy(TaskCompletionSource<UploadResult> tcs)
          : base("com.classvr.cvr_unity_java.AvnfsUploadCallbackInterface") {
        _tcs = tcs;
      }

      // Called from the Java background thread when upload completes
      public void onUploadComplete(string resultJson) {
        var result = ParseResult(resultJson);
        _tcs.TrySetResult(result);
      }
    }
  }
}
