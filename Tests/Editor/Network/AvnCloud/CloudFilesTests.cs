using System;
using System.Linq;
using System.Threading;
using Avn.Connect.V1;
using ClassVR.Tests.Common;
using Google.Protobuf.WellKnownTypes;
using NUnit.Framework;
using ProtoCloudFile = Avn.Connect.V1.CloudFile;

namespace ClassVR.Network.AvnCloud.Tests {
  /// <summary>
  /// EditMode unit tests for the cloud file query API. They exercise request building, response mapping, paging,
  /// and the error guards by substituting a <see cref="RecordingFetch"/> for the gRPC call — no network involved.
  /// </summary>
  public class CloudFilesTests {
    private const string TestJwt = "test-jwt";
    private const int TestOrgId = 1;

    // --- helpers -------------------------------------------------------------

    // A query with an explicit org so BuildRequest never falls back to the device-only CVRProperties singleton.
    private static CloudFileQuery Query() => new CloudFileQuery { OrganizationId = TestOrgId };

    private static CloudFilePageable Pageable(RecordingFetch fetch, CloudFileQuery query = null) =>
        new CloudFilePageable(query ?? Query(), TestJwt, fetch.Fetch);

    private static ProtoCloudFile File(int id) => new ProtoCloudFile { EntityId = id, FileUrl = "url" + id };

    private static SearchCloudFilesResponse Response(params ProtoCloudFile[] files) {
      var response = new SearchCloudFilesResponse();
      response.Results.AddRange(files);
      return response;
    }

    // The single request captured after enumerating an empty response (one round-trip, then the loop ends).
    private static SearchCloudFilesRequest CapturedRequest(CloudFileQuery query) {
      var fetch = new RecordingFetch(new SearchCloudFilesResponse());
      Pageable(fetch, query).ToListSync();
      return fetch.Requests.Single();
    }

    // --- mapping (FromProto) -------------------------------------------------

    [Test]
    public void MapsAllPopulatedFields() {
      var updated = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
      var proto = new ProtoCloudFile {
        EntityId = 42, FileUrl = "the-url", FileName = "photo.png", MediaType = "image/png",
        SizeBytes = 123, IconUrl = "icon-url", MetadataCount = 3, Updated = Timestamp.FromDateTimeOffset(updated)
      };
      proto.Tags.AddRange(new[] { 1, 2, 3 });

      var file = Pageable(new RecordingFetch(Response(proto))).ToListSync().Single();

      Assert.AreEqual(42, file.EntityId);
      Assert.AreEqual("photo.png", file.FileName);
      Assert.AreEqual("the-url", file.FileUrl);
      Assert.AreEqual("image/png", file.MediaType);
      Assert.AreEqual(123L, file.SizeBytes);
      Assert.AreEqual("icon-url", file.IconUrl);
      Assert.AreEqual(3, file.MetadataCount);
      Assert.AreEqual(updated, file.Updated);
      CollectionAssert.AreEqual(new[] { 1, 2, 3 }, file.Tags);
    }

    [Test]
    public void MapsUnsetOptionalsToNull() {
      int fileId = 7;
      var file = Pageable(new RecordingFetch(Response(File(fileId)))).ToListSync().Single();

      Assert.AreEqual(fileId, file.EntityId);
      Assert.IsNull(file.FileName);
      Assert.IsNull(file.MediaType);
      Assert.IsNull(file.SizeBytes);
      Assert.IsNull(file.Updated);
      Assert.IsEmpty(file.Tags);
      // Not an optional in the proto, so an absent count arrives as zero rather than null
      Assert.AreEqual(0, file.MetadataCount);
    }

    // --- paging --------------------------------------------------------------

    [Test]
    public void FollowsNextPageTokenAcrossPages() {
      var page1 = Response(File(1), File(2));
      page1.NextPageToken = "tok2";
      var page2 = Response(File(3)); // no token -> last page
      var fetch = new RecordingFetch(page1, page2);

      var ids = Pageable(fetch).ToListSync().Select(f => f.EntityId).ToArray();

      CollectionAssert.AreEqual(new[] { 1, 2, 3 }, ids);
      Assert.AreEqual(2, fetch.Requests.Count);
      Assert.IsFalse(fetch.Requests[0].HasPageToken, "first request should carry no continuation token");
      Assert.AreEqual("tok2", fetch.Requests[1].PageToken);
    }

