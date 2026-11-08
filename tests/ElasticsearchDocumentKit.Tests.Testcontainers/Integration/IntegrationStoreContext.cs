using CloudyWing.ElasticsearchDocumentKit.Indexing;
using CloudyWing.ElasticsearchDocumentKit.Modeling;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;

namespace CloudyWing.ElasticsearchDocumentKit.Tests.Testcontainers.Integration;

/// <summary>
/// 為單一整合場景建立獨立的 index 與 Store，結束時刪除 index。
/// </summary>
/// <typeparam name="TDocument">文件型別。</typeparam>
internal sealed class IntegrationStoreContext<TDocument> : IAsyncDisposable
    where TDocument : class {
    private readonly ElasticsearchClient client;

    private IntegrationStoreContext(
        ElasticsearchClient client,
        ElasticsearchDocumentStore<TDocument> store,
        string indexName
    ) {
        this.client = client;
        Store = store;
        IndexName = indexName;
    }

    public ElasticsearchDocumentStore<TDocument> Store { get; }

    public string IndexName { get; }

    public static async Task<IntegrationStoreContext<TDocument>> CreateAsync(
        ElasticsearchClient client,
        Action<TypeMappingDescriptor<TDocument>> configureMapping,
        Func<TDocument, string?> idSelector
    ) {
        ElasticsearchDocumentsOptions options = new() {
            IndexPrefix = $"estk-tests-{Guid.NewGuid():N}"
        };
        options.ConfigureDocument<TDocument>(model => model
            .ConfigureMapping(configureMapping)
            .HasId(idSelector)
        );
        DefaultIndexNameResolver resolver = new(options);
        DefaultElasticsearchDocumentModelResolver modelResolver = new(options, resolver);
        string indexName = resolver.Resolve<TDocument>();
        ElasticsearchIndexInitializer<TDocument> initializer = new(client, modelResolver);

        IndexInitializationResult initializationResult = await initializer.InitializeAsync().ConfigureAwait(false);
        Assert.That(initializationResult.Created, Is.True);

        return new IntegrationStoreContext<TDocument>(
            client,
            new ElasticsearchDocumentStore<TDocument>(client, modelResolver),
            indexName
        );
    }

    public async ValueTask DisposeAsync() {
        Elastic.Clients.Elasticsearch.IndexManagement.ExistsResponse existsResponse = await client
            .Indices
            .ExistsAsync((Indices)IndexName)
            .ConfigureAwait(false);

        if (existsResponse.Exists) {
            await client.Indices.DeleteAsync((Indices)IndexName).ConfigureAwait(false);
        }
    }
}
