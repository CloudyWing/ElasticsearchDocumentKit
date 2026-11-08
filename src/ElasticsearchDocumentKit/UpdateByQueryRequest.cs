using CloudyWing.ElasticsearchDocumentKit.Querying;
using Elastic.Clients.Elasticsearch;

namespace CloudyWing.ElasticsearchDocumentKit;

/// <summary>
/// 表示 update-by-query 的設定。
/// </summary>
/// <typeparam name="TDocument">文件型別。</typeparam>
/// <remarks>
/// <see cref="Filters"/> 沒有任何條件時，必須透過 <see cref="ConfigureRequest"/> 明確指定 query
/// （例如 <c>match_all</c>），否則會因為可能誤更新整個 index 而被拒絕。
/// </remarks>
public sealed class UpdateByQueryRequest<TDocument>
    where TDocument : class {
    /// <summary>
    /// 取得或設定篩選條件。
    /// </summary>
    /// <value>條件會組成 bool filter query。</value>
    public ElasticsearchQueryFilterBuilder<TDocument>? Filters { get; init; }

    /// <summary>
    /// 取得或設定更新使用的 script。
    /// </summary>
    /// <value>未設定時為不帶 script 的 update-by-query（原地重新索引符合條件的文件）。</value>
    public Script? Script { get; init; }

    /// <summary>
    /// 取得或設定一個值，用以指出完成後是否 refresh 受影響的 index。
    /// </summary>
    /// <value>若要 refresh，則為 <see langword="true"/>；否則為 <see langword="false"/>。預設為 <see langword="true"/>。</value>
    public bool Refresh { get; init; } = true;

    /// <summary>
    /// 取得或設定官方 request descriptor 設定動作。
    /// </summary>
    /// <value>於套件自身的設定之後執行，可覆寫前面的設定，例如指定 <c>conflicts=proceed</c>。</value>
    public Action<UpdateByQueryRequestDescriptor<TDocument>>? ConfigureRequest { get; init; }
}
