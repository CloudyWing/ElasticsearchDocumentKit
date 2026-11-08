# 文件模型設定

文件模型集中描述 .NET 文件型別與 Elasticsearch Index 的關係。Store、Index 初始化與 DI 都讀取同一份模型，因此應將名稱、ID 與 mapping 規則放在一個可追蹤的位置。

| 方法 | 語意 |
| --- | --- |
| `Document<TDocument>` | 取得指定文件型別的模型 builder |
| `ToIndex` | 指定不含 prefix 的基礎 Index 名稱 |
| `HasId` | 指定寫入與 Bulk 使用的文件 ID 選取器 |
| `ConfigureMapping` | 設定 typed mapping 的簡化入口 |
| `ConfigureIndex` | 使用官方 descriptor 設定完整的建立 Index 請求 |

## 集中設定模型

`ElasticsearchDocumentsOptions.ConfigureDocuments` 接收非泛型 `ElasticsearchDocumentModelBuilder`。它適合在一個位置設定多個文件型別。

```csharp
using CloudyWing.ElasticsearchDocumentKit;
using CloudyWing.ElasticsearchDocumentKit.Modeling;

ElasticsearchDocumentsOptions options = new() { IndexPrefix = "commerce" };
Action<ElasticsearchDocumentModelBuilder> configure = models => {
    ElasticsearchDocumentModelBuilder<OrderDocument> orders = models.Document<OrderDocument>();
    orders.ToIndex("orders").HasId(order => order.Id);
};

options.ConfigureDocuments(configure);
```

`IndexPrefix` 會在最終名稱前加入 `-`。例如基礎名稱為 `orders` 且 prefix 為 `commerce` 時，解析結果為 `commerce-orders`。

## 設定單一文件型別

`ConfigureDocument<TDocument>` 直接提供泛型 builder。這個寫法適合由功能模組各自設定自己的文件型別。

```csharp
using CloudyWing.ElasticsearchDocumentKit.Modeling;

Action<ElasticsearchDocumentModelBuilder<OrderDocument>> configureOrder = document => document
    .ToIndex("orders")
    .HasId(order => order.Id)
    .ConfigureMapping(mapping => mapping.Properties(properties => properties
        .Keyword(field => field.Id)
        .Text(field => field.CustomerName)
        .Date(field => field.CreatedAt)
        .DoubleNumber(field => field.TotalAmount)
    ));

options.ConfigureDocument(configureOrder);
```

`HasId` 可回傳 `null`。回傳 `null` 時，單筆寫入不指定 ID，`_id` 交由官方 client 決定。業務文件通常應提供穩定的 ID，以避免重送時產生重複資料。

未呼叫 `HasId` 的文件模型會讓啟用重試的 Bulk 寫入失敗。`BulkIndexAsync` 在 `MaxRetryAttempts` 大於 0 而文件模型沒有 ID 選取器時，會於送出任何請求前擲出 `InvalidOperationException`，避免沒有固定 `_id` 的文件在整批重送時重複寫入。詳細行為與放行方式見 [Bulk 寫入](bulk-indexing.md)。

## 設定 Index 細節

`ConfigureMapping` 只處理 mappings。需要 shard、replica 或其他 Index 級設定時，使用 `ConfigureIndex`，其委派直接接收官方 `CreateIndexRequestDescriptor<TDocument>`。

```csharp
options.ConfigureDocument<OrderDocument>(document => document
    .ConfigureIndex(descriptor => descriptor
        .Settings(settings => settings
            .NumberOfShards(1)
            .NumberOfReplicas(1)
        )
        .Mappings(mapping => mapping.Properties(properties => properties
            .Keyword(field => field.Id)
            .Text(field => field.CustomerName)
        ))
    )
);
```

同一個文件型別的 mapping 應只在一個地方設定。將一部分 mapping 放在建立 Index 的工作、另一部分放在文件模型，會增加環境之間不一致的機率。

## 宣告地理座標欄位

文件屬性使用 `GeoPoint?` 型別時，mapping 以 `GeoShape` 宣告。`GeoPoint` 序列化為 GeoJSON Point，對應 Elasticsearch 的 `geo_shape` 欄位型別。

```csharp
using CloudyWing.ElasticsearchDocumentKit.Modeling;

Action<ElasticsearchDocumentModelBuilder<ShipLocationDocument>> configureLocation = document => document
    .ToIndex("ship-locations")
    .HasId(location => location.Id)
    .ConfigureMapping(mapping => mapping.Properties(properties => properties
        .Keyword(field => field.Id)
        .GeoShape(field => field.Location)
    ));

options.ConfigureDocument(configureLocation);
```

其中 `ShipLocationDocument.Location` 的型別為 `GeoPoint?`。`GeoPoint` 的座標範圍與序列化格式，以及搭配 `GeoPolygon` 的空間關係查詢，見 [查詢條件擴充](query-filters.md)。

## 解析模型

`DefaultElasticsearchDocumentModelResolver` 結合 options 與名稱解析器，輸出不可變的 `ElasticsearchDocumentModel<TDocument>`。Store 與 Index 初始化服務都可使用這個結果。

```csharp
using CloudyWing.ElasticsearchDocumentKit;
using CloudyWing.ElasticsearchDocumentKit.Modeling;

IIndexNameResolver indexNames = new DefaultIndexNameResolver(options);
IElasticsearchDocumentModelResolver resolver = new DefaultElasticsearchDocumentModelResolver(
    options,
    indexNames
);

ElasticsearchDocumentModel<OrderDocument> model = resolver.Resolve<OrderDocument>();
string indexName = model.IndexName;
Func<OrderDocument, string?>? idSelector = model.IdSelector;
```

若沒有集中設定，resolver 仍可解析型別名稱或 `ElasticsearchIndexNameAttribute`。需要統一 prefix、mapping 或 ID 選取器時，集中設定較容易維護。

## 與寫入與初始化的關係

Store 使用模型的 Index 名稱與 ID 選取器。初始化服務使用模型的 `ConfigureIndex`，因此兩者會指向同一個 Index。

```csharp
using CloudyWing.ElasticsearchDocumentKit;
using CloudyWing.ElasticsearchDocumentKit.Indexing;

ElasticsearchDocumentStore<OrderDocument> store = new(client, resolver);
ElasticsearchIndexInitializer<OrderDocument> initializer = new(client, resolver);

IndexInitializationResult initialization = await initializer.InitializeAsync(cancellationToken).ConfigureAwait(false);
DocumentOperationResult indexed = await store.IndexAsync(order, cancellationToken: cancellationToken)
    .ConfigureAwait(false);
```

部署 mapping 變更前，先確認建立新 Index 的流程，再切換 Alias。需要重建欄位結構的變更，通常不適合直接修改正在服務的 Index mapping。

## 延伸閱讀

- [Index 命名與初始化](index-management.md)
- [Document Store](document-store.md)
- [相依性注入](dependency-injection.md)
- [Alias 與 Lifecycle 命名](alias-and-lifecycle.md)
