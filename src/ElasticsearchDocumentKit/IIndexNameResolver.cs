namespace CloudyWing.ElasticsearchDocumentKit;

/// <summary>
/// 解析文件型別對應的 Elasticsearch index 名稱。
/// </summary>
public interface IIndexNameResolver {
    /// <summary>
    /// 解析指定文件型別的 index 名稱。
    /// </summary>
    /// <typeparam name="TDocument">文件型別。</typeparam>
    /// <returns>Elasticsearch index 名稱。</returns>
    string Resolve<TDocument>();

    /// <summary>
    /// 解析指定文件型別的 index 名稱。
    /// </summary>
    /// <param name="documentType">文件型別。</param>
    /// <returns>Elasticsearch index 名稱。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="documentType"/> 為 <see langword="null"/>。</exception>
    string Resolve(Type documentType);
}
