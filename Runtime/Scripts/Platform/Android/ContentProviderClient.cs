using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ClassVR {
  /// <summary>
  /// Provides methods to retrieve files from Android ContentProviders.
  /// Files are copied from content:// URIs into a local cache directory
  /// and the resulting filepath is returned for use by Unity.
  /// </summary>
  public static class ContentProviderClient {
    private const string JavaClassName = "com.classvr.cvr_unity_java.ContentProviderHelper";

#if !UNITY_EDITOR && UNITY_ANDROID
    private static readonly AndroidJavaClass _javaClass = new AndroidJavaClass(JavaClassName);
#endif

    /// <summary>
    /// Retrieves a file from the specified content URI synchronously.
    /// The file is copied to a local cache directory and the filepath is returned.
    /// WARNING: This blocks the calling thread. Use <see cref="GetFileAsync"/> for large files.
    /// </summary>
    /// <param name="contentUri">A content:// URI string.</param>
    /// <returns>The absolute path to the cached file, or null if retrieval failed.</returns>
    public static string GetFile(string contentUri) {
      if (string.IsNullOrEmpty(contentUri)) {
        Debug.LogError("ContentProviderClient: contentUri is null or empty.");
        return null;
      }

#if !UNITY_EDITOR && UNITY_ANDROID
      try {
        var filePath = _javaClass.CallStatic<string>("getFileFromContentProvider", contentUri);
        if (string.IsNullOrEmpty(filePath)) {
          Debug.LogError($"ContentProviderClient: Failed to retrieve file from '{contentUri}'.");
        }
        return filePath;
      } catch (Exception ex) {
        Debug.LogError($"ContentProviderClient: Exception retrieving file from '{contentUri}': {ex.Message}");
        Debug.LogException(ex);
        return null;
      }
#else
      Debug.LogWarning("ContentProviderClient.GetFile is only supported on Android.");
      return null;
#endif
    }

    /// <summary>
    /// Retrieves a file from the specified content URI asynchronously.
    /// The file is copied to a local cache directory on a background thread.
    /// </summary>
    /// <param name="contentUri">A content:// URI string.</param>
    /// <returns>A Task resolving to the absolute path of the cached file, or null on failure.</returns>
    public static Task<string> GetFileAsync(string contentUri) {
      if (string.IsNullOrEmpty(contentUri)) {
        Debug.LogError("ContentProviderClient: contentUri is null or empty.");
        return Task.FromResult<string>(null);
      }

#if !UNITY_EDITOR && UNITY_ANDROID
      var tcs = new TaskCompletionSource<string>();

      try {
        var callback = new ContentProviderCallbackProxy(tcs);
        _javaClass.CallStatic("getFileFromContentProviderAsync", contentUri, callback);
      } catch (Exception ex) {
        Debug.LogError($"ContentProviderClient: Exception starting async retrieval for '{contentUri}': {ex.Message}");
        Debug.LogException(ex);
        tcs.TrySetResult(null);
      }

      return tcs.Task;
#else
      Debug.LogWarning("ContentProviderClient.GetFileAsync is only supported on Android.");
      return Task.FromResult<string>(null);
#endif
    }

    /// <summary>
    /// Clears all cached files retrieved from ContentProviders.
    /// </summary>
    /// <returns>The number of files deleted.</returns>
    public static int ClearCache() {
#if !UNITY_EDITOR && UNITY_ANDROID
      try {
        return _javaClass.CallStatic<int>("clearCache");
      } catch (Exception ex) {
        Debug.LogError($"ContentProviderClient: Exception clearing cache: {ex.Message}");
        Debug.LogException(ex);
        return 0;
      }
#else
      Debug.LogWarning("ContentProviderClient.ClearCache is only supported on Android.");
      return 0;
#endif
    }

    /// <summary>
    /// Implements the Java ContentProviderCallbackInterface via AndroidJavaProxy.
    /// The callback is invoked from a Java background thread.
    /// TaskCompletionSource is thread-safe, so TrySetResult can be called from any thread.
    /// </summary>
    private class ContentProviderCallbackProxy : AndroidJavaProxy {
      private readonly TaskCompletionSource<string> _tcs;

      public ContentProviderCallbackProxy(TaskCompletionSource<string> tcs)
          : base("com.classvr.cvr_unity_java.ContentProviderCallbackInterface") {
        _tcs = tcs;
      }

      // Called from the Java background thread when file retrieval completes
      public void onFileRetrieved(string filePath) {
        if (string.IsNullOrEmpty(filePath)) {
          Debug.LogError($"ContentProviderClient: Async retrieval failed.");
        }
        _tcs.TrySetResult(filePath);
      }
    }
  }
}
