using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avn.Connect.V1;
using ClassVR.Platform.Android;
using Google.Protobuf.WellKnownTypes;
using ProtoCloudFile = Avn.Connect.V1.CloudFile;

namespace ClassVR.Network.AvnCloud {
  // Fetches a single page of raw results. The production implementation calls the gRPC client; tests substitute
  // a lambda so the paging/mapping logic can be exercised without a network or any gRPC types.
  internal delegate Task<SearchCloudFilesResponse> SearchCloudFilesFetch(SearchCloudFilesRequest request, CancellationToken cancellationToken);

  /// <summary>
  /// Entry point for uploading files to, and querying files stored in, the ClassVR cloud.
  /// </summary>
  public static class CloudFiles {
    /// <summary>
    /// Searches the cloud for files matching <paramref name="query"/>.
    /// <para>
    /// No network request is made until the returned <see cref="CloudFilePageable"/> is enumerated.
    /// Stream every match with <c>await foreach</c> (paging is handled for you) or use
    /// <see cref="CloudFilePageable.AsPages"/> to drive paging yourself.
    /// </para>
    /// </summary>
    /// <param name="query">Describes what to search for. See <see cref="CloudFileQuery"/>.</param>
    /// <param name="endpointServer">Which backend to talk to. Defaults to Production.</param>
    /// <param name="jwt">Optional JWT for authentication. If null, uses the device JWT from CVRProperties (only available on Android).</param>
    /// <returns>A lazily-evaluated, auto-paging sequence of matching files.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="query"/> is null.</exception>
    /// <exception cref="Grpc.Core.RpcException">Thrown during enumeration if the cloud request fails.</exception>
    public static CloudFilePageable Search(CloudFileQuery query, EndpointServer endpointServer = EndpointServer.Production, string jwt = null) {
      if (query == null) {
        throw new ArgumentNullException(nameof(query));
      }
      return new CloudFilePageable(query, jwt, CreateFetch(endpointServer));
    }

    /// <summary>
    /// Uploads a file to the Shared Cloud area of ClassVR for the current Organization the device is enrolled in.
    /// </summary>
    /// <param name="filename">The name and extension of the file.</param>
    /// <param name="mediaType">The media (or MIME) type of the file.</param>
    /// <param name="data">The file contents as a string. This will be encoded using UTF8.</param>
    /// <param name="endpointServer">The endpoint to use for communication. Defaults to Production if not provided.</param>
    /// <param name="jwt">Optional JWT for authentication. If null, uses the device JWT from CVRProperties (only available on Android).</param>
    /// <returns>
    /// The new cloud file, whose <see cref="CloudUploadResult.EntityId"/> is the key for attaching metadata.
    /// Returns <c>null</c> if the upload was unsuccessful — unlike <see cref="Search"/>, a failure is logged
    /// rather than thrown.
    /// </returns>
    public static async Task<CloudUploadResult> Upload(string filename, string mediaType, string data, EndpointServer endpointServer = EndpointServer.Production, string jwt = null) {
      byte[] byteData = Encoding.UTF8.GetBytes(data);
      return await FileUploader.UploadBytesToSharedCloud(filename, mediaType, byteData, endpointServer, jwt);
    }

    /// <summary>
    /// Uploads a file already on disk to the Shared Cloud area of ClassVR for the current Organization the device is enrolled in.
    /// The display name is derived from the file path via <see cref="System.IO.Path.GetFileName"/>.
    /// Note: the maximum file size for upload is 5GB.
    /// </summary>
    /// <param name="filePath">Local file path (not a URI — the method handles file:// prefixing).</param>
    /// <param name="mediaType">The media (or MIME) type of the file.</param>
    /// <param name="endpointServer">The endpoint to use for communication. Defaults to Production if not provided.</param>
    /// <param name="jwt">Optional JWT for authentication. If null, uses the device JWT from CVRProperties (only available on Android).</param>
    /// <returns>
    /// The new cloud file, whose <see cref="CloudUploadResult.EntityId"/> is the key for attaching metadata.
    /// Returns <c>null</c> if the upload was unsuccessful — unlike <see cref="Search"/>, a failure is logged
    /// rather than thrown.
    /// </returns>
    public static async Task<CloudUploadResult> Upload(string filePath, string mediaType, EndpointServer endpointServer = EndpointServer.Production, string jwt = null) {
      return await FileUploader.UploadFileToSharedCloud(filePath, mediaType, endpointServer, jwt);
    }

