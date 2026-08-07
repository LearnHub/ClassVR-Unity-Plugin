using System;
using System.Collections.Generic;

namespace ClassVR.Network.AvnCloud {
  /// <summary>
  /// The order in which cloud file results are returned.
  /// </summary>
  public enum CloudFileOrder {
    /// <summary>Use the server's default ordering.</summary>
    Default,
    /// <summary>Most recently updated first.</summary>
    NewestFirst,
    /// <summary>Least recently updated first.</summary>
    OldestFirst,
    /// <summary>By file name, A→Z.</summary>
    NameAToZ,
    /// <summary>By file name, Z→A.</summary>
    NameZToA,
    /// <summary>Largest file first.</summary>
    LargestFirst,
    /// <summary>Smallest file first.</summary>
    SmallestFirst
  }

  /// <summary>
  /// What size of icon to ask the cloud to produce for each result. The image is transcoded to fit within
  /// this many pixels on its longest edge; SVG inputs are returned unchanged regardless.
  /// </summary>
  public enum CloudIconSize {
    /// <summary>Request no icon, leaving <see cref="CloudFile.IconUrl"/> empty. This is the default.</summary>
    None = 0,
    /// <summary>Fit within 8 pixels.</summary>
    Pixels8 = 8,
    /// <summary>Fit within 16 pixels.</summary>
    Pixels16 = 16,
    /// <summary>Fit within 32 pixels.</summary>
    Pixels32 = 32,
    /// <summary>Fit within 64 pixels.</summary>
    Pixels64 = 64,
    /// <summary>Fit within 128 pixels.</summary>
    Pixels128 = 128,
    /// <summary>Fit within 256 pixels.</summary>
    Pixels256 = 256,
    /// <summary>Fit within 512 pixels.</summary>
    Pixels512 = 512,
    /// <summary>Fit within 1024 pixels.</summary>
    Pixels1024 = 1024,
    /// <summary>Fit within 2048 pixels.</summary>
    Pixels2048 = 2048,
    /// <summary>Return the image at its original size, without transcoding.</summary>
    Original = -1
  }

  /// <summary>
  /// How a query's tag list is matched against a file's tags.
  /// </summary>
  public enum TagMatch {
    /// <summary>The file must have <em>all</em> of the query's tags.</summary>
    All,
    /// <summary>The file must have <em>at least one</em> of the query's tags.</summary>
    Any
  }

  /// <summary>
  /// How a metadata filter matches a file's metadata entry. See the factory methods on
  /// <see cref="CloudMetadataFilter"/>, which pick the condition for you.
  /// </summary>
  public enum MetadataMatch {
    /// <summary>The file has an entry for the key, whatever its value.</summary>
    HasKey,
    /// <summary>The file has no entry for the key.</summary>
    HasNotKey,
    /// <summary>The entry's value equals the match text.</summary>
    Equals,
    /// <summary>The entry's value starts with the match text.</summary>
    StartsWith,
    /// <summary>The entry's value contains the match text.</summary>
    Contains
  }

  /// <summary>
  /// One metadata condition a file must satisfy to be returned by a search. Build these with the factory
  /// methods — <see cref="HasKey"/>, <see cref="HasNotKey"/>, <see cref="ValueEquals"/>,
  /// <see cref="ValueStartsWith"/> and <see cref="ValueContains"/> — and add them to
  /// <see cref="CloudFileQuery.MetadataFilters"/>.
  /// </summary>
  /// <remarks>
  /// Keys are matched exactly and are case-sensitive; values are matched case-insensitively. The cloud
  /// limits a key to 128 characters and a value to 512, and holds one value per key.
  /// </remarks>
  public sealed class CloudMetadataFilter {
    /// <summary>The metadata key to filter on. Matched exactly, and case-sensitively.</summary>
    public string Key { get; }

    /// <summary>How the key (and, for the value conditions, its value) must match.</summary>
    public MetadataMatch Condition { get; }

    /// <summary>
    /// The text to match the value against, or <c>null</c> for <see cref="MetadataMatch.HasKey"/> and
    /// <see cref="MetadataMatch.HasNotKey"/>, which test only for the key's presence.
    /// </summary>
    public string Match { get; }

    private CloudMetadataFilter(string key, MetadataMatch condition, string match) {
      Key = key;
      Condition = condition;
      Match = match;
    }

    /// <summary>Matches files that have an entry for <paramref name="key"/>, whatever its value.</summary>
    /// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is null or empty.</exception>
    public static CloudMetadataFilter HasKey(string key) =>
        new CloudMetadataFilter(ValidateKey(key), MetadataMatch.HasKey, null);

    /// <summary>Matches files that have no entry for <paramref name="key"/>.</summary>
    /// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is null or empty.</exception>
    public static CloudMetadataFilter HasNotKey(string key) =>
        new CloudMetadataFilter(ValidateKey(key), MetadataMatch.HasNotKey, null);

