using System.Reflection;
using System.Text;
using CloudyWing.ElasticsearchDocumentKit.Modeling;

namespace CloudyWing.ElasticsearchDocumentKit;

/// <summary>
/// 依型別名稱與選項前綴解析 index 名稱。
/// </summary>
public sealed class DefaultIndexNameResolver : IIndexNameResolver {
    private readonly ElasticsearchDocumentsOptions options;

    /// <summary>
    /// 初始化 <see cref="DefaultIndexNameResolver"/> 類別的新執行個體。
    /// </summary>
    /// <param name="options">Elasticsearch documents 設定；未提供時不加 index 前綴。</param>
    /// <exception cref="InvalidOperationException">設定值不符合工具庫需求時。</exception>
    public DefaultIndexNameResolver(ElasticsearchDocumentsOptions? options = null) {
        ElasticsearchDocumentsOptions resolvedOptions = options ?? new ElasticsearchDocumentsOptions();
        resolvedOptions.Validate();

        this.options = resolvedOptions;
    }

    /// <inheritdoc/>
    public string Resolve<TDocument>() {
        return Resolve(typeof(TDocument));
    }

    /// <inheritdoc/>
    public string Resolve(Type documentType) {
        ArgumentNullException.ThrowIfNull(documentType);

        options.TryGetDocumentModel(documentType, out ElasticsearchDocumentModelDefinition? model);
        ElasticsearchIndexNameAttribute? attribute = documentType.GetCustomAttribute<ElasticsearchIndexNameAttribute>();
        string baseName = model?.IndexName ?? attribute?.Name ?? ToKebabCase(documentType.Name);
        string? prefix = Normalize(options.IndexPrefix);

        if (prefix is null) {
            ElasticsearchIndexNameValidator.Validate(baseName, nameof(documentType));
            return baseName;
        }

        string indexName = $"{prefix}-{baseName}";
        ElasticsearchIndexNameValidator.Validate(indexName, nameof(documentType));

        return indexName;
    }

    private static string? Normalize(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        string normalized = value.Trim().Trim('-', '_').ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string ToKebabCase(string value) {
        StringBuilder builder = new(value.Length + 8);

        for (int i = 0; i < value.Length; i++) {
            char current = value[i];

            if (char.IsUpper(current)) {
                if (i > 0 && ShouldInsertSeparator(value, i)) {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(current));
                continue;
            }

            if (current == '_' || current == ' ') {
                AppendSeparatorIfNeeded(builder);
                continue;
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString().Trim('-');
    }

    private static bool ShouldInsertSeparator(string value, int index) {
        char previous = value[index - 1];

        if (previous == '_' || previous == ' ' || previous == '-') {
            return false;
        }

        if (char.IsLower(previous) || char.IsDigit(previous)) {
            return true;
        }

        return index + 1 < value.Length && char.IsLower(value[index + 1]);
    }

    private static void AppendSeparatorIfNeeded(StringBuilder builder) {
        if (builder.Length > 0 && builder[^1] != '-') {
            builder.Append('-');
        }
    }
}
