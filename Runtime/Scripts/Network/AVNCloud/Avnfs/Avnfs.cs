using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using ClassVR.Platform.Android;
using UnityEngine.Networking;

namespace ClassVR.Network.AvnCloud {
  // Fetches an AVNFS file to a local path, returning true on success.
  // The web request takes the https form of the URL, the on-device provider the content form. Tests
  // substitute a lambda that writes bytes, so the cache, integrity check and cancellation logic can be
  // exercised without a network or a device. An implementation logs its own failures before returning
  // false, and may throw OperationCanceledException.
  internal delegate Task<bool> AvnfsDownload(AvnfsUri uri, string destinationPath, CancellationToken cancellationToken);

  // Copies a content URI to a destination path, returning the path or null. ContentProviderClient in
  // production; tests substitute a fake.
  internal delegate Task<string> ContentProviderFetch(string contentUri, string destinationPath);

  /// <summary>
  /// AVNFS is the flat, hash-addressed file store behind ClassVR. Retrieves files by URL, caching them
  /// locally by hash, and uploads new ones. For files associated with an organisation's Shared Cloud, see
  /// <see cref="CloudFiles"/>.
  /// </summary>
  public static class Avnfs {
    private const string CacheSubdirectory = "avnfs";
    private const string PartialSuffix = ".part";

    /// <summary>
    /// Gets a local copy of an AVNFS file, fetching it if not cached.
    /// <para>
    /// On a ClassVR headset the file comes from the on-device AVNFS ContentProvider, which has its own
    /// cache and verifies the file itself. Everywhere else, it is downloaded over HTTPS and verified
    /// against the hash in the URL.
    /// </para>
    /// <para>
    /// Must be awaited from the main thread: the fetch starts with a <see cref="UnityWebRequest"/> or a
    /// JNI call, and the result comes back on the main thread via Unity's synchronization context.
    /// </para>
    /// </summary>
    /// <param name="url">An AVNFS URL, in its <c>https://</c> or <c>content://</c> form. See <see cref="AvnfsUri"/>.</param>
    /// <param name="cancellationToken">
    /// Aborts the download, or stops waiting for the provider. Meant for a caller that no longer wants the file at all.
    /// It is not a timeout: a slow download is not a failure. Cancel from the main thread.
    /// </param>
    /// <returns>
    /// The absolute path of the file, named by its hash with no extension, or <c>null</c> if the download failed.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="url"/> is not an AVNFS URL.</exception>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is cancelled.</exception>
    public static Task<string> GetFileAsync(string url, CancellationToken cancellationToken = default) {
      var uri = AvnfsUri.Parse(url);
#if !UNITY_EDITOR && UNITY_ANDROID
      if (UseContentProvider(uri, ContentProviderClient.HasProvider)) {
        var viaProvider = ContentProviderDownload(ContentProviderClient.GetFileAsync);
        return GetFileAsync(uri, viaProvider, verifyHash: false, CacheDirectory, cancellationToken);
      }
#endif
      return GetFileAsync(uri, DownloadWithWebRequest, verifyHash: true, CacheDirectory, cancellationToken);
    }

    /// <summary>
    /// Uploads a file to AVNFS and returns its URL, without associating it with any organisation. Use
    /// <see cref="CloudFiles.Upload(string, string, string, EndpointServer, string)"/> instead if the file
    /// should appear in the device organisation's Shared Cloud library.
    /// </summary>
    /// <param name="filename">The name and extension of the file.</param>
    /// <param name="mediaType">The media (MIME) type of the file.</param>
    /// <param name="data">The file contents as a string.</param>
    /// <param name="endpointServer">The endpoint to use. Defaults to Production.</param>
    /// <param name="jwt">Optional JWT for authentication. If null, uses the device JWT from CVRProperties (only available on Android).</param>
    /// <returns>The AVNFS URL the file can be fetched from, or <c>null</c> if the upload failed.</returns>
    public static Task<string> UploadAsync(string filename, string mediaType, string data, EndpointServer endpointServer = EndpointServer.Production, string jwt = null) {
      return FileUploader.UploadBytesToAvnfs(filename, mediaType, Encoding.UTF8.GetBytes(data), endpointServer, jwt);
    }

