namespace CloudyWing.ElasticsearchDocumentKit;

/// <summary>
/// 表示單筆文件讀取結果。
/// </summary>
/// <typeparam name="TDocument">文件型別。</typeparam>
public sealed class DocumentGetResult<TDocument>
    where TDocument : class {
    /// <summary>
    /// 取得操作是否成功。
    /// </summary>
    public required bool IsSuccessful { get; init; }

    /// <summary>
    /// 取得文件是否存在。
    /// </summary>
    public required bool Found { get; init; }

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
    /// 取得文件的 sequence number，可搭配 <see cref="PrimaryTerm"/> 供下一次寫入做樂觀並行控制。
    /// </summary>
    public long? SeqNo { get; init; }

    /// <summary>
    /// 取得文件的 primary term，可搭配 <see cref="SeqNo"/> 供下一次寫入做樂觀並行控制。
    /// </summary>
    public long? PrimaryTerm { get; init; }

    /// <summary>
    /// 取得文件內容。
    /// </summary>
    public TDocument? Document { get; init; }

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
