using CloudyWing.ElasticsearchDocumentKit.Geo;

namespace CloudyWing.ElasticsearchDocumentKit.Tests;

[ElasticsearchIndexName("geo-sample-documents")]
internal sealed class GeoSampleDocument {
    public required string Id { get; init; }

    public GeoPoint? Location { get; init; }
}