    /// <summary>
    /// Uploads a file already on disk to AVNFS and returns its URL, without associating it with any
    /// organisation. Use <see cref="CloudFiles.Upload(string, string, EndpointServer, string)"/> instead if
    /// the file should appear in the device organisation's Shared Cloud library. The display name is the
    /// file name part of the path. The maximum file size is 5GB.
    /// </summary>
    /// <param name="filePath">Local file path, not a URI.</param>
    /// <param name="mediaType">The media (MIME) type of the file.</param>
    /// <param name="endpointServer">The endpoint to use. Defaults to Production.</param>
    /// <param name="jwt">Optional JWT for authentication. If null, uses the device JWT from CVRProperties (only available on Android).</param>
    /// <returns>The AVNFS URL the file can be fetched from, or <c>null</c> if the upload failed.</returns>
    public static Task<string> UploadAsync(string filePath, string mediaType, EndpointServer endpointServer = EndpointServer.Production, string jwt = null) {
      return FileUploader.UploadFileToAvnfs(filePath, mediaType, endpointServer, jwt);
    }

    /// <summary>
    /// Uploads a byte array to AVNFS and returns its URL, without associating it with any organisation. Use
    /// <see cref="CloudFiles.Upload(string, string, byte[], EndpointServer, string)"/> instead if the file
    /// should appear in the device organisation's Shared Cloud library.
    /// </summary>
    /// <param name="filename">The name and extension of the file.</param>
    /// <param name="mediaType">The media (MIME) type of the file.</param>
    /// <param name="data">The file contents.</param>
    /// <param name="endpointServer">The endpoint to use. Defaults to Production.</param>
    /// <param name="jwt">Optional JWT for authentication. If null, uses the device JWT from CVRProperties (only available on Android).</param>
    /// <returns>The AVNFS URL the file can be fetched from, or <c>null</c> if the upload failed.</returns>
    public static Task<string> UploadAsync(string filename, string mediaType, byte[] data, EndpointServer endpointServer = EndpointServer.Production, string jwt = null) {
      return FileUploader.UploadBytesToAvnfs(filename, mediaType, data, endpointServer, jwt);
    }

    /// <summary>
    /// Deletes every file in the AVNFS cache for this app. Do not call while a <see cref="GetFileAsync"/> is in
    /// progress, as it would return <c>null</c>.
    /// </summary>
    /// <returns>The number of files deleted.</returns>
    public static int ClearCache() {
      return ClearCache(CacheDirectory);
    }

    // Main thread only: Application.temporaryCachePath cannot be read from any other thread.
    private static string CacheDirectory => Path.Combine(Application.temporaryCachePath, CacheSubdirectory);

    // The testable core. Takes the cache directory and the download behind the seam so it runs against a
    // temp folder and a fake in tests. verifyHash is false for the provider route, which has checked the
    // hash already. Every await uses ConfigureAwait(false): nothing after the first await needs the main
    // thread, and it lets a test block on the Task without deadlocking the Unity synchronization context.
    internal static async Task<string> GetFileAsync(AvnfsUri uri, AvnfsDownload download, bool verifyHash, string cacheDirectory, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();

      Directory.CreateDirectory(cacheDirectory);
      var cachedPath = Path.Combine(cacheDirectory, uri.Hash);
      if (IsUsableCacheEntry(cachedPath, uri)) {
        return cachedPath;
      }

      // A unique temporary name, so concurrent fetches of one hash never write to the same file. Only a
      // verified file is ever renamed to the hash.
      var partialPath = Path.Combine(cacheDirectory, uri.Hash + "." + Guid.NewGuid().ToString("N") + PartialSuffix);
      try {
        var downloaded = await download(uri, partialPath, cancellationToken).ConfigureAwait(false);
        // An aborted request reports as a failed one, so check the token before calling it a failure
        cancellationToken.ThrowIfCancellationRequested();
        if (!downloaded) {
          return null;
        }

        if (!await IsIntact(partialPath, uri, verifyHash, cancellationToken).ConfigureAwait(false)) {
          return null;
        }

        return Commit(partialPath, cachedPath);
      } finally {
        TryDelete(partialPath);
      }
    }

    // The on-device provider needs size and type, and probing first keeps ContentProviderClient from
    // logging an error on every fetch where no provider is installed.
    internal static bool UseContentProvider(AvnfsUri uri, Func<string, bool> hasProvider) {
      return uri.HasSizeAndType && hasProvider(uri.Host);
    }

