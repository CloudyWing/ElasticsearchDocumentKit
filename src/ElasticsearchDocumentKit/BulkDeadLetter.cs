namespace CloudyWing.ElasticsearchDocumentKit;

/// <summary>
/// 表示 bulk 寫入失敗時交給 dead-letter callback 的文件摘要。
/// </summary>
/// <typeparam name="TDocument">文件型別。</typeparam>
public sealed class BulkDeadLetter<TDocument>
    where TDocument : class {
    /// <summary>
    /// 取得 index 名稱。
    /// </summary>
    public required string IndexName { get; init; }

    /// <summary>
    /// 取得失敗文件。
    /// </summary>
    public required TDocument Document { get; init; }

    /// <summary>
    /// 取得失敗摘要。
    /// </summary>
    public required BulkOperationFailure Failure { get; init; }

    /// <summary>
    /// 取得 chunk 順序。
    /// </summary>
    public required int ChunkIndex { get; init; }

    /// <summary>
    /// 取得最後一次嘗試次數。
    /// </summary>
    public required int Attempt { get; init; }
}
