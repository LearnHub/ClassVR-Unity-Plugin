using System;
using System.Collections.Generic;

namespace ClassVR.Network.AvnCloud {
  /// <summary>
  /// A file stored in the ClassVR cloud, as returned by <see cref="CloudFiles.Search"/>.
  /// </summary>
  public sealed class CloudFile {
    /// <summary>Unique cloud file ID.</summary>
    public int Id { get; }

    /// <summary>The file's display name, or <c>null</c> if the cloud has none recorded.</summary>
    public string FileName { get; }

    /// <summary>The AVNFS URL the file can be downloaded from.</summary>
    public string FileUrl { get; }

    /// <summary>The media (MIME) type, or <c>null</c> if unknown.</summary>
    public string MediaType { get; }

    /// <summary>The file size in bytes, or <c>null</c> if unknown.</summary>
    public long? SizeBytes { get; }

    /// <summary>A URL for a preview/thumbnail of the file. May be empty if none is available.</summary>
    public string PreviewUrl { get; }

    /// <summary>When the file was last modified.</summary>
    public DateTimeOffset Updated { get; }

    /// <summary>IDs of the tags associated with this file.</summary>
    public IReadOnlyList<int> Tags { get; }

    internal CloudFile(int id, string fileName, string fileUrl, string mediaType, long? sizeBytes, string previewUrl, DateTimeOffset updated, IReadOnlyList<int> tags) {
      Id = id;
      FileName = fileName;
      FileUrl = fileUrl;
      MediaType = mediaType;
      SizeBytes = sizeBytes;
      PreviewUrl = previewUrl;
      Updated = updated;
      Tags = tags;
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
