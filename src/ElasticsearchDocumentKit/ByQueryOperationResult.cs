namespace CloudyWing.ElasticsearchDocumentKit;

/// <summary>
/// 表示 update-by-query 或 delete-by-query 的操作結果。
/// </summary>
public sealed class ByQueryOperationResult {
    /// <summary>
    /// 取得操作是否成功。
    /// </summary>
    /// <value>請求成功且沒有任何文件處理失敗時為 <see langword="true"/>。</value>
    public required bool IsSuccessful { get; init; }

    /// <summary>
    /// 取得操作名稱。
    /// </summary>
    /// <value><c>update_by_query</c> 或 <c>delete_by_query</c>。</value>
    public required string Operation { get; init; }

    /// <summary>
    /// 取得 index 名稱。
    /// </summary>
    public string? IndexName { get; init; }

    /// <summary>
    /// 取得已更新的文件筆數。
    /// </summary>
    /// <value>delete-by-query 時為 0。</value>
    public long Updated { get; init; }

    /// <summary>
    /// 取得已刪除的文件筆數。
    /// </summary>
    /// <value>update-by-query 時為 0。</value>
    public long Deleted { get; init; }

    /// <summary>
    /// 取得版本衝突的文件筆數。
    /// </summary>
    public long VersionConflicts { get; init; }

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
