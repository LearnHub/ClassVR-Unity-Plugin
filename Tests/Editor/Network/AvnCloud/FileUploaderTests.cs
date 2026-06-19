using System.IO;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace ClassVR.Network.AvnCloud.Tests {
  /// <summary>
  /// EditMode unit tests for <see cref="FileUploader"/>. Only the file-path validation guards are exercised:
  /// these run before any authentication or gRPC/HTTP call, so they complete synchronously and need no network.
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
  }
}
