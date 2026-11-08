namespace CloudyWing.ElasticsearchDocumentKit.Search;

/// <summary>
/// 表示 from/size 分頁查詢的結果。
/// </summary>
/// <typeparam name="TDocument">文件型別。</typeparam>
public sealed class DocumentPageResult<TDocument>
    where TDocument : class {
    /// <summary>
    /// 取得查詢是否成功。
    /// </summary>
    public required bool IsSuccessful { get; init; }

    /// <summary>
    /// 取得目前頁面的文件集合。
    /// </summary>
    /// <value>查詢失敗或頁碼超出範圍時為空集合。</value>
    public IReadOnlyList<TDocument> Documents { get; init; } = [];

    /// <summary>
    /// 取得符合查詢條件的總筆數。
    /// </summary>
    /// <value>
    /// 請求的 <see cref="DocumentPageRequest{TDocument}.TrackTotalHits"/> 為 <see langword="false"/> 或查詢失敗時為
    /// <see langword="null"/>，表示未取得總筆數。
    /// </value>
    public long? TotalCount { get; init; }

    /// <summary>
    /// 取得請求的頁碼。
    /// </summary>
    public int PageNumber { get; init; }

    /// <summary>
    /// 取得請求的單頁筆數。
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// 取得錯誤類型。
    /// </summary>
    public string? ErrorType { get; init; }

    /// <summary>
    /// 取得錯誤原因。
    /// </summary>
    public string? ErrorReason { get; init; }

    /// <summary>
    /// 取得原始回應摘要。
    /// </summary>
    public string? ResponseSummary { get; init; }
}
