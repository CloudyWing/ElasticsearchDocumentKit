using System.Globalization;

namespace CloudyWing.ElasticsearchDocumentKit.Indexing;

/// <summary>
/// 提供常見 index lifecycle 命名輔助方法。
/// </summary>
public static class IndexLifecycleNames {
    /// <summary>
    /// 建立含日期 suffix 的 index 名稱。
    /// </summary>
    /// <param name="baseName">基礎 index 名稱。</param>
    /// <param name="date">日期。</param>
    /// <param name="format">日期格式。</param>
    /// <returns>含日期 suffix 的 index 名稱。</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="baseName"/> 或 <paramref name="format"/> 為空白字串。
    /// </exception>
    public static string WithDateSuffix(
        string baseName,
        DateTimeOffset date,
        string format = "yyyyMMdd"
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        string normalizedBaseName = NormalizePart(baseName, nameof(baseName));
        ElasticsearchIndexNameValidator.Validate(normalizedBaseName, nameof(baseName));

        string indexName = $"{normalizedBaseName}-{date.ToString(format, CultureInfo.InvariantCulture)}";
        ElasticsearchIndexNameValidator.Validate(indexName, nameof(baseName));

        return indexName;
    }

    /// <summary>
    /// 建立含版本 suffix 的 index 名稱。
    /// </summary>
    /// <param name="baseName">基礎 index 名稱。</param>
    /// <param name="version">版本識別。</param>
    /// <returns>含版本 suffix 的 index 名稱。</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="baseName"/> 或 <paramref name="version"/> 為空白字串。
    /// </exception>
    public static string WithVersionSuffix(string baseName, string version) {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        string normalizedBaseName = NormalizePart(baseName, nameof(baseName));
        ElasticsearchIndexNameValidator.Validate(normalizedBaseName, nameof(baseName));

        string indexName = $"{normalizedBaseName}-{NormalizePart(version, nameof(version))}";
        ElasticsearchIndexNameValidator.Validate(indexName, nameof(version));

        return indexName;
    }

    private static string NormalizePart(string value, string paramName) {
        string normalized = value.Trim().Trim('-', '_').ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalized)) {
            throw new ArgumentException("Index name part is required.", paramName);
        }

        return normalized;
    }
}
