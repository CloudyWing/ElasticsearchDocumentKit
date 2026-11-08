using System.Diagnostics;
using CloudyWing.ElasticsearchDocumentKit.Modeling;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using CloudyWing.ElasticsearchDocumentKit.Internal;
using IndexExistsResponse = Elastic.Clients.Elasticsearch.IndexManagement.ExistsResponse;

namespace CloudyWing.ElasticsearchDocumentKit.Indexing;

/// <summary>
/// 使用官方 <see cref="ElasticsearchClient"/> 顯式建立 index 與 mapping。
/// </summary>
/// <typeparam name="TDocument">文件型別。</typeparam>
public sealed class ElasticsearchIndexInitializer<TDocument> : IIndexInitializer<TDocument>
    where TDocument : class {
    private const string ResourceAlreadyExistsErrorType = "resource_already_exists_exception";

    private readonly IElasticsearchGateway gateway;
    private readonly IElasticsearchDocumentModelResolver documentModelResolver;

    /// <summary>
    /// 初始化 <see cref="ElasticsearchIndexInitializer{TDocument}"/> 類別的新執行個體。
    /// </summary>
    /// <param name="client">官方 Elasticsearch client。</param>
    /// <param name="documentModelResolver">文件模型解析器。</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="client"/> 或 <paramref name="documentModelResolver"/> 為 <see langword="null"/>。
    /// </exception>
    public ElasticsearchIndexInitializer(
        ElasticsearchClient client,
        IElasticsearchDocumentModelResolver documentModelResolver
    )
        : this(new ElasticsearchGateway(client), documentModelResolver) {
    }

    internal ElasticsearchIndexInitializer(
        IElasticsearchGateway gateway,
        IElasticsearchDocumentModelResolver documentModelResolver
    ) {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(documentModelResolver);

        this.gateway = gateway;
        this.documentModelResolver = documentModelResolver;
    }

    /// <inheritdoc/>
    public async Task<IndexInitializationResult> InitializeAsync(CancellationToken cancellationToken = default) {
        ElasticsearchDocumentModel<TDocument> model = documentModelResolver.Resolve<TDocument>();
        string indexName = model.IndexName;
        using Activity? activity = ElasticsearchDocumentsTelemetry.ActivitySource.StartActivity(
            "elasticsearch.index_initialize",
            ActivityKind.Client
        );
        activity?.SetTag("db.system", "elasticsearch");
        activity?.SetTag("db.operation", "index_initialize");
        activity?.SetTag("elasticsearch.index", indexName);

        IndexExistsResponse existsResponse = await gateway
            .IndexExistsAsync(indexName, cancellationToken)
            .ConfigureAwait(false);

        if (existsResponse.Exists) {
            IndexInitializationResult existingResult = ElasticsearchResultMapper.FromExistingIndex(
                indexName,
                existsResponse
            );
            SetActivityTags(activity, existingResult);
            return existingResult;
        }

        if (!existsResponse.IsValidResponse && existsResponse.ApiCallDetails?.HttpStatusCode != 404) {
            IndexInitializationResult failedResult = ElasticsearchResultMapper.FromExistingIndex(
                indexName,
                existsResponse
            );
            SetActivityTags(activity, failedResult);
            return failedResult;
        }

        CreateIndexResponse createResponse = await gateway
            .CreateIndexAsync(indexName, model.ConfigureIndex, cancellationToken)
            .ConfigureAwait(false);
        IndexInitializationResult createdResult = IsConcurrentCreateSuccess(createResponse)
            ? ElasticsearchResultMapper.FromConcurrentIndexCreation(indexName, createResponse)
            : ElasticsearchResultMapper.FromCreateIndexResponse(indexName, createResponse);

        SetActivityTags(activity, createdResult);

        return createdResult;
    }

    private static bool IsConcurrentCreateSuccess(CreateIndexResponse response) {
        return !response.IsValidResponse
            && IsResourceAlreadyExistsError(response.ElasticsearchServerError?.Error?.Type);
    }

    internal static bool IsResourceAlreadyExistsError(string? errorType) {
        return string.Equals(errorType, ResourceAlreadyExistsErrorType, StringComparison.Ordinal);
    }

    private static void SetActivityTags(Activity? activity, IndexInitializationResult result) {
        activity?.SetTag("elasticsearch.success", result.IsSuccessful);
        activity?.SetTag("elasticsearch.index_created", result.Created);
        activity?.SetTag("elasticsearch.index_already_exists", result.AlreadyExists);

        if (!string.IsNullOrWhiteSpace(result.ErrorType)) {
            activity?.SetTag("error.type", result.ErrorType);
        }
    }
}
