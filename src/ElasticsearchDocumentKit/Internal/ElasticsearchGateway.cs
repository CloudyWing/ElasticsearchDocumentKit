using System.Text.Json;
using CloudyWing.ElasticsearchDocumentKit.Querying;
using CloudyWing.ElasticsearchDocumentKit.Search;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Clients.Elasticsearch.IndexManagement;
using IndexExistsResponse = Elastic.Clients.Elasticsearch.IndexManagement.ExistsResponse;

namespace CloudyWing.ElasticsearchDocumentKit.Internal;

internal sealed class ElasticsearchGateway : IElasticsearchGateway {
    private readonly ElasticsearchClient client;

    internal ElasticsearchGateway(ElasticsearchClient client) {
        ArgumentNullException.ThrowIfNull(client);
        this.client = client;
    }

    public Task<IndexExistsResponse> IndexExistsAsync(string indexName, CancellationToken cancellationToken) {
        return client.Indices.ExistsAsync((Indices)indexName, cancellationToken);
    }

    public Task<CreateIndexResponse> CreateIndexAsync<TDocument>(
        string indexName,
        Action<CreateIndexRequestDescriptor<TDocument>>? configureIndex,
        CancellationToken cancellationToken
    ) where TDocument : class {
        return client.Indices.CreateAsync<TDocument>(
            (IndexName)indexName,
            descriptor => configureIndex?.Invoke(descriptor),
            cancellationToken
        );
    }

    public Task<IndexResponse> IndexAsync<TDocument>(
        string indexName,
        TDocument document,
        string? documentId,
        IndexDocumentOptions? options,
        CancellationToken cancellationToken
    ) where TDocument : class {
        if (documentId is null) {
            return client.IndexAsync(
                document,
                descriptor => {
                    descriptor.Index((IndexName)indexName);
                    ConfigureIndexOptions(descriptor, options);
                },
                cancellationToken
            );
        }

        return client.IndexAsync(
            document,
            (IndexName)indexName,
            (Id)documentId,
            descriptor => ConfigureIndexOptions(descriptor, options),
            cancellationToken
        );
    }

    public Task<IndexResponse> CreateAsync<TDocument>(
        string indexName,
        TDocument document,
        string? documentId,
        DocumentWriteOptions? options,
        CancellationToken cancellationToken
    ) where TDocument : class {
        return client.IndexAsync(
            document,
            descriptor => {
                descriptor.Index((IndexName)indexName);
                descriptor.OpType(OpType.Create);

                if (documentId is not null) {
                    descriptor.Id((Id)documentId);
                }

                if (options?.Refresh is not null) {
                    descriptor.Refresh(options.Refresh);
                }
            },
            cancellationToken
        );
    }

    public Task<DeleteResponse> DeleteAsync<TDocument>(
        string indexName,
        string documentId,
        DocumentWriteOptions? options,
        CancellationToken cancellationToken
    ) where TDocument : class {
        return client.DeleteAsync<TDocument>(
            (IndexName)indexName,
            (Id)documentId,
            descriptor => {
                if (options?.Refresh is not null) {
                    descriptor.Refresh(options.Refresh);
                }
            },
            cancellationToken
        );
    }

    public Task<GetResponse<TDocument>> GetAsync<TDocument>(
        string indexName,
        string documentId,
        CancellationToken cancellationToken
    ) where TDocument : class {
        return client.GetAsync<TDocument>((IndexName)indexName, (Id)documentId, cancellationToken);
    }

    public Task<UpdateResponse<TDocument>> UpdateAsync<TDocument, TPartialDocument>(
        string indexName,
        string documentId,
        TPartialDocument partialDocument,
        UpdateDocumentOptions? options,
        CancellationToken cancellationToken
    )
        where TDocument : class
        where TPartialDocument : class {
        return client.UpdateAsync<TDocument, TPartialDocument>(
            (IndexName)indexName,
            (Id)documentId,
            descriptor => {
                descriptor.Doc(partialDocument);
                ConfigureUpdateOptions(descriptor, options);
            },
            cancellationToken
        );
    }

