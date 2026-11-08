# 變更紀錄

本檔記錄 ElasticsearchDocumentKit 各版本的變更，供套件消費端判斷升級影響。

## 格式約定

版本標題寫成 `## v<版本>`，例如 `## v1.0.0` 或 `## v1.0.0 (2026-11-04)`，與 MinVer 的 tag 前綴 `v` 一致。

`release.yml` 的 `create-release` job 以 `changelog-path` 將本檔交給 `CloudyWing/devops-templates` 的 reusable workflow，由它擷取與 tag 版號相同的區段作為 release notes。比對不到版本區段時，發版會改用 GitHub 自動產生的 release notes，因此建立 tag 前應先確認本檔已有對應版本的標題。比對規則見 [發版流程](docs/articles/release.md)。

區段內的分類使用 `###`。該 workflow 擷取版本區段後會把每個標題提升一級，因此 `### 新增` 在 release notes 中會成為二級標題。

## v0.1.0

### 新增

- 文件模型設定。以 `ElasticsearchDocumentsOptions.ConfigureDocument<TDocument>` 集中宣告 Index 名稱、文件 ID 選取器與 mapping，取代散落在呼叫端的字串常數。
- Index 名稱驗證與初始化。`ElasticsearchIndexNameValidator` 依 Elasticsearch 的命名限制驗證名稱，含 255 位元組上限與非法字元檢查；`ElasticsearchIndexInitializer` 依文件模型顯式建立 Index。
- 單筆文件操作。`ElasticsearchDocumentStore<TDocument>` 提供寫入、讀取、部分更新與刪除，並以 `DocumentOperationResult` 與 `DocumentGetResult<TDocument>` 回傳可判斷控制流程的結果。
- 建立文件。`CreateAsync` 以 `op_type=create` 只在文件 ID 不存在時建立文件，ID 已存在時回傳狀態碼 409 的失敗結果，既有文件不會被覆寫。
- 樂觀並行控制。`GetAsync` 與單筆寫入的結果帶回 `SeqNo` 與 `PrimaryTerm`，`IndexAsync` 與 `UpdatePartialAsync` 可透過 `IndexDocumentOptions` 與 `UpdateDocumentOptions` 的 `IfSeqNo`、`IfPrimaryTerm` 帶入，版本不符時回傳失敗結果。
- 更新衝突重試。`UpdateDocumentOptions.RetryOnConflict` 設定 `UpdatePartialAsync` 發生版本衝突時的重試次數，預設為 0；與 `IfSeqNo` 並用時擲出 `InvalidOperationException`。
- Script 更新。`UpdateByScriptAsync` 以 Update API 對指定 ID 的文件執行 script，回傳單筆操作結果。
- 單筆寫入的 refresh。`DocumentWriteOptions`、`IndexDocumentOptions` 與 `UpdateDocumentOptions` 的 `Refresh` 可逐次指定 refresh 模式，預設不 refresh。
- 條件式更新與刪除。`UpdateByQueryAsync` 與 `DeleteByQueryAsync` 接受 `ElasticsearchQueryFilterBuilder<TDocument>` 與選擇性的官方 descriptor 委派，預設 refresh 並等待完成，以 `ByQueryOperationResult` 回傳 `Updated`、`Deleted` 與 `VersionConflicts`。沒有任何查詢條件時擲出 `ArgumentException`，需要處理全部文件時以 `ConfigureRequest` 明確指定 `match_all`。
- 筆數計算。`CountAsync` 依 filter 條件回傳 `DocumentCountResult`，builder 沒有條件時計算全部文件。
- 一般查詢。`SearchAsync` 以 `DocumentSearchRequest<TDocument>` 取得文件清單，可指定 `Size`，未指定時使用 Elasticsearch 預設的 10 筆。
- from/size 分頁。`SearchPageAsync` 以 `DocumentPageRequest<TDocument>` 取得指定頁的文件，並在 `DocumentPageResult<TDocument>` 回傳總筆數，預設精確計數，可透過 `TrackTotalHits` 關閉。
- 地理座標型別。`GeoPoint` 為存放於 `geo_shape` 欄位的 GeoJSON Point，經緯度使用 `double` 並於建構時驗證範圍；`GeoPolygon` 只作為查詢條件，建構時驗證至少 3 個不同座標，首尾不同時自動閉合。
- 地理空間關係查詢。`GeoPolygonWithRelation` 支援 `Within`、`Intersects`、`Disjoint` 與 `Contains` 四種關係；`GeoPolygonWithin` 與 `GeoPolygonWithRelation` 另提供以 `Expression` 指定欄位的多載。
- 查詢條件組合。`ElasticsearchQueryFilterBuilder<TDocument>` 組合 term、terms、wildcard、regexp、範圍、geo polygon 與巢狀 bool 條件，並支援 `_source` projection。
- Bulk 寫入。`BulkIndexAsync` 支援同步集合與 `IAsyncEnumerable<TDocument>` 來源，可設定每批大小、整批失敗重試、重試等待時間與 dead letter callback，並以 `BulkOperationFailure` 回傳單筆失敗資訊。
- Bulk 重試的安全前提。啟用重試時若文件模型未設定 `IdSelector`，`BulkIndexAsync` 會在送出任何請求前擲出 `InvalidOperationException`，避免沒有固定 `_id` 的文件在整批重送時重複寫入。官方 client 從 `Id` 屬性推斷 `_id` 的行為取決於呼叫端的 client 設定，Store 不以此判定，文件型別已有 `Id` 屬性時以 `HasId` 宣告即可。呼叫端可接受 at-least-once 寫入時，設定 `AllowRetryWithoutIdSelector` 為 `true` 明確放行。
- 取消語意。呼叫端取消權杖後，`BulkIndexAsync` 擲出 `OperationCanceledException`，不會把取消回報成寫入失敗結果。取消以呼叫端權杖狀態判定，原始例外或無效回應帶有的原始例外保留在 `InnerException`。取消前 Elasticsearch 已回報的 item 失敗仍會交給 `DeadLetterCallback`，callback 收到的權杖此時為取消狀態。
- Search-After 分頁。`SearchAfterAsync` 以 `SearchAfterRequest<TDocument>` 與 `SearchAfterPage<TDocument>` 處理深層分頁。
- Alias 切換。`ElasticsearchAliasSwitcher` 以單次請求將寫入 Alias 從舊 Index 切換至新 Index。
- 可觀測性。文件操作、Index 初始化與 Alias 切換發出可由 OpenTelemetry 收集的 `Activity`，含 Index 名稱、操作類型與結果標籤。
- 相依性注入擴充。`AddElasticsearchDocuments` 註冊文件模型解析器、Index 名稱解析器與 Document Store。
