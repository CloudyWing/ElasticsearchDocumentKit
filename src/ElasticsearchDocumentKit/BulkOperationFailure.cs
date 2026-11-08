namespace CloudyWing.ElasticsearchDocumentKit;

/// <summary>
/// 表示 bulk 操作中的單筆失敗摘要。
/// </summary>
public sealed class BulkOperationFailure {
    /// <summary>
    /// 取得 bulk item 順序。
    /// </summary>
    public required int ItemIndex { get; init; }

    /// <summary>
    /// 取得操作名稱。
    /// </summary>
    public string? Operation { get; init; }

    /// <summary>
    /// 取得 index 名稱。
    /// </summary>
    public string? IndexName { get; init; }

    /// <summary>
    /// 取得文件 ID。
    /// </summary>
    public string? DocumentId { get; init; }

    /// <summary>
    /// 取得 HTTP 狀態碼。
    /// </summary>
    public required int StatusCode { get; init; }

    /// <summary>
    /// 取得錯誤類型。
    /// </summary>
    public string? ErrorType { get; init; }

    /// <summary>
    /// 取得錯誤原因。
    /// </summary>
    public string? ErrorReason { get; init; }
}
