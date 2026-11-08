using System.Diagnostics;
using CloudyWing.ElasticsearchDocumentKit.Internal;
using CloudyWing.ElasticsearchDocumentKit.Modeling;
using CloudyWing.ElasticsearchDocumentKit.Querying;
using CloudyWing.ElasticsearchDocumentKit.Search;
using Elastic.Clients.Elasticsearch;

namespace CloudyWing.ElasticsearchDocumentKit;

/// <summary>
/// 提供常見業務文件寫入、更新、刪除、讀取、bulk 寫入與 search-after 分頁操作，
/// 單筆寫入可透過選項設定 refresh、樂觀並行控制與更新衝突重試。
/// </summary>
/// <typeparam name="TDocument">文件型別。</typeparam>
public sealed class ElasticsearchDocumentStore<TDocument> : IElasticsearchDocumentStore<TDocument>
    where TDocument : class {
    private readonly IElasticsearchGateway gateway;
    private readonly IElasticsearchDocumentModelResolver documentModelResolver;

    /// <summary>
    /// 初始化 <see cref="ElasticsearchDocumentStore{TDocument}"/> 類別的新執行個體。
    /// </summary>
    /// <param name="client">官方 Elasticsearch client。</param>
    /// <param name="documentModelResolver">文件模型解析器。</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="client"/> 或 <paramref name="documentModelResolver"/> 為 <see langword="null"/>。
    /// </exception>
    public ElasticsearchDocumentStore(
        ElasticsearchClient client,
        IElasticsearchDocumentModelResolver documentModelResolver
    )
        : this(new ElasticsearchGateway(client), documentModelResolver) {
    }

    internal ElasticsearchDocumentStore(
        IElasticsearchGateway gateway,
        IElasticsearchDocumentModelResolver documentModelResolver
    ) {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(documentModelResolver);

        this.gateway = gateway;
        this.documentModelResolver = documentModelResolver;
    }

    /// <summary>
    /// 建立或覆寫單筆文件。
    /// </summary>
    /// <param name="document">文件內容。</param>
    /// <param name="documentId">文件 ID；未設定時使用建構式提供的選取器或由 Elasticsearch 自動產生。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>文件操作結果。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> 為 <see langword="null"/>。</exception>
    public Task<DocumentOperationResult> IndexAsync(
        TDocument document,
        string? documentId = null,
        CancellationToken cancellationToken = default
    ) {
        return IndexCoreAsync(document, documentId, null, cancellationToken);
    }

    /// <summary>
    /// 依指定選項建立或覆寫單筆文件。
    /// </summary>
    /// <param name="document">文件內容。</param>
    /// <param name="documentId">文件 ID；未設定時使用建構式提供的選取器或由 Elasticsearch 自動產生。</param>
    /// <param name="options">單筆寫入選項，可設定 refresh 與樂觀並行控制。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>文件操作結果。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="document"/> 或 <paramref name="options"/> 為 <see langword="null"/>。
    /// </exception>
    /// <exception cref="InvalidOperationException"><paramref name="options"/> 的設定組合不合法。</exception>
    public async Task<DocumentOperationResult> IndexAsync(
        TDocument document,
        string? documentId,
        IndexDocumentOptions options,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        return await IndexCoreAsync(document, documentId, options, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DocumentOperationResult> IndexCoreAsync(
        TDocument document,
        string? documentId,
        IndexDocumentOptions? options,
        CancellationToken cancellationToken
    ) {
        ArgumentNullException.ThrowIfNull(document);

        ElasticsearchDocumentModel<TDocument> model = documentModelResolver.Resolve<TDocument>();
        string indexName = model.IndexName;
        string? resolvedDocumentId = ResolveDocumentId(model, document, documentId);
        using Activity? activity = StartActivity("index", indexName, 1);
        IndexResponse response = await gateway.IndexAsync(
            indexName,
            document,
            resolvedDocumentId,
            options,
            cancellationToken
        ).ConfigureAwait(false);
        DocumentOperationResult result = ElasticsearchResultMapper.FromIndexResponse(response);

        SetResultTags(activity, result.IsSuccessful, result.ErrorType);

        return result;
    }

    private static string? ResolveDocumentId(
        ElasticsearchDocumentModel<TDocument> model,
        TDocument document,
        string? documentId
    ) {
        return string.IsNullOrWhiteSpace(documentId)
            ? model.IdSelector?.Invoke(document)
            : documentId;
    }

    /// <summary>
    /// 建立單筆文件，文件 ID 已存在時不覆寫。
    /// </summary>
    /// <param name="document">文件內容。</param>
    /// <param name="documentId">文件 ID；未設定時使用文件模型的 IdSelector，兩者皆無值時由 Elasticsearch 自動產生。</param>
    /// <param name="options">單筆寫入選項，可設定 refresh。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>文件操作結果。文件 ID 已存在時 <see cref="DocumentOperationResult.IsSuccessful"/> 為 <see langword="false"/>，狀態碼為 409。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> 為 <see langword="null"/>。</exception>
    public async Task<DocumentOperationResult> CreateAsync(
        TDocument document,
        string? documentId = null,
        DocumentWriteOptions? options = null,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(document);

        ElasticsearchDocumentModel<TDocument> model = documentModelResolver.Resolve<TDocument>();
        string indexName = model.IndexName;
        string? resolvedDocumentId = ResolveDocumentId(model, document, documentId);
        using Activity? activity = StartActivity("create", indexName, 1);
        IndexResponse response = await gateway.CreateAsync(
            indexName,
            document,
            resolvedDocumentId,
            options,
            cancellationToken
        ).ConfigureAwait(false);
        DocumentOperationResult result = ElasticsearchResultMapper.FromIndexResponse(response, "create");

        SetResultTags(activity, result.IsSuccessful, result.ErrorType);

        return result;
    }

    /// <summary>
    /// 刪除單筆文件。
    /// </summary>
    /// <param name="documentId">文件 ID。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>文件操作結果。</returns>
    /// <exception cref="ArgumentException"><paramref name="documentId"/> 為空白字串。</exception>
    public Task<DocumentOperationResult> DeleteAsync(
        string documentId,
        CancellationToken cancellationToken = default
    ) {
        return DeleteCoreAsync(documentId, null, cancellationToken);
    }

    /// <summary>
    /// 依指定選項刪除單筆文件。
    /// </summary>
    /// <param name="documentId">文件 ID。</param>
    /// <param name="options">單筆寫入選項，可設定 refresh。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>文件操作結果。</returns>
    /// <exception cref="ArgumentException"><paramref name="documentId"/> 為空白字串。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> 為 <see langword="null"/>。</exception>
    public async Task<DocumentOperationResult> DeleteAsync(
        string documentId,
        DocumentWriteOptions options,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(options);

        return await DeleteCoreAsync(documentId, options, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DocumentOperationResult> DeleteCoreAsync(
        string documentId,
        DocumentWriteOptions? options,
        CancellationToken cancellationToken
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        string indexName = documentModelResolver.Resolve<TDocument>().IndexName;
        using Activity? activity = StartActivity("delete", indexName, 1);
        DeleteResponse response = await gateway.DeleteAsync<TDocument>(
            indexName,
            documentId,
            options,
            cancellationToken
        ).ConfigureAwait(false);
        DocumentOperationResult result = ElasticsearchResultMapper.FromDeleteResponse(response);

        SetResultTags(activity, result.IsSuccessful, result.ErrorType);

        return result;
    }

    /// <summary>
    /// 讀取單筆文件。
    /// </summary>
    /// <param name="documentId">文件 ID。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>文件讀取結果。</returns>
    /// <exception cref="ArgumentException"><paramref name="documentId"/> 為空白字串。</exception>
    public async Task<DocumentGetResult<TDocument>> GetAsync(
        string documentId,
        CancellationToken cancellationToken = default
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        string indexName = documentModelResolver.Resolve<TDocument>().IndexName;
        using Activity? activity = StartActivity("get", indexName, 1);
        GetResponse<TDocument> response = await gateway.GetAsync<TDocument>(
            indexName,
            documentId,
            cancellationToken
        ).ConfigureAwait(false);
        DocumentGetResult<TDocument> result = ElasticsearchResultMapper.FromGetResponse(response);

        SetResultTags(activity, result.IsSuccessful, result.ErrorType);

        return result;
    }

    /// <summary>
    /// 使用 partial document 更新單筆文件。
    /// </summary>
    /// <typeparam name="TPartialDocument">partial document 型別。</typeparam>
    /// <param name="documentId">文件 ID。</param>
    /// <param name="partialDocument">partial document。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>文件操作結果。</returns>
    /// <exception cref="ArgumentException"><paramref name="documentId"/> 為空白字串。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="partialDocument"/> 為 <see langword="null"/>。</exception>
    public Task<DocumentOperationResult> UpdatePartialAsync<TPartialDocument>(
        string documentId,
        TPartialDocument partialDocument,
        CancellationToken cancellationToken = default
    )
        where TPartialDocument : class {
        return UpdatePartialCoreAsync(documentId, partialDocument, null, cancellationToken);
    }

    /// <summary>
    /// 依指定選項使用 partial document 更新單筆文件。
    /// </summary>
    /// <typeparam name="TPartialDocument">partial document 型別。</typeparam>
    /// <param name="documentId">文件 ID。</param>
    /// <param name="partialDocument">partial document。</param>
    /// <param name="options">單筆更新選項，可設定 refresh、樂觀並行控制與衝突重試次數。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>文件操作結果。</returns>
    /// <exception cref="ArgumentException"><paramref name="documentId"/> 為空白字串。</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="partialDocument"/> 或 <paramref name="options"/> 為 <see langword="null"/>。
    /// </exception>
    /// <exception cref="InvalidOperationException"><paramref name="options"/> 的設定組合不合法。</exception>
    public async Task<DocumentOperationResult> UpdatePartialAsync<TPartialDocument>(
        string documentId,
        TPartialDocument partialDocument,
        UpdateDocumentOptions options,
        CancellationToken cancellationToken = default
    )
        where TPartialDocument : class {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        return await UpdatePartialCoreAsync(documentId, partialDocument, options, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<DocumentOperationResult> UpdatePartialCoreAsync<TPartialDocument>(
        string documentId,
        TPartialDocument partialDocument,
        UpdateDocumentOptions? options,
        CancellationToken cancellationToken
    )
        where TPartialDocument : class {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentNullException.ThrowIfNull(partialDocument);

        string indexName = documentModelResolver.Resolve<TDocument>().IndexName;
        using Activity? activity = StartActivity("update", indexName, 1);
        UpdateResponse<TDocument> response = await gateway.UpdateAsync<TDocument, TPartialDocument>(
            indexName,
            documentId,
            partialDocument,
            options,
            cancellationToken
        ).ConfigureAwait(false);
        DocumentOperationResult result = ElasticsearchResultMapper.FromUpdateResponse(response);

        SetResultTags(activity, result.IsSuccessful, result.ErrorType);

        return result;
    }

    /// <summary>
    /// 使用 script 更新單筆文件。
    /// </summary>
    /// <param name="documentId">文件 ID。</param>
    /// <param name="script">要執行的 script，包含 source、lang 與 params 或已儲存的 script ID。</param>
    /// <param name="options">單筆寫入選項，可設定 refresh。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>文件操作結果。文件不存在時 <see cref="DocumentOperationResult.IsSuccessful"/> 為 <see langword="false"/>，狀態碼為 404。</returns>
    /// <exception cref="ArgumentException"><paramref name="documentId"/> 為空白字串。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="script"/> 為 <see langword="null"/>。</exception>
    public async Task<DocumentOperationResult> UpdateByScriptAsync(
        string documentId,
        Script script,
        DocumentWriteOptions? options = null,
        CancellationToken cancellationToken = default
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentNullException.ThrowIfNull(script);

        string indexName = documentModelResolver.Resolve<TDocument>().IndexName;
        using Activity? activity = StartActivity("update_by_script", indexName, 1);
        UpdateResponse<TDocument> response = await gateway.UpdateByScriptAsync<TDocument>(
            indexName,
            documentId,
            script,
            options,
            cancellationToken
        ).ConfigureAwait(false);
        DocumentOperationResult result = ElasticsearchResultMapper.FromUpdateResponse(response, "update_by_script");

        SetResultTags(activity, result.IsSuccessful, result.ErrorType);

        return result;
    }

    /// <summary>
    /// 依條件更新符合的文件。
    /// </summary>
    /// <param name="request">update-by-query 設定。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>
    /// 操作結果。預設以 <c>conflicts=abort</c> 執行，遇到版本衝突時請求失敗；
    /// 可透過 <see cref="UpdateByQueryRequest{TDocument}.ConfigureRequest"/> 設定 <c>Conflicts.Proceed</c> 改為略過衝突並計入
    /// <see cref="ByQueryOperationResult.VersionConflicts"/>。
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> 為 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="request"/> 的 <see cref="UpdateByQueryRequest{TDocument}.Filters"/> 沒有任何條件，且
    /// <see cref="UpdateByQueryRequest{TDocument}.ConfigureRequest"/> 未指定 query。
    /// </exception>
    /// <remarks>
    /// 預設等待操作完成並 refresh。若透過 <see cref="UpdateByQueryRequest{TDocument}.ConfigureRequest"/> 覆寫為
    /// <c>wait_for_completion=false</c>，回應只含 task，結果的計數皆為 0。
    /// </remarks>
    public async Task<ByQueryOperationResult> UpdateByQueryAsync(
        UpdateByQueryRequest<TDocument> request,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(request);
        EnsureByQueryHasCondition(request.Filters, request.ConfigureRequest is not null);

        string indexName = documentModelResolver.Resolve<TDocument>().IndexName;
        using Activity? activity = StartActivity("update_by_query", indexName, 0);
        UpdateByQueryResponse response = await gateway.UpdateByQueryAsync(
            indexName,
            request,
            cancellationToken
        ).ConfigureAwait(false);
        ByQueryOperationResult result = ElasticsearchResultMapper.FromUpdateByQueryResponse(indexName, response);

        activity?.SetTag("elasticsearch.document_count", result.Updated);
        SetResultTags(activity, result.IsSuccessful, result.ErrorType);

        return result;
    }

    /// <summary>
    /// 依條件刪除符合的文件。
    /// </summary>
    /// <param name="request">delete-by-query 設定。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>
    /// 操作結果。預設以 <c>conflicts=abort</c> 執行，遇到版本衝突時請求失敗；
    /// 可透過 <see cref="DeleteByQueryRequest{TDocument}.ConfigureRequest"/> 設定 <c>Conflicts.Proceed</c> 改為略過衝突並計入
    /// <see cref="ByQueryOperationResult.VersionConflicts"/>。
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> 為 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="request"/> 的 <see cref="DeleteByQueryRequest{TDocument}.Filters"/> 沒有任何條件，且
    /// <see cref="DeleteByQueryRequest{TDocument}.ConfigureRequest"/> 未指定 query。
    /// </exception>
    /// <remarks>
    /// 預設等待操作完成並 refresh。若透過 <see cref="DeleteByQueryRequest{TDocument}.ConfigureRequest"/> 覆寫為
    /// <c>wait_for_completion=false</c>，回應只含 task，結果的計數皆為 0。
    /// </remarks>
    public async Task<ByQueryOperationResult> DeleteByQueryAsync(
        DeleteByQueryRequest<TDocument> request,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(request);
        EnsureByQueryHasCondition(request.Filters, request.ConfigureRequest is not null);

        string indexName = documentModelResolver.Resolve<TDocument>().IndexName;
        using Activity? activity = StartActivity("delete_by_query", indexName, 0);
        DeleteByQueryResponse response = await gateway.DeleteByQueryAsync(
            indexName,
            request,
            cancellationToken
        ).ConfigureAwait(false);
        ByQueryOperationResult result = ElasticsearchResultMapper.FromDeleteByQueryResponse(indexName, response);

        activity?.SetTag("elasticsearch.document_count", result.Deleted);
        SetResultTags(activity, result.IsSuccessful, result.ErrorType);

        return result;
    }

    private static void EnsureByQueryHasCondition(
        ElasticsearchQueryFilterBuilder<TDocument>? filters,
        bool hasConfigureRequest
    ) {
        if (hasConfigureRequest || FilterQueryFactory.Create(filters) is not null) {
            return;
        }

        throw new ArgumentException(
            "A by-query request must specify filters or a query through ConfigureRequest.",
            "request"
        );
    }

    /// <summary>
    /// 依條件計算符合的文件筆數。
    /// </summary>
    /// <param name="filters">篩選條件；沒有任何條件時計算 index 內的全部文件。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>文件筆數查詢結果。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filters"/> 為 <see langword="null"/>。</exception>
    public async Task<DocumentCountResult> CountAsync(
        ElasticsearchQueryFilterBuilder<TDocument> filters,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(filters);

        string indexName = documentModelResolver.Resolve<TDocument>().IndexName;
        using Activity? activity = StartActivity("count", indexName, 0);
        CountResponse response = await gateway.CountAsync(
            indexName,
            filters,
            cancellationToken
        ).ConfigureAwait(false);
        DocumentCountResult result = ElasticsearchResultMapper.FromCountResponse(indexName, response);

        activity?.SetTag("elasticsearch.document_count", result.Count);
        SetResultTags(activity, result.IsSuccessful, result.ErrorType);

        return result;
    }

    /// <summary>
    /// bulk 建立或覆寫多筆文件。
    /// </summary>
    /// <param name="documents">文件集合。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>bulk 操作結果。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="documents"/> 為 <see langword="null"/>。</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> 已取消時。</exception>
    public Task<BulkOperationResult> BulkIndexAsync(
        IEnumerable<TDocument> documents,
        CancellationToken cancellationToken = default
    ) {
        return BulkIndexAsync(documents, new BulkIndexOptions<TDocument>(), cancellationToken);
    }

    /// <summary>
    /// bulk 建立或覆寫非同步產生的多筆文件。
    /// </summary>
    /// <param name="documents">非同步文件集合。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>bulk 操作結果。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="documents"/> 為 <see langword="null"/>。</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> 已取消時。</exception>
    public Task<BulkOperationResult> BulkIndexAsync(
        IAsyncEnumerable<TDocument> documents,
        CancellationToken cancellationToken = default
    ) {
        return BulkIndexAsync(documents, new BulkIndexOptions<TDocument>(), cancellationToken);
    }

    /// <summary>
    /// 依指定設定 bulk 建立或覆寫多筆文件。
    /// </summary>
    /// <param name="documents">文件集合。</param>
    /// <param name="options">bulk 寫入設定。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>bulk 操作結果。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="documents"/> 或 <paramref name="options"/> 為 <see langword="null"/>。
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="options"/> 的設定不符合需求，或 <see cref="BulkIndexOptions{TDocument}.MaxRetryAttempts"/> 大於 0 而文件模型未設定 IdSelector 且
    /// <see cref="BulkIndexOptions{TDocument}.AllowRetryWithoutIdSelector"/> 為 <see langword="false"/> 時。
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> 已取消時。</exception>
    public async Task<BulkOperationResult> BulkIndexAsync(
        IEnumerable<TDocument> documents,
        BulkIndexOptions<TDocument> options,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        ElasticsearchDocumentModel<TDocument> model = documentModelResolver.Resolve<TDocument>();
        options.ValidateDocumentIdSelector(model.IdSelector);
        string indexName = model.IndexName;
        using Activity? activity = StartActivity("bulk_index", indexName, 0);
        List<BulkOperationResult> chunkResults = [];
        int chunkIndex = 0;
        int itemOffset = 0;
        int requestedCount = 0;
        List<TDocument> chunkDocuments = new(options.ChunkSize);

        foreach (TDocument document in documents) {
            cancellationToken.ThrowIfCancellationRequested();
            chunkDocuments.Add(document);

            if (chunkDocuments.Count < options.ChunkSize) {
                continue;
            }

            await ExecuteBulkChunkAndCollectAsync(
                indexName,
                chunkDocuments,
                chunkResults,
                chunkIndex,
                itemOffset,
                model.IdSelector,
                options,
                cancellationToken
            ).ConfigureAwait(false);

            requestedCount += chunkDocuments.Count;
            itemOffset += chunkDocuments.Count;
            chunkIndex++;
            chunkDocuments = new List<TDocument>(options.ChunkSize);
        }

        if (chunkDocuments.Count > 0) {
            await ExecuteBulkChunkAndCollectAsync(
                indexName,
                chunkDocuments,
                chunkResults,
                chunkIndex,
                itemOffset,
                model.IdSelector,
                options,
                cancellationToken
            ).ConfigureAwait(false);

            requestedCount += chunkDocuments.Count;
        }

        return CompleteBulkResult(activity, chunkResults, requestedCount);
    }

    /// <summary>
    /// 依指定設定 bulk 建立或覆寫非同步產生的多筆文件。
    /// </summary>
    /// <param name="documents">非同步文件集合。</param>
    /// <param name="options">bulk 寫入設定。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>bulk 操作結果。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="documents"/> 或 <paramref name="options"/> 為 <see langword="null"/>。
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="options"/> 的設定不符合需求，或 <see cref="BulkIndexOptions{TDocument}.MaxRetryAttempts"/> 大於 0 而文件模型未設定 IdSelector 且
    /// <see cref="BulkIndexOptions{TDocument}.AllowRetryWithoutIdSelector"/> 為 <see langword="false"/> 時。
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> 已取消時。</exception>
    public async Task<BulkOperationResult> BulkIndexAsync(
        IAsyncEnumerable<TDocument> documents,
        BulkIndexOptions<TDocument> options,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        ElasticsearchDocumentModel<TDocument> model = documentModelResolver.Resolve<TDocument>();
        options.ValidateDocumentIdSelector(model.IdSelector);
        string indexName = model.IndexName;
        using Activity? activity = StartActivity("bulk_index", indexName, 0);
        List<BulkOperationResult> chunkResults = [];
        int chunkIndex = 0;
        int itemOffset = 0;
        int requestedCount = 0;
        List<TDocument> chunkDocuments = new(options.ChunkSize);

        await foreach (TDocument document in documents.WithCancellation(cancellationToken).ConfigureAwait(false)) {
            chunkDocuments.Add(document);

            if (chunkDocuments.Count < options.ChunkSize) {
                continue;
            }

            await ExecuteBulkChunkAndCollectAsync(
                indexName,
                chunkDocuments,
                chunkResults,
                chunkIndex,
                itemOffset,
                model.IdSelector,
                options,
                cancellationToken
            ).ConfigureAwait(false);

            requestedCount += chunkDocuments.Count;
            itemOffset += chunkDocuments.Count;
            chunkIndex++;
            chunkDocuments = new List<TDocument>(options.ChunkSize);
        }

        if (chunkDocuments.Count > 0) {
            await ExecuteBulkChunkAndCollectAsync(
                indexName,
                chunkDocuments,
                chunkResults,
                chunkIndex,
                itemOffset,
                model.IdSelector,
                options,
                cancellationToken
            ).ConfigureAwait(false);

            requestedCount += chunkDocuments.Count;
        }

        return CompleteBulkResult(activity, chunkResults, requestedCount);
    }

    private async Task ExecuteBulkChunkAndCollectAsync(
        string indexName,
        IReadOnlyList<TDocument> chunkDocuments,
        List<BulkOperationResult> chunkResults,
        int chunkIndex,
        int itemOffset,
        Func<TDocument, string?>? documentIdSelector,
        BulkIndexOptions<TDocument> options,
        CancellationToken cancellationToken
    ) {
        BulkChunkExecutionResult chunkExecutionResult = await ExecuteBulkChunkWithRetryAsync(
            indexName,
            chunkDocuments,
            documentIdSelector,
            options,
            cancellationToken
        ).ConfigureAwait(false);
        await ElasticsearchDocumentStore<TDocument>.PublishDeadLettersAsync(
            indexName,
            chunkDocuments,
            chunkExecutionResult.Result,
            chunkIndex,
            chunkExecutionResult.Attempt,
            options,
            cancellationToken
        ).ConfigureAwait(false);

        chunkResults.Add(OffsetFailures(chunkExecutionResult.Result, itemOffset));
    }

    private static BulkOperationResult CompleteBulkResult(
        Activity? activity,
        List<BulkOperationResult> chunkResults,
        int requestedCount
    ) {
        activity?.SetTag("elasticsearch.document_count", requestedCount);

        if (requestedCount == 0) {
            BulkOperationResult emptyResult = CreateEmptyBulkResult();
            SetBulkResultTags(activity, emptyResult, chunkCount: 0);
            return emptyResult;
        }

        BulkOperationResult result = CombineBulkResults(chunkResults, requestedCount);
        SetBulkResultTags(activity, result, chunkResults.Count);

        return result;
    }

    /// <summary>
    /// 依條件查詢文件。
    /// </summary>
    /// <param name="request">查詢設定。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>查詢結果。未設定 <see cref="DocumentSearchRequest{TDocument}.Size"/> 時由 Elasticsearch 使用預設筆數（10 筆）。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> 為 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="DocumentSearchRequest{TDocument}.Size"/> 有設定且小於 1。
    /// </exception>
    public async Task<DocumentSearchResult<TDocument>> SearchAsync(
        DocumentSearchRequest<TDocument> request,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Size < 1) {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.Size,
                "Search size must be greater than zero."
            );
        }

        string indexName = documentModelResolver.Resolve<TDocument>().IndexName;
        using Activity? activity = StartActivity("search", indexName, 0);
        SearchResponse<TDocument> response = await gateway.SearchAsync(
            indexName,
            request,
            cancellationToken
        ).ConfigureAwait(false);
        DocumentSearchResult<TDocument> result = ElasticsearchResultMapper.FromDocumentSearchResponse(response);

        activity?.SetTag("elasticsearch.document_count", result.Documents.Count);
        SetResultTags(activity, result.IsSuccessful, result.ErrorType);

        return result;
    }

    /// <summary>
    /// 依 from/size 方式查詢指定頁的文件，並回傳符合條件的總筆數。
    /// </summary>
    /// <param name="request">分頁查詢設定。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>
    /// 分頁查詢結果。頁碼超出範圍時為成功結果、空文件集合與實際總筆數；
    /// <c>from + size</c> 超過 Elasticsearch 的 <c>index.max_result_window</c> 時為失敗結果。
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> 為 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="DocumentPageRequest{TDocument}.PageNumber"/> 或 <see cref="DocumentPageRequest{TDocument}.PageSize"/>
    /// 小於 1，或略過筆數 <c>(PageNumber - 1) * PageSize</c> 超過 <see cref="int.MaxValue"/>。
    /// </exception>
    /// <remarks>
    /// 不同頁之間不保證查詢同一份快照，查詢期間資料異動可能造成重複或遺漏。需要深度分頁時改用
    /// <see cref="SearchAfterAsync(SearchAfterRequest{TDocument}, CancellationToken)"/>。
    /// </remarks>
    public async Task<DocumentPageResult<TDocument>> SearchPageAsync(
        DocumentPageRequest<TDocument> request,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PageNumber < 1) {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.PageNumber,
                "Page number must be greater than zero."
            );
        }

        if (request.PageSize < 1) {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.PageSize,
                "Page size must be greater than zero."
            );
        }

        // 提早計算略過筆數，使溢位在送出請求之前就被拒絕
        request.GetFrom();

        string indexName = documentModelResolver.Resolve<TDocument>().IndexName;
        using Activity? activity = StartActivity("search_page", indexName, 0);
        SearchResponse<TDocument> response = await gateway.SearchPageAsync(
            indexName,
            request,
            cancellationToken
        ).ConfigureAwait(false);
        DocumentPageResult<TDocument> result = ElasticsearchResultMapper.FromDocumentPageResponse(response, request);

        activity?.SetTag("elasticsearch.document_count", result.Documents.Count);
        SetResultTags(activity, result.IsSuccessful, result.ErrorType);

        return result;
    }

    /// <summary>
    /// 依 search-after 方式查詢下一頁文件。
    /// </summary>
    /// <param name="request">search-after 查詢設定。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>search-after 分頁結果。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> 為 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="request"/> 的頁面大小小於 1。</exception>
    public async Task<SearchAfterPage<TDocument>> SearchAfterAsync(
        SearchAfterRequest<TDocument> request,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Size < 1) {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                request.Size,
                "Search-after size must be greater than zero."
            );
        }

        string indexName = documentModelResolver.Resolve<TDocument>().IndexName;
        using Activity? activity = StartActivity("search_after", indexName, request.Size);
        SearchResponse<TDocument> response = await gateway.SearchAfterAsync(
            indexName,
            request,
            cancellationToken
        ).ConfigureAwait(false);
        SearchAfterPage<TDocument> result = ElasticsearchResultMapper.FromSearchResponse(response);

        SetResultTags(activity, result.IsSuccessful, result.ErrorType);

        return result;
    }

    private async Task<BulkChunkExecutionResult> ExecuteBulkChunkWithRetryAsync(
        string indexName,
        IReadOnlyList<TDocument> documents,
        Func<TDocument, string?>? documentIdSelector,
        BulkIndexOptions<TDocument> options,
        CancellationToken cancellationToken
    ) {
        int attempt = 1;

        while (true) {
            try {
                BulkResponse response = await gateway.BulkIndexAsync(
                    indexName,
                    documents,
                    documentIdSelector,
                    cancellationToken
                ).ConfigureAwait(false);
                BulkOperationResult result = ElasticsearchResultMapper.FromBulkResponse(response, documents.Count);

                if (!response.IsValidResponse
                    && response.Items.Count == 0
                    && cancellationToken.IsCancellationRequested
                ) {
                    Exception? originalException = response.ApiCallDetails?.OriginalException;

                    throw originalException is null
                        ? new OperationCanceledException(cancellationToken)
                        : new OperationCanceledException(originalException.Message, originalException, cancellationToken);
                }

                if (response.IsValidResponse
                    || response.Items.Count > 0
                    || attempt > options.MaxRetryAttempts
                ) {
                    return new BulkChunkExecutionResult(result, attempt);
                }
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception) when (!cancellationToken.IsCancellationRequested && attempt <= options.MaxRetryAttempts) {
                await DelayBeforeRetryAsync(options, attempt, cancellationToken).ConfigureAwait(false);
                attempt++;
                continue;
            } catch (Exception ex) {
                if (cancellationToken.IsCancellationRequested) {
                    throw new OperationCanceledException(ex.Message, ex, cancellationToken);
                }

                return new BulkChunkExecutionResult(
                    CreateExceptionBulkResult(indexName, documents, documentIdSelector, ex),
                    attempt
                );
            }

            await DelayBeforeRetryAsync(options, attempt, cancellationToken).ConfigureAwait(false);
            attempt++;
        }
    }

    private static Task DelayBeforeRetryAsync(
        BulkIndexOptions<TDocument> options,
        int attempt,
        CancellationToken cancellationToken
    ) {
        TimeSpan delay = options.GetRetryDelay(attempt);

        if (delay <= TimeSpan.Zero) {
            return Task.CompletedTask;
        }

        return Task.Delay(delay, cancellationToken);
    }

    private static async Task PublishDeadLettersAsync(
        string indexName,
        IReadOnlyList<TDocument> documents,
        BulkOperationResult result,
        int chunkIndex,
        int attempt,
        BulkIndexOptions<TDocument> options,
        CancellationToken cancellationToken
    ) {
        if (options.DeadLetterCallback is null || result.Failures.Count == 0) {
            return;
        }

        foreach (BulkOperationFailure failure in result.Failures) {
            if (failure.ItemIndex < 0 || failure.ItemIndex >= documents.Count) {
                continue;
            }

            BulkDeadLetter<TDocument> deadLetter = new() {
                IndexName = indexName,
                Document = documents[failure.ItemIndex],
                Failure = failure,
                ChunkIndex = chunkIndex,
                Attempt = attempt
            };
            await options.DeadLetterCallback(deadLetter, cancellationToken).ConfigureAwait(false);
        }
    }

    private static BulkOperationResult CreateExceptionBulkResult(
        string indexName,
        IReadOnlyList<TDocument> documents,
        Func<TDocument, string?>? documentIdSelector,
        Exception exception
    ) {
        List<BulkOperationFailure> failures = documents
            .Select((document, index) => new BulkOperationFailure {
                ItemIndex = index,
                Operation = "index",
                IndexName = indexName,
                DocumentId = documentIdSelector?.Invoke(document),
                StatusCode = 0,
                ErrorType = exception.GetType().Name,
                ErrorReason = exception.Message
            })
            .ToList();

        return new BulkOperationResult {
            IsSuccessful = false,
            HasErrors = true,
            TotalCount = documents.Count,
            SucceededCount = 0,
            FailedCount = documents.Count,
            Failures = failures,
            ErrorType = exception.GetType().Name,
            ErrorReason = exception.Message,
            ResponseSummary = exception.ToString()
        };
    }

    private static BulkOperationResult OffsetFailures(BulkOperationResult result, int itemOffset) {
        if (itemOffset == 0 || result.Failures.Count == 0) {
            return result;
        }

        IReadOnlyList<BulkOperationFailure> failures = result.Failures
            .Select(failure => new BulkOperationFailure {
                ItemIndex = failure.ItemIndex + itemOffset,
                Operation = failure.Operation,
                IndexName = failure.IndexName,
                DocumentId = failure.DocumentId,
                StatusCode = failure.StatusCode,
                ErrorType = failure.ErrorType,
                ErrorReason = failure.ErrorReason
            })
            .ToList();

        return new BulkOperationResult {
            IsSuccessful = result.IsSuccessful,
            HasErrors = result.HasErrors,
            TotalCount = result.TotalCount,
            SucceededCount = result.SucceededCount,
            FailedCount = result.FailedCount,
            Failures = failures,
            ErrorType = result.ErrorType,
            ErrorReason = result.ErrorReason,
            ResponseSummary = result.ResponseSummary
        };
    }

    private static BulkOperationResult CombineBulkResults(
        IReadOnlyList<BulkOperationResult> results,
        int requestedCount
    ) {
        List<BulkOperationFailure> failures = results.SelectMany(result => result.Failures).ToList();
        string? errorType = results.FirstOrDefault(result => result.ErrorType is not null)?.ErrorType;
        string? errorReason = results.FirstOrDefault(result => result.ErrorReason is not null)?.ErrorReason;
        string responseSummary = string.Join(
            Environment.NewLine,
            results
                .Select(result => result.ResponseSummary)
                .Where(summary => !string.IsNullOrWhiteSpace(summary))
        );

        return new BulkOperationResult {
            IsSuccessful = results.All(result => result.IsSuccessful),
            HasErrors = results.Any(result => result.HasErrors),
            TotalCount = requestedCount,
            SucceededCount = results.Sum(result => result.SucceededCount),
            FailedCount = results.Sum(result => result.FailedCount),
            Failures = failures,
            ErrorType = errorType,
            ErrorReason = errorReason,
            ResponseSummary = string.IsNullOrWhiteSpace(responseSummary) ? null : responseSummary
        };
    }

    private static BulkOperationResult CreateEmptyBulkResult() {
        return new BulkOperationResult {
            IsSuccessful = true,
            HasErrors = false,
            TotalCount = 0,
            SucceededCount = 0,
            FailedCount = 0,
            Failures = []
        };
    }

    private static Activity? StartActivity(string operation, string indexName, int documentCount) {
        Activity? activity = ElasticsearchDocumentsTelemetry.ActivitySource.StartActivity(
            $"elasticsearch.{operation}",
            ActivityKind.Client
        );
        activity?.SetTag("db.system", "elasticsearch");
        activity?.SetTag("db.operation", operation);
        activity?.SetTag("elasticsearch.index", indexName);
        activity?.SetTag("elasticsearch.document_count", documentCount);

        return activity;
    }

    private static void SetResultTags(Activity? activity, bool isSuccessful, string? errorType) {
        activity?.SetTag("elasticsearch.success", isSuccessful);

        if (!string.IsNullOrWhiteSpace(errorType)) {
            activity?.SetTag("error.type", errorType);
        }
    }

    private static void SetBulkResultTags(Activity? activity, BulkOperationResult result, int chunkCount) {
        SetResultTags(activity, result.IsSuccessful, result.ErrorType);
        activity?.SetTag("elasticsearch.chunk_count", chunkCount);
        activity?.SetTag("elasticsearch.succeeded_count", result.SucceededCount);
        activity?.SetTag("elasticsearch.failed_count", result.FailedCount);
    }

    private sealed class BulkChunkExecutionResult {
        public BulkChunkExecutionResult(BulkOperationResult result, int attempt) {
            Result = result;
            Attempt = attempt;
        }

        public BulkOperationResult Result { get; }

        public int Attempt { get; }
    }
}
