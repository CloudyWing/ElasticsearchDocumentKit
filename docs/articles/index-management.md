# Index 命名與初始化

ElasticsearchDocumentKit 將 Index 命名、名稱驗證與建立流程分開。應用程式可先決定命名規則，之後在部署、資料同步或管理操作中顯式初始化 Index。

| 方法 | 語意 |
| --- | --- |
| `Resolve<TDocument>` | 以文件型別解析最終 Index 名稱 |
| `IsValid` | 快速判斷名稱是否符合 Elasticsearch 限制 |
| `TryGetValidationError` | 取得不合法名稱的可讀錯誤訊息 |
| `Validate` | 在設定邊界以例外拒絕不合法名稱 |
| `InitializeAsync` | 檢查存在性後建立 Index |

## 宣告基礎名稱

`ElasticsearchIndexNameAttribute` 適合將穩定的基礎名稱放在文件型別旁。名稱仍會由全域 prefix 或 `ConfigureDocument` 進一步處理。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

[ElasticsearchIndexName("ship-locations")]
public sealed class ShipLocationDocument {
    public required string Id { get; init; }

    public required string ShipName { get; init; }
}
```

若型別沒有標註 `ElasticsearchIndexNameAttribute`，也沒有集中設定，預設解析器會將型別名稱轉為 kebab-case。這適合原型或單一用途文件，正式系統通常會選擇明確的名稱。

## 解析與驗證名稱

`DefaultIndexNameResolver` 實作 `IIndexNameResolver`。解析後的名稱可先由 `ElasticsearchIndexNameValidator` 驗證，避免把格式錯誤延後到網路請求才發現。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

ElasticsearchDocumentsOptions options = new() { IndexPrefix = "vessel" };
IIndexNameResolver resolver = new DefaultIndexNameResolver(options);

string indexName = resolver.Resolve<ShipLocationDocument>();
bool isValid = ElasticsearchIndexNameValidator.IsValid(indexName);

if (ElasticsearchIndexNameValidator.TryGetValidationError(indexName, out string? error)) {
    throw new ArgumentException(error, nameof(indexName));
}
```

`ElasticsearchIndexNameValidator.Validate` 適合用在自訂命名入口。它會在名稱為空白、使用保留字元、開頭不合法或超過位元組長度限制時擲出例外。

## 建立 Index

`IIndexInitializer<TDocument>` 是顯式初始化的抽象介面。預設的 `ElasticsearchIndexInitializer<TDocument>` 先確認 Index 是否存在，不存在時才建立 Index 與 mapping。

```csharp
using CloudyWing.ElasticsearchDocumentKit.Indexing;
using CloudyWing.ElasticsearchDocumentKit.Modeling;

IElasticsearchDocumentModelResolver models = new DefaultElasticsearchDocumentModelResolver(
    options,
    resolver
);
IIndexInitializer<ShipLocationDocument> initializer = new ElasticsearchIndexInitializer<ShipLocationDocument>(
    client,
    models
);

IndexInitializationResult result = await initializer.InitializeAsync(cancellationToken).ConfigureAwait(false);
```

`IndexInitializationResult.Created` 表示這次操作建立了 Index。`AlreadyExists` 表示另一個流程已建立或目標原本存在。兩者都可搭配 `IsSuccessful` 判斷初始化是否可繼續。

## 處理初始化結果

初始化失敗時，結果保留 status code、error type、error reason 與官方 client 摘要。記錄這些欄位能區分名稱、權限、叢集狀態與 mapping 問題。

```csharp
if (!result.IsSuccessful) {
    logger.LogError(
        "Index initialization failed for {IndexName}. {ErrorType}: {ErrorReason}",
        result.IndexName,
        result.ErrorType,
        result.ErrorReason
    );
    return;
}

if (result.Created) {
    logger.LogInformation("Created Elasticsearch index {IndexName}", result.IndexName);
}
```

初始化可重複呼叫。Exists 檢查與建立之間若有其他執行個體先完成建立，預設實作會將 Elasticsearch 的已存在回應轉成成功結果，流程可以繼續。

## 初始化時機

將初始化安排在具備重試與記錄能力的地方。常見位置包括部署後工作、資料匯入前置作業、管理命令與受控的背景服務。

```csharp
using CloudyWing.ElasticsearchDocumentKit.Indexing;

public sealed class IndexBootstrapper {
    private readonly IIndexInitializer<ShipLocationDocument> initializer;

    public IndexBootstrapper(IIndexInitializer<ShipLocationDocument> initializer) {
        this.initializer = initializer;
    }

    public Task<IndexInitializationResult> RunAsync(CancellationToken cancellationToken) {
        return initializer.InitializeAsync(cancellationToken);
    }
}
```

文件寫入路徑不會自動建立 Index。這個界線讓 mapping 變更、Alias 切換與權限問題可以在受控流程中處理。

## 延伸閱讀

- [文件模型設定](document-model.md)
- [Alias 與 Lifecycle 命名](alias-and-lifecycle.md)
- [Document Store](document-store.md)
