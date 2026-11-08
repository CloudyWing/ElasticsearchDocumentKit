using Elastic.Clients.Elasticsearch;

namespace CloudyWing.ElasticsearchDocumentKit.Search;

/// <summary>
/// 表示 search-after 分頁查詢結果。
/// </summary>
/// <typeparam name="TDocument">文件型別。</typeparam>
public sealed class SearchAfterPage<TDocument>
    where TDocument : class {
    /// <summary>
    /// 取得目前頁面的文件集合。
    /// </summary>
    public IReadOnlyList<TDocument> Documents { get; init; } = [];

    /// <summary>
    /// 取得下一頁可使用的 search-after 排序值。
    /// </summary>
    public IReadOnlyList<FieldValue> NextSearchAfter { get; init; } = [];

    /// <summary>
    /// 取得符合查詢條件的總筆數。
    /// </summary>
    public required long TotalCount { get; init; }

    /// <summary>
    /// 取得查詢是否成功。
    /// </summary>
    public required bool IsSuccessful { get; init; }

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
