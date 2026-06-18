using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avn.Connect.V1;

namespace ClassVR.Network.AvnCloud.Tests {
  /// <summary>
  /// Test double for the <see cref="SearchCloudFilesFetch"/> seam. Returns canned responses in order, records the
  /// requests it was given, and honours cancellation — all without a network or any gRPC types.
  /// </summary>
  internal sealed class RecordingFetch {
    private readonly Queue<SearchCloudFilesResponse> pages;

    /// <summary>The requests passed to <see cref="Fetch"/>, in call order.</summary>
    public readonly List<SearchCloudFilesRequest> Requests = new List<SearchCloudFilesRequest>();

    public RecordingFetch(params SearchCloudFilesResponse[] pages) {
      this.pages = new Queue<SearchCloudFilesResponse>(pages);
    }

    /// <summary>The seam delegate to hand to <see cref="CloudFilePageable"/>.</summary>
    public Task<SearchCloudFilesResponse> Fetch(SearchCloudFilesRequest request, CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      Requests.Add(request);
      var response = pages.Count > 0 ? pages.Dequeue() : new SearchCloudFilesResponse();
      return Task.FromResult(response);
    }
  }
}
