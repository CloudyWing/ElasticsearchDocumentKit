# ElasticsearchDocumentKit

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/CloudyWing/ElasticsearchDocumentKit/blob/main/LICENSE.md)

ElasticsearchDocumentKit 是我用 Facade 包裝官方 `Elastic.Clients.Elasticsearch` 的 .NET 套件。

Elasticsearch 套件曾經改版過，當時官方提供的 `Elastic.Clients.Elasticsearch` 只有比較基礎的套件說明文件，而當時 AI 也因為缺乏訓練，產生的程式碼問題很多，網路上能參考的資源也極少，變成要去翻原始碼來看怎開發。而我本身就習慣把外部 API 包一層 Facade，只提供我會使用的功能介面，來方便使用。

這個專案是基於當時的概念，再利用 AI 優化而成，一部分是留個紀錄，另一部分是未來有需要開發 Elasticsearch，可以回頭看這專案，了解套件怎使用。

## 安裝

```powershell
dotnet add package CloudyWing.ElasticsearchDocumentKit
```

套件目標框架為 `net10.0`。應用程式負責建立與管理官方 `ElasticsearchClient`，包含連線、驗證與 timeout 設定。

## Quick Start

下列範例建立文件模型、解析 Index 名稱並寫入一筆文件。實際的連線位址與驗證資料應由安全的應用程式設定來源提供。

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using CloudyWing.ElasticsearchDocumentKit;
using CloudyWing.ElasticsearchDocumentKit.Modeling;
using Elastic.Clients.Elasticsearch;

public sealed class OrderDocument {
    public string Id { get; init; } = "";

    public string CustomerName { get; init; } = "";
}

internal static class Program {
    private static async Task Main() {
        ElasticsearchClient client = new(new Uri("https://localhost:9200"));
        ElasticsearchDocumentsOptions options = new() { IndexPrefix = "orders" };
        options.ConfigureDocument<OrderDocument>(document => document
            .ToIndex("orders")
            .HasId(order => order.Id)
        );

        IIndexNameResolver indexNames = new DefaultIndexNameResolver(options);
        IElasticsearchDocumentModelResolver models = new DefaultElasticsearchDocumentModelResolver(
            options,
            indexNames
        );
        ElasticsearchDocumentStore<OrderDocument> store = new(client, models);

        DocumentOperationResult result = await store.IndexAsync(
            new OrderDocument { Id = "ORD-001", CustomerName = "CloudyWing" },
            cancellationToken: CancellationToken.None
        );

        Console.WriteLine(result.IsSuccessful);
    }
}
```

`DocumentOperationResult.IsSuccessful` 用於判斷 Elasticsearch 是否接受寫入。失敗時可讀取 `ErrorType`、`ErrorReason` 與 `ResponseSummary` 取得診斷資訊。

## 能力總覽

| 能力 | 用途 | 說明 |
| --- | --- | --- |
| 文件模型 | 集中宣告 Index 名稱、ID 與 mapping | [文件模型設定](https://github.com/CloudyWing/ElasticsearchDocumentKit/blob/main/docs/articles/document-model.md) |
| Index 管理 | 驗證名稱並顯式建立 Index | [Index 命名與初始化](https://github.com/CloudyWing/ElasticsearchDocumentKit/blob/main/docs/articles/index-management.md) |
| 文件操作 | 建立、寫入、讀取、部分更新與刪除，支援樂觀鎖、Script 更新、依條件更新或刪除、筆數計算、查詢與分頁 | [Document Store](https://github.com/CloudyWing/ElasticsearchDocumentKit/blob/main/docs/articles/document-store.md) |
| 查詢條件 | 組合常用 filter、geo polygon 與四種空間關係，以及 `_source` projection | [查詢條件擴充](https://github.com/CloudyWing/ElasticsearchDocumentKit/blob/main/docs/articles/query-filters.md) |
| 批次寫入 | 控制分批、重試與失敗摘要 | [Bulk 寫入](https://github.com/CloudyWing/ElasticsearchDocumentKit/blob/main/docs/articles/bulk-indexing.md) |
| Alias 切換 | 以新 Index 接手寫入 Alias | [Alias 與 Lifecycle 命名](https://github.com/CloudyWing/ElasticsearchDocumentKit/blob/main/docs/articles/alias-and-lifecycle.md) |
| 可觀測性 | 發出可由 OpenTelemetry 收集的 Activity | [OpenTelemetry](https://github.com/CloudyWing/ElasticsearchDocumentKit/blob/main/docs/articles/telemetry.md) |

## 文件導覽

深入設定與操作方式位於 [完整文件](https://github.com/CloudyWing/ElasticsearchDocumentKit/tree/main/docs)。文章導覽如下：

- [入門指南](https://github.com/CloudyWing/ElasticsearchDocumentKit/blob/main/docs/articles/getting-started.md)
- [文件模型設定](https://github.com/CloudyWing/ElasticsearchDocumentKit/blob/main/docs/articles/document-model.md)
- [Index 命名與初始化](https://github.com/CloudyWing/ElasticsearchDocumentKit/blob/main/docs/articles/index-management.md)
- [相依性注入](https://github.com/CloudyWing/ElasticsearchDocumentKit/blob/main/docs/articles/dependency-injection.md)
- [Document Store](https://github.com/CloudyWing/ElasticsearchDocumentKit/blob/main/docs/articles/document-store.md)
- [查詢條件擴充](https://github.com/CloudyWing/ElasticsearchDocumentKit/blob/main/docs/articles/query-filters.md)
- [Bulk 寫入](https://github.com/CloudyWing/ElasticsearchDocumentKit/blob/main/docs/articles/bulk-indexing.md)
- [Alias 與 Lifecycle 命名](https://github.com/CloudyWing/ElasticsearchDocumentKit/blob/main/docs/articles/alias-and-lifecycle.md)
- [OpenTelemetry](https://github.com/CloudyWing/ElasticsearchDocumentKit/blob/main/docs/articles/telemetry.md)
- [Testcontainers 整合測試](https://github.com/CloudyWing/ElasticsearchDocumentKit/blob/main/docs/articles/testing.md)
- [發版流程](https://github.com/CloudyWing/ElasticsearchDocumentKit/blob/main/docs/articles/release.md)

各版本的變更見 [變更紀錄](https://github.com/CloudyWing/ElasticsearchDocumentKit/blob/main/CHANGELOG.md)。

