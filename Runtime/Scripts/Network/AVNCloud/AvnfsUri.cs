using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ClassVR.Network.AvnCloud {
  /// <summary>
  /// A parsed AVNFS URL: <c>https://&lt;host&gt;/&lt;hash&gt;?size=&lt;bytes&gt;&amp;type=&lt;media-type&gt;[&amp;name=&lt;file-name&gt;]</c>.
  /// The <c>content://</c> form used by the on-device AVNFS ContentProvider is accepted too, so a URL
  /// taken from an intent extra parses as-is.
  /// </summary>
  /// <remarks>
  /// An instance can only be made from an AVNFS URL — the host must be <see cref="RootDomain"/> or a
  /// subdomain of it.
  /// The path and query are kept exactly as supplied: the server refuses a bare hash with no query
  /// string, and the ContentProvider needs <c>size</c> and <c>type</c>, so neither is ever rebuilt.
  /// </remarks>
  public sealed class AvnfsUri {
    /// <summary>The domain AVNFS files are served from. Subdomains (CDN hosts) count as AVNFS too.</summary>
    public const string RootDomain = "avnfs.com";

    private const string HttpsScheme = "https";
    private const string ContentScheme = "content";
    private const string SizeParameter = "size";
    private const string TypeParameter = "type";
    private const string NameParameter = "name";

    /// <summary>The URL exactly as supplied.</summary>
    public string OriginalUrl { get; }

    /// <summary>The scheme the URL was supplied with, lower-cased: <c>https</c> or <c>content</c>.</summary>
    public string Scheme { get; }

    /// <summary>The host, lower-cased. Either <see cref="RootDomain"/> or a subdomain of it.</summary>
    public string Host { get; }

    /// <summary>The file's SHA-256 as unpadded base64url. It is the URL's only path segment.</summary>
    public string Hash { get; }

    /// <summary>
    /// The file size from the <c>size</c> parameter, or <c>null</c> if it is absent or not a
    /// non-negative integer.
    /// </summary>
    public long? SizeBytes { get; }

    /// <summary>The media (MIME) type from the <c>type</c> parameter, or <c>null</c> if absent or empty.</summary>
    public string MediaType { get; }

    /// <summary>The display name from the <c>name</c> parameter, or <c>null</c> if absent or empty.</summary>
    public string FileName { get; }

    /// <summary>
    /// True when both <c>size</c> and <c>type</c> are present. The on-device ContentProvider refuses a
    /// URL without them.
    /// </summary>
    public bool HasSizeAndType => SizeBytes.HasValue && MediaType != null;

    // Everything after the scheme, from "://" onwards. Swapping the scheme in front of it is the only
    // transformation this type performs.
    private readonly string afterScheme;

    private AvnfsUri(string originalUrl, string scheme, string afterScheme, string host, string hash,
        long? sizeBytes, string mediaType, string fileName) {
      OriginalUrl = originalUrl;
      Scheme = scheme;
      this.afterScheme = afterScheme;
      Host = host;
      Hash = hash;
      SizeBytes = sizeBytes;
      MediaType = mediaType;
      FileName = fileName;
    }

    /// <summary>
    /// True if <paramref name="url"/> is an AVNFS URL this type can represent: an <c>https</c> or
    /// <c>content</c> URL whose host is <see cref="RootDomain"/> or a subdomain of it, with a hash as
    /// its only path segment.
    /// </summary>
    public static bool IsAvnfsUrl(string url) {
      return TryParse(url, out _);
    }

    /// <summary>
    /// Parses <paramref name="url"/>, throwing if it is not an AVNFS URL. Use <see cref="TryParse"/>
    /// when the URL comes from outside the caller's control.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if <paramref name="url"/> is not an AVNFS URL.</exception>
    public static AvnfsUri Parse(string url) {
      if (!TryParse(url, out var result)) {
        throw new ArgumentException($"'{url}' is not an AVNFS URL. Expected https://{RootDomain}/<hash>?size=<bytes>&type=<media type>.", nameof(url));
      }
      return result;
    }

    /// <summary>
    /// Parses <paramref name="url"/> if it is an AVNFS URL.
    /// </summary>
    /// <param name="url">The URL to parse.</param>
    /// <param name="result">The parsed URL, or <c>null</c> if this returns false.</param>
    /// <returns>True if <paramref name="url"/> is an AVNFS URL.</returns>
    public static bool TryParse(string url, out AvnfsUri result) {
      result = null;
      if (string.IsNullOrEmpty(url)) {
        return false;
      }

      var schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
      if (schemeEnd <= 0) {
        return false;
      }
      var scheme = url.Substring(0, schemeEnd).ToLowerInvariant();
      if (scheme != HttpsScheme && scheme != ContentScheme) {
        return false;
      }

      var afterScheme = url.Substring(schemeEnd);
      var authorityStart = schemeEnd + 3;
      var pathStart = url.IndexOf('/', authorityStart);
      if (pathStart < 0) {
        return false;
      }
      // Everything up to the first '/' is taken as the host; IsAvnfsHost rejects anything that isn't
      // a plain DNS name, which covers ports, user info and a '?', '#' or '\' that ends the host early
      var host = url.Substring(authorityStart, pathStart - authorityStart).ToLowerInvariant();
      if (!IsAvnfsHost(host)) {
        return false;
      }

      var pathEnd = IndexOfAny(url, pathStart, '?', '#');
      var hash = url.Substring(pathStart + 1, pathEnd - pathStart - 1);
      if (!IsBase64Url(hash)) {
        return false;
      }

      var parameters = ParseQuery(url, pathEnd);
      long? sizeBytes = null;
      if (parameters.TryGetValue(SizeParameter, out var sizeText)
          && long.TryParse(sizeText, NumberStyles.None, CultureInfo.InvariantCulture, out var size)) {
        sizeBytes = size;
      }
      parameters.TryGetValue(TypeParameter, out var mediaType);
      parameters.TryGetValue(NameParameter, out var fileName);

      result = new AvnfsUri(url, scheme, afterScheme, host, hash, sizeBytes,
          EmptyToNull(mediaType), EmptyToNull(fileName));
      return true;
    }

    /// <summary>
    /// The URL with the <c>content</c> scheme, for the on-device AVNFS ContentProvider. Authority,
    /// path and query are kept exactly as supplied.
    /// </summary>
    public string ToContentUri() {
      return ContentScheme + afterScheme;
    }

    /// <summary>
    /// The URL with the <c>https</c> scheme, for a direct download. Authority, path and query are kept
    /// exactly as supplied.
    /// </summary>
    public string ToHttpsUri() {
      return HttpsScheme + afterScheme;
    }

    /// <summary>The URL exactly as supplied.</summary>
    public override string ToString() {
      return OriginalUrl;
    }

    // avnfs.com itself or a subdomain of it, as plain DNS labels. A suffix match alone would accept
    // notavnfs.com, and a prefix match would accept avnfs.com.example.
    // Only DNS characters are allowed: in "evil.com#.avnfs.com" a real URL parser ends the host at the
    // '#', so a suffix match would pass a URL that is actually fetched from evil.com.
    // \z rather than $ throughout, as $ also matches before a trailing newline.
    private static readonly Regex AvnfsHostPattern =
        new Regex(@"^([a-z0-9-]+\.)*" + Regex.Escape(RootDomain) + @"\z", RegexOptions.CultureInvariant);

    // The unpadded base64url alphabet. Also what makes the hash safe to use as a file name.
    private static readonly Regex Base64UrlPattern = new Regex(@"^[A-Za-z0-9_-]+\z", RegexOptions.CultureInvariant);

    private static bool IsAvnfsHost(string host) {
      return AvnfsHostPattern.IsMatch(host);
    }

    private static bool IsBase64Url(string text) {
      return Base64UrlPattern.IsMatch(text);
    }

    // Query parameters from the '?' at queryStart (if any) to the end or a '#'. The first occurrence of
    // a repeated key wins. Keys and values are percent-decoded.
    private static Dictionary<string, string> ParseQuery(string url, int queryStart) {
      var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
      if (queryStart >= url.Length || url[queryStart] != '?') {
        return parameters;
      }

      var queryEnd = IndexOfAny(url, queryStart, '#');
      var query = url.Substring(queryStart + 1, queryEnd - queryStart - 1);
      foreach (var pair in query.Split('&')) {
        if (pair.Length == 0) {
          continue;
        }
        var separator = pair.IndexOf('=');
        var key = Uri.UnescapeDataString(separator < 0 ? pair : pair.Substring(0, separator));
        var value = separator < 0 ? string.Empty : Uri.UnescapeDataString(pair.Substring(separator + 1));
        if (!parameters.ContainsKey(key)) {
          parameters.Add(key, value);
        }
      }
      return parameters;
    }

    // Index of the first of the given characters at or after start, or the string's length if none.
    private static int IndexOfAny(string text, int start, params char[] characters) {
      var index = text.IndexOfAny(characters, start);
      return index < 0 ? text.Length : index;
    }

    private static string EmptyToNull(string text) {
      return string.IsNullOrEmpty(text) ? null : text;
    }
  }
}
