# Alias 與 Lifecycle 命名

Index 重建常見流程是建立新版本 Index、寫入資料、驗證結果，最後將寫入 Alias 切到新 Index。ElasticsearchDocumentKit 提供命名與 Alias 更新的輔助 API，但資料搬移、舊 Index 刪除與 ILM policy 仍由應用程式決定。

| 方法 | 語意 |
| --- | --- |
| `WithDateSuffix` | 建立帶日期尾碼的 Index 名稱 |
| `WithVersionSuffix` | 建立帶版本尾碼的 Index 名稱 |
| `SwitchAsync` | 加入新 Index Alias 並移除舊 Index Alias |
| `IsWriteIndex` | 將新 Index 指定為 Alias 的寫入目標 |
| `RemovedIndexNames` | 回報此次已解除 Alias 的舊 Index |

## 建立 Lifecycle 名稱

`IndexLifecycleNames` 將基礎名稱與日期或版本組成可預期的 Index 名稱。輸入應已經符合 Elasticsearch 命名限制。

```csharp
using CloudyWing.ElasticsearchDocumentKit.Indexing;

string dailyIndex = IndexLifecycleNames.WithDateSuffix(
    "ship-locations",
    new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero)
);
string versionedIndex = IndexLifecycleNames.WithVersionSuffix("ship-locations", "v2");

Console.WriteLine(dailyIndex);
Console.WriteLine(versionedIndex);
```

日期名稱適合不可變事件或週期資料。版本名稱適合 mapping 重建，因為它能明確表示新舊 Index 的交接關係。

## 初始化新 Index

Alias 切換前先建立新 Index，並在寫入前確認 mapping 與設定。使用與文件模型相同的 `IIndexInitializer<TDocument>`，可確保名稱與 mapping 來源一致。

```csharp
using CloudyWing.ElasticsearchDocumentKit.Indexing;

IIndexInitializer<ShipLocationDocument> initializer = new ElasticsearchIndexInitializer<ShipLocationDocument>(
    client,
    documentModels
);
IndexInitializationResult initialization = await initializer.InitializeAsync(cancellationToken).ConfigureAwait(false);

if (!initialization.IsSuccessful) {
    throw new InvalidOperationException(initialization.ErrorReason);
}
```

若需要用版本名稱覆寫預設模型 Index，應在建立新版本前調整文件模型或以受控的部署設定提供名稱。避免讓匯入程式與 Alias 切換程式各自推導名稱。

## 切換寫入 Alias

`IElasticsearchAliasSwitcher` 是可注入抽象，`ElasticsearchAliasSwitcher` 則是預設實作。傳入的舊 Index 清單可以是空集合，適合第一次建立 Alias 的情境。

```csharp
using CloudyWing.ElasticsearchDocumentKit.Indexing;

IElasticsearchAliasSwitcher aliasSwitcher = new ElasticsearchAliasSwitcher(client);

AliasSwitchResult switched = await aliasSwitcher.SwitchAsync(
    aliasName: "ship-locations-write",
    newIndexName: "ship-locations-v2",
    oldIndexNames: ["ship-locations-v1"],
    cancellationToken
).ConfigureAwait(false);
```

預設操作會對新 Index 加入 Alias 並設定 `IsWriteIndex = true`。舊 Index 只會移除該 Alias，不會被刪除，因此可保留一段觀察期與回復空間。

## 驗證切換結果

`AliasSwitchResult` 提供 Alias 名稱、新 Index、已移除的舊 Index、status code 與錯誤摘要。應先判斷成功，再進行舊 Index 清理或後續寫入。

```csharp
if (!switched.IsSuccessful) {
    logger.LogError(
        "Alias {AliasName} failed to switch. {ErrorType}: {ErrorReason}",
        switched.AliasName,
        switched.ErrorType,
        switched.ErrorReason
    );
    return;
}

logger.LogInformation(
    "Alias {AliasName} now writes to {IndexName}",
    switched.AliasName,
    switched.AddedIndexName
);
```

`RemovedIndexNames` 可用於記錄被替換的目標。它不是保留策略的依據，刪除舊 Index 前仍應檢查讀取 Alias、資料保留期限與回復需求。

## 建議作業順序

將重建流程分成可驗證的步驟，失敗時就能只重跑失敗的階段。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

await InitializeIndexAsync(cancellationToken).ConfigureAwait(false);
BulkOperationResult imported = await store.BulkIndexAsync(source, cancellationToken).ConfigureAwait(false);

if (!imported.HasErrors) {
    await aliasSwitcher.SwitchAsync(
        "ship-locations-write",
        "ship-locations-v2",
        ["ship-locations-v1"],
        cancellationToken
    ).ConfigureAwait(false);
}
```

寫入完成不代表資料正確。Alias 切換前應依業務需求檢查筆數、抽樣查詢、mapping 與讀取路徑。

## 不在此 API 範圍的工作

`ElasticsearchAliasSwitcher` 不負責建立 ILM policy、重新索引、刪除舊 Index 或同步讀取 Alias。這些操作通常與資料保留、租戶隔離及部署流程有關，應由應用程式的管理層明確編排。

```csharp
using CloudyWing.ElasticsearchDocumentKit.Indexing;

AliasSwitchResult result = await aliasSwitcher.SwitchAsync(
    "ship-locations-write",
    "ship-locations-v2",
    cancellationToken: cancellationToken
);

IReadOnlyList<string> removed = result.RemovedIndexNames;
```

將清除步驟放在 Alias 切換之後的獨立作業，可避免 API 成功後立刻失去回復所需的舊資料。

## 延伸閱讀

- [Index 命名與初始化](index-management.md)
- [Bulk 寫入](bulk-indexing.md)
- [OpenTelemetry](telemetry.md)