    public Task<UpdateResponse<TDocument>> UpdateByScriptAsync<TDocument>(
        string indexName,
        string documentId,
        Script script,
        DocumentWriteOptions? options,
        CancellationToken cancellationToken
    ) where TDocument : class {
        return client.UpdateAsync<TDocument, object>(
            (IndexName)indexName,
            (Id)documentId,
            descriptor => {
                descriptor.Script(script);

                if (options?.Refresh is not null) {
                    descriptor.Refresh(options.Refresh);
                }
            },
            cancellationToken
        );
    }

    public Task<UpdateByQueryResponse> UpdateByQueryAsync<TDocument>(
        string indexName,
        UpdateByQueryRequest<TDocument> request,
        CancellationToken cancellationToken
    ) where TDocument : class {
        Action<QueryDescriptor<TDocument>>? query = FilterQueryFactory.Create(request.Filters);
        Action<UpdateByQueryRequestDescriptor<TDocument>> configure = descriptor => {
            if (query is not null) {
                descriptor.Query(query);
            }

            if (request.Script is not null) {
                descriptor.Script(request.Script);
            }

            descriptor.Refresh(request.Refresh);
            descriptor.WaitForCompletion(true);
            request.ConfigureRequest?.Invoke(descriptor);
        };

        if (query is null) {
            UpdateByQueryRequestDescriptor<TDocument> probe = new();
            configure(probe);
            EnsureRequestHasQuery((UpdateByQueryRequest)probe);
        }

        return client.UpdateByQueryAsync((Indices)indexName, configure, cancellationToken);
    }

    public Task<DeleteByQueryResponse> DeleteByQueryAsync<TDocument>(
        string indexName,
        DeleteByQueryRequest<TDocument> request,
        CancellationToken cancellationToken
    ) where TDocument : class {
        Action<QueryDescriptor<TDocument>>? query = FilterQueryFactory.Create(request.Filters);
        Action<DeleteByQueryRequestDescriptor<TDocument>> configure = descriptor => {
            if (query is not null) {
                descriptor.Query(query);
            }

            descriptor.Refresh(request.Refresh);
            descriptor.WaitForCompletion(true);
            request.ConfigureRequest?.Invoke(descriptor);
        };

        if (query is null) {
            DeleteByQueryRequestDescriptor<TDocument> probe = new();
            configure(probe);
            EnsureRequestHasQuery((DeleteByQueryRequest)probe);
        }

        return client.DeleteByQueryAsync((Indices)indexName, configure, cancellationToken);
    }

    public Task<CountResponse> CountAsync<TDocument>(
        string indexName,
        ElasticsearchQueryFilterBuilder<TDocument>? filters,
        CancellationToken cancellationToken
    ) where TDocument : class {
        Action<QueryDescriptor<TDocument>>? query = FilterQueryFactory.Create(filters);

        return client.CountAsync<TDocument>(
            (Indices)indexName,
            descriptor => {
                if (query is not null) {
                    descriptor.Query(query);
                }
            },
            cancellationToken
        );
    }

    public Task<BulkResponse> BulkIndexAsync<TDocument>(
        string indexName,
        IReadOnlyList<TDocument> documents,
        Func<TDocument, string?>? documentIdSelector,
        CancellationToken cancellationToken
    ) where TDocument : class {
        return client.BulkAsync(
            descriptor => descriptor
                .Index((IndexName)indexName)
                .IndexMany(documents, (itemDescriptor, document) => ConfigureBulkIndexItem(
                    itemDescriptor,
                    document,
                    documentIdSelector
                )),
            cancellationToken
        );
    }

    public Task<SearchResponse<TDocument>> SearchAsync<TDocument>(
        string indexName,
        DocumentSearchRequest<TDocument> request,
        CancellationToken cancellationToken
    ) where TDocument : class {
        Action<QueryDescriptor<TDocument>>? query = FilterQueryFactory.Create(request.Filters);

        return client.SearchAsync<TDocument>(
            (Indices)indexName,
            descriptor => {
                if (query is not null) {
                    descriptor.Query(query);
                }

                if (request.Size is not null) {
                    descriptor.Size(request.Size);
                }

                request.ConfigureSearch?.Invoke(descriptor);
            },
            cancellationToken
        );
    }

