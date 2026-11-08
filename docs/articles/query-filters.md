# 查詢條件擴充

`ElasticsearchQueryFilterBuilder<TDocument>` 將常用的 filter clause 組成可讀的鏈式 API。需要完整 Elasticsearch DSL 時，可用 `Raw` 直接設定官方 `QueryDescriptor<TDocument>`，不必為少見查詢擴大套件 API。

| 方法 | 語意 |
| --- | --- |
| `QueryFilters` | 將 builder 結果套用為 bool filter query |
| `Term` 與 `Terms` | 對一般欄位建立精確比對 |
| `TermKeyword` 與 `TermsKeyword` | 對 `.keyword` 子欄位建立精確比對 |
| `NumberRange` 與 `DateRange` | 建立數值或時間範圍條件 |
| `Should` 與 `MustNot` | 建立巢狀 should 與排除條件 |
| `GeoPolygonWithin` 與 `GeoPolygonWithRelation` | 以多邊形與空間關係建立 geo_shape 條件 |

## 套用常用條件

`ElasticsearchSearchRequestDescriptorExtensions.QueryFilters` 建立 bool filter。沒有任何有效條件時，它會保留原 search descriptor，不會送出空的 bool filter。

```csharp
using CloudyWing.ElasticsearchDocumentKit.Querying;
using Elastic.Clients.Elasticsearch;

SearchRequestDescriptor<OrderDocument> descriptor = new();
SearchRequestDescriptor<OrderDocument> configured = ElasticsearchSearchRequestDescriptorExtensions.QueryFilters(
    descriptor,
    filters => filters
        .TermKeyword(document => document.CustomerName, "CloudyWing")
        .NumberRange(document => document.TotalAmount, greaterThanOrEqualTo: 1000)
);
```

`ElasticsearchQueryFilterBuilder<TDocument>` 會忽略 `null`、空白字串與空集合等無意義輸入。呼叫端仍應在輸入邊界驗證使用者可接受的篩選條件。

## 選擇一般欄位與 keyword 欄位

`Term` 適合 mapping 為 keyword、數字或布林值的欄位。`TermKeyword` 將欄位名稱加上預設 `keyword` suffix，適合 Elasticsearch `text` 欄位的精確比對。

```csharp
using CloudyWing.ElasticsearchDocumentKit.Querying;

ElasticsearchQueryFilterBuilder<OrderDocument> filters = new ElasticsearchQueryFilterBuilder<OrderDocument>();
filters
    .Term(document => document.TotalAmount, 1200m)
    .Terms(document => document.Id, ["ORD-001", "ORD-002"])
    .TermKeyword(document => document.CustomerName, "CloudyWing")
    .TermsKeyword(document => document.CustomerName, ["CloudyWing", "Contoso"]);

IReadOnlyList<Action<Elastic.Clients.Elasticsearch.QueryDsl.QueryDescriptor<OrderDocument>>> clauses = filters.Build();
```

若 mapping 使用非預設 suffix，可在 keyword 方法傳入 `suffix`。不要將 analyzed text 欄位當成精確比對欄位，否則查詢語意會隨 analyzer 設定改變。

## 範圍、文字模式與地理範圍

範圍方法可只提供下限或上限。Wildcard 與 regexp 會增加查詢成本，應限制輸入長度，並使用 mapping 支援該查詢的欄位。

```csharp
descriptor.QueryFilters(filters => filters
    .DateRange(
        document => document.CreatedAt,
        greaterThanOrEqualTo: startTime,
        lessThanOrEqualTo: endTime
    )
    .WildcardKeyword(document => document.CustomerName, "cloud*", caseInsensitive: true)
    .RegexpKeyword(document => document.CustomerName, ".*wing.*")
    .GeoPolygonWithin(
        "location",
        [(121.0, 25.0), (122.0, 25.0), (122.0, 26.0)]
    )
);
```

`GeoPolygonWithin` 的座標順序為 longitude、latitude。最後一點未與第一點重合時，builder 會補上閉合點，呼叫端仍應確認輸入範圍符合資料的座標系統。

### GeoPoint 與 geo_shape 欄位

`GeoPoint` 位於 `CloudyWing.ElasticsearchDocumentKit.Geo` 命名空間，用於 `geo_shape` 欄位。它序列化為 GeoJSON Point，座標順序為經度、緯度，例如 `{"type":"Point","coordinates":[121.5,25.05]}`。建構式的參數順序為 `GeoPoint(longitude, latitude)`，可由 `Longitude` 與 `Latitude` 屬性讀回。經緯度為 `double`，建構時驗證範圍，經度須介於 -180 與 180，緯度須介於 -90 與 90。超出範圍、`NaN` 或無限大會擲出 `ArgumentOutOfRangeException`。從 `_source` 反序列化時，非 Point 的 GeoJSON、座標缺漏或超出範圍會擲出 `JsonException`。

文件屬性使用 `GeoPoint?` 型別時，mapping 須以 `GeoShape` 宣告，geo_shape 查詢才會對該欄位生效。

```csharp
using CloudyWing.ElasticsearchDocumentKit.Geo;

public sealed class ShipLocationDocument {
    public string Id { get; init; } = "";

    public GeoPoint? Location { get; init; }
}
```

