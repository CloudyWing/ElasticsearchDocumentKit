namespace CloudyWing.ElasticsearchDocumentKit;

/// <summary>
/// 表示單筆文件寫入或刪除操作結果。
/// </summary>
public sealed class DocumentOperationResult {
    /// <summary>
    /// 取得操作是否成功。
    /// </summary>
    public required bool IsSuccessful { get; init; }

    /// <summary>
    /// 取得操作名稱。
    /// </summary>
    public required string Operation { get; init; }

    /// <summary>
    /// 取得 index 名稱。
    /// </summary>
    public string? IndexName { get; init; }

    /// <summary>
    /// 取得文件 ID。
    /// </summary>
    public string? DocumentId { get; init; }

    /// <summary>
    /// 取得文件版本。
    /// </summary>
    public long? Version { get; init; }

    /// <summary>
    /// 取得寫入後文件的 sequence number，可搭配 <see cref="PrimaryTerm"/> 供下一次寫入做樂觀並行控制。
    /// </summary>
    public long? SeqNo { get; init; }

    /// <summary>
    /// 取得寫入後文件的 primary term，可搭配 <see cref="SeqNo"/> 供下一次寫入做樂觀並行控制。
    /// </summary>
    public long? PrimaryTerm { get; init; }

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
