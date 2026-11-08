# 測試策略

ElasticsearchDocumentKit 將不需叢集的行為放在單元測試，與真實 Elasticsearch 的往返放在 Testcontainers 整合測試。兩類測試的執行成本與失敗訊號不同，應分別處理。

| 方法 | 語意 |
| --- | --- |
| 單元測試 | 驗證名稱、模型、結果對應與 descriptor 序列化邏輯 |
| Testcontainers 測試 | 驗證官方 client 與 Elasticsearch 的實際往返 |
| `IElasticsearchDocumentStore<T>` | 讓應用程式服務可用替身測試控制流程 |
| `IsSuccessful` | 以結果契約判斷操作是否可繼續 |
| 公開 API 煙霧測試 | 確認消費端可編譯存取預期的公開型別 |

## 執行單元測試

單元測試涵蓋不需要 Docker 或真實 Elasticsearch 的行為。它們適合在每次程式碼變更後快速執行。

```powershell
dotnet test ElasticsearchDocumentKit.slnx --configuration Release --filter "TestCategory!=Integration"
```

名稱驗證、模型解析、結果對應、Bulk 失敗摘要與查詢 descriptor 都屬於單元測試的合理範圍。官方 client 的網路呼叫則由整合測試驗證。

## 以介面測試應用程式服務

應用程式服務依賴 `IElasticsearchDocumentStore<TDocument>` 時，可以提供測試 double，驗證失敗結果是否被正確處理，而不需要建立 HTTP 或 container。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

public sealed class OrderSynchronizer {
    private readonly IElasticsearchDocumentStore<OrderDocument> store;

    public OrderSynchronizer(IElasticsearchDocumentStore<OrderDocument> store) {
        this.store = store;
    }

    public async Task<bool> SynchronizeAsync(OrderDocument order, CancellationToken cancellationToken) {
        DocumentOperationResult result = await store.IndexAsync(order, cancellationToken: cancellationToken);
        return result.IsSuccessful;
    }
}
```

測試 double 應回傳與真實結果相同的成功與失敗語意。只回傳 `true` 或直接擲出例外，會遺漏應用程式對 `ErrorType` 與 `ErrorReason` 的處理路徑。

## 驗證結果型別

結果型別可直接建立，適合測試呼叫端的記錄、重試與回應邏輯。建立資料時應填入與測試情境相關的欄位。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

BulkOperationFailure failure = new() {
    ItemIndex = 0,
    StatusCode = 429,
    ErrorType = "es_rejected_execution_exception",
    ErrorReason = "queue is full"
};

BulkOperationResult result = new() {
    IsSuccessful = false,
    HasErrors = true,
    TotalCount = 1,
    SucceededCount = 0,
    FailedCount = 1,
    Failures = [failure]
};
```

單元測試應區分整批請求失敗、個別 item 失敗與找不到文件。它們會對應不同的重試與使用者回應策略。

## 執行 Testcontainers 測試

Testcontainers 測試會啟動短暫的 Elasticsearch container，適合確認 mapping、文件操作、樂觀鎖、條件式更新與刪除、from/size 分頁、geo 查詢與 search-after 的實際結果。執行前需要可用的 Docker 環境。

```powershell
dotnet test tests\ElasticsearchDocumentKit.Tests.Testcontainers\ElasticsearchDocumentKit.Tests.Testcontainers.csproj --configuration Release --filter TestCategory=Integration
```

整合測試類別以 `Integration` category 與 `Explicit` 標記隔離，因此一般測試命令不會意外啟動 container。CI 應有專用 job 執行這個 filter。

### 連線至外部 Elasticsearch

Testcontainers 整合測試支援容器模式與外部模式。未設定 `ELASTICSEARCHDOCUMENTKIT_TEST_URI` 時使用容器模式，由測試啟動 Elasticsearch container。設定該變數後使用外部模式，測試不會啟動 container。

外部模式使用下列環境變數。

| 環境變數 | 用途 |
| --- | --- |
| `ELASTICSEARCHDOCUMENTKIT_TEST_URI` | 外部 Elasticsearch 的連線 URI；有值時啟用外部模式 |
| `ELASTICSEARCHDOCUMENTKIT_TEST_USERNAME` | Basic authentication 使用者名稱；需與密碼同時有值才會套用 |
| `ELASTICSEARCHDOCUMENTKIT_TEST_PASSWORD` | Basic authentication 密碼；需與使用者名稱同時有值才會套用 |

兩種模式的 Index 命名相同，都以 `estk-tests-` 加上 32 碼十六進位的 GUID 作為 `IndexPrefix`，外部模式藉此避免與目標叢集的既有資料衝突。測試完成或失敗時都會刪除該暫存 Index。

## 組裝整合測試的操作

整合測試使用與正式程式相同的模型、初始化與 Store。範例中的 fixture 負責提供已啟動 container 的 client 與測試隔離名稱。

