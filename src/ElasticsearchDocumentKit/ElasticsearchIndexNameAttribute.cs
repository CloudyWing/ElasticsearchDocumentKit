namespace CloudyWing.ElasticsearchDocumentKit;

/// <summary>
/// 指定文件型別預設解析出的 index 名稱。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class ElasticsearchIndexNameAttribute : Attribute {
    /// <summary>
    /// 初始化 <see cref="ElasticsearchIndexNameAttribute"/> 類別的新執行個體。
    /// </summary>
    /// <param name="name">index 名稱。</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> 不符合 Elasticsearch index 命名限制。</exception>
    public ElasticsearchIndexNameAttribute(string name) {
        ElasticsearchIndexNameValidator.Validate(name, nameof(name));

        Name = name;
    }

    /// <summary>
    /// 取得 index 名稱。
    /// </summary>
    public string Name { get; }
}
