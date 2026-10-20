using System;
using NUnit.Framework;

namespace ClassVR.Network.AvnCloud.Tests {
  /// <summary>
  /// EditMode unit tests for <see cref="AvnfsUri"/>: the URL shape, the host rule that keeps the type
  /// AVNFS-only, and the scheme conversions, which must leave the path and query untouched.
  /// </summary>
  public class AvnfsUriTests {
    // A real AVNFS URL as the ClassVR client supplies it in an intent extra
    private const string Hash = "AOP6OOHckt7YWoO9321hD4sQABwnXszytVTYdFwu5ds";
    private const string Query = "?size=17&type=application%2Fx.sample.saveload&name=SampleSaveLoad-967.saveload";
    private const string HttpsUrl = "https://avnfs.com/" + Hash + Query;
    private const string ContentUrl = "content://avnfs.com/" + Hash + Query;

    // --- Parsing ---------------------------------------------------------------

    [Test]
    public void Parse_FullHttpsUrl_ExposesEveryPart() {
      var uri = AvnfsUri.Parse(HttpsUrl);

      Assert.AreEqual("https", uri.Scheme);
      Assert.AreEqual("avnfs.com", uri.Host);
      Assert.AreEqual(Hash, uri.Hash);
      Assert.AreEqual(17L, uri.SizeBytes);
      Assert.AreEqual("application/x.sample.saveload", uri.MediaType);
      Assert.AreEqual("SampleSaveLoad-967.saveload", uri.FileName);
      Assert.IsTrue(uri.HasSizeAndType);
      Assert.AreEqual(HttpsUrl, uri.OriginalUrl);
    }

    [Test]
    public void Parse_ContentScheme_ExposesTheSameParts() {
      var uri = AvnfsUri.Parse(ContentUrl);

      Assert.AreEqual("content", uri.Scheme);
      Assert.AreEqual("avnfs.com", uri.Host);
      Assert.AreEqual(Hash, uri.Hash);
      Assert.AreEqual(17L, uri.SizeBytes);
      Assert.AreEqual("application/x.sample.saveload", uri.MediaType);
      Assert.AreEqual("SampleSaveLoad-967.saveload", uri.FileName);
    }

    [Test]
    public void Parse_NoName_FileNameIsNull() {
      var uri = AvnfsUri.Parse("https://avnfs.com/" + Hash + "?size=17&type=text%2Fplain");

      Assert.IsNull(uri.FileName);
      Assert.IsTrue(uri.HasSizeAndType);
    }

    // Values are percent-decoded, matching the client's java.net.URI.getQuery() and the %20 that
    // Uri.EscapeDataString produces on upload. A '+' is a literal plus, not a space, on both sides.

    [Test]
    public void Parse_PercentEncodedName_FileNameIsDecoded() {
      var uri = AvnfsUri.Parse("https://avnfs.com/" + Hash + "?size=17&type=text%2Fplain&name=My%20File%20%281%29.txt");

      Assert.AreEqual("My File (1).txt", uri.FileName);
    }

    [Test]
    public void Parse_PlusInName_IsKeptAsAPlus() {
      var uri = AvnfsUri.Parse("https://avnfs.com/" + Hash + "?size=17&type=text%2Fplain&name=My+File.txt");

      Assert.AreEqual("My+File.txt", uri.FileName);
    }

    [TestCase("", false)]
    [TestCase("?size=17", false)]
    [TestCase("?type=text%2Fplain", false)]
    [TestCase("?size=17&type=text%2Fplain", true)]
    [TestCase("?type=text%2Fplain&size=17", true)]
    public void HasSizeAndType_RequiresBothParameters(string query, bool expected) {
      var uri = AvnfsUri.Parse("https://avnfs.com/" + Hash + query);

      Assert.AreEqual(expected, uri.HasSizeAndType);
    }

    [TestCase("abc")]
    [TestCase("-1")]
    [TestCase("")]
    [TestCase("1.5")]
    public void Parse_SizeNotANonNegativeInteger_SizeBytesIsNull(string size) {
      var uri = AvnfsUri.Parse("https://avnfs.com/" + Hash + "?size=" + size + "&type=text%2Fplain");

      Assert.IsNull(uri.SizeBytes);
      Assert.IsFalse(uri.HasSizeAndType);
    }

    [Test]
    public void Parse_EmptyType_MediaTypeIsNull() {
      var uri = AvnfsUri.Parse("https://avnfs.com/" + Hash + "?size=17&type=");

      Assert.IsNull(uri.MediaType);
      Assert.IsFalse(uri.HasSizeAndType);
    }

    [Test]
    public void Parse_UpperCaseHost_HostIsLowerCased() {
      var uri = AvnfsUri.Parse("HTTPS://BNY.AVNFS.COM/" + Hash + Query);

      Assert.AreEqual("https", uri.Scheme);
      Assert.AreEqual("bny.avnfs.com", uri.Host);
    }

