using CloudyWing.ElasticsearchDocumentKit.Querying;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace CloudyWing.ElasticsearchDocumentKit.Internal;

internal static class FilterQueryFactory {
    /// <summary>
    /// 將 filter builder 轉為 bool filter query 的設定動作。
    /// </summary>
    /// <typeparam name="TDocument">文件型別。</typeparam>
    /// <param name="builder">filter builder；可為 <see langword="null"/>。</param>
    /// <returns>query 設定動作；<paramref name="builder"/> 為 <see langword="null"/> 或沒有任何條件時為 <see langword="null"/>。</returns>
    internal static Action<QueryDescriptor<TDocument>>? Create<TDocument>(
        ElasticsearchQueryFilterBuilder<TDocument>? builder
    )
        where TDocument : class {
        if (builder is null) {
            return null;
        }

        Action<QueryDescriptor<TDocument>>[] conditions = builder.Build().ToArray();

        if (conditions.Length == 0) {
            return null;
        }

        return query => query.Bool(boolean => boolean.Filter(conditions));
    }
}
