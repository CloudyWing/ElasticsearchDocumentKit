namespace CloudyWing.ElasticsearchDocumentKit.Indexing;

/// <summary>
/// 切換 Elasticsearch alias 指向的 index。
/// </summary>
public interface IElasticsearchAliasSwitcher {
    /// <summary>
    /// 將 alias 切換到指定 index，並從指定舊 index 移除同一 alias。
    /// </summary>
    /// <param name="aliasName">alias 名稱。</param>
    /// <param name="newIndexName">新的 index 名稱。</param>
    /// <param name="oldIndexNames">要移除 alias 的舊 index 名稱集合。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>alias 切換結果。</returns>
    Task<AliasSwitchResult> SwitchAsync(
        string aliasName,
        string newIndexName,
        IEnumerable<string>? oldIndexNames = null,
        CancellationToken cancellationToken = default
    );
}