    /// <summary>
    /// Uploads a file to the Shared Cloud area of ClassVR for the current Organization the device is enrolled in.
    /// </summary>
    /// <param name="filename">The name and extension of the file.</param>
    /// <param name="mediaType">The media (or MIME) type of the file.</param>
    /// <param name="data">The file contents as a byte array.</param>
    /// <param name="endpointServer">The endpoint to use for communication. Defaults to Production if not provided.</param>
    /// <param name="jwt">Optional JWT for authentication. If null, uses the device JWT from CVRProperties (only available on Android).</param>
    /// <returns>
    /// The new cloud file, whose <see cref="CloudUploadResult.EntityId"/> is the key for attaching metadata.
    /// Returns <c>null</c> if the upload was unsuccessful — unlike <see cref="Search"/>, a failure is logged
    /// rather than thrown.
    /// </returns>
    public static async Task<CloudUploadResult> Upload(string filename, string mediaType, byte[] data, EndpointServer endpointServer = EndpointServer.Production, string jwt = null) {
      return await FileUploader.UploadBytesToSharedCloud(filename, mediaType, data, endpointServer, jwt);
    }

    // The production page fetcher: the only place that touches the gRPC client and channel singleton.
    private static SearchCloudFilesFetch CreateFetch(EndpointServer endpointServer) {
      var client = new CloudService.CloudServiceClient(AvnCloudChannel.Instance.ChannelForServer(endpointServer));
      return (request, cancellationToken) => client.SearchCloudFilesAsync(request, cancellationToken: cancellationToken).ResponseAsync;
    }
  }

  /// <summary>
  /// A lazily-evaluated, auto-paging sequence of <see cref="CloudFile"/> results.
  /// <para>
  /// Enumerate it directly with <c>await foreach</c> to stream every match,
  /// or call <see cref="AsPages"/> to retrieve results one page at a time.
  /// </para>
  /// </summary>
  public sealed class CloudFilePageable : IAsyncEnumerable<CloudFile> {
    private readonly CloudFileQuery query;
    private readonly string jwt;
    private readonly SearchCloudFilesFetch fetchPage;

    internal CloudFilePageable(CloudFileQuery query, string jwt, SearchCloudFilesFetch fetchPage) {
      this.query = query;
      this.jwt = jwt;
      this.fetchPage = fetchPage;
    }

    /// <summary>
    /// Enumerates the results one page at a time, following each page's <see cref="CloudFilePage.NextPageToken"/>
    /// until the cloud reports no more pages.
    /// </summary>
    /// <param name="continuationToken">Resume from a previously returned <see cref="CloudFilePage.NextPageToken"/>. Null starts from the first page.</param>
    /// <param name="pageSize">How many results the server should return per page. Null lets the server choose. Affects network round-trips, not which results you can enumerate.</param>
    /// <param name="cancellationToken">Cancels the enumeration.</param>
    /// <exception cref="Grpc.Core.RpcException">Thrown if a cloud request fails.</exception>
    public async IAsyncEnumerable<CloudFilePage> AsPages(string continuationToken = null, int? pageSize = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
      var pageToken = continuationToken;
      do {
        var request = BuildRequest(pageToken, pageSize);
        var response = await fetchPage(request, cancellationToken);

        var files = new List<CloudFile>(response.Results.Count);
        foreach (var proto in response.Results) {
          files.Add(FromProto(proto));
        }

        var next = response.HasNextPageToken && !string.IsNullOrEmpty(response.NextPageToken) ? response.NextPageToken : null;
        yield return new CloudFilePage(files, next);
        pageToken = next;
      } while (!string.IsNullOrEmpty(pageToken));
    }

    /// <summary>
    /// Streams every matching file, transparently following page tokens. Call with
    /// <c>await foreach (var file in CloudFiles.Search(query)) { ... }</c>.
    /// </summary>
    public async IAsyncEnumerator<CloudFile> GetAsyncEnumerator(CancellationToken cancellationToken = default) {
      await foreach (var page in AsPages(cancellationToken: cancellationToken)) {
        foreach (var file in page.Files) {
          cancellationToken.ThrowIfCancellationRequested();
          yield return file;
        }
      }
    }

