using System.Globalization;
using System.Linq.Expressions;
using CloudyWing.ElasticsearchDocumentKit.Geo;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace CloudyWing.ElasticsearchDocumentKit.Querying;

/// <summary>
/// 建立常見 Elasticsearch filter query 條件。
/// </summary>
/// <typeparam name="TDocument">文件型別。</typeparam>
public sealed class ElasticsearchQueryFilterBuilder<TDocument>
    where TDocument : class {
    /// <summary>
    /// 取得 keyword 欄位預設 suffix。
    /// </summary>
    public const string KeywordSuffix = "keyword";

    private readonly List<Action<QueryDescriptor<TDocument>>> filters = [];

    /// <summary>
    /// 加入官方 query descriptor 設定動作。
    /// </summary>
    /// <param name="configureQuery">query descriptor 設定動作。</param>
    /// <returns>目前 builder。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configureQuery"/> 為 <see langword="null"/>。</exception>
    public ElasticsearchQueryFilterBuilder<TDocument> Raw(Action<QueryDescriptor<TDocument>> configureQuery) {
        ArgumentNullException.ThrowIfNull(configureQuery);

        filters.Add(configureQuery);
        return this;
    }

    /// <summary>
    /// 條件成立時執行指定的 filter 設定。
    /// </summary>
    /// <param name="condition">是否執行設定動作。</param>
    /// <param name="configure">filter builder 設定動作。</param>
    /// <returns>目前 builder。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> 為 <see langword="null"/>。</exception>
    public ElasticsearchQueryFilterBuilder<TDocument> When(
        bool condition,
        Action<ElasticsearchQueryFilterBuilder<TDocument>> configure
    ) {
        ArgumentNullException.ThrowIfNull(configure);

        if (condition) {
            configure(this);
        }

        return this;
    }

    /// <summary>
    /// 加入 term filter；值為 <see langword="null"/> 或空白字串時略過。
    /// </summary>
    /// <param name="field">欄位選擇式。</param>
    /// <param name="value">查詢值。</param>
    /// <returns>目前 builder。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="field"/> 為 <see langword="null"/>。</exception>
    public ElasticsearchQueryFilterBuilder<TDocument> Term(
        Expression<Func<TDocument, object?>> field,
        object? value
    ) {
        ArgumentNullException.ThrowIfNull(field);

        if (ShouldSkipValue(value)) {
            return this;
        }

        filters.Add(query => query.Term(term => term
            .Field(field)
            .Value(FieldValue.FromValue(value))
        ));

        return this;
    }

    /// <summary>
    /// 加入 keyword 子欄位的 term filter；值為 <see langword="null"/> 或空白字串時略過。
    /// </summary>
    /// <param name="field">欄位選擇式。</param>
    /// <param name="value">查詢值。</param>
    /// <param name="suffix">keyword 子欄位 suffix。</param>
    /// <returns>目前 builder。</returns>
    public ElasticsearchQueryFilterBuilder<TDocument> TermKeyword(
        Expression<Func<TDocument, object?>> field,
        string? value,
        string suffix = KeywordSuffix
    ) {
        return Term(AppendSuffix(field, suffix), value);
    }

    /// <summary>
    /// 加入 terms filter；集合為 <see langword="null"/> 或沒有有效值時略過。
    /// </summary>
    /// <param name="field">欄位選擇式。</param>
    /// <param name="values">查詢值集合。</param>
    /// <returns>目前 builder。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="field"/> 為 <see langword="null"/>。</exception>
    public ElasticsearchQueryFilterBuilder<TDocument> Terms(
        Expression<Func<TDocument, object?>> field,
        IEnumerable<object?>? values
    ) {
        ArgumentNullException.ThrowIfNull(field);

        if (values is null) {
            return this;
        }

        List<FieldValue> fieldValues = values
            .Where(value => !ShouldSkipValue(value))
            .Select(value => FieldValue.FromValue(value))
            .ToList();

        if (fieldValues.Count == 0) {
            return this;
        }

        filters.Add(query => query.Terms(terms => terms
            .Field(field)
            .Terms(new TermsQueryField(fieldValues))
        ));

        return this;
    }

    /// <summary>
    /// 加入 keyword 子欄位的 terms filter。
    /// </summary>
    /// <param name="field">欄位選擇式。</param>
    /// <param name="values">查詢值集合。</param>
    /// <param name="suffix">keyword 子欄位 suffix。</param>
    /// <returns>目前 builder。</returns>
    public ElasticsearchQueryFilterBuilder<TDocument> TermsKeyword(
        Expression<Func<TDocument, object?>> field,
        IEnumerable<string?>? values,
        string suffix = KeywordSuffix
    ) {
        return Terms(AppendSuffix(field, suffix), values?.Cast<object?>());
    }

    /// <summary>
    /// 加入 exists filter。
    /// </summary>
    /// <param name="field">欄位選擇式。</param>
    /// <param name="condition">是否加入 filter。</param>
    /// <returns>目前 builder。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="field"/> 為 <see langword="null"/>。</exception>
    public ElasticsearchQueryFilterBuilder<TDocument> Exists(
        Expression<Func<TDocument, object?>> field,
        bool condition = true
    ) {
        ArgumentNullException.ThrowIfNull(field);

        if (!condition) {
            return this;
        }

        filters.Add(query => query.Exists(exists => exists.Field(field)));
        return this;
    }

    /// <summary>
    /// 加入 keyword 子欄位的 wildcard contains filter；值為 <see langword="null"/> 或空白字串時略過。
    /// </summary>
    /// <param name="field">欄位選擇式。</param>
    /// <param name="value">查詢文字。</param>
    /// <param name="caseInsensitive">是否使用大小寫不敏感比對。</param>
    /// <param name="suffix">keyword 子欄位 suffix。</param>
    /// <returns>目前 builder。</returns>
    public ElasticsearchQueryFilterBuilder<TDocument> ContainsKeyword(
        Expression<Func<TDocument, object?>> field,
        string? value,
        bool caseInsensitive = false,
        string suffix = KeywordSuffix
    ) {
        if (string.IsNullOrWhiteSpace(value)) {
            return this;
        }

        return WildcardKeyword(field, $"*{EscapeWildcard(value)}*", caseInsensitive, suffix);
    }

    /// <summary>
    /// 加入 keyword 子欄位的 wildcard filter。
    /// </summary>
    /// <param name="field">欄位選擇式。</param>
    /// <param name="pattern">wildcard pattern。</param>
    /// <param name="caseInsensitive">是否使用大小寫不敏感比對。</param>
    /// <param name="suffix">keyword 子欄位 suffix。</param>
    /// <returns>目前 builder。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="field"/> 為 <see langword="null"/>。</exception>
    public ElasticsearchQueryFilterBuilder<TDocument> WildcardKeyword(
        Expression<Func<TDocument, object?>> field,
        string? pattern,
        bool caseInsensitive = false,
        string suffix = KeywordSuffix
    ) {
        ArgumentNullException.ThrowIfNull(field);

        if (string.IsNullOrWhiteSpace(pattern)) {
            return this;
        }

        Expression<Func<TDocument, object?>> keywordField = AppendSuffix(field, suffix);
        filters.Add(query => query.Wildcard(wildcard => wildcard
            .Field(keywordField)
            .Value(pattern)
            .CaseInsensitive(caseInsensitive)
        ));

        return this;
    }

    /// <summary>
    /// 加入 keyword 子欄位的 regexp filter；pattern 為 <see langword="null"/> 或空白字串時略過。
    /// </summary>
    /// <param name="field">欄位選擇式。</param>
    /// <param name="pattern">regexp pattern。</param>
    /// <param name="suffix">keyword 子欄位 suffix。</param>
    /// <returns>目前 builder。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="field"/> 為 <see langword="null"/>。</exception>
    public ElasticsearchQueryFilterBuilder<TDocument> RegexpKeyword(
        Expression<Func<TDocument, object?>> field,
        string? pattern,
        string suffix = KeywordSuffix
    ) {
        ArgumentNullException.ThrowIfNull(field);

        if (string.IsNullOrWhiteSpace(pattern)) {
            return this;
        }

        Expression<Func<TDocument, object?>> keywordField = AppendSuffix(field, suffix);
        filters.Add(query => query.Regexp(regexp => regexp
            .Field(keywordField)
            .Value(pattern)
        ));

        return this;
    }

    /// <summary>
    /// 加入 number range filter；未提供任何界線時略過。
    /// </summary>
    /// <param name="field">欄位選擇式。</param>
    /// <param name="greaterThanOrEqualTo">大於等於界線。</param>
    /// <param name="lessThanOrEqualTo">小於等於界線。</param>
    /// <returns>目前 builder。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="field"/> 為 <see langword="null"/>。</exception>
    public ElasticsearchQueryFilterBuilder<TDocument> NumberRange(
        Expression<Func<TDocument, object?>> field,
        double? greaterThanOrEqualTo = null,
        double? lessThanOrEqualTo = null
    ) {
        ArgumentNullException.ThrowIfNull(field);

        if (!greaterThanOrEqualTo.HasValue && !lessThanOrEqualTo.HasValue) {
            return this;
        }

        filters.Add(query => query.Range(range => range.Number(numberRange => {
            numberRange.Field(field);

            if (greaterThanOrEqualTo.HasValue) {
                numberRange.Gte(greaterThanOrEqualTo.Value);
            }

            if (lessThanOrEqualTo.HasValue) {
                numberRange.Lte(lessThanOrEqualTo.Value);
            }
        })));

        return this;
    }

    /// <summary>
    /// 加入 date range filter；未提供任何界線時略過。
    /// </summary>
    /// <param name="field">欄位選擇式。</param>
    /// <param name="greaterThanOrEqualTo">大於等於界線。</param>
    /// <param name="lessThanOrEqualTo">小於等於界線。</param>
    /// <returns>目前 builder。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="field"/> 為 <see langword="null"/>。</exception>
    public ElasticsearchQueryFilterBuilder<TDocument> DateRange(
        Expression<Func<TDocument, object?>> field,
        DateTimeOffset? greaterThanOrEqualTo = null,
        DateTimeOffset? lessThanOrEqualTo = null
    ) {
        ArgumentNullException.ThrowIfNull(field);

        if (!greaterThanOrEqualTo.HasValue && !lessThanOrEqualTo.HasValue) {
            return this;
        }

        filters.Add(query => query.Range(range => range.Date(dateRange => {
            dateRange.Field(field);

            if (greaterThanOrEqualTo.HasValue) {
                dateRange.Gte(ToDateMath(greaterThanOrEqualTo.Value));
            }

            if (lessThanOrEqualTo.HasValue) {
                dateRange.Lte(ToDateMath(lessThanOrEqualTo.Value));
            }
        })));

        return this;
    }

    /// <summary>
    /// 加入 geo shape polygon within filter；座標為 <see langword="null"/> 或空集合時略過。
    /// </summary>
    /// <param name="fieldName">geo shape 欄位名稱。</param>
    /// <param name="coordinates">polygon 座標集合，格式為 longitude、latitude。</param>
    /// <returns>目前 builder。</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="fieldName"/> 為空白字串，或 <paramref name="coordinates"/> 少於三個不同座標。
    /// </exception>
    public ElasticsearchQueryFilterBuilder<TDocument> GeoPolygonWithin(
        string fieldName,
        IEnumerable<(double Longitude, double Latitude)>? coordinates
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        return AddGeoPolygonWithin(fieldName, coordinates);
    }

    /// <summary>
    /// 加入 geo shape polygon within filter；座標為 <see langword="null"/> 或空集合時略過。
    /// </summary>
    /// <param name="field">geo shape 欄位。</param>
    /// <param name="coordinates">polygon 座標集合，格式為 longitude、latitude。</param>
    /// <returns>目前 builder。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="field"/> 為 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException"><paramref name="coordinates"/> 少於三個不同座標。</exception>
    public ElasticsearchQueryFilterBuilder<TDocument> GeoPolygonWithin(
        Expression<Func<TDocument, object?>> field,
        IEnumerable<(double Longitude, double Latitude)>? coordinates
    ) {
        ArgumentNullException.ThrowIfNull(field);

        return AddGeoPolygonWithin(Infer.Field<TDocument, object?>(field), coordinates);
    }

    /// <summary>
    /// 加入 geo shape polygon filter，空間關係由 <paramref name="relation"/> 指定；
    /// <paramref name="polygon"/> 為 <see langword="null"/> 時略過。
    /// </summary>
    /// <param name="fieldName">geo shape 欄位名稱。</param>
    /// <param name="polygon">查詢用的多邊形。</param>
    /// <param name="relation">欄位幾何與多邊形的空間關係，例如 <see cref="GeoShapeRelation.Within"/>。</param>
    /// <returns>目前 builder。</returns>
    /// <exception cref="ArgumentException"><paramref name="fieldName"/> 為空白字串。</exception>
    public ElasticsearchQueryFilterBuilder<TDocument> GeoPolygonWithRelation(
        string fieldName,
        GeoPolygon? polygon,
        GeoShapeRelation relation
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        return AddGeoPolygonWithRelation(fieldName, polygon, relation);
    }

    /// <summary>
    /// 加入 geo shape polygon filter，空間關係由 <paramref name="relation"/> 指定；
    /// <paramref name="polygon"/> 為 <see langword="null"/> 時略過。
    /// </summary>
    /// <param name="field">geo shape 欄位。</param>
    /// <param name="polygon">查詢用的多邊形。</param>
    /// <param name="relation">欄位幾何與多邊形的空間關係，例如 <see cref="GeoShapeRelation.Within"/>。</param>
    /// <returns>目前 builder。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="field"/> 為 <see langword="null"/>。</exception>
    public ElasticsearchQueryFilterBuilder<TDocument> GeoPolygonWithRelation(
        Expression<Func<TDocument, object?>> field,
        GeoPolygon? polygon,
        GeoShapeRelation relation
    ) {
        ArgumentNullException.ThrowIfNull(field);

        return AddGeoPolygonWithRelation(Infer.Field<TDocument, object?>(field), polygon, relation);
    }

    /// <summary>
    /// 加入至少符合其中一項的 should filter。
    /// </summary>
    /// <param name="configure">內層 filter builder 設定動作。</param>
    /// <returns>目前 builder。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> 為 <see langword="null"/>。</exception>
    public ElasticsearchQueryFilterBuilder<TDocument> Should(
        Action<ElasticsearchQueryFilterBuilder<TDocument>> configure
    ) {
        ArgumentNullException.ThrowIfNull(configure);

        ElasticsearchQueryFilterBuilder<TDocument> builder = new();
        configure(builder);
        IReadOnlyList<Action<QueryDescriptor<TDocument>>> innerFilters = builder.Build();

        if (innerFilters.Count == 0) {
            return this;
        }

        filters.Add(query => query.Bool(boolean => boolean
            .Should(innerFilters.ToArray())
            .MinimumShouldMatch(1)
        ));

        return this;
    }

    /// <summary>
    /// 加入 must-not filter。
    /// </summary>
    /// <param name="configure">內層 filter builder 設定動作。</param>
    /// <returns>目前 builder。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> 為 <see langword="null"/>。</exception>
    public ElasticsearchQueryFilterBuilder<TDocument> MustNot(
        Action<ElasticsearchQueryFilterBuilder<TDocument>> configure
    ) {
        ArgumentNullException.ThrowIfNull(configure);

        ElasticsearchQueryFilterBuilder<TDocument> builder = new();
        configure(builder);
        IReadOnlyList<Action<QueryDescriptor<TDocument>>> innerFilters = builder.Build();

        if (innerFilters.Count == 0) {
            return this;
        }

        filters.Add(query => query.Bool(boolean => boolean.MustNot(innerFilters.ToArray())));
        return this;
    }

    /// <summary>
    /// 建立官方 bool query 可接受的 filter 設定動作集合。
    /// </summary>
    /// <returns>filter 設定動作集合。</returns>
    public IReadOnlyList<Action<QueryDescriptor<TDocument>>> Build() {
        return filters.ToList();
    }

    private static bool ShouldSkipValue(object? value) {
        return value is null || value is string text && string.IsNullOrWhiteSpace(text);
    }

    private static Expression<Func<TDocument, object?>> AppendSuffix(
        Expression<Func<TDocument, object?>> field,
        string suffix
    ) {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(suffix);

        Expression<Func<TDocument, object>> nonNullableField = Expression.Lambda<Func<TDocument, object>>(
            field.Body,
            field.Parameters
        );
        Expression<Func<TDocument, object>> suffixedField = nonNullableField.AppendSuffix(suffix);

        return Expression.Lambda<Func<TDocument, object?>>(suffixedField.Body, suffixedField.Parameters);
    }

    private static string EscapeWildcard(string value) {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("?", "\\?", StringComparison.Ordinal);
    }

    private ElasticsearchQueryFilterBuilder<TDocument> AddGeoPolygonWithin(
        Field field,
        IEnumerable<(double Longitude, double Latitude)>? coordinates
    ) {
        if (coordinates is null) {
            return this;
        }

        List<(double Longitude, double Latitude)> polygonCoordinates = coordinates.ToList();

        if (polygonCoordinates.Count == 0) {
            return this;
        }

        if (polygonCoordinates.Distinct().Count() < 3) {
            throw new ArgumentException(
                "Geo polygon must contain at least three distinct coordinates.",
                nameof(coordinates)
            );
        }

        if (polygonCoordinates[0] != polygonCoordinates[^1]) {
            polygonCoordinates.Add(polygonCoordinates[0]);
        }

        return AddGeoPolygonFilter(field, polygonCoordinates, GeoShapeRelation.Within);
    }

    private ElasticsearchQueryFilterBuilder<TDocument> AddGeoPolygonWithRelation(
        Field field,
        GeoPolygon? polygon,
        GeoShapeRelation relation
    ) {
        if (polygon is null) {
            return this;
        }

        List<(double Longitude, double Latitude)> closedCoordinates = polygon.Points
            .Select(point => (point.Longitude, point.Latitude))
            .ToList();

        return AddGeoPolygonFilter(field, closedCoordinates, relation);
    }

    private ElasticsearchQueryFilterBuilder<TDocument> AddGeoPolygonFilter(
        Field field,
        IReadOnlyList<(double Longitude, double Latitude)> closedCoordinates,
        GeoShapeRelation relation
    ) {
        filters.Add(query => query.GeoShape(new GeoShapeQuery {
            Field = field,
            Shape = new GeoShapeFieldQuery {
                Shape = CreateGeoPolygonShape(closedCoordinates),
                Relation = relation
            }
        }));

        return this;
    }

    private static IReadOnlyDictionary<string, object> CreateGeoPolygonShape(
        IReadOnlyList<(double Longitude, double Latitude)> coordinates
    ) {
        double[][][] geoJsonCoordinates = [
            coordinates
                .Select(coordinate => new[] { coordinate.Longitude, coordinate.Latitude })
                .ToArray()
        ];

        return new Dictionary<string, object> {
            ["type"] = "polygon",
            ["coordinates"] = geoJsonCoordinates
        };
    }

    private static DateMath ToDateMath(DateTimeOffset value) {
        return DateMath.FromString(value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
    }
}
