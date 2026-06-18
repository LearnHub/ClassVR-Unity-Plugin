using System.Runtime.CompilerServices;

// Exposes internal types (the search-fetch seam and CloudFilePageable's constructor) to the EditMode test
// assembly so the query/paging/mapping logic can be unit-tested without a network or any gRPC types.
[assembly: InternalsVisibleTo("Avantis.ClassVR.EditorTests")]
