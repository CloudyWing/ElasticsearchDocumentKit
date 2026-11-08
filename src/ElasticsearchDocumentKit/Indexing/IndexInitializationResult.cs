namespace CloudyWing.ElasticsearchDocumentKit.Indexing;

/// <summary>
/// 表示 index 初始化結果。
/// </summary>
public sealed class IndexInitializationResult {
    /// <summary>
    /// 取得初始化是否成功。
    /// </summary>
    public required bool IsSuccessful { get; init; }

    /// <summary>
    /// 取得 index 名稱。
    /// </summary>
    public required string IndexName { get; init; }

    /// <summary>
    /// 取得是否已建立 index。
    /// </summary>
    public required bool Created { get; init; }

    /// <summary>
    /// 取得 index 是否原本已存在。
    /// </summary>
    public required bool AlreadyExists { get; init; }

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
