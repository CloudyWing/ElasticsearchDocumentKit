using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using CloudyWing.ElasticsearchDocumentKit.Geo;
using CloudyWing.ElasticsearchDocumentKit.Querying;
using CloudyWing.ElasticsearchDocumentKit.Tests.Internal;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

namespace CloudyWing.ElasticsearchDocumentKit.Tests.Querying;

[TestFixture]
internal sealed class ElasticsearchQueryFilterBuilderTests {
    [Test]
    public void Build_WithEmptyValues_ShouldSkipFilters() {
        ElasticsearchQueryFilterBuilder<SampleDocument> sut = new();

        IReadOnlyList<Action<Elastic.Clients.Elasticsearch.QueryDsl.QueryDescriptor<SampleDocument>>> result = sut
            .TermKeyword(document => document.Name, "")
            .TermsKeyword(document => document.Id, [])
            .ContainsKeyword(document => document.Name, null)
            .NumberRange(document => document.SortKey)
            .Build();

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Build_WithNullBlankAndEmptyCollections_ShouldSkipFilters() {
        ElasticsearchQueryFilterBuilder<SampleDocument> sut = new();

        IReadOnlyList<Action<Elastic.Clients.Elasticsearch.QueryDsl.QueryDescriptor<SampleDocument>>> result = sut
            .Term(document => document.Name, null)
            .Term(document => document.Name, " ")
            .Terms(document => document.Id, null)
            .Terms(document => document.Id, new object?[] { null, "", " " })
            .WildcardKeyword(document => document.Name, null)
            .RegexpKeyword(document => document.Name, " ")
            .GeoPolygonWithin("location", null)
            .GeoPolygonWithin("location", [])
            .Should(_ => { })
            .MustNot(_ => { })
            .Build();

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Build_WithSingleRangeBoundariesAndClosedPolygon_AddsFilters() {
        ElasticsearchQueryFilterBuilder<SampleDocument> sut = new();

        IReadOnlyList<Action<Elastic.Clients.Elasticsearch.QueryDsl.QueryDescriptor<SampleDocument>>> result = sut
            .Terms(document => document.Id, new object?[] { "order-1", "order-2" })
            .NumberRange(document => document.SortKey, greaterThanOrEqualTo: 1)
            .NumberRange(document => document.SortKey, lessThanOrEqualTo: 10)
            .DateRange(
                document => document.CreatedAt,
                greaterThanOrEqualTo: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            )
            .DateRange(
                document => document.CreatedAt,
                lessThanOrEqualTo: new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero)
            )
            .GeoPolygonWithin(
                "location",
                [
                    (121.0, 25.0),
                    (122.0, 25.0),
                    (122.0, 26.0),
                    (121.0, 25.0)
                ]
            )
            .Build();

        Assert.That(result, Has.Count.EqualTo(6));
    }

    [Test]
    public void Build_WithKeywordAndNonStringValues_AddsFilters() {
        ElasticsearchQueryFilterBuilder<SampleDocument> sut = new();

        IReadOnlyList<Action<Elastic.Clients.Elasticsearch.QueryDsl.QueryDescriptor<SampleDocument>>> result = sut
            .Term(document => document.SortKey, 10)
            .TermKeyword(document => document.Name, "Order", suffix: "exact")
            .TermsKeyword(document => document.Id, ["order-1"], suffix: "exact")
            .Build();

        Assert.That(result, Has.Count.EqualTo(3));
    }

    [Test]
    public void Build_WithCommonFilters_ShouldReturnConfiguredFilters() {
        ElasticsearchQueryFilterBuilder<SampleDocument> sut = new();

        IReadOnlyList<Action<Elastic.Clients.Elasticsearch.QueryDsl.QueryDescriptor<SampleDocument>>> result = sut
            .TermKeyword(document => document.Id, "order-1")
            .ContainsKeyword(document => document.Name, "Order")
            .RegexpKeyword(document => document.Name, ".*Order.*")
            .NumberRange(document => document.SortKey, greaterThanOrEqualTo: 1, lessThanOrEqualTo: 10)
            .GeoPolygonWithin(
                "location",
                [
                    (121.0, 25.0),
                    (122.0, 25.0),
                    (122.0, 26.0)
                ]
            )
            .Should(should => should
                .TermKeyword(document => document.Name, "A")
                .TermKeyword(document => document.Name, "B")
            )
            .Build();

        Assert.That(result, Has.Count.EqualTo(6));
    }

    [Test]
    public void Build_ConditionalAndNestedFilters_ReturnsOnlyEnabledFilters() {
        ElasticsearchQueryFilterBuilder<SampleDocument> sut = new();

        IReadOnlyList<Action<Elastic.Clients.Elasticsearch.QueryDsl.QueryDescriptor<SampleDocument>>> result = sut
            .Raw(query => query.MatchAll())
            .When(false, builder => builder.Term(document => document.Id, "ignored"))
            .When(true, builder => builder.Exists(document => document.Name))
            .Exists(document => document.Name, false)
            .DateRange(
                document => document.CreatedAt,
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero)
            )
            .MustNot(builder => builder.Term(document => document.Id, "deleted"))
            .MustNot(_ => { })
            .Build();

        Assert.That(result, Has.Count.EqualTo(4));
    }

