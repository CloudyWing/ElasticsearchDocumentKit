using System.Linq.Expressions;
using CloudyWing.ElasticsearchDocumentKit.Internal;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace CloudyWing.ElasticsearchDocumentKit.Querying;

/// <summary>
/// 提供常見 search descriptor 設定的擴充方法。
/// </summary>
public static class ElasticsearchSearchRequestDescriptorExtensions {
    /// <summary>
    /// 使用直觀的 filter builder 設定 bool filter query。
    /// </summary>
    /// <typeparam name="TDocument">文件型別。</typeparam>
    /// <param name="descriptor">search request descriptor。</param>
    /// <param name="configure">filter builder 設定動作。</param>
    /// <returns>search request descriptor。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> 為 <see langword="null"/>。</exception>
    public static SearchRequestDescriptor<TDocument> QueryFilters<TDocument>(
        this SearchRequestDescriptor<TDocument> descriptor,
        Action<ElasticsearchQueryFilterBuilder<TDocument>> configure
    )
        where TDocument : class {
        ArgumentNullException.ThrowIfNull(configure);

        ElasticsearchQueryFilterBuilder<TDocument> builder = new();
        configure(builder);
        Action<QueryDescriptor<TDocument>>? filterQuery = FilterQueryFactory.Create(builder);

        if (filterQuery is null) {
            return descriptor;
        }

        return descriptor.Query(filterQuery);
    }

    /// <summary>
    /// 限制回傳的 <c>_source</c> 欄位。
    /// </summary>
    /// <typeparam name="TDocument">文件型別。</typeparam>
    /// <param name="descriptor">search request descriptor。</param>
    /// <param name="fields">要包含的欄位；未提供欄位時不改變 descriptor。</param>
    /// <returns>search request descriptor。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fields"/> 為 <see langword="null"/>。</exception>
    public static SearchRequestDescriptor<TDocument> SourceIncludes<TDocument>(
        this SearchRequestDescriptor<TDocument> descriptor,
        params Expression<Func<TDocument, object?>>[] fields
    )
        where TDocument : class {
        ArgumentNullException.ThrowIfNull(fields);

        if (fields.Length == 0) {
            return descriptor;
        }

        return descriptor.SourceIncludes(Fields.FromExpressions(fields));
    }

    /// <summary>
    /// 排除指定的 <c>_source</c> 欄位。
    /// </summary>
    /// <typeparam name="TDocument">文件型別。</typeparam>
    /// <param name="descriptor">search request descriptor。</param>
    /// <param name="fields">要排除的欄位。</param>
    /// <returns>search request descriptor。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fields"/> 為 <see langword="null"/>。</exception>
    public static SearchRequestDescriptor<TDocument> SourceExcludes<TDocument>(
        this SearchRequestDescriptor<TDocument> descriptor,
        params Expression<Func<TDocument, object?>>[] fields
    )
        where TDocument : class {
        ArgumentNullException.ThrowIfNull(fields);

        if (fields.Length == 0) {
            return descriptor;
        }

        return descriptor.SourceExcludes(Fields.FromExpressions(fields));
    }

    /// <summary>
    /// 關閉 <c>_source</c> 回傳。
    /// </summary>
    /// <typeparam name="TDocument">文件型別。</typeparam>
    /// <param name="descriptor">search request descriptor。</param>
    /// <returns>search request descriptor。</returns>
    public static SearchRequestDescriptor<TDocument> DisableSource<TDocument>(
        this SearchRequestDescriptor<TDocument> descriptor
    )
        where TDocument : class {
        return descriptor.Source(false);
    }
}
