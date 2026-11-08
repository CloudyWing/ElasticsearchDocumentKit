namespace CloudyWing.ElasticsearchDocumentKit.Modeling;

/// <summary>
/// 建立文件型別的 Elasticsearch 模型設定。
/// </summary>
public sealed class ElasticsearchDocumentModelBuilder {
    private readonly ElasticsearchDocumentsOptions options;

    internal ElasticsearchDocumentModelBuilder(ElasticsearchDocumentsOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options;
    }

    /// <summary>
    /// 設定指定文件型別。
    /// </summary>
    /// <typeparam name="TDocument">文件型別。</typeparam>
    /// <returns>文件模型設定 builder。</returns>
    public ElasticsearchDocumentModelBuilder<TDocument> Document<TDocument>()
        where TDocument : class {
        return new ElasticsearchDocumentModelBuilder<TDocument>(options.GetOrAddDocumentModel(typeof(TDocument)));
    }
}
