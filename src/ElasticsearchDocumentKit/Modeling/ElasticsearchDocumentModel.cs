using Elastic.Clients.Elasticsearch.IndexManagement;

namespace CloudyWing.ElasticsearchDocumentKit.Modeling;

/// <summary>
/// 表示單一文件型別的 Elasticsearch 模型設定。
/// </summary>
/// <typeparam name="TDocument">文件型別。</typeparam>
public sealed class ElasticsearchDocumentModel<TDocument>
    where TDocument : class {
    /// <summary>
    /// 初始化 <see cref="ElasticsearchDocumentModel{TDocument}"/> 類別的新執行個體。
    /// </summary>
    /// <param name="indexName">index 名稱。</param>
    /// <param name="idSelector">文件 ID 選取器。</param>
    /// <param name="configureIndex">index 建立 descriptor 設定動作。</param>
    /// <exception cref="ArgumentException"><paramref name="indexName"/> 為空白字串。</exception>
    public ElasticsearchDocumentModel(
        string indexName,
        Func<TDocument, string?>? idSelector = null,
        Action<CreateIndexRequestDescriptor<TDocument>>? configureIndex = null
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(indexName);

        IndexName = indexName;
        IdSelector = idSelector;
        ConfigureIndex = configureIndex;
    }

    /// <summary>
    /// 取得 index 名稱。
    /// </summary>
    public string IndexName { get; }

    /// <summary>
    /// 取得文件 ID 選取器。
    /// </summary>
    /// <value>未設定時由 Elasticsearch 自動產生 ID。</value>
    public Func<TDocument, string?>? IdSelector { get; }

    /// <summary>
    /// 取得 index 建立 descriptor 設定動作。
    /// </summary>
    /// <value>未設定時使用官方 client 預設 mapping。</value>
    public Action<CreateIndexRequestDescriptor<TDocument>>? ConfigureIndex { get; }
}
