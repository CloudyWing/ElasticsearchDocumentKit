# 入門指南

本指南建立一個可寫入、讀取與分頁查詢的最小文件流程。應用程式保有官方 Elasticsearch client 的連線與驗證責任，ElasticsearchDocumentKit 負責文件模型與常用操作。

| 方法 | 語意 |
| --- | --- |
| `ConfigureDocument` | 宣告文件型別對應的 Index 名稱與 ID 選取器 |
| `Resolve<TDocument>` | 取得套用 prefix 後的最終 Index 名稱 |
| `InitializeAsync` | 顯式建立不存在的 Index 與其 mapping |
| `IndexAsync` | 寫入一筆文件並取得操作摘要 |
| `SearchAfterAsync` | 依穩定排序值讀取下一頁文件 |

## 安裝

在使用套件的專案目錄執行下列命令。

```powershell
dotnet add package CloudyWing.ElasticsearchDocumentKit
```

套件目標框架為 `net10.0`。連線字串、API Key 與憑證應由環境變數、Secret Manager 或部署平台設定提供。

## 定義文件型別

文件型別可以使用 `ElasticsearchIndexNameAttribute` 提供基礎 Index 名稱。若應用程式集中設定了同型別的名稱，集中設定會優先於 attribute 指定的名稱。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

[ElasticsearchIndexName("orders")]
public sealed class OrderDocument {
    public required string Id { get; init; }

    public required string CustomerName { get; init; }

    public decimal TotalAmount { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
```

文件 ID 應在來源系統中保持穩定。寫入後若改用不同的 ID，Elasticsearch 會將資料視為另一筆文件。

## 建立 client 與文件模型

`ElasticsearchDocumentsOptions` 保存文件模型定義。`DefaultIndexNameResolver` 依 prefix、集中設定與屬性名稱解析 Index。

```csharp
using CloudyWing.ElasticsearchDocumentKit;
using CloudyWing.ElasticsearchDocumentKit.Modeling;
using Elastic.Clients.Elasticsearch;

ElasticsearchClientSettings settings = new(new Uri("https://localhost:9200"));
ElasticsearchClient client = new(settings);

ElasticsearchDocumentsOptions options = new() { IndexPrefix = "commerce" };
options.ConfigureDocument<OrderDocument>(document => document
    .ToIndex("orders")
    .HasId(order => order.Id)
);

IIndexNameResolver indexNames = new DefaultIndexNameResolver(options);
IElasticsearchDocumentModelResolver documentModels = new DefaultElasticsearchDocumentModelResolver(
    options,
    indexNames
);
```

`IIndexNameResolver` 與 `IElasticsearchDocumentModelResolver` 可替換為應用程式的實作。自訂實作應確保同一文件型別在同一次執行期間，每次都解析出相同的名稱與 ID 規則。

## 顯式初始化與寫入

Index 初始化不會在 Store 建構時隱性發生。將初始化安排在部署、管理命令或資料同步前置步驟，mapping 變更與失敗重試才容易觀察與追蹤。

```csharp
using CloudyWing.ElasticsearchDocumentKit.Indexing;

IIndexInitializer<OrderDocument> initializer = new ElasticsearchIndexInitializer<OrderDocument>(
    client,
    documentModels
);
IndexInitializationResult initialization = await initializer.InitializeAsync(cancellationToken).ConfigureAwait(false);

if (!initialization.IsSuccessful) {
    throw new InvalidOperationException(initialization.ErrorReason);
}
```

初始化成功後，使用 Store 寫入與讀取文件。`IElasticsearchDocumentStore<TDocument>` 適合注入應用程式服務，`ElasticsearchDocumentStore<TDocument>` 適合直接組裝的小型程式或測試。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

IElasticsearchDocumentStore<OrderDocument> store = new ElasticsearchDocumentStore<OrderDocument>(
    client,
    documentModels
);

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

`DocumentOperationResult` 與 `DocumentGetResult<TDocument>` 將操作是否成功、HTTP status code 與 Elasticsearch 錯誤摘要放在同一結果物件。呼叫端可依 `IsSuccessful`、`ErrorType` 與 `ErrorReason` 決定重試或回應使用者。

## 讀取下一頁

Search-After 適合持續同步與深分頁。排序欄位必須穩定，最後一個排序欄位通常應是唯一識別欄位，避免相同排序值造成資料遺漏或重複。

```csharp
using CloudyWing.ElasticsearchDocumentKit.Search;
using Elastic.Clients.Elasticsearch;

SearchAfterRequest<OrderDocument> request = new() {
    Size = 100,
    ConfigureSearch = descriptor => descriptor.Sort(
        sort => sort.Field(document => document.CreatedAt, SortOrder.Asc),
        sort => sort.Field(document => document.Id, SortOrder.Asc)
    )
};

SearchAfterPage<OrderDocument> page = await store.SearchAfterAsync(request, cancellationToken).ConfigureAwait(false);
```

下一次查詢將 `page.NextSearchAfter` 指定為新 request 的 `SearchAfter`。`Documents` 為空表示目前條件下已沒有下一頁。伺服器端錯誤以 `IsSuccessful` 為 `false` 表示。

## 下一步

- [文件模型設定](document-model.md)
- [Index 命名與初始化](index-management.md)
- [相依性注入](dependency-injection.md)
- [Document Store](document-store.md)
- [查詢條件擴充](query-filters.md)
- [Bulk 寫入](bulk-indexing.md)