    [Test]
    public void StopsAfterSinglePageWithNoToken() {
      var fetch = new RecordingFetch(Response(File(1)));

      Pageable(fetch).ToListSync();

      Assert.AreEqual(1, fetch.Requests.Count);
    }

    [Test]
    public void PageSizeOverrideIsSentOnRequest() {
      var fetch = new RecordingFetch(new SearchCloudFilesResponse());

      Pageable(fetch).AsPages(pageSize: 5).ToListSync();

      Assert.AreEqual(5, fetch.Requests.Single().PageSize);
    }

    // --- tag matching --------------------------------------------------------

    [TestCase(TagMatch.All, TagFilterCondition.HasAllOf)]
    [TestCase(TagMatch.Any, TagFilterCondition.HasAnyOf)]
    public void TagMatchMapsToCondition(TagMatch match, TagFilterCondition expected) {
      var query = Query();
      query.Tags.Add(1);
      query.Tags.Add(2);
      query.TagMatch = match;

      var request = CapturedRequest(query);

      Assert.AreEqual(1, request.TagFilters.Count);
      Assert.AreEqual(expected, request.TagFilters[0].Condition);
      CollectionAssert.AreEqual(new[] { 1, 2 }, request.TagFilters[0].Tags);
    }

    [Test]
    public void NoTagsMeansNoTagFilter() {
      Assert.IsEmpty(CapturedRequest(Query()).TagFilters);
    }

    // --- ordering ------------------------------------------------------------

    [TestCase(CloudFileOrder.NewestFirst, EntityProperty.Updated, SortOrder.Desc)]
    [TestCase(CloudFileOrder.OldestFirst, EntityProperty.Updated, SortOrder.Asc)]
    [TestCase(CloudFileOrder.NameAToZ, EntityProperty.Name, SortOrder.Asc)]
    [TestCase(CloudFileOrder.NameZToA, EntityProperty.Name, SortOrder.Desc)]
    [TestCase(CloudFileOrder.LargestFirst, EntityProperty.Size, SortOrder.Desc)]
    [TestCase(CloudFileOrder.SmallestFirst, EntityProperty.Size, SortOrder.Asc)]
    public void OrderByMapsToOrderClause(CloudFileOrder order, EntityProperty property, SortOrder sortOrder) {
      var query = Query();
      query.OrderBy = order;

      var request = CapturedRequest(query);

      Assert.AreEqual(1, request.OrderBy.Count);
      Assert.AreEqual(property, request.OrderBy[0].Property);
      Assert.AreEqual(sortOrder, request.OrderBy[0].SortOrder);
    }

    [Test]
    public void DefaultOrderSendsNoOrderClause() {
      var query = Query();
      query.OrderBy = CloudFileOrder.Default;

      Assert.IsEmpty(CapturedRequest(query).OrderBy);
    }

    // --- filters -------------------------------------------------------------

    [Test]
    public void FiltersMapToRequest() {
      var after = DateTimeOffset.FromUnixTimeSeconds(1_000);
      var before = DateTimeOffset.FromUnixTimeSeconds(2_000);
      var query = Query();
      query.Text = "mars";
      query.MediaTypes.Add("image/png");
      query.CreatedAfter = after;
      query.CreatedBefore = before;

      var request = CapturedRequest(query);

      Assert.AreEqual("mars", request.TextSearch.Text);
      CollectionAssert.AreEqual(new[] { "image/png" }, request.FilterMediaTypes);
      Assert.AreEqual(Timestamp.FromDateTimeOffset(after), request.After);
      Assert.AreEqual(Timestamp.FromDateTimeOffset(before), request.Before);
    }

    [Test]
    public void OwnerDefaultsToQueryOrganization() {
      var request = CapturedRequest(Query());

      Assert.AreEqual(TestOrgId, request.OrganizationId);
    }

    // --- icon size -----------------------------------------------------------

    [TestCase(CloudIconSize.Pixels256, 256)]
    [TestCase(CloudIconSize.Pixels8, 8)]
    [TestCase(CloudIconSize.Pixels2048, 2048)]
    [TestCase(CloudIconSize.Original, -1)]
    public void IconSizeMapsToIconSpec(CloudIconSize size, int expectedPixels) {
      var query = Query();
      query.IconSize = size;

      var request = CapturedRequest(query);

      Assert.AreEqual(expectedPixels, request.IconSpec.MaxSizePixels);
    }

