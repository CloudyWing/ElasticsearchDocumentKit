# Document Store

`ElasticsearchDocumentStore<TDocument>` 封裝常見的文件寫入、讀取、部分更新、刪除、條件式更新與刪除、筆數計算、一般查詢、分頁、批次寫入與 Search-After 查詢。它保留 Elasticsearch 的結果資訊，呼叫端可以用結果型別判斷重試、告警或回應內容。

| 方法 | 語意 |
| --- | --- |
| `IndexAsync` | 建立或覆寫一筆文件，可帶入樂觀鎖與 refresh 選項 |
| `CreateAsync` | 只在文件 ID 不存在時建立文件 |
| `GetAsync` | 讀取指定 ID 的文件，並帶回 `SeqNo` 與 `PrimaryTerm` |
| `UpdatePartialAsync` | 以 partial document 更新欄位，可帶入樂觀鎖、衝突重試與 refresh 選項 |
| `UpdateByScriptAsync` | 以 script 更新指定 ID 的文件 |
| `DeleteAsync` | 刪除指定 ID 的文件，可帶入 refresh 選項 |
| `UpdateByQueryAsync` | 依條件更新符合的文件 |
| `DeleteByQueryAsync` | 依條件刪除符合的文件 |
| `CountAsync` | 依條件計算符合的文件筆數 |
| `SearchAsync` | 依條件取得文件清單，可指定筆數上限 |
| `SearchPageAsync` | 以頁碼與每頁筆數取得指定頁的文件與總筆數 |
| `BulkIndexAsync` | 以同步或非同步來源寫入多筆文件 |
| `SearchAfterAsync` | 依排序游標讀取下一頁 |

## 建立 Store

Store 需要官方 `ElasticsearchClient` 與文件模型解析器。生產環境通常注入 `IElasticsearchDocumentStore<TDocument>`，直接建立則適合小型工具或組裝測試。

```csharp
using CloudyWing.ElasticsearchDocumentKit;
using CloudyWing.ElasticsearchDocumentKit.Modeling;

IElasticsearchDocumentModelResolver models = new DefaultElasticsearchDocumentModelResolver(
    options,
    indexNames
);
IElasticsearchDocumentStore<OrderDocument> store = new ElasticsearchDocumentStore<OrderDocument>(
    client,
    models
);
```

Store 會從 `ElasticsearchDocumentModel<TDocument>` 讀取 Index 名稱與 ID 選取器。建立 Store 不會執行網路 I/O，也不會隱性建立 Index。

## 單筆寫入與讀取

`IndexAsync` 可使用模型中的 ID 選取器，也可以明確傳入 ID。明確 ID 適合資料來源與文件物件採不同鍵值的情境。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

OrderDocument order = new() {
    Id = "ORD-001",
    CustomerName = "CloudyWing",
    TotalAmount = 1200m,
    CreatedAt = DateTimeOffset.UtcNow
};

DocumentOperationResult indexed = await store.IndexAsync(order, cancellationToken: cancellationToken)
    .ConfigureAwait(false);
DocumentGetResult<OrderDocument> loaded = await store.GetAsync(order.Id, cancellationToken).ConfigureAwait(false);
```

`DocumentGetResult<TDocument>.Found` 與 `IsSuccessful` 是不同訊號。文件不存在時 `Found` 為 `false`。是否視為正常流程，由應用程式對 404 的處理策略決定。

### 只在文件不存在時建立

`CreateAsync` 對應 Elasticsearch 的 `op_type=create`。文件 ID 的決定順序與 `IndexAsync` 相同，依序為明確傳入的 ID、文件模型的 ID 選取器，兩者皆無值時由 Elasticsearch 自動產生。文件 ID 已存在時，結果的 `IsSuccessful` 為 `false`，`StatusCode` 為 409，既有文件維持原內容。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

DocumentOperationResult created = await store.CreateAsync(order, cancellationToken: cancellationToken)
    .ConfigureAwait(false);

if (!created.IsSuccessful && created.StatusCode == 409) {
    logger.LogInformation("Order {DocumentId} already exists.", created.DocumentId);
}
```

### 以 SeqNo 與 PrimaryTerm 控制並行寫入

`GetAsync` 的結果帶有文件目前的 `SeqNo` 與 `PrimaryTerm`。將這組值傳入 `IndexDocumentOptions` 的 `IfSeqNo` 與 `IfPrimaryTerm`，Elasticsearch 只在文件讀取後沒有被修改過時，才接受寫入。單筆寫入的 `DocumentOperationResult` 也帶有寫入後的 `SeqNo` 與 `PrimaryTerm`，連續寫入同一筆文件時不必每次重新讀取。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

