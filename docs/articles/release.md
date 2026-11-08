# 發版流程

ElasticsearchDocumentKit 的正式發佈由 GitHub Actions 自動執行。符合 `v*.*.*` 的 tag 會啟動驗證、建立 GitHub Release 與 Trusted Publishing 發佈，套件憑證在 CI 執行期間以 OIDC 取得。

| 方法 | 語意 |
| --- | --- |
| `v*.*.*` tag | 觸發正式發版 workflow |
| `verify` | 還原、建置與非整合測試 |
| `create-release` | 建立對應版本的 GitHub Release，release notes 取自 `CHANGELOG.md` |
| `publish-nuget` | 透過 Trusted Publishing 發佈套件 |
| `probe` | 以 `workflow_dispatch` 檢查發佈組態 |

## 版本與觸發條件

MinVer 使用 `v` 作為 tag 前綴。正式版本應使用 `v1.2.0` 這類符合 SemVer 的 tag，預發佈版本可使用合法的預發佈後綴。

release workflow 只接受 `v*.*.*` tag 與手動 `workflow_dispatch`。前者執行完整發佈鏈，後者提供不發佈的 probe job，用來確認 Trusted Publishing 組態。

## 自動發佈鏈

正式 tag 啟動後，workflow 依序執行 `verify`、`create-release` 與 `publish-nuget`。前一個 job 成功後，下一個 job 才會執行。`verify` 與 CI 的建置測試 job 共用 `CloudyWing/devops-templates` 的 `reusable-dotnet-ci`，以 `-warnaserror` 建置並執行非整合測試。

`publish-nuget` 使用 Trusted Publishing 的 OIDC 身分，不需要長期 NuGet API Key。repository 名稱、workflow 路徑與 nuget.org 的信任綁定必須保持一致。發佈前會以 tag 名稱檢查每個 nupkg 的版號，版號與 tag 不一致時停止發佈。

`devops-templates` 的 reusable workflow 與 composite action 皆以 major tag `@v1` 引用，major 版本內的修正會自動套用。

## CHANGELOG 與 release notes

`create-release` 以 `changelog-path` 將根目錄的 `CHANGELOG.md` 交給 `CloudyWing/devops-templates` 的 reusable workflow，由它擷取對應版本的區段作為 release notes。

該 workflow 取 `## ` 後的第一個版號，去掉前方的 `[` 與 `v` 後與 tag 的版號比對，版號後接空白、`]` 或 `(` 即結束。tag 為 `v1.0.0` 時，`## v1.0.0`、`## 1.0.0`、`## v1.0.0 (2026-11-04)` 與 Keep a Changelog 的 `## [1.0.0] - 2026-11-04` 都能比對到。本專案統一使用 `## v<版本>`，與 MinVer 的 tag 前綴一致。

比對不到版本區段或檔案不存在時，workflow 不會失敗，而是改用 GitHub 自動產生的 release notes 並輸出警告。改用自動產生的內容不會中斷發佈，所以建立 tag 前應先確認 `CHANGELOG.md` 已有對應版本的標題。

區段內的分類使用 `###`。擷取後每個標題會被提升一級，因此 `### 新增` 在 release notes 中呈現為二級標題。

## 變更前的消費端驗證

發版前先以消費端角度編譯主要 API 用法。這能及早發現公開型別、命名空間或泛型簽章的意外變更。

```csharp
using CloudyWing.ElasticsearchDocumentKit;
using CloudyWing.ElasticsearchDocumentKit.Modeling;

ElasticsearchDocumentsOptions options = new() { IndexPrefix = "release-check" };
options.ConfigureDocument<OrderDocument>(document => document
    .ToIndex("orders")
    .HasId(order => order.Id)
);

IIndexNameResolver indexNames = new DefaultIndexNameResolver(options);
IElasticsearchDocumentModelResolver models = new DefaultElasticsearchDocumentModelResolver(
    options,
    indexNames
);
```

這個檢查由現有的公開 API 宣告與建置警告共同把關。新增公開成員時，先更新 `PublicAPI.Unshipped.txt`，發版時再將已承諾的項目移入 `PublicAPI.Shipped.txt`。

## 驗證文件操作契約

發版前的測試應包含成功與失敗結果。結果型別的欄位是消費端判斷控制流程的契約，不應在沒有對應測試時任意移除或改名。

```csharp
using CloudyWing.ElasticsearchDocumentKit;

DocumentOperationResult operation = await store.IndexAsync(order, cancellationToken: cancellationToken);
DocumentGetResult<OrderDocument> loaded = await store.GetAsync(order.Id, cancellationToken);

if (!operation.IsSuccessful) {
    throw new InvalidOperationException(operation.ErrorReason);
}

if (loaded.IsSuccessful && loaded.Found) {
    Console.WriteLine(loaded.DocumentId);
}
```

若變更會影響 Search-After、Bulk 或 Alias 工作流，也應在發版前加入相對應的消費端驗收情境。

## 驗證進階操作契約

下列範例同時引用批次、分頁與 Alias 的公開結果型別。這些型別應持續提供消費端可直接讀取與記錄的資訊。

```csharp
using CloudyWing.ElasticsearchDocumentKit;
using CloudyWing.ElasticsearchDocumentKit.Indexing;
using CloudyWing.ElasticsearchDocumentKit.Search;

BulkOperationResult bulk = await store.BulkIndexAsync(orders, cancellationToken);
SearchAfterPage<OrderDocument> page = await store.SearchAfterAsync(request, cancellationToken);
AliasSwitchResult alias = await aliasSwitcher.SwitchAsync(
    "orders-write",
    "orders-v2",
    ["orders-v1"],
    cancellationToken
);

bool releaseScenarioSucceeded = bulk.IsSuccessful && page.IsSuccessful && alias.IsSuccessful;
```

這個範例不取代真實叢集驗證。它確認應用程式仍能對三類流程取得與判斷結果，而整合測試負責確認與 Elasticsearch 的實際互動。

## 首版後的套件驗證

首版尚未有可比較的已發佈版本，因此 `EnablePackageValidation` 暫時維持關閉。首版公開發佈後，設定 `PackageValidationBaselineVersion` 為該版本，並啟用 package validation，讓後續發版能檢查相容性。

```csharp
Version baseline = new(1, 0, 0);
bool shouldEnablePackageValidation = baseline.Major >= 1;

Console.WriteLine($"Package validation baseline: {baseline}");
Console.WriteLine($"Enable validation: {shouldEnablePackageValidation}");
```

這段程式碼只用來說明版本基準概念。實際設定位於 `Directory.Build.props`，應由維護者在首版發佈後更新。

## 失敗處理

`verify` 失敗時，workflow 不會建立 Release 或進入發佈 job。修正還原、建置或非整合測試問題後，建立新的正確 tag 再重新觸發流程。

還原階段會執行 NuGet Audit，範圍包含傳遞相依性。CI 環境（`CI` 為 `true`）會把 `NU1902`、`NU1903` 與 `NU1904`，也就是 moderate、high 與 critical 等級的已知弱點，升級為錯誤，因此相依套件新公布的弱點也可能讓 `verify` 在還原時失敗。本機建置只會顯示警告。

Trusted Publishing 驗證失敗時，檢查 nuget.org 的 repository、workflow 與環境設定是否仍對應目前專案。不要以長期憑證取代 OIDC，否則會失去短期身分與供應鏈追蹤的好處。

## 相關文件

- [測試策略](testing.md)
- [公開 API 宣告 ADR](../adr/0001-public-api-declaration-as-build-error.md)
- [Trusted Publishing ADR](../adr/0002-nuget-publish-via-composite-action.md)