    [Test]
    public void NoIconSizeLeavesIconSpecUnset() {
      // The cloud returns no icon unless a spec is sent, so the default must not send one
      var request = CapturedRequest(Query());

      Assert.IsNull(request.IconSpec, "an unset IconSize must not put an IconSpec on the request");
    }

    // --- metadata filters ----------------------------------------------------
    //
    // The factories are the only way to build a filter, so they are what enforces that match text is present
    // for the value conditions and absent for the presence ones (where the cloud ignores it).

    private const string TestKey = "Task";
    private const string TestMatch = "scan-room";

    [Test]
    public void HasKeyFilterTestsPresenceOnly() {
      var filter = CloudMetadataFilter.HasKey(TestKey);

      Assert.AreEqual(TestKey, filter.Key);
      Assert.AreEqual(MetadataMatch.HasKey, filter.Condition);
      Assert.IsNull(filter.Match, "presence conditions must not carry match text");
    }

    [Test]
    public void HasNotKeyFilterTestsAbsenceOnly() {
      var filter = CloudMetadataFilter.HasNotKey(TestKey);

      Assert.AreEqual(TestKey, filter.Key);
      Assert.AreEqual(MetadataMatch.HasNotKey, filter.Condition);
      Assert.IsNull(filter.Match, "presence conditions must not carry match text");
    }

    [Test]
    public void ValueEqualsFilterCarriesMatchText() {
      var filter = CloudMetadataFilter.ValueEquals(TestKey, "scan-room");

      Assert.AreEqual(TestKey, filter.Key);
      Assert.AreEqual(MetadataMatch.Equals, filter.Condition);
      Assert.AreEqual("scan-room", filter.Match);
    }

    [Test]
    public void ValueStartsWithFilterCarriesMatchText() {
      var filter = CloudMetadataFilter.ValueStartsWith(TestKey, "scan-");

      Assert.AreEqual(TestKey, filter.Key);
      Assert.AreEqual(MetadataMatch.StartsWith, filter.Condition);
      Assert.AreEqual("scan-", filter.Match);
    }

    [Test]
    public void ValueContainsFilterCarriesMatchText() {
      var filter = CloudMetadataFilter.ValueContains(TestKey, "room");

      Assert.AreEqual(TestKey, filter.Key);
      Assert.AreEqual(MetadataMatch.Contains, filter.Condition);
      Assert.AreEqual("room", filter.Match);
    }

    [TestCase(null)]
    [TestCase("")]
    public void FilterWithoutKeyThrows(string key) {
      Assert.Throws<ArgumentException>(() => CloudMetadataFilter.HasKey(key));
      Assert.Throws<ArgumentException>(() => CloudMetadataFilter.HasNotKey(key));
      Assert.Throws<ArgumentException>(() => CloudMetadataFilter.ValueEquals(key, "x"));
      Assert.Throws<ArgumentException>(() => CloudMetadataFilter.ValueStartsWith(key, "x"));
      Assert.Throws<ArgumentException>(() => CloudMetadataFilter.ValueContains(key, "x"));
    }

    [Test]
    public void ValueFilterWithNullMatchThrows() {
      Assert.Throws<ArgumentNullException>(() => CloudMetadataFilter.ValueEquals(TestKey, null));
      Assert.Throws<ArgumentNullException>(() => CloudMetadataFilter.ValueStartsWith(TestKey, null));
      Assert.Throws<ArgumentNullException>(() => CloudMetadataFilter.ValueContains(TestKey, null));
    }

    [Test]
    public void ValueFilterAcceptsEmptyMatch() {
      // Deliberately allowed: "contains nothing" is unhelpful but well-formed, unlike a null match
      Assert.AreEqual("", CloudMetadataFilter.ValueContains(TestKey, "").Match);
    }

    // Builds a filter for the given condition, so the mapping can be driven from a TestCase
    private static CloudMetadataFilter FilterWith(MetadataMatch match) {
      switch (match) {
        case MetadataMatch.HasKey: return CloudMetadataFilter.HasKey(TestKey);
        case MetadataMatch.HasNotKey: return CloudMetadataFilter.HasNotKey(TestKey);
        case MetadataMatch.Equals: return CloudMetadataFilter.ValueEquals(TestKey, TestMatch);
        case MetadataMatch.StartsWith: return CloudMetadataFilter.ValueStartsWith(TestKey, TestMatch);
        case MetadataMatch.Contains: return CloudMetadataFilter.ValueContains(TestKey, TestMatch);
        default: throw new ArgumentOutOfRangeException(nameof(match));
      }
    }

