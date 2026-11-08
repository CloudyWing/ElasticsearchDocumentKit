namespace CloudyWing.ElasticsearchDocumentKit.Indexing;

/// <summary>
/// 表示 alias 切換結果。
/// </summary>
public sealed class AliasSwitchResult {
    /// <summary>
    /// 取得操作是否成功。
    /// </summary>
    public required bool IsSuccessful { get; init; }

    /// <summary>
    /// 取得 alias 名稱。
    /// </summary>
    public required string AliasName { get; init; }

    /// <summary>
    /// 取得新增 alias 的 index 名稱。
    /// </summary>
    public required string AddedIndexName { get; init; }

    /// <summary>
    /// 取得移除 alias 的 index 名稱集合。
    /// </summary>
    public IReadOnlyList<string> RemovedIndexNames { get; init; } = [];

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