DocumentGetResult<OrderDocument> loaded = await store.GetAsync(order.Id, cancellationToken).ConfigureAwait(false);

if (loaded.Document is not null && loaded.SeqNo is long seqNo && loaded.PrimaryTerm is long primaryTerm) {
    DocumentOperationResult saved = await store.IndexAsync(
        loaded.Document,
        order.Id,
        new IndexDocumentOptions {
            IfSeqNo = seqNo,
            IfPrimaryTerm = primaryTerm
        },
        cancellationToken
    ).ConfigureAwait(false);
}
```

`SeqNo` 與 `PrimaryTerm` 的型別為 `long?`。找不到文件或回應沒有帶出這兩個值時為 `null`，因此範例以 `is long` 判斷後才傳入。`IfSeqNo` 與 `IfPrimaryTerm` 必須同時設定，只設定其中一個時會擲出 `InvalidOperationException`，請求不會送出。文件在讀取之後被其他寫入修改時，結果的 `IsSuccessful` 為 `false`，`StatusCode` 為 409，`ErrorType` 為 `version_conflict_engine_exception`。

使用樂觀鎖時，請明確指定文件 ID，或確認文件模型的 `IdSelector` 能解析出 ID。Store 在送出前只檢查選項本身的組合，不檢查 ID 是否存在。

## 更新與刪除

部分更新接收任意 `TPartialDocument`。只放入要變更的欄位，避免將未載入的欄位寫回預設值。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

DocumentOperationResult updated = await store.UpdatePartialAsync(
    order.Id,
    new { CustomerName = "Updated Customer" },
    cancellationToken
).ConfigureAwait(false);

DocumentOperationResult deleted = await store.DeleteAsync(order.Id, cancellationToken).ConfigureAwait(false);
```

`DocumentOperationResult.Operation` 說明實際操作類型。`Version` 是文件的版本計數，適合記錄與除錯。需要樂觀並行控制時使用 `SeqNo` 與 `PrimaryTerm`。

### 更新時的樂觀鎖與衝突重試

`UpdatePartialAsync` 的 `UpdateDocumentOptions` 可設定 `IfSeqNo`、`IfPrimaryTerm` 與 `RetryOnConflict`。`IfSeqNo` 與 `IfPrimaryTerm` 的用法和 `IndexAsync` 相同，版本不符時回傳狀態碼 409 的失敗結果。

`RetryOnConflict` 預設為 0，表示發生版本衝突時不重試。設定大於 0 的值時，Elasticsearch 在發生版本衝突後最多重試指定次數，適合只修改特定欄位，且更新內容不取決於讀取當下文件內容的情境。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

DocumentOperationResult retried = await store.UpdatePartialAsync(
    order.Id,
    new { CustomerName = "Updated Customer" },
    new UpdateDocumentOptions {
        RetryOnConflict = 3
    },
    cancellationToken
).ConfigureAwait(false);
```

`RetryOnConflict` 大於 0 時不可同時設定 `IfSeqNo` 與 `IfPrimaryTerm`，Elasticsearch 不接受兩者並用。這個組合會使 `UpdatePartialAsync` 擲出 `InvalidOperationException`，請求不會送出。`UpdateByScriptAsync` 與 `DeleteAsync` 只接受 `DocumentWriteOptions`，不提供樂觀鎖與衝突重試，並行寫入時以最後寫入為準。

### 以 script 更新單筆文件

`UpdateByScriptAsync` 以 Update API 對指定 ID 的文件執行 script，回傳的 `DocumentOperationResult.Operation` 為 `update_by_script`。文件不存在時，結果的 `IsSuccessful` 為 `false`，`StatusCode` 為 404，不會建立文件。

```csharp
using CloudyWing.ElasticsearchDocumentKit;
using Elastic.Clients.Elasticsearch;

DocumentOperationResult scripted = await store.UpdateByScriptAsync(
    order.Id,
    new Script {
        Source = "ctx._source.totalAmount += params.delta",
        Params = new Dictionary<string, object> { ["delta"] = 100 }
    },
    cancellationToken: cancellationToken
).ConfigureAwait(false);
```

動態的值放在 `Params`，`Source` 保持固定字串。若把值串接進 `Source`，Elasticsearch 會為每個不同的值重新編譯 script，輸入內容也會被當成 script 執行。

### 單筆寫入的 refresh 選項

單筆寫入預設不送出 refresh 參數，寫入的文件何時可被查詢由 index 的 refresh 設定決定。`DocumentWriteOptions`、`IndexDocumentOptions` 與 `UpdateDocumentOptions` 的 `Refresh` 屬性可逐次指定官方 `Refresh` 值，`CreateAsync`、`UpdateByScriptAsync` 與 `DeleteAsync` 使用 `DocumentWriteOptions`。

```csharp
using CloudyWing.ElasticsearchDocumentKit;
using Elastic.Clients.Elasticsearch;