    [TestCase(MetadataMatch.HasKey, MetadataFilterCondition.HasKey)]
    [TestCase(MetadataMatch.HasNotKey, MetadataFilterCondition.HasNotKey)]
    [TestCase(MetadataMatch.Equals, MetadataFilterCondition.Equals)]
    [TestCase(MetadataMatch.StartsWith, MetadataFilterCondition.StartsWith)]
    [TestCase(MetadataMatch.Contains, MetadataFilterCondition.Contains)]
    public void MetadataMatchMapsToCondition(MetadataMatch match, MetadataFilterCondition expected) {
      var query = Query();
      query.MetadataFilters.Add(FilterWith(match));

      var filter = CapturedRequest(query).MetadataFilters.Single();

      Assert.AreEqual(TestKey, filter.Key);
      Assert.AreEqual(expected, filter.Condition);
    }

    [Test]
    public void ValueFilterSendsMatchText() {
      var query = Query();
      query.MetadataFilters.Add(CloudMetadataFilter.ValueEquals(TestKey, TestMatch));

      var filter = CapturedRequest(query).MetadataFilters.Single();

      Assert.AreEqual(TestMatch, filter.Match);
    }

    [Test]
    public void PresenceFilterSendsNoMatchText() {
      var query = Query();
      query.MetadataFilters.Add(CloudMetadataFilter.HasKey(TestKey));

      var filter = CapturedRequest(query).MetadataFilters.Single();

      Assert.IsFalse(filter.HasMatch, "the cloud ignores match text for presence conditions, so it must not be sent");
    }

    [Test]
    public void MultipleMetadataFiltersAllReachTheRequest() {
      // They combine with AND server-side, so every one of them has to be sent
      var query = Query();
      query.MetadataFilters.Add(CloudMetadataFilter.ValueEquals("Task", "scan-room"));
      query.MetadataFilters.Add(CloudMetadataFilter.HasNotKey("Archived"));

      var request = CapturedRequest(query);

      CollectionAssert.AreEqual(new[] { "Task", "Archived" }, request.MetadataFilters.Select(f => f.Key).ToArray());
      CollectionAssert.AreEqual(
          new[] { MetadataFilterCondition.Equals, MetadataFilterCondition.HasNotKey },
          request.MetadataFilters.Select(f => f.Condition).ToArray());
    }

    [Test]
    public void NoMetadataFiltersMeansNoneOnRequest() {
      Assert.IsEmpty(CapturedRequest(Query()).MetadataFilters);
    }

    // --- cancellation --------------------------------------------------------

    [Test]
    public void CancelledTokenStopsEnumeration() {
      using (var cts = new CancellationTokenSource()) {
        cts.Cancel();
        var pageable = Pageable(new RecordingFetch(Response(File(1))));

        Assert.Throws<OperationCanceledException>(() => pageable.ToListSync(cts.Token));
      }
    }

    // --- error guards --------------------------------------------------------

    [Test]
    public void MissingJwtThrowsBeforeFetching() {
      var fetch = new RecordingFetch();
      var pageable = new CloudFilePageable(Query(), jwt: null, fetch.Fetch);

      var ex = Assert.Throws<InvalidOperationException>(() => pageable.ToListSync());

      Assert.That(ex.Message, Does.Contain("authentication token"));
      Assert.IsEmpty(fetch.Requests, "should fail before any request is sent");
    }

    [Test]
    public void MissingOrganizationThrowsBeforeFetching() {
      var fetch = new RecordingFetch();
      // No OrganizationId/UserId, and CVRProperties has none in the Editor -> org guard.
      var pageable = new CloudFilePageable(new CloudFileQuery(), TestJwt, fetch.Fetch);

      var ex = Assert.Throws<InvalidOperationException>(() => pageable.ToListSync());

      Assert.That(ex.Message, Does.Contain("organization"));
      Assert.IsEmpty(fetch.Requests, "should fail before any request is sent");
    }
  }
}
