using System.Text.Json;
using CloudyWing.ElasticsearchDocumentKit.Internal;
using CloudyWing.ElasticsearchDocumentKit.Querying;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace CloudyWing.ElasticsearchDocumentKit.Tests.Internal;

[TestFixture]
internal sealed class FilterQueryFactoryTests {
    [Test]
    public void Create_WithNullBuilder_ReturnsNull() {
        Action<QueryDescriptor<SampleDocument>>? result = FilterQueryFactory.Create<SampleDocument>(null);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Create_WithEmptyBuilder_ReturnsNull() {
        Action<QueryDescriptor<SampleDocument>>? result = FilterQueryFactory.Create(
            new ElasticsearchQueryFilterBuilder<SampleDocument>()
        );

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Create_WithBuilderThatSkipsAllValues_ReturnsNull() {
        ElasticsearchQueryFilterBuilder<SampleDocument> builder = new();
        builder.Term(document => document.Name, null);

        Action<QueryDescriptor<SampleDocument>>? result = FilterQueryFactory.Create(builder);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void Create_WithConditions_ProducesSameJsonAsQueryFilters() {
        ElasticsearchClient client = RecordingClientFactory.Create();
        ElasticsearchQueryFilterBuilder<SampleDocument> builder = new();
        builder.Term(document => document.Name, "Order 1");
        builder.Term(document => document.SortKey, 1);

        Action<QueryDescriptor<SampleDocument>>? factoryQuery = FilterQueryFactory.Create(builder);
        SearchRequest factoryRequest = new SearchRequestDescriptor<SampleDocument>().Query(factoryQuery!);
        SearchRequest extensionRequest = new SearchRequestDescriptor<SampleDocument>().QueryFilters(configured => {
            configured.Term(document => document.Name, "Order 1");
            configured.Term(document => document.SortKey, 1);
        });
        string factoryJson = RecordingClientFactory.Serialize(client, factoryRequest);
        string extensionJson = RecordingClientFactory.Serialize(client, extensionRequest);

        using JsonDocument parsed = JsonDocument.Parse(factoryJson);
        JsonElement filter = parsed.RootElement.GetProperty("query").GetProperty("bool").GetProperty("filter");

        using (Assert.EnterMultipleScope()) {
            Assert.That(factoryQuery, Is.Not.Null);
            Assert.That(filter.GetArrayLength(), Is.EqualTo(2));
            Assert.That(factoryJson, Is.EqualTo(extensionJson));
        }
    }
}
