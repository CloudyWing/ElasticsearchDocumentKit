using Elastic.Clients.Elasticsearch;

namespace CloudyWing.ElasticsearchDocumentKit;

/// <summary>
/// 表示單筆更新文件的選項。
/// </summary>
public sealed class UpdateDocumentOptions {
    /// <summary>
    /// 取得或設定寫入後的 refresh 行為。
    /// </summary>
    /// <value>未設定時不送出 refresh 參數。</value>
    public Refresh? Refresh { get; init; }

    /// <summary>
    /// 取得或設定樂觀並行控制使用的序號。
    /// </summary>
    /// <value>必須與 <see cref="IfPrimaryTerm"/> 同時設定，且不得與大於 0 的 <see cref="RetryOnConflict"/> 並用。</value>
    public long? IfSeqNo { get; init; }

    /// <summary>
    /// 取得或設定樂觀並行控制使用的 primary term。
    /// </summary>
    /// <value>必須與 <see cref="IfSeqNo"/> 同時設定。</value>
    public long? IfPrimaryTerm { get; init; }

    /// <summary>
    /// 取得或設定發生版本衝突時由 Elasticsearch 自動重試的次數。
    /// </summary>
    /// <value>預設為 0，表示不重試。大於 0 時不得同時設定 <see cref="IfSeqNo"/>。</value>
    public int RetryOnConflict { get; init; }

    internal void Validate() {
        if (IfSeqNo.HasValue != IfPrimaryTerm.HasValue) {
            throw new InvalidOperationException("IfSeqNo and IfPrimaryTerm must be set together.");
        }

        if (IfSeqNo < 0 || IfPrimaryTerm < 0) {
            throw new InvalidOperationException("IfSeqNo and IfPrimaryTerm must be greater than or equal to zero.");
        }

        if (RetryOnConflict < 0) {
            throw new InvalidOperationException("RetryOnConflict must be greater than or equal to zero.");
        }

        if (RetryOnConflict > 0 && IfSeqNo is not null) {
            throw new InvalidOperationException("RetryOnConflict cannot be combined with IfSeqNo and IfPrimaryTerm.");
        }
    }
}
