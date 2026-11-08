# Bulk 寫入

`BulkIndexAsync` 將多筆文件分批送往 Elasticsearch，並回傳整體計數與每筆失敗摘要。呼叫端可選擇同步集合或 `IAsyncEnumerable<TDocument>` 來源，避免為大量資料建立不必要的記憶體副本。

| 方法 | 語意 |
| --- | --- |
| `BulkIndexAsync(IEnumerable<T>)` | 從同步來源分批寫入 |
| `BulkIndexAsync(IAsyncEnumerable<T>)` | 從串流來源分批寫入 |
| `Validate` | 驗證 chunk 與重試選項 |
| `AllowRetryWithoutIdSelector` | 在未設定 `IdSelector` 時放行重試，表示接受 at-least-once 寫入 |
| `DeadLetterCallback` | 接收無法完成寫入的文件與錯誤摘要 |
| `Failures` | 取得每筆失敗文件的資訊，含 item 失敗與整批失敗 |

## 基本寫入

沒有額外選項時，Store 使用預設設定處理文件。結果中的成功與失敗數都以呼叫端送入的文件為準。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

IReadOnlyList<OrderDocument> orders = GetOrders();

BulkOperationResult result = await store.BulkIndexAsync(orders, cancellationToken).ConfigureAwait(false);

if (result.HasErrors) {
    logger.LogWarning("Bulk indexing completed with {FailedCount} failures", result.FailedCount);
}
```

`IsSuccessful` 只有在每一批回應都有效，且沒有任何一筆文件失敗時才為 `true`，任一 item 失敗都會使它成為 `false`。`HasErrors` 在回應無效、Elasticsearch 回報 `errors`，或有任一筆失敗時為 `true`。一般流程以 `IsSuccessful` 判斷是否全部寫入成功，再依 `Failures` 處理失敗文件。

## 設定 chunk 與重試

`BulkIndexOptions<TDocument>` 控制每批大小、整批失敗時的重試次數與等待時間。選項物件可在呼叫前明確驗證，讓錯誤提早出現在同步工作開始前。`Validate` 只檢查選項本身的值，重試所需的 `IdSelector` 前提要到 `BulkIndexAsync` 解析文件模型後才檢查。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

BulkIndexOptions<OrderDocument> options = new() {
    ChunkSize = 500,
    MaxRetryAttempts = 2,
    RetryDelayProvider = attempt => TimeSpan.FromMilliseconds(200 * attempt)
};

options.Validate();
BulkOperationResult result = await store.BulkIndexAsync(orders, options, cancellationToken).ConfigureAwait(false);
```

重試適用於整批請求失敗或傳輸例外。Elasticsearch 成功接收 bulk request 但部分 item 失敗時，失敗 item 會留在 `Failures`，不會觸發整批重送。

整批失敗且不再重試時（未啟用重試，或重試次數已用完），該批的每份文件都會成為一筆 `Failures` 並交給 `DeadLetterCallback`。無效回應的 `StatusCode` 為 HTTP 狀態碼；傳輸例外沒有 HTTP 回應，`StatusCode` 為 0，`ErrorType` 為例外型別名稱。

啟用重試（`MaxRetryAttempts` 大於 0）需要文件模型設定 `IdSelector`，讓同一文件在整批重送時使用相同的 `_id`。未設定 `IdSelector` 時，`BulkIndexAsync` 會在送出任何請求前擲出 `InvalidOperationException`。

官方 client 在預設設定下會讀取名為 `Id` 的屬性推斷 `_id`。這項推斷可由呼叫端的 client 設定停用或改指其他屬性，`Id` 為 null 或空字串的文件也仍由伺服器產生 `_id`。Store 無法得知呼叫端的 client 設定，也無法在送出請求前檢查串流來源的每一筆文件，因此只以文件模型是否宣告 `IdSelector` 判定。文件型別已有 `Id` 屬性時，以 `HasId(order => order.Id)` 宣告即可啟用重試。

若呼叫端可接受 at-least-once 寫入，也就是沒有固定 `_id` 的文件可能因整批重送而重複寫入，可將 `AllowRetryWithoutIdSelector` 設為 `true` 明確放行。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

BulkIndexOptions<OrderDocument> options = new() {
    MaxRetryAttempts = 2,
    AllowRetryWithoutIdSelector = true
};
```

## 取消寫入

呼叫端取消權杖後，`BulkIndexAsync` 擲出 `OperationCanceledException`，不會把取消回報成 `BulkOperationResult` 的失敗結果。取消不會觸發重試，取消本身也不會產生 dead letter。

取消發生在 bulk request 送出之後、而 Elasticsearch 已回傳部分 item 失敗時，這些失敗是 Elasticsearch 實際回報的結果，仍會交給 `DeadLetterCallback`，callback 收到的權杖此時已是取消狀態。callback 若以該權杖執行耐久寫入，取消會中斷寫入。需要保證失敗文件落地時，callback 應改用不受呼叫端取消影響的權杖或期限。

同步來源在組下一批前即擲出 `OperationCanceledException`。串流來源依賴來源端遵守取消權杖，來源未遵守時，Store 要到下一次呼叫官方 client 時才判定取消並擲出同一例外。取消發生在最後一批回應之後時，該次呼叫正常回傳結果。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

try {
    BulkOperationResult result = await store.BulkIndexAsync(orders, options, cancellationToken).ConfigureAwait(false);
    logger.LogInformation("Bulk indexing wrote {SucceededCount} documents", result.SucceededCount);
} catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
    logger.LogInformation("Bulk indexing was cancelled by the caller");
}
```

