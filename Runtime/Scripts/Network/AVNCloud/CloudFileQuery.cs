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
  /// How a query's tag list is matched against a file's tags.
  /// </summary>
  public enum TagMatch {
    /// <summary>The file must have <em>all</em> of the query's tags.</summary>
    All,
    /// <summary>The file must have <em>at least one</em> of the query's tags.</summary>
    Any
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

    /// <summary>Only return files updated strictly after this time.</summary>
    public DateTimeOffset? CreatedAfter { get; set; }

    /// <summary>Only return files updated strictly before this time.</summary>
    public DateTimeOffset? CreatedBefore { get; set; }

    /// <summary>How to order the results. Defaults to <see cref="CloudFileOrder.Default"/>.</summary>
    public CloudFileOrder OrderBy { get; set; } = CloudFileOrder.Default;

    /// <summary>
    /// Search a specific organization's cloud. When <c>null</c> (and <see cref="UserId"/> is also <c>null</c>),
    /// the device's current organization is used.
    /// </summary>
    public int? OrganizationId { get; set; }

    /// <summary>Search a specific user's cloud instead of an organization's. Mutually exclusive with <see cref="OrganizationId"/>.</summary>
    public int? UserId { get; set; }
  }
}
