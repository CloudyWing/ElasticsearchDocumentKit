using Elastic.Clients.Elasticsearch;

namespace CloudyWing.ElasticsearchDocumentKit;

/// <summary>
/// 表示單筆文件寫入或刪除操作的選項。
/// </summary>
public sealed class DocumentWriteOptions {
    /// <summary>
    /// 取得或設定寫入後的 refresh 行為。
    /// </summary>
    /// <value>
    /// 未設定時不送出 refresh 參數，由 Elasticsearch 依 index 設定決定可見時機。
    /// 設定為 <see cref="Elastic.Clients.Elasticsearch.Refresh.True"/> 或 <see cref="Elastic.Clients.Elasticsearch.Refresh.WaitFor"/>
    /// 可讓寫入結果在回應後立即可被查詢，但會增加每次寫入的成本。
    /// </value>
    public Refresh? Refresh { get; init; }
}