判斷取消的依據是呼叫端權杖的狀態，不是例外型別。官方 client 的設定由應用程式掌握，取消有可能被包成其他例外型別或表達為無效回應，因此 Store 只檢查權杖。權杖已取消而原始例外是其他型別，或取消表達為帶有原始例外的無效回應時，擲出的 `OperationCanceledException` 會把原始例外保留在 `InnerException`，供診斷使用。

HTTP timeout 不屬於呼叫端取消。它以未綁定呼叫端權杖的 `TaskCanceledException` 表現，此時權杖並未取消，仍會依重試設定處理並對應為失敗結果，行為與其他傳輸例外相同。

Bulk 寫入不具交易性。取消前已送出並由 Elasticsearch 接受的批次仍然寫入成功，取消只停止後續批次。需要知道已寫入多少筆時，應在取消前自行記錄進度，或於取消後重新查詢目標 Index。

## 接收 dead letter

每個無法完成的文件都可傳入 `DeadLetterCallback`。`BulkDeadLetter<TDocument>` 包含原始文件、Index 名稱、所在批次（`ChunkIndex`）、嘗試次數（`Attempt`）與最後一次失敗資訊，適合交給耐久佇列、資料庫或檔案處理。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

BulkIndexOptions<OrderDocument> options = new() {
    ChunkSize = 250,
    DeadLetterCallback = async (BulkDeadLetter<OrderDocument> deadLetter, CancellationToken token) => {
        BulkOperationFailure failure = deadLetter.Failure;
        await deadLetterWriter.WriteAsync(
            deadLetter.IndexName,
            deadLetter.Document,
            failure.DocumentId,
            failure.ErrorType,
            token
        );
    }
};

BulkOperationResult result = await store.BulkIndexAsync(orders, options, cancellationToken).ConfigureAwait(false);
```

dead letter 接收端應具備冪等性。同步工作在重試、重啟或人工補送時可能再次交付同一文件。

## 使用串流來源

使用非同步來源時，資料庫分頁、檔案串流或外部 API 的資料可以邊讀取邊送出。不需要預先把全部資料物化為 `List<TDocument>`。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

async IAsyncEnumerable<OrderDocument> ReadOrdersAsync() {
    await foreach (OrderDocument order in source.ReadAsync(cancellationToken)) {
        yield return order;
    }
}

BulkOperationResult streamed = await store.BulkIndexAsync(
    ReadOrdersAsync(),
    new BulkIndexOptions<OrderDocument> { ChunkSize = 500 },
    cancellationToken
).ConfigureAwait(false);
```

呼叫端仍需處理來源端的例外與取消。Store 只能處理已接收到的文件，無法回補來源端尚未讀取的資料。

## 分析失敗結果

`BulkOperationFailure` 會提供 item index、操作名稱、Index、文件 ID、status code 與 Elasticsearch 錯誤資訊。這些資料可用於統計失敗原因與安排後續補送。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

foreach (BulkOperationFailure failure in result.Failures) {
    logger.LogWarning(
        "Bulk item {ItemIndex} for {DocumentId} failed with {StatusCode}. {ErrorType}: {ErrorReason}",
        failure.ItemIndex,
        failure.DocumentId,
        failure.StatusCode,
        failure.ErrorType,
        failure.ErrorReason
    );
}
```

不要將單筆 mapping 錯誤視為可立即重試的暫時性故障。先修正文件或 mapping，再由 dead letter 中的資料建立受控的補送流程。

## 設計取捨

chunk 越大，網路往返次數越少，但每次失敗需要處理的文件也越多。初始值應以文件大小、叢集負載、timeout 與可接受的重試成本決定，再透過監控調整。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

BulkIndexOptions<OrderDocument> cautiousOptions = new() {
    ChunkSize = 100,
    MaxRetryAttempts = 1,
    RetryDelayProvider = _ => TimeSpan.FromSeconds(1)
};

await store.BulkIndexAsync(orders, cautiousOptions, cancellationToken).ConfigureAwait(false);
```

寫入新版本 Index 後的 Alias 切換流程見 [Alias 與 Lifecycle 命名](alias-and-lifecycle.md)。

## 延伸閱讀

- [Document Store](document-store.md)
- [Alias 與 Lifecycle 命名](alias-and-lifecycle.md)
- [OpenTelemetry](telemetry.md)
