using System.IO;
using System.Text;
using NUnit.Framework;

namespace ClassVR.Network.AvnCloud.Tests {
  /// <summary>
  /// EditMode unit tests for <see cref="AvnfsHash"/>, pinned to the published SHA-256 test vectors for the
  /// empty input and "abc", so the encoding (base64url, no padding) is checked against something other
  /// than itself.
  /// </summary>
  public class AvnfsHashTests {
    private const string EmptyHash = "47DEQpj8HBSa-_TImW-5JCeuQeRkm5NMpJWZG3hSuFU";
    private const string AbcHash = "ungWv48Bz-pBQUDeXa4iI7ADYaOWF3qctBD_YfIAFa0";

    [Test]
    public void Compute_EmptyInput_MatchesKnownVector() {
      Assert.AreEqual(EmptyHash, AvnfsHash.Compute(new byte[0]));
    }

    [Test]
    public void Compute_Abc_MatchesKnownVector() {
      // The standard base64 of this digest contains both '+' and '/', so it exercises both substitutions
      Assert.AreEqual(AbcHash, AvnfsHash.Compute(Encoding.ASCII.GetBytes("abc")));
    }

    [Test]
    public void ComputeForFile_MatchesComputeOnTheSameBytes() {
      var path = Path.Combine(Path.GetTempPath(), "avnfs-hash-tests-" + Path.GetRandomFileName());
      try {
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes("abc"));
        Assert.AreEqual(AbcHash, AvnfsHash.ComputeForFile(path));
      } finally {
        File.Delete(path);
      }
    }
  }
}