```csharp
options.ConfigureDocument<ShipLocationDocument>(document => document
    .ToIndex("ship-locations")
    .HasId(location => location.Id)
    .ConfigureMapping(mapping => mapping.Properties(properties => properties
        .Keyword(field => field.Id)
        .GeoShape(field => field.Location)
    ))
);
```

### GeoPolygon 與空間關係

`GeoPolygon` 只作為查詢條件，不支援內環，也不提供 JSON converter，不能當成文件屬性。建構時至少需要 3 個不同座標，不足時擲出 `ArgumentException`，`points` 為 `null` 時擲出 `ArgumentNullException`。首尾座標不同時自動補上閉合點，`Points` 回傳已閉合的座標清單。

`GeoPolygonWithRelation` 以官方 `GeoShapeRelation` 指定空間關係，支援下列四種。

| 關係 | 命中條件 |
| --- | --- |
| `GeoShapeRelation.Within` | 文件的形狀完全位於多邊形內 |
| `GeoShapeRelation.Intersects` | 文件的形狀與多邊形有重疊 |
| `GeoShapeRelation.Disjoint` | 文件的形狀與多邊形沒有重疊 |
| `GeoShapeRelation.Contains` | 文件的形狀完全包含多邊形 |

關係會序列化為 `geo_shape.<欄位>.relation`，與 `shape` 屬於同一層。文件欄位是 `GeoPoint` 時，點無法包含多邊形，`Contains` 不會命中任何文件。`polygon` 為 `null` 時，`GeoPolygonWithRelation` 略過該條件，行為與座標 tuple 版的 `GeoPolygonWithin` 一致。

```csharp
using CloudyWing.ElasticsearchDocumentKit.Geo;
using Elastic.Clients.Elasticsearch;

GeoPolygon area = new([
    new GeoPoint(121.0, 25.0),
    new GeoPoint(122.0, 25.0),
    new GeoPoint(122.0, 26.0),
    new GeoPoint(121.0, 26.0)
]);

descriptor.QueryFilters(filters => filters
    .GeoPolygonWithRelation(document => document.Location, area, GeoShapeRelation.Intersects)
);
```

### 以 Expression 指定 geo 欄位

`GeoPolygonWithin` 與 `GeoPolygonWithRelation` 除了字串欄位名稱，也提供 `Expression<Func<TDocument, object?>>` 欄位的多載，例如 `document => document.Location`。兩種寫法輸出的 JSON 相同，使用 Expression 版本時，欄位更名後編譯器會找出沒有同步修改的地方。欄位名稱字串為空白時擲出 `ArgumentException`，Expression 為 `null` 時擲出 `ArgumentNullException`。

```csharp
descriptor.QueryFilters(filters => filters
    .GeoPolygonWithin(
        document => document.Location,
        [(121.0, 25.0), (122.0, 25.0), (122.0, 26.0)]
    )
);
```

座標 tuple 版的 `GeoPolygonWithin` 固定使用 `Within` 關係，需要其他關係時使用 `GeoPolygonWithRelation`。

## 組合可選條件

`When` 可將可選輸入與其查詢條件放在相同位置。`Should` 用於至少符合一個子條件，`MustNot` 用於排除符合條件的文件。

```csharp
descriptor.QueryFilters(filters => filters
    .When(onlyActive, active => active.Term(document => document.IsActive, true))
    .Should(should => should
        .ContainsKeyword(document => document.CustomerName, searchText)
        .TermKeyword(document => document.Id, searchText)
    )
    .MustNot(excluded => excluded.Term(document => document.IsDeleted, true))
);
```

應避免以 `Should` 取代必要的篩選條件。should 子句需要影響符合條件時，確認 Elasticsearch bool query 的 `minimum_should_match` 語意與應用程式預期一致。

## 交給官方 descriptor 的查詢

`Raw` 可加入套件未抽象的 Elasticsearch query。此作法保留官方 API 的功能，同時讓常用條件維持相同的鏈式閱讀方式。

```csharp
descriptor.QueryFilters(filters => filters
    .Raw(query => query.Match(match => match
        .Field(document => document.Description)
        .Query(searchText)
    ))
    .Exists(document => document.CustomerName)
);
```

`Raw` 內容由官方 client 序列化。升級 Elasticsearch client 後，應依官方相容性說明檢查這些直接使用的 descriptor。

## 控制 `_source` 欄位

`SourceIncludes`、`SourceExcludes` 與 `DisableSource` 由 `ElasticsearchSearchRequestDescriptorExtensions` 提供。它們只影響回應內容，不會改變 query 的符合條件。

```csharp
descriptor
    .SourceIncludes(document => document.Id, document => document.CustomerName)
    .SourceExcludes(document => document.InternalNotes);

SearchRequestDescriptor<OrderDocument> metadataOnly = descriptor.DisableSource();
```

停用 `_source` 後，應確認後續流程不依賴回傳文件內容。只取需要的欄位能降低傳輸量，但不應在需要完整文件的寫回流程中使用。

## 延伸閱讀

- [Document Store](document-store.md)
- [文件模型設定](document-model.md)
- [OpenTelemetry](telemetry.md)