    public Task<SearchResponse<TDocument>> SearchPageAsync<TDocument>(
        string indexName,
        DocumentPageRequest<TDocument> request,
        CancellationToken cancellationToken
    ) where TDocument : class {
        Action<QueryDescriptor<TDocument>>? query = FilterQueryFactory.Create(request.Filters);
        int from = request.GetFrom();

        return client.SearchAsync<TDocument>(
            (Indices)indexName,
            descriptor => {
                if (query is not null) {
                    descriptor.Query(query);
                }

                descriptor.From(from);
                descriptor.Size(request.PageSize);
                descriptor.TrackTotalHits(new TrackHits(request.TrackTotalHits));
                request.ConfigureSearch?.Invoke(descriptor);
            },
            cancellationToken
        );
    }

    public Task<SearchResponse<TDocument>> SearchAfterAsync<TDocument>(
        string indexName,
        SearchAfterRequest<TDocument> request,
        CancellationToken cancellationToken
    ) where TDocument : class {
        return client.SearchAsync<TDocument>(
            (Indices)indexName,
            descriptor => ConfigureSearchAfterDescriptor(descriptor, request),
            cancellationToken
        );
    }

    private void EnsureRequestHasQuery<TRequest>(TRequest request) {
        using MemoryStream stream = new();
        client.RequestResponseSerializer.Serialize(request, stream);
        stream.Position = 0;
        using JsonDocument document = JsonDocument.Parse(stream);

        if (!document.RootElement.TryGetProperty("query", out _)) {
            throw new ArgumentException(
                "A by-query request without filters must specify a query through ConfigureRequest.",
                nameof(request)
            );
        }
    }

    private static void ConfigureIndexOptions<TDocument>(
        IndexRequestDescriptor<TDocument> descriptor,
        IndexDocumentOptions? options
    ) where TDocument : class {
        if (options is null) {
            return;
        }

        if (options.Refresh is not null) {
            descriptor.Refresh(options.Refresh);
        }

        if (options.IfSeqNo is not null) {
            descriptor.IfSeqNo(options.IfSeqNo);
        }

        if (options.IfPrimaryTerm is not null) {
            descriptor.IfPrimaryTerm(options.IfPrimaryTerm);
        }
    }

    private static void ConfigureUpdateOptions<TDocument, TPartialDocument>(
        UpdateRequestDescriptor<TDocument, TPartialDocument> descriptor,
        UpdateDocumentOptions? options
    )
        where TDocument : class
        where TPartialDocument : class {
        if (options is null) {
            return;
        }

        if (options.Refresh is not null) {
            descriptor.Refresh(options.Refresh);
        }

        if (options.IfSeqNo is not null) {
            descriptor.IfSeqNo(options.IfSeqNo);
        }

        if (options.IfPrimaryTerm is not null) {
            descriptor.IfPrimaryTerm(options.IfPrimaryTerm);
        }

        if (options.RetryOnConflict > 0) {
            descriptor.RetryOnConflict(options.RetryOnConflict);
        }
    }

    private static void ConfigureBulkIndexItem<TDocument>(
        Elastic.Clients.Elasticsearch.Core.Bulk.BulkIndexOperationDescriptor<TDocument> descriptor,
        TDocument document,
        Func<TDocument, string?>? documentIdSelector
    ) where TDocument : class {
        string? documentId = documentIdSelector?.Invoke(document);

        if (!string.IsNullOrWhiteSpace(documentId)) {
            descriptor.Id((Id)documentId);
        }
    }

    private static void ConfigureSearchAfterDescriptor<TDocument>(
        SearchRequestDescriptor<TDocument> descriptor,
        SearchAfterRequest<TDocument> request
    ) where TDocument : class {
        descriptor.Size(request.Size);

        if (request.SearchAfter.Count > 0) {
            descriptor.SearchAfter(request.SearchAfter.ToArray());
        }

        request.ConfigureSearch?.Invoke(descriptor);
    }
}
