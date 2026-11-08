# OpenTelemetry

ElasticsearchDocumentKit 透過 `ActivitySource` 發出與文件操作相關的 Activity。套件不綁定特定 exporter，宿主可將同一個 source 接到既有的 OpenTelemetry、Application Insights 或其他 tracing 管線。

| 方法 | 語意 |
| --- | --- |
| `ActivitySourceName` | 取得可供 tracing provider 註冊的來源名稱 |
| `ActivitySource` | 取得套件共用的 `ActivitySource` 執行個體 |
| `StartActivity` | 由套件內部建立操作 span |
| `IsSuccessful` | 對應結果型別的成功狀態標記 |
| `ResponseSummary` | 提供官方 client 的診斷摘要 |

## 註冊來源名稱

`ElasticsearchDocumentsTelemetry.ActivitySourceName` 是穩定的來源識別。使用常數可避免宿主與套件因字串拼寫差異而沒有連接到同一個 source。

```csharp
using CloudyWing.ElasticsearchDocumentKit;
using System.Diagnostics;

string sourceName = ElasticsearchDocumentsTelemetry.ActivitySourceName;
ActivitySource activitySource = ElasticsearchDocumentsTelemetry.ActivitySource;

Console.WriteLine(sourceName);
```

應用程式不需要自行重新建立相同名稱的 `ActivitySource`。直接使用套件提供的 source 可保留同一個 instrumentation 身分。

## 連接 OpenTelemetry provider

宿主可在既有 service registration 中加入 source。exporter、sampling 與 resource 設定屬於應用程式責任，應依部署平台與資料保留政策選擇。

```csharp
using CloudyWing.ElasticsearchDocumentKit;
using Microsoft.Extensions.DependencyInjection;

IServiceCollection services = new ServiceCollection();
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddSource(ElasticsearchDocumentsTelemetry.ActivitySourceName)
        .AddOtlpExporter()
    );
```

若 tracing provider 沒有註冊 source，`ActivitySource` 仍會存在，但不會產生 Activity 供 listener 收集。這通常表示監控設定尚未涵蓋套件來源。

## 操作名稱

套件對常見操作建立可辨識的 span 名稱。這些名稱適合用於儀表板分組與告警條件。

| 操作 | Span 名稱 |
| --- | --- |
| 單筆寫入 | `elasticsearch.index` |
| 單筆建立 | `elasticsearch.create` |
| 單筆讀取 | `elasticsearch.get` |
| 單筆更新 | `elasticsearch.update` |
| 單筆 script 更新 | `elasticsearch.update_by_script` |
| 單筆刪除 | `elasticsearch.delete` |
| 條件式更新 | `elasticsearch.update_by_query` |
| 條件式刪除 | `elasticsearch.delete_by_query` |
| 筆數計算 | `elasticsearch.count` |
| Bulk 寫入 | `elasticsearch.bulk_index` |
| 一般查詢 | `elasticsearch.search` |
| from/size 分頁查詢 | `elasticsearch.search_page` |
| Search-After 查詢 | `elasticsearch.search_after` |
| Index 初始化 | `elasticsearch.index_initialize` |
| Alias 切換 | `elasticsearch.alias_switch` |

span 名稱表示套件操作，不代表 Elasticsearch transport 一定成功。套件不設定 Activity status，操作結果以 `elasticsearch.success` tag 與結果物件的成功欄位判斷。

Bulk 寫入被呼叫端取消時，`BulkIndexAsync` 直接擲出 `OperationCanceledException`，Activity 只會帶有開始時設定的 tags，不會有 `elasticsearch.success`、`elasticsearch.succeeded_count` 與 `elasticsearch.failed_count`。

## 讀取結果與記錄

應用程式可在操作邊界，依自身的記錄策略記錄結果型別的內容。錯誤摘要應寫入受控的內部記錄，避免直接暴露給外部使用者。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

DocumentOperationResult result = await store.IndexAsync(order, cancellationToken: cancellationToken)
    .ConfigureAwait(false);

if (!result.IsSuccessful) {
    logger.LogWarning(
        "Elasticsearch write failed. {ErrorType}: {ErrorReason}. {ResponseSummary}",
        result.ErrorType,
        result.ErrorReason,
        result.ResponseSummary
    );
}
```

`BulkOperationResult` 的 `SucceededCount`、`FailedCount` 與 `Failures` 適合補充批次工作指標。以彙總計數記錄，可避免為每筆文件各寫一筆 log。

## 常用 Tags

套件會依操作提供下列 tags。部分 tags 只有在相應操作或結果可取得時才會出現。

- `db.system`
- `db.operation`
- `elasticsearch.index`
- `elasticsearch.document_count`
- `elasticsearch.success`
- `elasticsearch.chunk_count`
- `elasticsearch.succeeded_count`
- `elasticsearch.failed_count`
- `error.type`

`elasticsearch.document_count` 的語意依操作而不同。單筆操作固定為 1。條件式更新與刪除在開始時設為 0，完成後改為結果的 `Updated` 或 `Deleted`。筆數計算在完成後設為 `Count`，一般查詢與 from/size 分頁查詢在完成後設為回傳的 `Documents` 筆數，不包含 `TotalCount`。

Tags 中的 Index 名稱可能包含租戶、環境或業務識別資訊。傳送 telemetry 前，應確認 exporter 的遮罩與保留政策符合組織的資料分類規則。

## 在測試中收集 Activity

測試可以使用 `ActivityListener` 驗證 source 有可訂閱的 Activity。listener 的生命週期應限於測試範圍，避免影響其他平行測試。

```csharp
using CloudyWing.ElasticsearchDocumentKit;
using System.Diagnostics;

using ActivityListener listener = new() {
    ShouldListenTo = source => source.Name == ElasticsearchDocumentsTelemetry.ActivitySourceName,
    Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
};

ActivitySource.AddActivityListener(listener);
```

測試應驗證可觀察的名稱、必要 tags 與失敗旗標，而不是依賴 Activity 的內部實作順序。這能讓 instrumentation 在重構後仍保有同一契約。

## 效能與取捨

採樣率決定追蹤資料量。高流量 bulk 工作若採全量追蹤，可能產生大量資料；可只保留錯誤與慢操作的追蹤，並以 metrics 補足整體吞吐量。

```csharp
using CloudyWing.ElasticsearchDocumentKit.Search;

SearchAfterPage<OrderDocument> page = await store.SearchAfterAsync(request, cancellationToken).ConfigureAwait(false);

if (!page.IsSuccessful) {
    logger.LogWarning("Search-after failed with {ErrorType}", page.ErrorType);
}
```

追蹤結果與功能結果是互補資訊。操作結果決定控制流程，Activity 用於跨服務診斷與趨勢分析。

## 延伸閱讀

- [Document Store](document-store.md)
- [Bulk 寫入](bulk-indexing.md)
- [Index 命名與初始化](index-management.md)
