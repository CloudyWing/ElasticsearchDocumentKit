using Elastic.Clients.Elasticsearch;

namespace CloudyWing.ElasticsearchDocumentKit.Search;

/// <summary>
/// 表示 search-after 分頁查詢設定。
/// </summary>
/// <typeparam name="TDocument">文件型別。</typeparam>
public sealed class SearchAfterRequest<TDocument>
    where TDocument : class {
    /// <summary>
    /// 取得或設定單頁筆數。
    /// </summary>
    /// <value>單頁筆數。預設為 100。</value>
    public int Size { get; init; } = 100;

    /// <summary>
    /// 取得或設定前一頁最後一筆 hit 的排序值。
    /// </summary>
    /// <value>排序值順序必須與 search request 的 sort 設定一致。</value>
    public IReadOnlyList<FieldValue> SearchAfter { get; init; } = [];

    /// <summary>
    /// 取得或設定官方 search descriptor 設定動作。
    /// </summary>
    /// <value>查詢條件與排序設定；排序必須穩定才能正確使用 search-after。</value>
    public Action<SearchRequestDescriptor<TDocument>>? ConfigureSearch { get; init; }
}
