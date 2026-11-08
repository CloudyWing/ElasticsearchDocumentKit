---
_layout: landing
---

# ElasticsearchDocumentKit

![ElasticsearchDocumentKit](images/logo.png)

`ElasticsearchDocumentKit` 為使用 Elasticsearch 管理業務文件索引的 .NET 類別庫。文件模型與常用操作由套件提供，搜尋 DSL、連線設定與叢集管理仍可直接使用官方 `Elastic.Clients.Elasticsearch`。

## 文件導覽

依使用情境選擇文章。入門指南先建立可用的文件模型與 Store，其餘文章分別說明 Index、查詢、批次、Alias、可觀測性、測試與發版。

- [入門指南](articles/getting-started.md)
- [文件模型設定](articles/document-model.md)
- [Index 命名與初始化](articles/index-management.md)
- [相依性注入](articles/dependency-injection.md)
- [Document Store](articles/document-store.md)
- [查詢條件擴充](articles/query-filters.md)
- [Bulk 寫入](articles/bulk-indexing.md)
- [Alias 與 Lifecycle 命名](articles/alias-and-lifecycle.md)
- [OpenTelemetry](articles/telemetry.md)
- [Testcontainers 整合測試](articles/testing.md)
- [發版流程](articles/release.md)

## API 與決策紀錄

- [API 文件](xref:CloudyWing.ElasticsearchDocumentKit)
- [架構決策紀錄](adr/index.md)

## License

This project is MIT [licensed](https://github.com/CloudyWing/ElasticsearchDocumentKit/blob/main/LICENSE.md).
