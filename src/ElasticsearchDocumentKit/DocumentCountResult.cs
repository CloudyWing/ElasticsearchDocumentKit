namespace CloudyWing.ElasticsearchDocumentKit;

/// <summary>
/// 表示文件筆數查詢結果。
/// </summary>
public sealed class DocumentCountResult {
    /// <summary>
    /// 取得操作是否成功。
    /// </summary>
    public required bool IsSuccessful { get; init; }

    /// <summary>
    /// 取得 index 名稱。
    /// </summary>
    public string? IndexName { get; init; }

    /// <summary>
    /// 取得符合條件的文件筆數。
    /// </summary>
    /// <value>操作失敗時為 0。</value>
    public long Count { get; init; }

    /// <summary>
    /// 取得 HTTP 狀態碼。
    /// </summary>
    public int? StatusCode { get; init; }

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
