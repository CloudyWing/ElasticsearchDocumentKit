using CloudyWing.ElasticsearchDocumentKit;

namespace CloudyWing.ElasticsearchDocumentKit.Tests;

[ElasticsearchIndexName("sample-documents")]
internal sealed class SampleDocument {
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required int SortKey { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
