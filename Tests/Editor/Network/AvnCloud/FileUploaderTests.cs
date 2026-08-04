using System.IO;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace ClassVR.Network.AvnCloud.Tests {
  /// <summary>
  /// EditMode unit tests for <see cref="FileUploader"/>. Two groups, both network-free: the file-path
  /// validation guards, which run before any authentication or gRPC/HTTP call and so complete
  /// synchronously; and <see cref="FileUploader.EnsureFileNameParameter"/>, which is pure string logic.
  /// The actual AVNFS upload and org-association round-trip must be manually verified.
  ///
  /// The guards report failure by logging a Debug.LogError and returning null. Unity's test runner fails any
  /// test that emits an unexpected error log, so each test opts out via LogAssert.ignoreFailingMessages —
  /// these tests assert the return value, not the log text, so they stay decoupled from the exact wording of
  /// the error messages. The opt-out must be set inside the test body: the LogScope that grades the body is
  /// created (with the flag reset to false) after [SetUp] runs, so setting it in [SetUp] has no effect.
  /// </summary>
  public class FileUploaderTests {
    private const string TestMediaType = "text/plain";

    // The guards return before any await, so blocking on the Task is safe and cannot deadlock.
    private static string Result(System.Threading.Tasks.Task<string> task) => task.GetAwaiter().GetResult();

    private static string MissingPath() {
      var path = Path.Combine(Path.GetTempPath(), "file-uploader-tests-does-not-exist.txt");
      Assert.IsFalse(File.Exists(path), "precondition: the test path must not exist");
      return path;
    }

    // --- UploadToAvnfs (file path overload) guards ---------------------------

    [Test]
    public void UploadToAvnfs_NullFilePath_ReturnsNull() {
      LogAssert.ignoreFailingMessages = true;
      Assert.IsNull(Result(FileUploader.UploadToAvnfs(null, TestMediaType)));
    }

    [Test]
    public void UploadToAvnfs_EmptyFilePath_ReturnsNull() {
      LogAssert.ignoreFailingMessages = true;
      Assert.IsNull(Result(FileUploader.UploadToAvnfs("", TestMediaType)));
    }

    [Test]
    public void UploadToAvnfs_MissingFile_ReturnsNull() {
      LogAssert.ignoreFailingMessages = true;
      Assert.IsNull(Result(FileUploader.UploadToAvnfs(MissingPath(), TestMediaType)));
    }

    // --- UploadToSharedCloud (file path overload) shares the same guards -----

    [Test]
    public void UploadToSharedCloud_NullFilePath_ReturnsNull() {
      LogAssert.ignoreFailingMessages = true;
      Assert.IsNull(Result(FileUploader.UploadToSharedCloud(null, TestMediaType)));
    }

    [Test]
    public void UploadToSharedCloud_EmptyFilePath_ReturnsNull() {
      LogAssert.ignoreFailingMessages = true;
      Assert.IsNull(Result(FileUploader.UploadToSharedCloud("", TestMediaType)));
    }

    [Test]
    public void UploadToSharedCloud_MissingFile_ReturnsNull() {
      LogAssert.ignoreFailingMessages = true;
      Assert.IsNull(Result(FileUploader.UploadToSharedCloud(MissingPath(), TestMediaType)));
    }

    // --- EnsureFileNameParameter ---------------------------------------------
    //
    // Works around the AVNFS provider discarding the supplied name when the content already
    // exists. Pure string logic, so unlike the guards above these need no LogAssert opt-out.

    private const string UndecoratedUrl =
      "https://avnfs.com/288NtgMek-nsTvXlUu-uQo1scmN70wYh80oGQERXvK8?size=42&type=text%2Fplain";

    [Test]
    public void EnsureFileNameParameter_UrlWithoutName_AppendsName() {
      var result = FileUploader.EnsureFileNameParameter(UndecoratedUrl, "probe.txt");

      Assert.AreEqual($"{UndecoratedUrl}&name=probe.txt", result);
    }

    [Test]
    public void EnsureFileNameParameter_UrlWithNoQueryString_UsesQuestionMarkSeparator() {
      var result = FileUploader.EnsureFileNameParameter("https://avnfs.com/abc", "probe.txt");

      Assert.AreEqual("https://avnfs.com/abc?name=probe.txt", result);
    }

    [Test]
    public void EnsureFileNameParameter_NameAlreadyPresent_ReturnsUrlUnchanged() {
      // A fresh upload already carries the name; appending again would duplicate the parameter
      var alreadyNamed = $"{UndecoratedUrl}&name=original.txt";

      var result = FileUploader.EnsureFileNameParameter(alreadyNamed, "replacement.txt");

      Assert.AreEqual(alreadyNamed, result);
    }

    [Test]
    public void EnsureFileNameParameter_NameIsOnlyQueryParameter_ReturnsUrlUnchanged() {
      var alreadyNamed = "https://avnfs.com/abc?name=original.txt";

      var result = FileUploader.EnsureFileNameParameter(alreadyNamed, "replacement.txt");

      Assert.AreEqual(alreadyNamed, result);
    }

    [Test]
    public void EnsureFileNameParameter_FilenameNeedingEscaping_EscapesIt() {
      var result = FileUploader.EnsureFileNameParameter(UndecoratedUrl, "my save & config.txt");

      // Unescaped, the & would split the query string and the name would be truncated
      Assert.AreEqual($"{UndecoratedUrl}&name=my%20save%20%26%20config.txt", result);
    }

    [Test]
    public void EnsureFileNameParameter_SimilarlyNamedParameter_StillAppends() {
      // "filename=" must not be mistaken for the "name=" parameter
      var url = $"{UndecoratedUrl}&filename=decoy.txt";

      var result = FileUploader.EnsureFileNameParameter(url, "probe.txt");

      Assert.AreEqual($"{url}&name=probe.txt", result);
    }

    [TestCase(null)]
    [TestCase("")]
    public void EnsureFileNameParameter_NoFilename_ReturnsUrlUnchanged(string filename) {
      var result = FileUploader.EnsureFileNameParameter(UndecoratedUrl, filename);

      Assert.AreEqual(UndecoratedUrl, result);
    }

    [TestCase(null)]
    [TestCase("")]
    public void EnsureFileNameParameter_NoUrl_ReturnsInputUnchanged(string avnfsUrl) {
      var result = FileUploader.EnsureFileNameParameter(avnfsUrl, "probe.txt");

      Assert.AreEqual(avnfsUrl, result);
    }
  }
}
