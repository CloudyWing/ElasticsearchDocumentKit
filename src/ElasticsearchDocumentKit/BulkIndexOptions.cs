namespace CloudyWing.ElasticsearchDocumentKit;

/// <summary>
/// 表示 bulk 寫入的進階設定。
/// </summary>
/// <typeparam name="TDocument">文件型別。</typeparam>
public sealed class BulkIndexOptions<TDocument>
    where TDocument : class {
    /// <summary>
    /// 取得或設定每批寫入筆數。
    /// </summary>
    /// <value>每批寫入筆數。預設為 500。</value>
    public int ChunkSize { get; init; } = 500;

    /// <summary>
    /// 取得或設定整批請求失敗時的重試次數。
    /// </summary>
    /// <value>重試次數。預設為 0。</value>
    public int MaxRetryAttempts { get; init; }

    /// <summary>
    /// 取得或設定重試等待時間計算方式。
    /// </summary>
    /// <value>輸入值為目前重試次數，從 1 開始。未設定時使用 200ms 乘以重試次數。</value>
    public Func<int, TimeSpan>? RetryDelayProvider { get; init; }

    /// <summary>
    /// 取得或設定單筆寫入失敗時的 dead-letter callback。
    /// </summary>
    public Func<BulkDeadLetter<TDocument>, CancellationToken, Task>? DeadLetterCallback { get; init; }

    /// <summary>
    /// 取得或設定一個值，用以指出是否允許在文件模型未設定 IdSelector 時啟用重試。
    /// </summary>
    /// <value>
    /// 若要在未設定 IdSelector 時仍允許重試，則為 <see langword="true"/>；否則為 <see langword="false"/>。預設為 <see langword="false"/>。
    /// 未設定 IdSelector 而重試時，沒有固定 _id 的文件可能因整批重送而重複寫入，僅適用於可接受 at-least-once 的呼叫端。
    /// </value>
    public bool AllowRetryWithoutIdSelector { get; init; }

    /// <summary>
    /// 驗證目前設定是否可用。
    /// </summary>
    /// <exception cref="InvalidOperationException">設定值不符合工具庫需求時。</exception>
    public void Validate() {
        if (ChunkSize < 1) {
            throw new InvalidOperationException("Bulk chunk size must be greater than zero.");
        }

        if (MaxRetryAttempts < 0) {
            throw new InvalidOperationException("Bulk max retry attempts must be greater than or equal to zero.");
        }
    }

    internal void ValidateDocumentIdSelector(Func<TDocument, string?>? idSelector) {
        if (MaxRetryAttempts > 0 && idSelector is null && !AllowRetryWithoutIdSelector) {
            throw new InvalidOperationException(
                "Bulk retry requires a document model IdSelector. "
                + "Set AllowRetryWithoutIdSelector to true to accept at-least-once delivery."
            );
        }
    }

    internal TimeSpan GetRetryDelay(int retryAttempt) {
        return RetryDelayProvider?.Invoke(retryAttempt) ?? TimeSpan.FromMilliseconds(200 * retryAttempt);
    }
}
