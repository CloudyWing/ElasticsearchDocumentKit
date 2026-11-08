namespace CloudyWing.ElasticsearchDocumentKit.Indexing;

/// <summary>
/// 顯式初始化指定文件型別的 Elasticsearch index。
/// </summary>
/// <typeparam name="TDocument">文件型別。</typeparam>
public interface IIndexInitializer<TDocument>
    where TDocument : class {
    /// <summary>
    /// 建立 index 與 mapping。
    /// </summary>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>index 初始化結果。</returns>
    Task<IndexInitializationResult> InitializeAsync(CancellationToken cancellationToken = default);
}