    // Translates the public query (+ paging state) into the underlying protobuf request.
    private SearchCloudFilesRequest BuildRequest(string pageToken, int? pageSize) {
      // The device JWT is only populated on a ClassVR device, so fail with a clear message in the Editor
      // (rather than a NullReferenceException from the gRPC call) when no token is available.
      var jwtToUse = jwt ?? CVRProperties.Instance.DeviceJWT;
      if (string.IsNullOrEmpty(jwtToUse)) {
        throw new InvalidOperationException("Cloud file query failed: no authentication token available. The device JWT is only populated on a ClassVR device, not in the Editor. Pass an explicit 'jwt' to CloudFiles.Search to query from the Editor.");
      }

      var request = new SearchCloudFilesRequest {
        Auth = new Authorization { DeviceJwt = jwtToUse }
      };

      // Owner: explicit override on the query wins, otherwise default to the device's current organization.
      if (query.OrganizationId.HasValue) {
        request.OrganizationId = query.OrganizationId.Value;
      } else if (query.UserId.HasValue) {
        request.UserId = query.UserId.Value;
      } else {
        // OrganizationInfo is only populated on a ClassVR device. Guard against the NullReferenceException
        // that would otherwise occur in the Editor, and explain how to query without device properties.
        var organizationInfo = CVRProperties.Instance.OrganizationInfo;
        if (organizationInfo == null) {
          throw new InvalidOperationException("Cloud file query failed: no organization available. The device's organization info is only populated on a ClassVR device, not in the Editor. Set CloudFileQuery.OrganizationId (or UserId) to query from the Editor.");
        }
        request.OrganizationId = organizationInfo.Id;
      }

      if (!string.IsNullOrEmpty(query.Text)) {
        request.TextSearch = new TextSearch { Text = query.Text };
      }

      if (query.MediaTypes.Count > 0) {
        request.FilterMediaTypes.AddRange(query.MediaTypes);
      }

      if (query.Tags.Count > 0) {
        var condition = query.TagMatch == TagMatch.Any ? TagFilterCondition.HasAnyOf : TagFilterCondition.HasAllOf;
        var tagFilter = new TagFilter { Condition = condition };
        tagFilter.Tags.AddRange(query.Tags);
        request.TagFilters.Add(tagFilter);
      }

      if (query.CreatedAfter.HasValue) {
        request.After = Timestamp.FromDateTimeOffset(query.CreatedAfter.Value);
      }
      if (query.CreatedBefore.HasValue) {
        request.Before = Timestamp.FromDateTimeOffset(query.CreatedBefore.Value);
      }

      var orderClause = OrderClauseFor(query.OrderBy);
      if (orderClause != null) {
        request.OrderBy.Add(orderClause);
      }

      // The cloud returns no icon at all unless the request carries transcoding instructions
      if (query.IconSize != CloudIconSize.None) {
        request.IconSpec = new TranscodeImageSpec { MaxSizePixels = (int)query.IconSize };
      }

      if (pageSize.HasValue) {
        request.PageSize = pageSize.Value;
      }

      if (!string.IsNullOrEmpty(pageToken)) {
        request.PageToken = pageToken;
      }

      return request;
    }

    // Maps the friendly ordering enum to a single protobuf OrderClause (null = server default).
    private static OrderClause OrderClauseFor(CloudFileOrder order) {
      switch (order) {
        case CloudFileOrder.NewestFirst: return new OrderClause { Property = EntityProperty.Updated, SortOrder = SortOrder.Desc };
        case CloudFileOrder.OldestFirst: return new OrderClause { Property = EntityProperty.Updated, SortOrder = SortOrder.Asc };
        case CloudFileOrder.NameAToZ: return new OrderClause { Property = EntityProperty.Name, SortOrder = SortOrder.Asc };
        case CloudFileOrder.NameZToA: return new OrderClause { Property = EntityProperty.Name, SortOrder = SortOrder.Desc };
        case CloudFileOrder.LargestFirst: return new OrderClause { Property = EntityProperty.Size, SortOrder = SortOrder.Desc };
        case CloudFileOrder.SmallestFirst: return new OrderClause { Property = EntityProperty.Size, SortOrder = SortOrder.Asc };
        case CloudFileOrder.Default:
        default: return null;
      }
    }

    // Maps the transport type to the public POCO, normalising protobuf optionals to nullable C# values.
    private static CloudFile FromProto(ProtoCloudFile proto) {
      var fileName = proto.HasFileName ? proto.FileName : null;
      var mediaType = proto.HasMediaType ? proto.MediaType : null;
      long? sizeBytes = proto.HasSizeBytes ? proto.SizeBytes : (long?)null;
      var updated = proto.Updated?.ToDateTimeOffset();
      var tags = new List<int>(proto.Tags);

      return new CloudFile(proto.EntityId, fileName, proto.FileUrl, mediaType, sizeBytes, proto.IconUrl, updated, tags, proto.MetadataCount);
    }
  }
}