    [Test]
    public void Parse_Fragment_IsIgnored() {
      var uri = AvnfsUri.Parse("https://avnfs.com/" + Hash + "?size=17&type=text%2Fplain#section");

      Assert.AreEqual(Hash, uri.Hash);
      Assert.AreEqual(17L, uri.SizeBytes);
      Assert.AreEqual("text/plain", uri.MediaType);
    }

    // --- The host rule ---------------------------------------------------------

    [TestCase("https://avnfs.com/" + Hash + Query)]
    [TestCase("https://bny.avnfs.com/" + Hash + Query)]
    [TestCase("https://cdn.eu.avnfs.com/" + Hash + Query)]
    [TestCase("content://avnfs.com/" + Hash + Query)]
    [TestCase("https://avnfs.com/" + Hash)]
    public void IsAvnfsUrl_AvnfsHosts_True(string url) {
      Assert.IsTrue(AvnfsUri.IsAvnfsUrl(url));
    }

    // Only avnfs.com and its subdomains, so the type cannot be used as a general downloader
    [TestCase("https://avnfs.com.example.com/" + Hash + Query)]
    [TestCase("https://notavnfs.com/" + Hash + Query)]
    [TestCase("https://example.com/" + Hash + Query)]
    [TestCase("https://avnfs.com:8443/" + Hash + Query)]
    [TestCase("https://user@avnfs.com/" + Hash + Query)]
    // A real parser reads these hosts as evil.com
    [TestCase("https://evil.com#.avnfs.com/" + Hash + Query)]
    [TestCase("https://evil.com?.avnfs.com/" + Hash + Query)]
    [TestCase("https://evil.com\\.avnfs.com/" + Hash + Query)]
    [TestCase("https://avnfs.com@evil.com/" + Hash + Query)]
    [TestCase("https://.avnfs.com/" + Hash + Query)]
    [TestCase("https://a..avnfs.com/" + Hash + Query)]
    [TestCase("https://avnfs.com\n/" + Hash + Query)]
    [TestCase("http://avnfs.com/" + Hash + Query)]
    [TestCase("file:///avnfs.com/" + Hash + Query)]
    [TestCase("avnfs.com/" + Hash + Query)]
    [TestCase("")]
    [TestCase(null)]
    public void IsAvnfsUrl_OtherHostsAndSchemes_False(string url) {
      Assert.IsFalse(AvnfsUri.IsAvnfsUrl(url));
    }

    // --- The hash ---------------------------------------------------------------

    [TestCase("https://avnfs.com")]
    [TestCase("https://avnfs.com/")]
    [TestCase("https://avnfs.com/?size=17&type=text%2Fplain")]
    [TestCase("https://avnfs.com/" + Hash + "/extra")]
    [TestCase("https://avnfs.com/../etc/passwd")]
    [TestCase("https://avnfs.com/" + Hash + "=")]
    [TestCase("https://avnfs.com/a%20b")]
    [TestCase("https://avnfs.com/" + Hash + "\n")]
    public void TryParse_MissingOrMalformedHash_False(string url) {
      Assert.IsFalse(AvnfsUri.TryParse(url, out var result));
      Assert.IsNull(result);
    }

    [Test]
    public void Parse_NotAnAvnfsUrl_ThrowsArgumentException() {
      Assert.Throws<ArgumentException>(() => AvnfsUri.Parse("https://example.com/" + Hash + Query));
    }

    // --- Scheme conversion -----------------------------------------------------
    //
    // The path and query must come through byte-for-byte: the server refuses a bare hash, and the
    // ContentProvider needs size and type, so nothing may be re-encoded or dropped.

    [Test]
    public void ToContentUri_FromHttps_ChangesOnlyTheScheme() {
      Assert.AreEqual(ContentUrl, AvnfsUri.Parse(HttpsUrl).ToContentUri());
    }

    [Test]
    public void ToHttpsUri_FromContent_ChangesOnlyTheScheme() {
      Assert.AreEqual(HttpsUrl, AvnfsUri.Parse(ContentUrl).ToHttpsUri());
    }

    [Test]
    public void ToContentUri_FromContent_IsUnchanged() {
      Assert.AreEqual(ContentUrl, AvnfsUri.Parse(ContentUrl).ToContentUri());
    }

    [Test]
    public void ToHttpsUri_FromHttps_IsUnchanged() {
      Assert.AreEqual(HttpsUrl, AvnfsUri.Parse(HttpsUrl).ToHttpsUri());
    }

    [Test]
    public void ToContentUri_UpperCaseScheme_IsNormalisedButRestIsKept() {
      Assert.AreEqual("content://BNY.AVNFS.COM/" + Hash + Query, AvnfsUri.Parse("HTTPS://BNY.AVNFS.COM/" + Hash + Query).ToContentUri());
    }

    [Test]
    public void ToString_ReturnsTheOriginalUrl() {
      Assert.AreEqual(HttpsUrl, AvnfsUri.Parse(HttpsUrl).ToString());
    }
  }
}
