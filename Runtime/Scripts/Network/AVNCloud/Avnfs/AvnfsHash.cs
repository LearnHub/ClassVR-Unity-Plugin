using System;
using System.IO;
using System.Security.Cryptography;

namespace ClassVR.Network.AvnCloud {
  /// <summary>
  /// The hash AVNFS identifies files by: SHA-256, encoded as base64url without padding. It is the path
  /// segment of every AVNFS URL (<see cref="AvnfsUri.Hash"/>).
  /// </summary>
  internal static class AvnfsHash {
    private const int FileBufferSize = 1 << 16;

    /// <summary>The AVNFS hash of <paramref name="data"/>.</summary>
    internal static string Compute(byte[] data) {
      using (var sha256 = SHA256.Create()) {
        return ToBase64Url(sha256.ComputeHash(data));
      }
    }

    /// <summary>
    /// The AVNFS hash of the file at <paramref name="path"/>, streamed so a large file is never held
    /// in memory whole.
    /// </summary>
    internal static string ComputeForFile(string path) {
      using (var sha256 = SHA256.Create())
      using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileBufferSize, FileOptions.SequentialScan)) {
        return ToBase64Url(sha256.ComputeHash(stream));
      }
    }

    // https://base64.guru/standards/base64url, minus the '=' padding AVNFS omits
    private static string ToBase64Url(byte[] bytes) {
      return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
  }
}
