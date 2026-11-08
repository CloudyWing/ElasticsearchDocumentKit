using System.Text;

namespace CloudyWing.ElasticsearchDocumentKit;

/// <summary>
/// 驗證 Elasticsearch index 名稱是否符合建立 index 的基本限制。
/// </summary>
public static class ElasticsearchIndexNameValidator {
    /// <summary>
    /// 取得 Elasticsearch index 名稱最大 UTF-8 byte 長度。
    /// </summary>
    public const int MaxIndexNameByteLength = 255;

    private const string InvalidCharacters = "\\/*?\"<>| ,#:";

    /// <summary>
    /// 判斷指定 index 名稱是否有效。
    /// </summary>
    /// <param name="indexName">index 名稱。</param>
    /// <returns>若 index 名稱有效則為 <see langword="true" />；否則為 <see langword="false" />。</returns>
    public static bool IsValid(string? indexName) {
        return !TryGetValidationError(indexName, out _);
    }

    /// <summary>
    /// 驗證指定 index 名稱。
    /// </summary>
    /// <param name="indexName">index 名稱。</param>
    /// <param name="paramName">例外參數名稱。</param>
    /// <exception cref="ArgumentException"><paramref name="indexName"/> 不符合 Elasticsearch index 命名限制。</exception>
    public static void Validate(string? indexName, string? paramName = null) {
        if (TryGetValidationError(indexName, out string? errorMessage)) {
            throw new ArgumentException(errorMessage, paramName ?? nameof(indexName));
        }
    }

    /// <summary>
    /// 取得指定 index 名稱的驗證錯誤。
    /// </summary>
    /// <param name="indexName">index 名稱。</param>
    /// <param name="errorMessage">驗證錯誤訊息；有效時為 <see langword="null" />。</param>
    /// <returns>若 index 名稱無效則為 <see langword="true" />；否則為 <see langword="false" />。</returns>
    public static bool TryGetValidationError(string? indexName, out string? errorMessage) {
        if (string.IsNullOrWhiteSpace(indexName)) {
            errorMessage = "Elasticsearch index name is required.";
            return true;
        }

        if (indexName is "." or "..") {
            errorMessage = "Elasticsearch index name cannot be '.' or '..'.";
            return true;
        }

        if (indexName[0] is '-' or '_' or '+') {
            errorMessage = "Elasticsearch index name cannot start with '-', '_' or '+'.";
            return true;
        }

        if (Encoding.UTF8.GetByteCount(indexName) > MaxIndexNameByteLength) {
            errorMessage = $"Elasticsearch index name cannot be longer than {MaxIndexNameByteLength} bytes.";
            return true;
        }

        foreach (char current in indexName) {
            if (char.IsUpper(current)) {
                errorMessage = "Elasticsearch index name must be lowercase.";
                return true;
            }

            if (InvalidCharacters.Contains(current)) {
                errorMessage = $"Elasticsearch index name cannot contain '{current}'.";
                return true;
            }
        }

        errorMessage = null;
        return false;
    }
}
