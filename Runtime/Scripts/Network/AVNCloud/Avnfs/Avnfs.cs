using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ClassVR.Network.AvnCloud {
  // Downloads a URL to a local path, returning true on success. The production implementation is a
  // UnityWebRequest; tests substitute a lambda that writes bytes, so the cache, integrity check and
  // cancellation logic can be exercised without a network. An implementation logs its own failures
  // before returning false, and may throw OperationCanceledException.
  internal delegate Task<bool> AvnfsDownload(string url, string destinationPath, CancellationToken cancellationToken);

  /// <summary>
  /// AVNFS is the flat, hash-addressed file store behind ClassVR. Retrieves files by URL, caching them
  /// locally by hash. For files associated with an organisation's Shared Cloud, see <see cref="CloudFiles"/>.
  /// </summary>
  public static class Avnfs {
    private const string CacheSubdirectory = "avnfs";
    private const string PartialSuffix = ".part";

    /// <summary>
    /// Gets a local copy of an AVNFS file, downloading if not cached.
    /// <para>
    /// Must be awaited from the main thread: the download is a <see cref="UnityWebRequest"/>, and the
    /// result comes back on the main thread via Unity's synchronization context.
    /// </para>
    /// </summary>
    /// <param name="url">An AVNFS URL, in its <c>https://</c> or <c>content://</c> form. See <see cref="AvnfsUri"/>.</param>
    /// <param name="cancellationToken">
    /// Aborts the download. Meant for a caller that no longer wants the file at all.
    /// It is not a timeout: a slow download is not a failure. Cancel from the main thread.
    /// </param>
    /// <returns>
    /// The absolute path of the file, named by its hash with no extension, or <c>null</c> if the download failed.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="url"/> is not an AVNFS URL.</exception>
    /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is cancelled.</exception>
    public static Task<string> GetFileAsync(string url, CancellationToken cancellationToken = default) {
      var uri = AvnfsUri.Parse(url);
      return GetFileAsync(uri, DownloadWithWebRequest, CacheDirectory, cancellationToken);
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
    // temp folder and a fake in tests. Every await uses ConfigureAwait(false): nothing after the first
    // await needs the main thread, and it lets a test block on the Task without deadlocking the Unity
    // synchronization context.
    internal static async Task<string> GetFileAsync(AvnfsUri uri, AvnfsDownload download, string cacheDirectory, CancellationToken cancellationToken) {
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
        Debug.Log($"Avnfs: downloading '{uri}'");
        var downloaded = await download(uri.ToHttpsUri(), partialPath, cancellationToken).ConfigureAwait(false);
        // An aborted request reports as a failed one, so check the token before calling it a failure
        cancellationToken.ThrowIfCancellationRequested();
        if (!downloaded) {
          return null;
        }

        var actualHash = await ComputeHashAsync(partialPath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(actualHash, uri.Hash, StringComparison.Ordinal)) {
          Debug.LogError($"Avnfs: '{uri}' failed its integrity check: the downloaded content hashes to '{actualHash}'. Discarding it.");
          return null;
        }

        return Commit(partialPath, cachedPath);
      } finally {
        TryDelete(partialPath);
      }
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
    private static async Task<bool> DownloadWithWebRequest(string url, string destinationPath, CancellationToken cancellationToken) {
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