DocumentOperationResult removed = await store.DeleteAsync(
    order.Id,
    new DocumentWriteOptions {
        Refresh = Refresh.WaitFor
    },
    cancellationToken
).ConfigureAwait(false);
```

`Refresh.True` 在寫入後立即 refresh，`Refresh.WaitFor` 等到下一次 refresh 完成才回應。兩者都讓寫入結果在回應後立即可被查詢，但每次寫入的成本會增加。大量寫入時使用預設值或 `BulkIndexAsync`，只在後續流程立即需要查詢結果時才逐次指定。

## 判斷操作結果

所有單筆操作的結果都保留 transport 與產品錯誤摘要。應用程式可以集中處理暫時性故障與無法重試的請求錯誤。

```csharp
if (!indexed.IsSuccessful) {
    logger.LogWarning(
        "Elasticsearch {Operation} failed for {DocumentId}. {ErrorType}: {ErrorReason}",
        indexed.Operation,
        indexed.DocumentId,
        indexed.ErrorType,
        indexed.ErrorReason
    );
}

if (loaded.IsSuccessful && loaded.Found && loaded.Document is not null) {
    Process(loaded.Document);
}
```

`ResponseSummary` 來自官方 client 的診斷摘要。它適合寫入受控的診斷記錄，不應未經過濾直接回傳給外部使用者。

## 條件式更新、刪除與計數

`UpdateByQueryAsync` 與 `DeleteByQueryAsync` 對符合條件的文件批次更新或刪除，`CountAsync` 計算符合條件的文件筆數。查詢條件使用 `ElasticsearchQueryFilterBuilder<TDocument>`，需要官方 descriptor 才能表達的設定則放在 `ConfigureRequest`。

```csharp
using CloudyWing.ElasticsearchDocumentKit;
using CloudyWing.ElasticsearchDocumentKit.Querying;
using Elastic.Clients.Elasticsearch;

ElasticsearchQueryFilterBuilder<OrderDocument> filters = new();
filters.TermKeyword(document => document.CustomerName, "CloudyWing");

ByQueryOperationResult updateResult = await store.UpdateByQueryAsync(
    new UpdateByQueryRequest<OrderDocument> {
        Filters = filters,
        Script = new Script { Source = "ctx._source.totalAmount = 0" }
    },
    cancellationToken
).ConfigureAwait(false);

ByQueryOperationResult deleteResult = await store.DeleteByQueryAsync(
    new DeleteByQueryRequest<OrderDocument> { Filters = filters },
    cancellationToken
).ConfigureAwait(false);

DocumentCountResult countResult = await store.CountAsync(filters, cancellationToken).ConfigureAwait(false);
```

`ByQueryOperationResult` 以 `Updated` 或 `Deleted` 回報處理筆數，以 `VersionConflicts` 回報版本衝突筆數，`Operation` 為 `update_by_query` 或 `delete_by_query`。Elasticsearch 回應的 `failures` 有內容時，`IsSuccessful` 為 `false`，`ErrorType` 與 `ErrorReason` 取自第一筆失敗。`UpdateByQueryRequest<TDocument>.Script` 可省略，省略時 Elasticsearch 只重新索引符合條件的文件。

`UpdateByQueryRequest<TDocument>.Refresh` 與 `DeleteByQueryRequest<TDocument>.Refresh` 預設為 `true`，套件另外送出 `wait_for_completion=true`。Elasticsearch 在操作完成並 refresh 後才回應，因此結果可以立即透過 `CountAsync` 或查詢取得。

`CountAsync` 只接受 filter builder。builder 沒有任何條件時會計算 index 內的全部文件，要計算全部文件時，傳入尚未加入條件的 builder 即可。`filters` 為 `null` 時擲出 `ArgumentNullException`。計數失敗時 `DocumentCountResult.IsSuccessful` 為 `false`，`Count` 為 0。

### 沒有查詢條件的處理

`UpdateByQueryAsync` 與 `DeleteByQueryAsync` 在 `Filters` 沒有任何條件，且 `ConfigureRequest` 沒有指定 query 時，擲出 `ArgumentException`，請求不會送出。這個限制可避免漏寫條件時，操作套用到整個 index。確實要處理全部文件時，在 `ConfigureRequest` 明確指定 `match_all`。

```csharp
using CloudyWing.ElasticsearchDocumentKit;
using Elastic.Clients.Elasticsearch;

