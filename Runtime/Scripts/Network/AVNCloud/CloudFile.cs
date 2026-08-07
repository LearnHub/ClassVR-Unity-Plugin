using System;
using System.Collections.Generic;

namespace ClassVR.Network.AvnCloud {
  /// <summary>
  /// A file stored in the ClassVR cloud, as returned by <see cref="CloudFiles.Search"/>.
  /// </summary>
  public sealed class CloudFile {
    /// <summary>
    /// Unique cloud file ID, and the key used to attach metadata to the file — the same value
    /// <see cref="CloudUploadResult.EntityId"/> carries for a freshly uploaded one.
    /// </summary>
    public int EntityId { get; }

    /// <summary>The file's display name, or <c>null</c> if the cloud has none recorded.</summary>
    public string FileName { get; }

    /// <summary>The AVNFS URL the file can be downloaded from.</summary>
    public string FileUrl { get; }

    /// <summary>The media (MIME) type, or <c>null</c> if unknown.</summary>
    public string MediaType { get; }

    /// <summary>The file size in bytes, or <c>null</c> if unknown.</summary>
    public long? SizeBytes { get; }

    /// <summary>
    /// A URL for an icon/preview image of the file. Empty unless the query asked for an icon via
    /// <see cref="CloudFileQuery.IconSize"/>.
    /// </summary>
    public string IconUrl { get; }

    /// <summary>A URL for a preview/thumbnail of the file. May be empty if none is available.</summary>
    [Obsolete("Use IconUrl instead, which carries the identical value.")]
    public string PreviewUrl => IconUrl;

    /// <summary>When the file was last modified, or <c>null</c> if the cloud has no timestamp recorded.</summary>
    public DateTimeOffset? Updated { get; }

    /// <summary>IDs of the tags associated with this file.</summary>
    public IReadOnlyList<int> Tags { get; }

    /// <summary>How many metadata entries are attached to this file.</summary>
    public int MetadataCount { get; }

    internal CloudFile(int entityId, string fileName, string fileUrl, string mediaType, long? sizeBytes, string iconUrl, DateTimeOffset? updated, IReadOnlyList<int> tags, int metadataCount) {
      EntityId = entityId;
      FileName = fileName;
      FileUrl = fileUrl;
      MediaType = mediaType;
      SizeBytes = sizeBytes;
      IconUrl = iconUrl;
      Updated = updated;
      Tags = tags;
      MetadataCount = metadataCount;
    }
  }

  /// <summary>
  /// The outcome of a successful upload to an Organization's Shared Cloud.
  /// </summary>
  public sealed class CloudUploadResult {
    /// <summary>
    /// The cloud file's entity ID — the key used to attach metadata to the file. Appears as
    /// <see cref="CloudFile.EntityId"/> when the same file comes back from <see cref="CloudFiles.Search"/>.
    /// </summary>
    public int EntityId { get; }

    /// <summary>The AVNFS URL the file can be downloaded from.</summary>
    public string FileUrl { get; }

    /// <summary>The display name the file was uploaded under.</summary>
    public string FileName { get; }

    /// <summary>The media (MIME) type the file was uploaded with, as supplied by the caller.</summary>
    public string MediaType { get; }

    /// <summary>
    /// The size of the uploaded content in bytes, or <c>null</c> if it could not be determined.
    /// </summary>
    public long? SizeBytes { get; }

    internal CloudUploadResult(int entityId, string fileUrl, string fileName, string mediaType, long? sizeBytes) {
      EntityId = entityId;
      FileUrl = fileUrl;
      FileName = fileName;
      MediaType = mediaType;
      SizeBytes = sizeBytes;
    }
  }

  /// <summary>
  /// A single page of <see cref="CloudFile"/> results. Exposed via <see cref="CloudFilePageable.AsPages"/> for
  /// callers that want to drive paging themselves (e.g. a "load more" button).
  /// </summary>
  public sealed class CloudFilePage {
    /// <summary>The files in this page.</summary>
    public IReadOnlyList<CloudFile> Files { get; }

    /// <summary>
    /// Token to fetch the next page, or <c>null</c> if this is the last page.
    /// Pass it back via <see cref="CloudFilePageable.AsPages"/>'s <c>continuationToken</c> to resume later.
    /// </summary>
    public string NextPageToken { get; }

    internal CloudFilePage(IReadOnlyList<CloudFile> files, string nextPageToken) {
      Files = files;
      NextPageToken = nextPageToken;
    }
  }
}
