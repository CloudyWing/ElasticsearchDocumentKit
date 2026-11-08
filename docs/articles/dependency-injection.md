# 相依性注入

`AddElasticsearchDocuments` 將套件需要的選項、解析器與服務註冊到 `IServiceCollection`。官方 `ElasticsearchClient` 的建立方式由宿主決定，套件不保存連線字串或驗證資料。

| 方法 | 語意 |
| --- | --- |
| `AddElasticsearchDocuments` | 註冊文件模型、Store、Index 初始化與 Alias 切換服務 |
| `ConfigureDocument` | 為指定文件型別加入集中設定 |
| `Resolve<TDocument>` | 由 DI 內的模型解析器取得文件模型 |
| `InitializeAsync` | 從注入的初始化服務建立 Index |
| `SwitchAsync` | 從注入的 Alias 服務切換寫入目標 |

## 建立官方 client

官方 client 通常以 singleton 註冊。應用程式可依自己的設定系統建立 client，並在這一層處理 API Key、憑證與 timeout。

```csharp
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.DependencyInjection;

IServiceCollection services = new ServiceCollection();
services.AddSingleton(_ => {
    ElasticsearchClientSettings settings = new(new Uri("https://localhost:9200"));
    settings.RequestTimeout(TimeSpan.FromSeconds(30));
    return new ElasticsearchClient(settings);
});
```

若 client 使用自訂節點選擇、proxy 或 TLS 設定，這些設定都放在這個 factory 中。套件服務會使用同一個 client，因此不會各自建立連線池。

## 註冊套件服務

`ElasticsearchDocumentsServiceCollectionExtensions` 提供擴充方法，也可使用靜態方法形式呼叫，兩者建立相同的服務註冊。

```csharp
using CloudyWing.ElasticsearchDocumentKit;
using CloudyWing.ElasticsearchDocumentKit.Modeling;

ElasticsearchDocumentsServiceCollectionExtensions.AddElasticsearchDocuments(
    services,
    options => {
        options.IndexPrefix = "commerce";
        options.ConfigureDocument<OrderDocument>(document => document
            .ToIndex("orders")
            .HasId(order => order.Id)
        );
    });

ElasticsearchDocumentsOptions configuredOptions = new() { IndexPrefix = "commerce" };
```

實際註冊應只呼叫一次。第二個 `ElasticsearchDocumentsOptions` 範例用於說明型別，不應取代由 extension method 建立並設定好的選項。

## 可注入的抽象

呼叫端通常依賴介面，保留替換實作與測試 double 的空間。下列服務由預設註冊提供。

```csharp
using CloudyWing.ElasticsearchDocumentKit;
using CloudyWing.ElasticsearchDocumentKit.Indexing;
using CloudyWing.ElasticsearchDocumentKit.Modeling;
using Microsoft.Extensions.DependencyInjection;

IIndexNameResolver indexNames = serviceProvider.GetRequiredService<IIndexNameResolver>();
IElasticsearchDocumentModelResolver models = serviceProvider
    .GetRequiredService<IElasticsearchDocumentModelResolver>();
IElasticsearchDocumentStore<OrderDocument> store = serviceProvider
    .GetRequiredService<IElasticsearchDocumentStore<OrderDocument>>();
IIndexInitializer<OrderDocument> initializer = serviceProvider
    .GetRequiredService<IIndexInitializer<OrderDocument>>();
IElasticsearchAliasSwitcher aliases = serviceProvider
    .GetRequiredService<IElasticsearchAliasSwitcher>();
```

預設型別分別是 `DefaultIndexNameResolver`、`DefaultElasticsearchDocumentModelResolver`、`ElasticsearchDocumentStore<TDocument>`、`ElasticsearchIndexInitializer<TDocument>` 與 `ElasticsearchAliasSwitcher`。這些實作不需要呼叫端自行建立。

## 在應用程式服務中使用 Store

應用程式服務注入以泛型參數指定文件型別的 Store，避免使用字串 Index 名稱繞過模型設定。這也讓單元測試能以同一介面替換 Store。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

public sealed class OrderIndexer {
    private readonly IElasticsearchDocumentStore<OrderDocument> store;

    public OrderIndexer(IElasticsearchDocumentStore<OrderDocument> store) {
        this.store = store;
    }

    public Task<DocumentOperationResult> IndexAsync(
        OrderDocument order,
        CancellationToken cancellationToken
    ) {
        return store.IndexAsync(order, cancellationToken: cancellationToken);
    }
}
```

建立 Index 與寫入文件適合分成不同的應用程式操作。這能避免資料寫入路徑被第一次建立 Index 的網路 I/O、mapping 錯誤或權限問題阻塞。

## 設定驗證

服務建立期間會驗證 options。Index prefix 與文件模型設定有問題時，應在應用程式啟動或組裝測試中發現，而不是等待第一筆寫入。

```csharp
using CloudyWing.ElasticsearchDocumentKit;
using CloudyWing.ElasticsearchDocumentKit.Modeling;

ElasticsearchDocumentsOptions options = serviceProvider
    .GetRequiredService<ElasticsearchDocumentsOptions>();
options.Validate();

ElasticsearchDocumentModel<OrderDocument> model = models.Resolve<OrderDocument>();
string indexName = indexNames.Resolve<OrderDocument>();
```

`ElasticsearchDocumentModel<TDocument>` 的 `IndexName`、`IdSelector` 與 `ConfigureIndex` 會被 Store 與初始化服務共用。變更文件模型時，應同時檢查這兩條操作路徑。

## 延伸閱讀

- [文件模型設定](document-model.md)
- [Index 命名與初始化](index-management.md)
- [Document Store](document-store.md)
- [Alias 與 Lifecycle 命名](alias-and-lifecycle.md)
