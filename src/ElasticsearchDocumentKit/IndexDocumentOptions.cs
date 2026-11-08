using Elastic.Clients.Elasticsearch;

namespace CloudyWing.ElasticsearchDocumentKit;

/// <summary>
/// 表示單筆建立或覆寫文件的選項。
/// </summary>
public sealed class IndexDocumentOptions {
    /// <summary>
    /// 取得或設定寫入後的 refresh 行為。
    /// </summary>
    /// <value>未設定時不送出 refresh 參數。</value>
    public Refresh? Refresh { get; init; }

    /// <summary>
    /// 取得或設定樂觀並行控制使用的序號。
    /// </summary>
    /// <value>必須與 <see cref="IfPrimaryTerm"/> 同時設定。文件目前的序號與此值不符時，Elasticsearch 回應 409。</value>
    public long? IfSeqNo { get; init; }

    /// <summary>
    /// 取得或設定樂觀並行控制使用的 primary term。
    /// </summary>
    /// <value>必須與 <see cref="IfSeqNo"/> 同時設定。</value>
    public long? IfPrimaryTerm { get; init; }

    internal void Validate() {
        if (IfSeqNo.HasValue != IfPrimaryTerm.HasValue) {
            throw new InvalidOperationException("IfSeqNo and IfPrimaryTerm must be set together.");
        }

        if (IfSeqNo < 0 || IfPrimaryTerm < 0) {
            throw new InvalidOperationException("IfSeqNo and IfPrimaryTerm must be greater than or equal to zero.");
        }
    }
}
