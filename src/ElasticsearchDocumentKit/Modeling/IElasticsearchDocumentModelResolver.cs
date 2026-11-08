namespace CloudyWing.ElasticsearchDocumentKit.Modeling;

/// <summary>
/// 解析文件型別對應的 Elasticsearch 模型設定。
/// </summary>
public interface IElasticsearchDocumentModelResolver {
    /// <summary>
    /// 解析指定文件型別的 Elasticsearch 模型設定。
    /// </summary>
    /// <typeparam name="TDocument">文件型別。</typeparam>
    /// <returns>文件模型設定。</returns>
    ElasticsearchDocumentModel<TDocument> Resolve<TDocument>()
        where TDocument : class;
}