    // Adapts the on-device provider to the download seam. The provider's copy cannot be aborted, so on
    // cancellation this stops waiting and removes the copy once it lands, rather than leaving it behind.
    internal static AvnfsDownload ContentProviderDownload(ContentProviderFetch fetch) {
      return async (uri, destinationPath, cancellationToken) => {
        var contentUri = uri.ToContentUri();
        Debug.Log($"Avnfs: fetching '{contentUri}' from the on-device provider");
        var copy = fetch(contentUri, destinationPath);

        var cancelled = new TaskCompletionSource<bool>();
        using (cancellationToken.Register(() => cancelled.TrySetResult(true))) {
          if (await Task.WhenAny(copy, cancelled.Task).ConfigureAwait(false) != copy) {
            _ = copy.ContinueWith(late => TryDelete(destinationPath), TaskScheduler.Default);
            return false;
          }
        }
        return await copy.ConfigureAwait(false) != null;
      };
    }

    internal static int ClearCache(string cacheDirectory) {
      if (!Directory.Exists(cacheDirectory)) {
        return 0;
      }

      var count = 0;
      foreach (var file in Directory.GetFiles(cacheDirectory)) {
        try {
          File.Delete(file);
          count++;
        } catch (Exception ex) {
          Debug.LogWarning($"Avnfs: could not delete cached file '{file}': {ex.Message}");
        }
      }
      return count;
    }

    // A cached file is trusted on its name, which it only gets after verification. The size is a cheap
    // guard against truncation; a full re-hash per hit would cost as much as the download for a large
    // file. An unusable entry is deleted so it gets refetched.
    private static bool IsUsableCacheEntry(string cachedPath, AvnfsUri uri) {
      var info = new FileInfo(cachedPath);
      if (!info.Exists) {
        return false;
      }
      if (!uri.SizeBytes.HasValue || info.Length == uri.SizeBytes.Value) {
        return true;
      }

      Debug.LogWarning($"Avnfs: cached file for '{uri}' is {info.Length} bytes but the URL says {uri.SizeBytes.Value}. Refetching.");
      TryDelete(cachedPath);
      return false;
    }

    // Moves the verified download into place. If a concurrent fetch of the same hash got there first,
    // its copy passed the same check, so keep that one; the finally block drops ours.
    private static string Commit(string partialPath, string cachedPath) {
      try {
        File.Move(partialPath, cachedPath);
      } catch (IOException) when (File.Exists(cachedPath)) {
      }
      return cachedPath;
    }

    // The HTTPS route verifies the download against the hash in the URL. The provider route trusts the
    // provider, which verified the file itself, and only checks the size: a full re-hash would read the
    // whole file again on the headset.
    private static async Task<bool> IsIntact(string path, AvnfsUri uri, bool verifyHash, CancellationToken cancellationToken) {
      if (verifyHash) {
        var actualHash = await ComputeHashAsync(path, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(actualHash, uri.Hash, StringComparison.Ordinal)) {
          Debug.LogError($"Avnfs: '{uri}' failed its integrity check: the downloaded content hashes to '{actualHash}'. Discarding it.");
          return false;
        }
        return true;
      }

      var length = new FileInfo(path).Length;
      if (uri.SizeBytes.HasValue && length != uri.SizeBytes.Value) {
        Debug.LogError($"Avnfs: '{uri}' failed its size check: the provider supplied {length} bytes but the URL says {uri.SizeBytes.Value}. Discarding it.");
        return false;
      }
      return true;
    }

    // WebGL has no thread pool, so hash on the main thread there.
    private static Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken) {
#if UNITY_WEBGL
      return Task.FromResult(AvnfsHash.ComputeForFile(path));
#else
      return Task.Run(() => AvnfsHash.ComputeForFile(path), cancellationToken);
#endif
    }

    private static void TryDelete(string path) {
      try {
        File.Delete(path);
      } catch (Exception) {
        // best-effort cleanup
      }
    }

    // The production download: a UnityWebRequest writing straight to disk, so a large file never passes
    // through managed memory. Runs on the caller's thread up to the first await, which is where the
    // request must be created, hence the main-thread requirement on GetFileAsync. Cancelling the token
    // aborts the request, which also removes the partial file.
    private static async Task<bool> DownloadWithWebRequest(AvnfsUri uri, string destinationPath, CancellationToken cancellationToken) {
      var url = uri.ToHttpsUri();
      Debug.Log($"Avnfs: downloading '{url}'");
      using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET)) {
        request.downloadHandler = new DownloadHandlerFile(destinationPath) { removeFileOnAbort = true };
        var operation = request.SendWebRequest();
        using (cancellationToken.Register(request.Abort)) {
          await operation;
        }

        if (request.result == UnityWebRequest.Result.Success) {
          return true;
        }
        if (!cancellationToken.IsCancellationRequested) {
          Debug.LogError($"Avnfs: download of '{url}' failed: {request.error} (HTTP {request.responseCode}).");
        }
        return false;
      }
    }
  }
}