    [Test]
    public async Task QueryFilters_WithCommonFilters_ShouldSerializeExpectedJsonAsync() {
        using JsonDocument document = await CaptureSearchRequestJsonAsync(
            descriptor => descriptor.QueryFilters(filters => filters
                .TermKeyword(sample => sample.Id, "order-1")
                .ContainsKeyword(sample => sample.Name, "Order")
                .RegexpKeyword(sample => sample.Name, ".*Order.*")
                .NumberRange(sample => sample.SortKey, greaterThanOrEqualTo: 1, lessThanOrEqualTo: 10)
                .GeoPolygonWithin(
                    "location",
                    [
                        (121.0, 25.0),
                        (122.0, 25.0),
                        (122.0, 26.0)
                    ]
                )
                .Should(should => should
                    .TermKeyword(sample => sample.Name, "A")
                    .TermKeyword(sample => sample.Name, "B")
                )
            )
        ).ConfigureAwait(false);
        JsonElement filter = document.RootElement
            .GetProperty("query")
            .GetProperty("bool")
            .GetProperty("filter");

        using (Assert.EnterMultipleScope()) {
            Assert.That(filter.GetArrayLength(), Is.EqualTo(6));
            Assert.That(filter[0].GetProperty("term").TryGetProperty("id.keyword", out _), Is.True);
            Assert.That(filter[1].GetProperty("wildcard").TryGetProperty("name.keyword", out _), Is.True);
            Assert.That(
                filter[2].GetProperty("regexp").GetProperty("name.keyword").GetProperty("value").GetString(),
                Is.EqualTo(".*Order.*")
            );
            Assert.That(
                filter[3].GetProperty("range").GetProperty("sortKey").GetProperty("gte").GetDouble(),
                Is.EqualTo(1)
            );
            Assert.That(
                filter[3].GetProperty("range").GetProperty("sortKey").GetProperty("lte").GetDouble(),
                Is.EqualTo(10)
            );
            Assert.That(
                filter[4]
                    .GetProperty("geo_shape")
                    .GetProperty("location")
                    .GetProperty("shape")
                    .GetProperty("type")
                    .GetString(),
                Is.EqualTo("polygon")
            );
            Assert.That(filter[5].GetProperty("bool").GetProperty("should").GetArrayLength(), Is.EqualTo(2));
        }
    }

    [Test]
    public async Task QueryFilters_WithDeferredFilterBranches_ShouldSerializeAllConfiguredFiltersAsync() {
        using JsonDocument document = await CaptureSearchRequestJsonAsync(
            descriptor => descriptor.QueryFilters(filters => filters
                .Terms(sample => sample.Id, ["order-1", "order-2"])
                .Exists(sample => sample.Name)
                .WildcardKeyword(sample => sample.Name, "*Order*", caseInsensitive: true)
                .NumberRange(sample => sample.SortKey, greaterThanOrEqualTo: 1)
                .NumberRange(sample => sample.SortKey, lessThanOrEqualTo: 10)
                .DateRange(
                    sample => sample.CreatedAt,
                    greaterThanOrEqualTo: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
                )
                .DateRange(
                    sample => sample.CreatedAt,
                    lessThanOrEqualTo: new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero)
                )
                .MustNot(mustNot => mustNot.Term(sample => sample.Id, "deleted"))
            )
        ).ConfigureAwait(false);
        JsonElement filters = document.RootElement
            .GetProperty("query")
            .GetProperty("bool")
            .GetProperty("filter");