ByQueryOperationResult archiveResult = await store.UpdateByQueryAsync(
    new UpdateByQueryRequest<OrderDocument> {
        Script = new Script { Source = "ctx._source.isArchived = true" },
        ConfigureRequest = descriptor => descriptor.Query(query => query.MatchAll())
    },
    cancellationToken
).ConfigureAwait(false);
```

`Filters` 沒有條件而 `ConfigureRequest` 有提供時，套件為了確認結果含有頂層 query，會先呼叫一次 `ConfigureRequest`，送出請求時再呼叫一次，因此該委派會被呼叫兩次。委派內不可有副作用，例如累加計數器或寫入記錄。`Filters` 有條件時，`ConfigureRequest` 只呼叫一次。

### 版本衝突

兩個方法預設使用 Elasticsearch 的 `conflicts=abort`。在 `ConfigureRequest` 設定 `Conflicts.Proceed` 後，發生版本衝突的文件會被略過，其餘文件照常處理，衝突筆數累計在 `VersionConflicts`。

```csharp
using CloudyWing.ElasticsearchDocumentKit;
using Elastic.Clients.Elasticsearch;

ByQueryOperationResult proceedResult = await store.UpdateByQueryAsync(
    new UpdateByQueryRequest<OrderDocument> {
        Filters = filters,
        Script = new Script { Source = "ctx._source.totalAmount = 0" },
        ConfigureRequest = descriptor => descriptor.Conflicts(Conflicts.Proceed)
    },
    cancellationToken
).ConfigureAwait(false);
```

衝突筆數的保證範圍如下。

- 沒有衝突時，`abort` 與 `Conflicts.Proceed` 兩種模式都回傳成功結果，`Updated`（或 `Deleted`）為實際處理的筆數，`VersionConflicts` 為 0，`StatusCode` 為 200。
- Elasticsearch 回應中的 `updated`、`deleted` 與 `version_conflicts` 依名稱對應到結果的 `Updated`、`Deleted` 與 `VersionConflicts`，回應缺值時為 0。
- `abort` 模式遇到真實的版本衝突時，請求會被 Elasticsearch 中止，`Updated`、`Deleted` 與 `VersionConflicts` 的值取決於該次回應的內容。套件不保證這三個欄位在中止時的精確性，呼叫端應先以 `IsSuccessful` 與 `StatusCode` 判斷結果。
- 需要在結果中取得衝突筆數時使用 `Conflicts.Proceed`。

### 覆寫 wait_for_completion

`ConfigureRequest` 在套件的預設設定之後執行，可以覆寫 `wait_for_completion`。覆寫為 `false` 時，Elasticsearch 的回應只含 task 資訊，結果的 `Updated`、`Deleted` 與 `VersionConflicts` 皆為 0，`IsSuccessful` 只表示請求已被接受，不表示文件已處理完成。需要處理結果的呼叫端應保留預設值。

## 一般查詢與 from/size 分頁

`SearchAsync` 依 filter 條件取得文件清單，`SearchPageAsync` 以頁碼與每頁筆數取得指定頁的文件與總筆數。兩者都可以在 `ConfigureSearch` 使用官方 descriptor 設定排序或 `_source` 篩選，該委派在套件自身的設定之後執行，可以覆寫前面的設定。

```csharp
using CloudyWing.ElasticsearchDocumentKit.Search;
using Elastic.Clients.Elasticsearch;

DocumentSearchResult<OrderDocument> found = await store.SearchAsync(
    new DocumentSearchRequest<OrderDocument> {
        Filters = filters,
        Size = 50
    },
    cancellationToken
).ConfigureAwait(false);

