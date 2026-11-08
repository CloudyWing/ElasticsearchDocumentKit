using CloudyWing.ElasticsearchDocumentKit.Querying;
using CloudyWing.ElasticsearchDocumentKit.Search;
using Elastic.Clients.Elasticsearch;

namespace CloudyWing.ElasticsearchDocumentKit;

/// <summary>
/// 提供指定文件型別的常見 Elasticsearch 文件操作。
/// </summary>
/// <typeparam name="TDocument">文件型別。</typeparam>
public interface IElasticsearchDocumentStore<TDocument>
    where TDocument : class {
    /// <summary>
    /// 建立或覆寫單筆文件。
    /// </summary>
    /// <param name="document">文件內容。</param>
    /// <param name="documentId">文件 ID。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>文件操作結果。</returns>
    Task<DocumentOperationResult> IndexAsync(
        TDocument document,
        string? documentId = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 依指定選項建立或覆寫單筆文件。
    /// </summary>
    /// <param name="document">文件內容。</param>
    /// <param name="documentId">文件 ID。</param>
    /// <param name="options">單筆寫入選項，可設定 refresh 與樂觀並行控制。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>文件操作結果。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="document"/> 或 <paramref name="options"/> 為 <see langword="null"/>。
    /// </exception>
    /// <exception cref="InvalidOperationException"><paramref name="options"/> 的設定組合不合法。</exception>
    Task<DocumentOperationResult> IndexAsync(
        TDocument document,
        string? documentId,
        IndexDocumentOptions options,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 建立單筆文件，文件 ID 已存在時不覆寫。
    /// </summary>
    /// <param name="document">文件內容。</param>
    /// <param name="documentId">文件 ID；未設定時使用文件模型的 IdSelector，兩者皆無值時由 Elasticsearch 自動產生。</param>
    /// <param name="options">單筆寫入選項，可設定 refresh。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>文件操作結果。文件 ID 已存在時 <see cref="DocumentOperationResult.IsSuccessful"/> 為 <see langword="false"/>，狀態碼為 409。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> 為 <see langword="null"/>。</exception>
    Task<DocumentOperationResult> CreateAsync(
        TDocument document,
        string? documentId = null,
        DocumentWriteOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 刪除單筆文件。
    /// </summary>
    /// <param name="documentId">文件 ID。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>文件操作結果。</returns>
    Task<DocumentOperationResult> DeleteAsync(
        string documentId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 依指定選項刪除單筆文件。
    /// </summary>
    /// <param name="documentId">文件 ID。</param>
    /// <param name="options">單筆寫入選項，可設定 refresh。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>文件操作結果。</returns>
    /// <exception cref="ArgumentException"><paramref name="documentId"/> 為空白字串。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> 為 <see langword="null"/>。</exception>
    Task<DocumentOperationResult> DeleteAsync(
        string documentId,
        DocumentWriteOptions options,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 讀取單筆文件。
    /// </summary>
    /// <param name="documentId">文件 ID。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>文件讀取結果。</returns>
    Task<DocumentGetResult<TDocument>> GetAsync(
        string documentId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 使用 partial document 更新單筆文件。
    /// </summary>
    /// <typeparam name="TPartialDocument">partial document 型別。</typeparam>
    /// <param name="documentId">文件 ID。</param>
    /// <param name="partialDocument">partial document。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>文件操作結果。</returns>
    Task<DocumentOperationResult> UpdatePartialAsync<TPartialDocument>(
        string documentId,
        TPartialDocument partialDocument,
        CancellationToken cancellationToken = default
    ) where TPartialDocument : class;

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
    Task<DocumentOperationResult> UpdatePartialAsync<TPartialDocument>(
        string documentId,
        TPartialDocument partialDocument,
        UpdateDocumentOptions options,
        CancellationToken cancellationToken = default
    ) where TPartialDocument : class;

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
    Task<DocumentOperationResult> UpdateByScriptAsync(
        string documentId,
        Script script,
        DocumentWriteOptions? options = null,
        CancellationToken cancellationToken = default
    );

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
    Task<ByQueryOperationResult> UpdateByQueryAsync(
        UpdateByQueryRequest<TDocument> request,
        CancellationToken cancellationToken = default
    );

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
    Task<ByQueryOperationResult> DeleteByQueryAsync(
        DeleteByQueryRequest<TDocument> request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 依條件計算符合的文件筆數。
    /// </summary>
    /// <param name="filters">篩選條件；沒有任何條件時計算 index 內的全部文件。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>文件筆數查詢結果。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filters"/> 為 <see langword="null"/>。</exception>
    Task<DocumentCountResult> CountAsync(
        ElasticsearchQueryFilterBuilder<TDocument> filters,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Bulk 建立或覆寫多筆文件。
    /// </summary>
    /// <param name="documents">文件集合。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>Bulk 操作結果。</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> 已取消時。</exception>
    Task<BulkOperationResult> BulkIndexAsync(
        IEnumerable<TDocument> documents,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Bulk 建立或覆寫非同步產生的多筆文件。
    /// </summary>
    /// <param name="documents">非同步文件集合。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>Bulk 操作結果。</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> 已取消時。</exception>
    Task<BulkOperationResult> BulkIndexAsync(
        IAsyncEnumerable<TDocument> documents,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 依指定設定 Bulk 建立或覆寫多筆文件。
    /// </summary>
    /// <param name="documents">文件集合。</param>
    /// <param name="options">Bulk 寫入設定。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>Bulk 操作結果。</returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="options"/> 的設定不符合需求，或 <see cref="BulkIndexOptions{TDocument}.MaxRetryAttempts"/> 大於 0 而文件模型未設定 IdSelector 且
    /// <see cref="BulkIndexOptions{TDocument}.AllowRetryWithoutIdSelector"/> 為 <see langword="false"/> 時。
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> 已取消時。</exception>
    Task<BulkOperationResult> BulkIndexAsync(
        IEnumerable<TDocument> documents,
        BulkIndexOptions<TDocument> options,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 依指定設定 Bulk 建立或覆寫非同步產生的多筆文件。
    /// </summary>
    /// <param name="documents">非同步文件集合。</param>
    /// <param name="options">Bulk 寫入設定。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>Bulk 操作結果。</returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="options"/> 的設定不符合需求，或 <see cref="BulkIndexOptions{TDocument}.MaxRetryAttempts"/> 大於 0 而文件模型未設定 IdSelector 且
    /// <see cref="BulkIndexOptions{TDocument}.AllowRetryWithoutIdSelector"/> 為 <see langword="false"/> 時。
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> 已取消時。</exception>
    Task<BulkOperationResult> BulkIndexAsync(
        IAsyncEnumerable<TDocument> documents,
        BulkIndexOptions<TDocument> options,
        CancellationToken cancellationToken = default
    );

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
    Task<DocumentSearchResult<TDocument>> SearchAsync(
        DocumentSearchRequest<TDocument> request,
        CancellationToken cancellationToken = default
    );

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
    Task<DocumentPageResult<TDocument>> SearchPageAsync(
        DocumentPageRequest<TDocument> request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 依 Search-After 方式查詢下一頁文件。
    /// </summary>
    /// <param name="request">Search-After 查詢設定。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>Search-After 分頁結果。</returns>
    Task<SearchAfterPage<TDocument>> SearchAfterAsync(
        SearchAfterRequest<TDocument> request,
        CancellationToken cancellationToken = default
    );
}