    /// <summary>Matches files whose <paramref name="key"/> entry equals <paramref name="value"/>, ignoring case.</summary>
    /// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="value"/> is null.</exception>
    public static CloudMetadataFilter ValueEquals(string key, string value) =>
        new CloudMetadataFilter(ValidateKey(key), MetadataMatch.Equals, ValidateMatch(value, nameof(value)));

    /// <summary>Matches files whose <paramref name="key"/> entry starts with <paramref name="prefix"/>, ignoring case.</summary>
    /// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="prefix"/> is null.</exception>
    public static CloudMetadataFilter ValueStartsWith(string key, string prefix) =>
        new CloudMetadataFilter(ValidateKey(key), MetadataMatch.StartsWith, ValidateMatch(prefix, nameof(prefix)));

    /// <summary>Matches files whose <paramref name="key"/> entry contains <paramref name="text"/>, ignoring case.</summary>
    /// <exception cref="ArgumentException">Thrown if <paramref name="key"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="text"/> is null.</exception>
    public static CloudMetadataFilter ValueContains(string key, string text) =>
        new CloudMetadataFilter(ValidateKey(key), MetadataMatch.Contains, ValidateMatch(text, nameof(text)));

    // A filter with no key can't mean anything, and the cloud would reject it with an opaque error, so fail
    // here where the caller can see which filter was at fault.
    private static string ValidateKey(string key) {
      if (string.IsNullOrEmpty(key)) {
        throw new ArgumentException("A metadata filter needs a key.", nameof(key));
      }
      return key;
    }

    // Null match text is a mistake; empty is allowed, since "starts with nothing" is a meaningful (if
    // unhelpful) request rather than a malformed one.
    private static string ValidateMatch(string match, string parameterName) {
      if (match == null) {
        throw new ArgumentNullException(parameterName, "A value metadata filter needs text to match against. Use HasKey to test only for the key.");
      }
      return match;
    }
  }

  /// <summary>
  /// Describes a cloud file search. Construct one and pass it to <see cref="CloudFiles.Search"/>.
  /// Every property is an optional filter — an empty query returns every file the caller can see,
  /// in the server's default order.
  /// </summary>
  public sealed class CloudFileQuery {
    /// <summary>Free-text search across the files. Leave <c>null</c>/empty for no text filter.</summary>
    public string Text { get; set; }

    /// <summary>Restrict results to these media (MIME) types. Empty means "any type".</summary>
    public IList<string> MediaTypes { get; } = new List<string>();

    /// <summary>Restrict results to files matching these tag IDs (see <see cref="TagMatch"/>). Empty means "any tags".</summary>
    public IList<int> Tags { get; } = new List<int>();

    /// <summary>Whether a file must have all of <see cref="Tags"/> or just any one of them. Defaults to <see cref="TagMatch.All"/>.</summary>
    public TagMatch TagMatch { get; set; } = TagMatch.All;

    /// <summary>
    /// Restrict results to files whose metadata satisfies every one of these conditions. Build them with the
    /// factory methods on <see cref="CloudMetadataFilter"/>. Empty means "any metadata".
    /// </summary>
    /// <remarks>Multiple filters combine with AND — a file must satisfy all of them to be returned.</remarks>
    public IList<CloudMetadataFilter> MetadataFilters { get; } = new List<CloudMetadataFilter>();

    /// <summary>Only return files added to the cloud strictly after this time.</summary>
    public DateTimeOffset? CreatedAfter { get; set; }

    /// <summary>Only return files added to the cloud strictly before this time.</summary>
    public DateTimeOffset? CreatedBefore { get; set; }

    /// <summary>How to order the results. Defaults to <see cref="CloudFileOrder.Default"/>.</summary>
    public CloudFileOrder OrderBy { get; set; } = CloudFileOrder.Default;

    /// <summary>
    /// What size icon to request for each result, surfaced as <see cref="CloudFile.IconUrl"/>. Defaults to
    /// <see cref="CloudIconSize.None"/> — the cloud returns no icon unless this is set.
    /// </summary>
    public CloudIconSize IconSize { get; set; } = CloudIconSize.None;

    /// <summary>
    /// Search a specific organization's cloud. If set, this takes precedence over <see cref="UserId"/>.
    /// When both this and <see cref="UserId"/> are <c>null</c>, the device's current organization is used.
    /// </summary>
    public int? OrganizationId { get; set; }

    /// <summary>
    /// Search a specific user's cloud instead of an organization's. Used only when <see cref="OrganizationId"/>
    /// is <c>null</c> (<see cref="OrganizationId"/> takes precedence if both are set).
    /// </summary>
    public int? UserId { get; set; }
  }
}