```csharp
using CloudyWing.ElasticsearchDocumentKit;
using CloudyWing.ElasticsearchDocumentKit.Indexing;
using CloudyWing.ElasticsearchDocumentKit.Modeling;

IElasticsearchDocumentModelResolver models = fixture.CreateDocumentModelResolver<OrderDocument>();
IIndexInitializer<OrderDocument> initializer = new ElasticsearchIndexInitializer<OrderDocument>(
    fixture.Client,
    models
);
IElasticsearchDocumentStore<OrderDocument> store = new ElasticsearchDocumentStore<OrderDocument>(
    fixture.Client,
    models
);

IndexInitializationResult initialized = await initializer.InitializeAsync(cancellationToken);
DocumentOperationResult indexed = await store.IndexAsync(order, cancellationToken: cancellationToken);
```

整合測試失敗時，先確認 Docker、container image、叢集啟動狀態與 TLS 設定。這些環境因素與套件邏輯失敗有不同的修正方式。

## 公開 API 編譯煙霧測試

下列測試將公開入口型別作為編譯目標。它適合在消費端套件驗收或大型重構後，確認各主題的入口仍可被引用。

```csharp
using CloudyWing.ElasticsearchDocumentKit;
using CloudyWing.ElasticsearchDocumentKit.Geo;
using CloudyWing.ElasticsearchDocumentKit.Indexing;
using CloudyWing.ElasticsearchDocumentKit.Modeling;
using CloudyWing.ElasticsearchDocumentKit.Querying;
using CloudyWing.ElasticsearchDocumentKit.Search;

Type[] publicEntryPoints = [
    typeof(BulkDeadLetter<>),
    typeof(BulkIndexOptions<>),
    typeof(BulkOperationFailure),
    typeof(BulkOperationResult),
    typeof(ByQueryOperationResult),
    typeof(DefaultIndexNameResolver),
    typeof(DeleteByQueryRequest<>),
    typeof(DocumentCountResult),
    typeof(DocumentGetResult<>),
    typeof(DocumentOperationResult),
    typeof(DocumentWriteOptions),
    typeof(ElasticsearchDocumentsOptions),
    typeof(ElasticsearchDocumentsServiceCollectionExtensions),
    typeof(ElasticsearchDocumentsTelemetry),
    typeof(ElasticsearchDocumentStore<>),
    typeof(ElasticsearchIndexNameAttribute),
    typeof(ElasticsearchIndexNameValidator),
    typeof(IElasticsearchDocumentStore<>),
    typeof(IIndexNameResolver),
    typeof(IndexDocumentOptions),
    typeof(UpdateByQueryRequest<>),
    typeof(UpdateDocumentOptions),
    typeof(GeoPoint),
    typeof(GeoPolygon),
    typeof(AliasSwitchResult),
    typeof(ElasticsearchAliasSwitcher),
    typeof(ElasticsearchIndexInitializer<>),
    typeof(IElasticsearchAliasSwitcher),
    typeof(IIndexInitializer<>),
    typeof(IndexInitializationResult),
    typeof(IndexLifecycleNames),
    typeof(DefaultElasticsearchDocumentModelResolver),
    typeof(ElasticsearchDocumentModel<>),
    typeof(ElasticsearchDocumentModelBuilder),
    typeof(ElasticsearchDocumentModelBuilder<>),
    typeof(IElasticsearchDocumentModelResolver),
    typeof(ElasticsearchQueryFilterBuilder<>),
    typeof(ElasticsearchSearchRequestDescriptorExtensions),
    typeof(DocumentPageRequest<>),
    typeof(DocumentPageResult<>),
    typeof(DocumentSearchRequest<>),
    typeof(DocumentSearchResult<>),
    typeof(SearchAfterPage<>),
    typeof(SearchAfterRequest<>)
];

Assert.That(publicEntryPoints, Has.All.Not.Null);
```

這類測試只確認編譯與公開入口可存取，不取代行為測試。實際行為仍應由單元測試與整合測試涵蓋。

## 涵蓋率與失敗分析

涵蓋率可指出未經過的分支，但不是功能正確性的替代品。先根據錯誤處理、輸入邊界與業務風險選擇測試情境，再以涵蓋率確認沒有遺漏重要路徑。

```csharp
using CloudyWing.ElasticsearchDocumentKit.Search;

SearchAfterPage<OrderDocument> page = await store.SearchAfterAsync(request, cancellationToken);

if (!page.IsSuccessful) {
    Assert.Fail($"Search-after failed: {page.ErrorType} {page.ErrorReason}");
}

Assert.That(page.NextSearchAfter, Is.Not.Null);
```

container 測試應使用可辨識且隔離的 Index 名稱。測試完成後的資料清理策略由 fixture 控制，避免平行測試互相讀到資料。

## 延伸閱讀

- [Document Store](document-store.md)
- [Bulk 寫入](bulk-indexing.md)
- [Index 命名與初始化](index-management.md)
- [發版流程](release.md)
