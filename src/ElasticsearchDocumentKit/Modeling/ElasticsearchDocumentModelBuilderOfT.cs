using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;

namespace CloudyWing.ElasticsearchDocumentKit.Modeling;

/// <summary>
/// 建立指定文件型別的 Elasticsearch 模型設定。
/// </summary>
/// <typeparam name="TDocument">文件型別。</typeparam>
public sealed class ElasticsearchDocumentModelBuilder<TDocument>
    where TDocument : class {
    private readonly ElasticsearchDocumentModelDefinition model;

    internal ElasticsearchDocumentModelBuilder(ElasticsearchDocumentModelDefinition model) {
        ArgumentNullException.ThrowIfNull(model);

        this.model = model;
    }

    /// <summary>
    /// 指定文件型別使用的 index 名稱。
    /// </summary>
    /// <param name="indexName">index 名稱。</param>
    /// <returns>目前 builder。</returns>
    /// <exception cref="ArgumentException"><paramref name="indexName"/> 不符合 Elasticsearch index 命名限制。</exception>
    public ElasticsearchDocumentModelBuilder<TDocument> ToIndex(string indexName) {
        ElasticsearchIndexNameValidator.Validate(indexName, nameof(indexName));

        model.IndexName = indexName;
        return this;
    }

    /// <summary>
    /// 指定文件 ID 選取器。
    /// </summary>
    /// <param name="idSelector">文件 ID 選取器。</param>
    /// <returns>目前 builder。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="idSelector"/> 為 <see langword="null"/>。</exception>
    public ElasticsearchDocumentModelBuilder<TDocument> HasId(Func<TDocument, string?> idSelector) {
        ArgumentNullException.ThrowIfNull(idSelector);

        model.IdSelector = idSelector;
        return this;
    }

    /// <summary>
    /// 加入 index 建立 descriptor 設定動作。
    /// </summary>
    /// <param name="configureIndex">index 建立 descriptor 設定動作。</param>
    /// <returns>目前 builder。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configureIndex"/> 為 <see langword="null"/>。</exception>
    public ElasticsearchDocumentModelBuilder<TDocument> ConfigureIndex(
        Action<CreateIndexRequestDescriptor<TDocument>> configureIndex
    ) {
        ArgumentNullException.ThrowIfNull(configureIndex);

        AddConfigureIndex(configureIndex);
        return this;
    }

    /// <summary>
    /// 加入 mapping 設定動作。
    /// </summary>
    /// <param name="configureMapping">mapping 設定動作。</param>
    /// <returns>目前 builder。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configureMapping"/> 為 <see langword="null"/>。</exception>
    public ElasticsearchDocumentModelBuilder<TDocument> ConfigureMapping(
        Action<TypeMappingDescriptor<TDocument>> configureMapping
    ) {
        ArgumentNullException.ThrowIfNull(configureMapping);

        AddConfigureIndex(descriptor => descriptor.Mappings(configureMapping));
        return this;
    }

    private void AddConfigureIndex(Action<CreateIndexRequestDescriptor<TDocument>> configureIndex) {
        Action<CreateIndexRequestDescriptor<TDocument>>? existing = model.ConfigureIndex
            as Action<CreateIndexRequestDescriptor<TDocument>>;

        Action<CreateIndexRequestDescriptor<TDocument>> combined = descriptor => {
            existing?.Invoke(descriptor);
            configureIndex(descriptor);
        };
        model.ConfigureIndex = combined;
    }
}
