using Elastic.Clients.Elasticsearch.IndexManagement;

namespace CloudyWing.ElasticsearchDocumentKit.Modeling;

/// <summary>
/// 依工具庫設定與 index 命名規則解析文件模型。
/// </summary>
public sealed class DefaultElasticsearchDocumentModelResolver : IElasticsearchDocumentModelResolver {
    private readonly ElasticsearchDocumentsOptions options;
    private readonly IIndexNameResolver indexNameResolver;

    /// <summary>
    /// 初始化 <see cref="DefaultElasticsearchDocumentModelResolver"/> 類別的新執行個體。
    /// </summary>
    /// <param name="options">Elasticsearch documents 設定。</param>
    /// <param name="indexNameResolver">index 名稱解析器。</param>
    /// <exception cref="InvalidOperationException">設定值不符合工具庫需求時。</exception>
    public DefaultElasticsearchDocumentModelResolver(
        ElasticsearchDocumentsOptions? options = null,
        IIndexNameResolver? indexNameResolver = null
    ) {
        ElasticsearchDocumentsOptions resolvedOptions = options ?? new ElasticsearchDocumentsOptions();
        resolvedOptions.Validate();

        this.options = resolvedOptions;
        this.indexNameResolver = indexNameResolver ?? new DefaultIndexNameResolver(resolvedOptions);
    }

    /// <inheritdoc/>
    public ElasticsearchDocumentModel<TDocument> Resolve<TDocument>()
        where TDocument : class {
        string indexName = indexNameResolver.Resolve<TDocument>();
        Func<TDocument, string?>? idSelector = null;
        Action<CreateIndexRequestDescriptor<TDocument>>? configureIndex = null;

        if (options.TryGetDocumentModel(typeof(TDocument), out ElasticsearchDocumentModelDefinition? model)
            && model is not null
        ) {
            idSelector = model.IdSelector as Func<TDocument, string?>;
            configureIndex = model.ConfigureIndex as Action<CreateIndexRequestDescriptor<TDocument>>;
        }

        return new ElasticsearchDocumentModel<TDocument>(indexName, idSelector, configureIndex);
    }
}
