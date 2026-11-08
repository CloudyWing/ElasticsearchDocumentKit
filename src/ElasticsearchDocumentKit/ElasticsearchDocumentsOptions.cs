using CloudyWing.ElasticsearchDocumentKit.Modeling;

namespace CloudyWing.ElasticsearchDocumentKit;

/// <summary>
/// 表示 Elasticsearch documents 工具庫使用的設定。
/// </summary>
public sealed class ElasticsearchDocumentsOptions {
    private readonly Dictionary<Type, ElasticsearchDocumentModelDefinition> documentModels = [];

    /// <summary>
    /// 取得設定節點名稱。
    /// </summary>
    public const string OptionsName = "ElasticsearchDocuments";

    /// <summary>
    /// 取得或設定 index 名稱前綴。
    /// </summary>
    /// <value>index 名稱前綴；未設定時不加前綴。</value>
    public string? IndexPrefix { get; set; }

    internal IReadOnlyDictionary<Type, ElasticsearchDocumentModelDefinition> DocumentModels => documentModels;

    /// <summary>
    /// 設定文件模型。
    /// </summary>
    /// <param name="configure">文件模型設定動作。</param>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> 為 <see langword="null"/>。</exception>
    public void ConfigureDocuments(Action<ElasticsearchDocumentModelBuilder> configure) {
        ArgumentNullException.ThrowIfNull(configure);

        ElasticsearchDocumentModelBuilder builder = new(this);
        configure(builder);
    }

    /// <summary>
    /// 設定單一文件模型。
    /// </summary>
    /// <typeparam name="TDocument">文件型別。</typeparam>
    /// <param name="configure">文件模型設定動作。</param>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> 為 <see langword="null"/>。</exception>
    public void ConfigureDocument<TDocument>(Action<ElasticsearchDocumentModelBuilder<TDocument>> configure)
        where TDocument : class {
        ArgumentNullException.ThrowIfNull(configure);

        configure(new ElasticsearchDocumentModelBuilder<TDocument>(GetOrAddDocumentModel(typeof(TDocument))));
    }

    /// <summary>
    /// 驗證目前設定是否可用。
    /// </summary>
    /// <exception cref="InvalidOperationException">設定值不符合工具庫需求時。</exception>
    public void Validate() {
        if (IndexPrefix is not null && string.IsNullOrWhiteSpace(IndexPrefix)) {
            throw new InvalidOperationException("Elasticsearch index prefix must be null or non-whitespace.");
        }
    }

    internal ElasticsearchDocumentModelDefinition GetOrAddDocumentModel(Type documentType) {
        ArgumentNullException.ThrowIfNull(documentType);

        if (!documentModels.TryGetValue(documentType, out ElasticsearchDocumentModelDefinition? model)) {
            model = new ElasticsearchDocumentModelDefinition(documentType);
            documentModels.Add(documentType, model);
        }

        return model;
    }

    internal bool TryGetDocumentModel(
        Type documentType,
        out ElasticsearchDocumentModelDefinition? model
    ) {
        ArgumentNullException.ThrowIfNull(documentType);

        return documentModels.TryGetValue(documentType, out model);
    }
}
