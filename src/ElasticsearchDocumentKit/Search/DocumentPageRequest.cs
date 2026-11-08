using CloudyWing.ElasticsearchDocumentKit.Querying;
using Elastic.Clients.Elasticsearch;

namespace CloudyWing.ElasticsearchDocumentKit.Search;

/// <summary>
/// 表示 from/size 分頁查詢的設定。
/// </summary>
/// <typeparam name="TDocument">文件型別。</typeparam>
/// <remarks>
/// <c>from + size</c> 超過 Elasticsearch 的 <c>index.max_result_window</c>（預設 10000）時請求會失敗，
/// 需要深度分頁時改用 search-after。不同頁之間不保證查詢同一份快照。
/// </remarks>
public sealed class DocumentPageRequest<TDocument>
    where TDocument : class {
    /// <summary>
    /// 取得或設定頁碼。
    /// </summary>
    /// <value>從 1 開始，必須大於或等於 1。</value>
    public required int PageNumber { get; init; }

    /// <summary>
    /// 取得或設定單頁筆數。
    /// </summary>
    /// <value>必須大於或等於 1。</value>
    public required int PageSize { get; init; }

    /// <summary>
    /// 取得或設定篩選條件。
    /// </summary>
    /// <value>條件會組成 bool filter query；未設定或沒有任何條件時不送出 query，查詢 index 內的全部文件。</value>
    public ElasticsearchQueryFilterBuilder<TDocument>? Filters { get; init; }

    /// <summary>
    /// 取得或設定一個值，用以指出是否精確計算符合條件的總筆數。
    /// </summary>
    /// <value>
    /// 若要精確計算，則為 <see langword="true"/>（送出 <c>track_total_hits: true</c>）；
    /// 否則為 <see langword="false"/>，此時結果的 <see cref="DocumentPageResult{TDocument}.TotalCount"/> 為 <see langword="null"/>。
    /// 預設為 <see langword="true"/>。
    /// </value>
    public bool TrackTotalHits { get; init; } = true;

    /// <summary>
    /// 取得或設定官方 search descriptor 設定動作。
    /// </summary>
    /// <value>於套件自身的設定之後執行，可覆寫前面的設定，例如排序或 <c>_source</c> 篩選。</value>
    public Action<SearchRequestDescriptor<TDocument>>? ConfigureSearch { get; init; }

    internal int GetFrom() {
        long from = ((long)PageNumber - 1) * PageSize;

        if (from > int.MaxValue) {
            throw new ArgumentOutOfRangeException(
                nameof(PageNumber),
                PageNumber,
                "The offset of the requested page exceeds the maximum supported value."
            );
        }

        return (int)from;
    }
}
