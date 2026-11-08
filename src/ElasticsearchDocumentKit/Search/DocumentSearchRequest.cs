using CloudyWing.ElasticsearchDocumentKit.Querying;
using Elastic.Clients.Elasticsearch;

namespace CloudyWing.ElasticsearchDocumentKit.Search;

/// <summary>
/// 表示一般查詢的設定。
/// </summary>
/// <typeparam name="TDocument">文件型別。</typeparam>
public sealed class DocumentSearchRequest<TDocument>
    where TDocument : class {
    /// <summary>
    /// 取得或設定篩選條件。
    /// </summary>
    /// <value>條件會組成 bool filter query；未設定或沒有任何條件時不送出 query，查詢 index 內的全部文件。</value>
    public ElasticsearchQueryFilterBuilder<TDocument>? Filters { get; init; }

    /// <summary>
    /// 取得或設定回傳筆數上限。
    /// </summary>
    /// <value>
    /// 設定時必須大於或等於 1。未設定時不送出 <c>size</c>，由 Elasticsearch 使用預設值（10 筆）。
    /// </value>
    public int? Size { get; init; }

    /// <summary>
    /// 取得或設定官方 search descriptor 設定動作。
    /// </summary>
    /// <value>於套件自身的設定之後執行，可覆寫前面的設定，例如排序或 <c>_source</c> 篩選。</value>
    public Action<SearchRequestDescriptor<TDocument>>? ConfigureSearch { get; init; }
}
