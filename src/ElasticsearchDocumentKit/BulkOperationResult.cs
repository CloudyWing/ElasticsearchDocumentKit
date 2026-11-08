namespace CloudyWing.ElasticsearchDocumentKit;

/// <summary>
/// 表示 bulk 寫入操作結果。
/// </summary>
public sealed class BulkOperationResult {
    /// <summary>
    /// 取得操作是否完全成功。
    /// </summary>
    public required bool IsSuccessful { get; init; }

    /// <summary>
    /// 取得操作是否包含失敗。
    /// </summary>
    public required bool HasErrors { get; init; }

    /// <summary>
    /// 取得操作總筆數。
    /// </summary>
    public required int TotalCount { get; init; }

    /// <summary>
    /// 取得成功筆數。
    /// </summary>
    public required int SucceededCount { get; init; }

    /// <summary>
    /// 取得失敗筆數。
    /// </summary>
    public required int FailedCount { get; init; }

    /// <summary>
    /// 取得失敗文件摘要。
    /// </summary>
    public IReadOnlyList<BulkOperationFailure> Failures { get; init; } = [];

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