DocumentPageResult<OrderDocument> page = await store.SearchPageAsync(
    new DocumentPageRequest<OrderDocument> {
        PageNumber = 3,
        PageSize = 20,
        Filters = filters,
        ConfigureSearch = descriptor => descriptor.Sort(
            sort => sort.Field(document => document.CreatedAt, SortOrder.Desc)
        )
    },
    cancellationToken
).ConfigureAwait(false);
```

### SearchAsync 的筆數

`DocumentSearchRequest<TDocument>.Size` 未指定時，套件不送出 `size`，由 Elasticsearch 使用預設的 10 筆。需要更多筆數時明確指定 `Size`。`Size` 有指定時必須大於或等於 1，否則擲出 `ArgumentOutOfRangeException`。`Filters` 沒有任何條件時不送出 query，查詢 index 內的全部文件。查詢失敗時 `IsSuccessful` 為 `false`，`Documents` 為空。

### SearchPageAsync 的頁碼與總筆數

`PageNumber` 從 1 開始，套件送出 `from = (PageNumber - 1) * PageSize` 與 `size = PageSize`。`PageNumber` 或 `PageSize` 小於 1，或計算出的略過筆數（`from`）超過 `int.MaxValue` 時，擲出 `ArgumentOutOfRangeException`。

`DocumentPageResult<TDocument>` 包含該頁的 `Documents`、`PageNumber`、`PageSize` 與 `long?` 型別的 `TotalCount`。`TrackTotalHits` 預設為 `true`，送出 `track_total_hits: true`，`TotalCount` 為精確的符合筆數。設為 `false` 可省去精確計數的成本，此時 `TotalCount` 為 `null`。

頁碼超出資料範圍但仍在 `max_result_window` 之內時，結果為成功的空頁，`Documents` 為空，`TotalCount` 仍是真實的總筆數。

### max_result_window

Elasticsearch 的 `index.max_result_window` 預設為 10000，限制 `from + size` 的上限。超過上限的請求會被 Elasticsearch 拒絕，結果的 `IsSuccessful` 為 `false`，`ErrorType` 為 `search_phase_execution_exception`，`ErrorReason` 為 `all shards failed`，`TotalCount` 為 `null`，`Documents` 為空。需要讀取超過上限的資料時，改用 `SearchAfterAsync`，不建議以調高 `max_result_window` 解決。

### 與 Search-After 的選擇

from/size 可以直接跳到任一頁，適合資料量不大、需要頁碼導覽的畫面。不同頁的請求之間不保證讀到同一份快照，查詢期間資料異動可能造成重複或遺漏。逐頁讀取大量資料或需要穩定順序時，使用下一節的 Search-After。兩種分頁的總筆數語意不同，`SearchPageAsync` 預設精確計數，`SearchAfterPage<TDocument>.TotalCount` 則取決於 Elasticsearch 的回應。

## Search-After 分頁

Search-After 以最後一筆的排序值作為下一頁游標。排序欄位順序必須與 `SearchAfter` 的值順序相同，並加入穩定的唯一欄位。

```csharp
using CloudyWing.ElasticsearchDocumentKit.Search;
using Elastic.Clients.Elasticsearch;

SearchAfterRequest<OrderDocument> request = new() {
    Size = 100,
    SearchAfter = previousPage?.NextSearchAfter ?? [],
    ConfigureSearch = descriptor => descriptor.Sort(
        sort => sort.Field(document => document.CreatedAt, SortOrder.Asc),
        sort => sort.Field(document => document.Id, SortOrder.Asc)
    )
};

SearchAfterPage<OrderDocument> page = await store.SearchAfterAsync(request, cancellationToken).ConfigureAwait(false);
```

`SearchAfterPage<TDocument>.TotalCount` 由 Elasticsearch 回應提供。若查詢未要求完整 hits total，應用程式不應將其當作精確的全部資料筆數。

## 操作邊界

Store 不包裝完整的 Elasticsearch query DSL，也不自動建立 Index。複雜查詢可在 `ConfigureSearch` 使用官方 descriptor，Index 初始化則交由 `IIndexInitializer<TDocument>`。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

ElasticsearchDocumentStore<OrderDocument> concreteStore = new(client, models);
Task<BatchOutcome> writeTask = SynchronizeOrdersAsync(concreteStore, cancellationToken);

async Task<BatchOutcome> SynchronizeOrdersAsync(
    IElasticsearchDocumentStore<OrderDocument> target,
    CancellationToken token
) {
    BulkOperationResult result = await target.BulkIndexAsync(orderStream, token).ConfigureAwait(false);
    return new BatchOutcome(result.SucceededCount, result.FailedCount);
}
```

Bulk 細節見 [Bulk 寫入](bulk-indexing.md)，查詢條件見 [查詢條件擴充](query-filters.md)。

## 延伸閱讀

- [文件模型設定](document-model.md)
- [Bulk 寫入](bulk-indexing.md)
- [查詢條件擴充](query-filters.md)
- [Index 命名與初始化](index-management.md)