        using (Assert.EnterMultipleScope()) {
            Assert.That(filters.GetArrayLength(), Is.EqualTo(8));
            Assert.That(filters[0].GetProperty("terms").ValueKind, Is.EqualTo(JsonValueKind.Object));
            Assert.That(filters[1].GetProperty("exists").ValueKind, Is.EqualTo(JsonValueKind.Object));
            Assert.That(filters[2].GetProperty("wildcard").ValueKind, Is.EqualTo(JsonValueKind.Object));
            Assert.That(
                filters[3].GetProperty("range").GetProperty("sortKey").GetProperty("gte").GetDouble(),
                Is.EqualTo(1)
            );
            Assert.That(
                filters[4].GetProperty("range").GetProperty("sortKey").GetProperty("lte").GetDouble(),
                Is.EqualTo(10)
            );
            Assert.That(
                filters[5].GetProperty("range").GetProperty("createdAt").GetProperty("gte").GetString(),
                Is.EqualTo("2026-01-01T00:00:00.0000000Z")
            );
            Assert.That(
                filters[6].GetProperty("range").GetProperty("createdAt").GetProperty("lte").GetString(),
                Is.EqualTo("2026-12-31T23:59:59.0000000Z")
            );
            Assert.That(
                filters[7].GetProperty("bool").GetProperty("must_not").ValueKind,
                Is.EqualTo(JsonValueKind.Object)
            );
        }
    }

    [Test]
    public async Task SourceIncludes_WithFields_ShouldSendSourceIncludesQueryStringAsync() {
        ApiCallDetails apiCallDetails = await CaptureSearchApiCallDetailsAsync(descriptor => descriptor.SourceIncludes(
            sample => sample.Id,
            sample => sample.Name
        )).ConfigureAwait(false);
        string query = Uri.UnescapeDataString(apiCallDetails.Uri?.Query ?? "");

        Assert.That(query, Does.Contain("_source_includes=id,name"));
    }

    [Test]
    public async Task SourceExcludes_WithFields_SendsSourceExcludesQueryStringAsync() {
        ApiCallDetails apiCallDetails = await CaptureSearchApiCallDetailsAsync(descriptor => descriptor.SourceExcludes(
            sample => sample.Name
        )).ConfigureAwait(false);
        string query = Uri.UnescapeDataString(apiCallDetails.Uri?.Query ?? "");

        Assert.That(query, Does.Contain("_source_excludes=name"));
    }

    [Test]
    public async Task DisableSource_ValidDescriptor_SerializesSourceAsFalseAsync() {
        using JsonDocument document = await CaptureSearchRequestJsonAsync(
            descriptor => descriptor.DisableSource()
        ).ConfigureAwait(false);

        Assert.That(document.RootElement.GetProperty("_source").GetBoolean(), Is.False);
    }

    [Test]
    public void GeoPolygonWithin_WithInvalidPolygon_ShouldThrowArgumentException() {
        ElasticsearchQueryFilterBuilder<SampleDocument> sut = new();

        Assert.That(
            () => sut.GeoPolygonWithin(
                "location",
                [
                    (121.0, 25.0),
                    (122.0, 25.0)
                ]
            ),
            Throws.TypeOf<ArgumentException>()
                .With.Property(nameof(ArgumentException.ParamName)).EqualTo("coordinates")
        );
    }

    [Test]
    public void SearchDescriptorExtensions_WithNullArguments_ShouldThrowArgumentNullException() {
        SearchRequestDescriptor<SampleDocument> descriptor = new();

        using (Assert.EnterMultipleScope()) {
            Assert.That(
                () => descriptor.QueryFilters(null!),
                Throws
                    .TypeOf<ArgumentNullException>()
                    .With.Property(nameof(ArgumentNullException.ParamName)).EqualTo("configure")
            );
            Assert.That(
                () => ElasticsearchSearchRequestDescriptorExtensions.SourceIncludes(
                    descriptor,
                    (Expression<Func<SampleDocument, object?>>[])null!
                ),
                Throws
                    .TypeOf<ArgumentNullException>()
                    .With.Property(nameof(ArgumentNullException.ParamName)).EqualTo("fields")
            );
            Assert.That(
                () => ElasticsearchSearchRequestDescriptorExtensions.SourceExcludes(
                    descriptor,
                    (Expression<Func<SampleDocument, object?>>[])null!
                ),
                Throws
                    .TypeOf<ArgumentNullException>()
                    .With.Property(nameof(ArgumentNullException.ParamName)).EqualTo("fields")
            );
        }
    }

    [TestCaseSource(nameof(GeoRelationCases))]
    public void GeoPolygonWithRelation_WithRelation_SerializesRelation(GeoShapeRelation relation, string expected) {
        using JsonDocument document = SerializeFilters<SampleDocument>(
            filters => filters.GeoPolygonWithRelation("location", CreatePolygon(), relation)
        );
        JsonElement location = document.RootElement
            .GetProperty("query")
            .GetProperty("bool")
            .GetProperty("filter")
            .GetProperty("geo_shape")
            .GetProperty("location");

        using (Assert.EnterMultipleScope()) {
            Assert.That(location.GetProperty("relation").GetString(), Is.EqualTo(expected));
            Assert.That(location.GetProperty("shape").GetProperty("type").GetString(), Is.EqualTo("polygon"));
        }
    }

    [Test]
    public void GeoPolygonWithRelation_WithOpenPolygon_SerializesClosedRing() {
        using JsonDocument document = SerializeFilters<SampleDocument>(
            filters => filters.GeoPolygonWithRelation("location", CreatePolygon(), GeoShapeRelation.Intersects)
        );
        JsonElement ring = document.RootElement
            .GetProperty("query")
            .GetProperty("bool")
            .GetProperty("filter")
            .GetProperty("geo_shape")
            .GetProperty("location")
            .GetProperty("shape")
            .GetProperty("coordinates")[0];

        using (Assert.EnterMultipleScope()) {
            Assert.That(ring.GetArrayLength(), Is.EqualTo(4));
            Assert.That(ToDoubles(ring[0]), Is.EqualTo(new[] { 121.0, 25.0 }));
            Assert.That(ToDoubles(ring[1]), Is.EqualTo(new[] { 122.0, 25.0 }));
            Assert.That(ToDoubles(ring[2]), Is.EqualTo(new[] { 122.0, 26.0 }));
            Assert.That(ToDoubles(ring[3]), Is.EqualTo(new[] { 121.0, 25.0 }));
        }
    }

    [TestCaseSource(nameof(GeoRelationCases))]
    public void GeoPolygonWithRelation_WithExpressionField_SerializesSameAsStringField(
        GeoShapeRelation relation,
        string expected
    ) {
        using JsonDocument fromString = SerializeFilters<GeoSampleDocument>(
            filters => filters.GeoPolygonWithRelation("location", CreatePolygon(), relation)
        );
        using JsonDocument fromExpression = SerializeFilters<GeoSampleDocument>(
            filters => filters.GeoPolygonWithRelation(document => document.Location, CreatePolygon(), relation)
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(fromExpression.RootElement.GetRawText(), Is.EqualTo(fromString.RootElement.GetRawText()));
            Assert.That(fromExpression.RootElement.GetRawText(), Does.Contain(expected));
        }
    }

    [Test]
    public void GeoPolygonWithin_WithExpressionField_SerializesSameAsStringField() {
        IEnumerable<(double Longitude, double Latitude)> coordinates = [
            (121.0, 25.0),
            (122.0, 25.0),
            (122.0, 26.0)
        ];

        using JsonDocument fromString = SerializeFilters<GeoSampleDocument>(
            filters => filters.GeoPolygonWithin("location", coordinates)
        );
        using JsonDocument fromExpression = SerializeFilters<GeoSampleDocument>(
            filters => filters.GeoPolygonWithin(document => document.Location, coordinates)
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(fromExpression.RootElement.GetRawText(), Is.EqualTo(fromString.RootElement.GetRawText()));
            Assert.That(fromExpression.RootElement.GetRawText(), Does.Contain("\"relation\":\"within\""));
        }
    }

    [Test]
    public void GeoPolygonWithin_WithExpressionFieldAndInvalidPolygon_ThrowsArgumentException() {
        ElasticsearchQueryFilterBuilder<GeoSampleDocument> sut = new();

        Assert.That(
            () => sut.GeoPolygonWithin(document => document.Location, [(121.0, 25.0), (122.0, 25.0)]),
            Throws.TypeOf<ArgumentException>()
                .With.Property(nameof(ArgumentException.ParamName)).EqualTo("coordinates")
        );
    }

    [Test]
    public void GeoPolygonWithin_WithExpressionFieldAndNullOrEmptyCoordinates_SkipsFilter() {
        ElasticsearchQueryFilterBuilder<GeoSampleDocument> sut = new();

        IReadOnlyList<Action<Elastic.Clients.Elasticsearch.QueryDsl.QueryDescriptor<GeoSampleDocument>>> result = sut
            .GeoPolygonWithin(document => document.Location, null)
            .GeoPolygonWithin(document => document.Location, [])
            .Build();

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void GeoPolygonWithRelation_WithNullPolygon_SkipsFilter() {
        ElasticsearchQueryFilterBuilder<GeoSampleDocument> sut = new();

        IReadOnlyList<Action<Elastic.Clients.Elasticsearch.QueryDsl.QueryDescriptor<GeoSampleDocument>>> result = sut
            .GeoPolygonWithRelation("location", null, GeoShapeRelation.Within)
            .GeoPolygonWithRelation(document => document.Location, null, GeoShapeRelation.Contains)
            .Build();

        Assert.That(result, Is.Empty);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void GeoPolygonWithRelation_WithBlankFieldName_ThrowsArgumentException(string? fieldName) {
        ElasticsearchQueryFilterBuilder<SampleDocument> sut = new();

        Assert.That(
            () => sut.GeoPolygonWithRelation(fieldName!, CreatePolygon(), GeoShapeRelation.Within),
            Throws.InstanceOf<ArgumentException>()
        );
    }

    [Test]
    public void GeoPolygonWithRelation_WithNullExpressionField_ThrowsArgumentNullException() {
        ElasticsearchQueryFilterBuilder<SampleDocument> sut = new();

        Assert.That(
            () => sut.GeoPolygonWithRelation(
                (Expression<Func<SampleDocument, object?>>)null!,
                CreatePolygon(),
                GeoShapeRelation.Within
            ),
            Throws.TypeOf<ArgumentNullException>()
                .With.Property(nameof(ArgumentNullException.ParamName)).EqualTo("field")
        );
    }

    [Test]
    public void GeoPolygonWithin_WithNullExpressionField_ThrowsArgumentNullException() {
        ElasticsearchQueryFilterBuilder<SampleDocument> sut = new();

        Assert.That(
            () => sut.GeoPolygonWithin((Expression<Func<SampleDocument, object?>>)null!, [(121.0, 25.0)]),
            Throws.TypeOf<ArgumentNullException>()
                .With.Property(nameof(ArgumentNullException.ParamName)).EqualTo("field")
        );
    }

    private static IEnumerable<TestCaseData> GeoRelationCases() {
        yield return new TestCaseData(GeoShapeRelation.Within, "within");
        yield return new TestCaseData(GeoShapeRelation.Intersects, "intersects");
        yield return new TestCaseData(GeoShapeRelation.Disjoint, "disjoint");
        yield return new TestCaseData(GeoShapeRelation.Contains, "contains");
    }

    private static double[] ToDoubles(JsonElement array) {
        return array.EnumerateArray().Select(value => value.GetDouble()).ToArray();
    }

    private static GeoPolygon CreatePolygon() {
        return new GeoPolygon([
            new GeoPoint(121.0, 25.0),
            new GeoPoint(122.0, 25.0),
            new GeoPoint(122.0, 26.0)
        ]);
    }

    private static JsonDocument SerializeFilters<TDocument>(
        Action<ElasticsearchQueryFilterBuilder<TDocument>> configure
    )
        where TDocument : class {
        ElasticsearchClient client = RecordingClientFactory.Create();
        SearchRequest request = new SearchRequestDescriptor<TDocument>().QueryFilters(configure);

        return JsonDocument.Parse(RecordingClientFactory.Serialize(client, request));
    }

    private static async Task<JsonDocument> CaptureSearchRequestJsonAsync(
        Func<SearchRequestDescriptor<SampleDocument>, SearchRequestDescriptor<SampleDocument>> configure
    ) {
        ApiCallDetails apiCallDetails = await CaptureSearchApiCallDetailsAsync(configure).ConfigureAwait(false);
        byte[] requestBody = apiCallDetails.RequestBodyInBytes
            ?? throw new InvalidOperationException("Search request body was not captured.");

        return JsonDocument.Parse(requestBody);
    }

    private static async Task<ApiCallDetails> CaptureSearchApiCallDetailsAsync(
        Func<SearchRequestDescriptor<SampleDocument>, SearchRequestDescriptor<SampleDocument>> configure
    ) {
        byte[] responseBody = Encoding.UTF8.GetBytes(
            """
            {
              "took": 0,
              "timed_out": false,
              "_shards": {
                "total": 0,
                "successful": 0,
                "skipped": 0,
                "failed": 0
              },
              "hits": {
                "total": {
                  "value": 0,
                  "relation": "eq"
                },
                "hits": []
              }
            }
            """
        );
        Dictionary<string, IEnumerable<string>> headers = new(StringComparer.OrdinalIgnoreCase) {
            ["X-Elastic-Product"] = ["Elasticsearch"]
        };
        InMemoryRequestInvoker requestInvoker = new(responseBody, 200, null, "application/json", headers);
        ElasticsearchClientSettings settings = new(requestInvoker);
        settings.DisableDirectStreaming();
        ElasticsearchClient client = new(settings);
        SearchResponse<SampleDocument> response = await client
            .SearchAsync<SampleDocument>(descriptor => configure(descriptor))
            .ConfigureAwait(false);

        return response.ApiCallDetails ?? throw new InvalidOperationException("Search API call details were not captured.");
    }
}
