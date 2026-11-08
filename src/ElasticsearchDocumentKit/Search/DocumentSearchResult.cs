namespace CloudyWing.ElasticsearchDocumentKit.Search;

/// <summary>
/// 表示一般查詢的結果。
/// </summary>
/// <typeparam name="TDocument">文件型別。</typeparam>
public sealed class DocumentSearchResult<TDocument>
    where TDocument : class {
    /// <summary>
    /// 取得查詢是否成功。
    /// </summary>
    public required bool IsSuccessful { get; init; }

    /// <summary>
    /// 取得符合條件的文件集合。
    /// </summary>
    /// <value>查詢失敗或沒有符合的文件時為空集合。</value>
    public IReadOnlyList<TDocument> Documents { get; init; } = [];

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
