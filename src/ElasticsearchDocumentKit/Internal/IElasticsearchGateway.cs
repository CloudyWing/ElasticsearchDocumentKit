using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using CloudyWing.ElasticsearchDocumentKit.Querying;
using CloudyWing.ElasticsearchDocumentKit.Search;
using IndexExistsResponse = Elastic.Clients.Elasticsearch.IndexManagement.ExistsResponse;

namespace CloudyWing.ElasticsearchDocumentKit.Internal;

internal interface IElasticsearchGateway {
    Task<IndexExistsResponse> IndexExistsAsync(string indexName, CancellationToken cancellationToken);

    Task<CreateIndexResponse> CreateIndexAsync<TDocument>(
        string indexName,
        Action<CreateIndexRequestDescriptor<TDocument>>? configureIndex,
        CancellationToken cancellationToken
    )
        where TDocument : class;

    Task<IndexResponse> IndexAsync<TDocument>(
        string indexName,
        TDocument document,
        string? documentId,
        IndexDocumentOptions? options,
        CancellationToken cancellationToken
    )
        where TDocument : class;

    Task<IndexResponse> CreateAsync<TDocument>(
        string indexName,
        TDocument document,
        string? documentId,
        DocumentWriteOptions? options,
        CancellationToken cancellationToken
    )
        where TDocument : class;

    Task<DeleteResponse> DeleteAsync<TDocument>(
        string indexName,
        string documentId,
        DocumentWriteOptions? options,
        CancellationToken cancellationToken
    )
        where TDocument : class;

    Task<GetResponse<TDocument>> GetAsync<TDocument>(
        string indexName,
        string documentId,
        CancellationToken cancellationToken
    )
        where TDocument : class;

    Task<UpdateResponse<TDocument>> UpdateAsync<TDocument, TPartialDocument>(
        string indexName,
        string documentId,
        TPartialDocument partialDocument,
        UpdateDocumentOptions? options,
        CancellationToken cancellationToken
    )
        where TDocument : class
        where TPartialDocument : class;

    Task<UpdateResponse<TDocument>> UpdateByScriptAsync<TDocument>(
        string indexName,
        string documentId,
        Script script,
        DocumentWriteOptions? options,
        CancellationToken cancellationToken
    )
        where TDocument : class;

    Task<UpdateByQueryResponse> UpdateByQueryAsync<TDocument>(
        string indexName,
        UpdateByQueryRequest<TDocument> request,
        CancellationToken cancellationToken
    )
        where TDocument : class;

    Task<DeleteByQueryResponse> DeleteByQueryAsync<TDocument>(
        string indexName,
        DeleteByQueryRequest<TDocument> request,
        CancellationToken cancellationToken
    )
        where TDocument : class;

    Task<CountResponse> CountAsync<TDocument>(
        string indexName,
        ElasticsearchQueryFilterBuilder<TDocument>? filters,
        CancellationToken cancellationToken
    )
        where TDocument : class;

    Task<BulkResponse> BulkIndexAsync<TDocument>(
        string indexName,
        IReadOnlyList<TDocument> documents,
        Func<TDocument, string?>? documentIdSelector,
        CancellationToken cancellationToken
    )
        where TDocument : class;

    Task<SearchResponse<TDocument>> SearchAsync<TDocument>(
        string indexName,
        DocumentSearchRequest<TDocument> request,
        CancellationToken cancellationToken
    )
        where TDocument : class;

    Task<SearchResponse<TDocument>> SearchPageAsync<TDocument>(
        string indexName,
        DocumentPageRequest<TDocument> request,
        CancellationToken cancellationToken
    )
        where TDocument : class;

    Task<SearchResponse<TDocument>> SearchAfterAsync<TDocument>(
        string indexName,
        SearchAfterRequest<TDocument> request,
        CancellationToken cancellationToken
    )
        where TDocument : class;
}
