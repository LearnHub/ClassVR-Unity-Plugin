using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace ClassVR.Network.AvnCloud.Tests {
  /// <summary>
  /// EditMode unit tests for <see cref="Avnfs"/>. The download sits behind the <see cref="AvnfsDownload"/>
  /// seam and the cache directory is a temp folder per test, so the cache, integrity check and
  /// cancellation logic run without a network. The real UnityWebRequest path is covered by one explicit
  /// test against the live server, run by hand.
  ///
  /// The core awaits with ConfigureAwait(false) and the fakes complete synchronously, so blocking on the
  /// Task cannot deadlock the Unity synchronization context. Failures are reported by logging an error
  /// and returning null, so those tests opt out of the runner failing on an error log via
  /// LogAssert.ignoreFailingMessages, set inside the test body (see FileUploaderTests for why).
  /// </summary>
  public class AvnfsTests {
    private static readonly byte[] Content = Encoding.ASCII.GetBytes("abc");
    private static readonly string ContentHash = AvnfsHash.Compute(Content);
    private const string SizeAndType = "?size=3&type=text%2Fplain";

    private string cacheDirectory;

    [SetUp]
    public void SetUp() {
      cacheDirectory = Path.Combine(Path.GetTempPath(), "avnfs-files-tests-" + Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(cacheDirectory);
    }

    [TearDown]
    public void TearDown() {
      if (Directory.Exists(cacheDirectory)) {
        Directory.Delete(cacheDirectory, true);
      }
    }

    private static AvnfsUri ContentUrl(string query = SizeAndType) {
      return AvnfsUri.Parse("https://avnfs.com/" + ContentHash + query);
    }

    private string CachedPath => Path.Combine(cacheDirectory, ContentHash);

    private string[] CachedFiles => Directory.GetFiles(cacheDirectory);

    private static T Result<T>(Task<T> task) => task.GetAwaiter().GetResult();

    private Task<string> Fetch(AvnfsUri uri, AvnfsDownload download, CancellationToken cancellationToken = default) {
      return Avnfs.GetFileAsync(uri, download, cacheDirectory, cancellationToken);
    }

    // Stands in for the web request: writes the given bytes to the destination and records each URL.
    private sealed class FakeDownload {
      public readonly List<string> Urls = new List<string>();
      private readonly byte[] bytes;
      private readonly bool succeeds;

      public FakeDownload(byte[] bytes, bool succeeds = true) {
        this.bytes = bytes;
        this.succeeds = succeeds;
      }

      public Task<bool> Download(string url, string destinationPath, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        Urls.Add(url);
        if (bytes != null) {
          File.WriteAllBytes(destinationPath, bytes);
        }
        return Task.FromResult(succeeds);
      }
    }

    // --- Cache miss --------------------------------------------------------------

    [Test]
    public void CacheMiss_DownloadsVerifiesAndReturnsTheHashNamedFile() {
      var fake = new FakeDownload(Content);

      var path = Result(Fetch(ContentUrl(), fake.Download));

      Assert.AreEqual(CachedPath, path);
      Assert.AreEqual(Content, File.ReadAllBytes(path));
      Assert.AreEqual(1, fake.Urls.Count);
      // The partial file is gone, leaving only the verified copy
      Assert.AreEqual(new[] { CachedPath }, CachedFiles);
    }

    [Test]
    public void CacheMiss_DownloadsTheHttpsFormWithPathAndQueryUntouched() {
      var fake = new FakeDownload(Content);
      var contentForm = AvnfsUri.Parse("content://avnfs.com/" + ContentHash + "?size=3&type=text%2Fplain&name=a%20b.txt");

      Result(Fetch(contentForm, fake.Download));

      Assert.AreEqual("https://avnfs.com/" + ContentHash + "?size=3&type=text%2Fplain&name=a%20b.txt", fake.Urls[0]);
    }

    [Test]
    public void HashMismatch_ReturnsNullAndLeavesNothingBehind() {
      LogAssert.ignoreFailingMessages = true;
      var fake = new FakeDownload(Encoding.ASCII.GetBytes("not the content"));

      var path = Result(Fetch(ContentUrl(), fake.Download));

      Assert.IsNull(path);
      Assert.IsEmpty(CachedFiles);
    }

    [Test]
    public void DownloadFailure_ReturnsNullAndLeavesNothingBehind() {
      // A failed request may have written a partial file before giving up
      var fake = new FakeDownload(Encoding.ASCII.GetBytes("ab"), succeeds: false);

      var path = Result(Fetch(ContentUrl(), fake.Download));

      Assert.IsNull(path);
      Assert.IsEmpty(CachedFiles);
    }

    [Test]
    public void ConcurrentFetch_KeepsTheCopyThatCommittedFirst() {
      // Another fetch of the same hash lands while ours is downloading
      Task<bool> Download(string url, string destinationPath, CancellationToken cancellationToken) {
        File.WriteAllBytes(CachedPath, Content);
        File.WriteAllBytes(destinationPath, Content);
        return Task.FromResult(true);
      }

      var path = Result(Fetch(ContentUrl(), Download));

      Assert.AreEqual(CachedPath, path);
      Assert.AreEqual(Content, File.ReadAllBytes(path));
      Assert.AreEqual(new[] { CachedPath }, CachedFiles);
    }

    // --- Cache hit ---------------------------------------------------------------

    [Test]
    public void CacheHit_ReturnsTheFileWithoutDownloading() {
      File.WriteAllBytes(CachedPath, Content);
      var fake = new FakeDownload(Content);

      var path = Result(Fetch(ContentUrl(), fake.Download));

      Assert.AreEqual(CachedPath, path);
      Assert.IsEmpty(fake.Urls);
    }

    [Test]
    public void CacheHit_UrlWithoutSize_IsTrustedOnItsName() {
      File.WriteAllBytes(CachedPath, Content);
      var fake = new FakeDownload(Content);

      var path = Result(Fetch(ContentUrl("?type=text%2Fplain"), fake.Download));

      Assert.AreEqual(CachedPath, path);
      Assert.IsEmpty(fake.Urls);
    }

    [Test]
    public void CacheEntryWithWrongSize_IsRefetched() {
      File.WriteAllBytes(CachedPath, Encoding.ASCII.GetBytes("truncated or not"));
      var fake = new FakeDownload(Content);

      var path = Result(Fetch(ContentUrl(), fake.Download));

      Assert.AreEqual(CachedPath, path);
      Assert.AreEqual(1, fake.Urls.Count);
      Assert.AreEqual(Content, File.ReadAllBytes(path));
    }

    // --- Cancellation ------------------------------------------------------------

    [Test]
    public void CancelledBeforeStart_ThrowsWithoutDownloading() {
      var fake = new FakeDownload(Content);
      var cancellation = new CancellationTokenSource();
      cancellation.Cancel();

      Assert.Throws<OperationCanceledException>(() => Result(Fetch(ContentUrl(), fake.Download, cancellation.Token)));
      Assert.IsEmpty(fake.Urls);
      Assert.IsEmpty(CachedFiles);
    }

    [Test]
    public void CancelledDuringDownload_ThrowsAndLeavesNothingBehind() {
      // An aborted UnityWebRequest comes back as a failure, so the token is what distinguishes the two
      var cancellation = new CancellationTokenSource();
      Task<bool> AbortedDownload(string url, string destinationPath, CancellationToken cancellationToken) {
        File.WriteAllBytes(destinationPath, Encoding.ASCII.GetBytes("ab"));
        cancellation.Cancel();
        return Task.FromResult(false);
      }

      Assert.Throws<OperationCanceledException>(() => Result(Fetch(ContentUrl(), AbortedDownload, cancellation.Token)));
      Assert.IsEmpty(CachedFiles);
    }

    [Test]
    public void DownloadThrowingCancellation_Propagates() {
      Task<bool> Download(string url, string destinationPath, CancellationToken cancellationToken) {
        throw new OperationCanceledException(cancellationToken);
      }

      Assert.Throws<OperationCanceledException>(() => Result(Fetch(ContentUrl(), Download)));
      Assert.IsEmpty(CachedFiles);
    }

    // --- Public surface ----------------------------------------------------------

    [Test]
    public void GetFileAsync_NotAnAvnfsUrl_ThrowsArgumentException() {
      Assert.Throws<ArgumentException>(() => Avnfs.GetFileAsync("https://example.com/" + ContentHash + SizeAndType));
    }

    [Test]
    public void ClearCache_DeletesEveryFileAndReportsTheCount() {
      File.WriteAllBytes(Path.Combine(cacheDirectory, "one"), Content);
      File.WriteAllBytes(Path.Combine(cacheDirectory, "two"), Content);

      Assert.AreEqual(2, Avnfs.ClearCache(cacheDirectory));
      Assert.IsEmpty(CachedFiles);
    }

    [Test]
    public void ClearCache_MissingDirectory_ReportsZero() {
      Assert.AreEqual(0, Avnfs.ClearCache(Path.Combine(cacheDirectory, "does-not-exist")));
    }

    // --- Live server -------------------------------------------------------------

    // The one test that exercises the real UnityWebRequest path. Excluded from normal runs; select it by
    // name to run it. A real 17-byte file as the ClassVR client supplies it in an intent extra.
    [UnityTest, Explicit("Downloads from the live AVNFS server.")]
    public IEnumerator GetFileAsync_LiveServer_DownloadsAndVerifies() {
      const string hash = "AOP6OOHckt7YWoO9321hD4sQABwnXszytVTYdFwu5ds";
      const string url = "https://avnfs.com/" + hash + "?size=17&type=application%2Fx.sample.saveload&name=SampleSaveLoad-967.saveload";
      Avnfs.ClearCache();

      var task = Avnfs.GetFileAsync(url);
      while (!task.IsCompleted) {
        yield return null;
      }

      var path = task.GetAwaiter().GetResult();
      Assert.IsNotNull(path);
      Assert.AreEqual(hash, Path.GetFileName(path));
      Assert.AreEqual(17, new FileInfo(path).Length);
    }
  }
}
