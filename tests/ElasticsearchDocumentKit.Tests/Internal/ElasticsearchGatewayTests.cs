using System.Text;
using System.Text.Json;
using CloudyWing.ElasticsearchDocumentKit.Internal;
using CloudyWing.ElasticsearchDocumentKit.Querying;
using CloudyWing.ElasticsearchDocumentKit.Search;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Transport;

namespace CloudyWing.ElasticsearchDocumentKit.Tests.Internal;

[TestFixture]
internal sealed class ElasticsearchGatewayTests {
    private const string UpdateResponseBody = """
        {
          "_index": "sample-documents",
          "_id": "order-1",
          "_version": 3,
          "result": "updated",
          "_shards": { "total": 2, "successful": 1, "failed": 0 },
          "_seq_no": 9,
          "_primary_term": 2
        }
        """;

    private const string DeleteResponseBody = """
        {
          "_index": "sample-documents",
          "_id": "order-1",
          "_version": 4,
          "result": "deleted",
          "_shards": { "total": 2, "successful": 1, "failed": 0 },
          "_seq_no": 10,
          "_primary_term": 2
        }
        """;

    private const string UpdateByQueryResponseBody = """
        {
          "took": 3,
          "timed_out": false,
          "total": 3,
          "updated": 3,
          "deleted": 0,
          "batches": 1,
          "version_conflicts": 0,
          "noops": 0,
          "retries": { "bulk": 0, "search": 0 },
          "throttled_millis": 0,
          "requests_per_second": -1.0,
          "throttled_until_millis": 0,
          "failures": []
        }
        """;

    private const string DeleteByQueryResponseBody = """
        {
          "took": 3,
          "timed_out": false,
          "total": 2,
          "deleted": 2,
          "batches": 1,
          "version_conflicts": 0,
          "noops": 0,
          "retries": { "bulk": 0, "search": 0 },
          "throttled_millis": 0,
          "requests_per_second": -1.0,
          "throttled_until_millis": 0,
          "failures": []
        }
        """;

    private const string CountResponseBody = """
        {
          "count": 5,
          "_shards": { "total": 1, "successful": 1, "skipped": 0, "failed": 0 }
        }
        """;

    private const string SearchResponseBody = """
        {
          "took": 1,
          "timed_out": false,
          "_shards": { "total": 1, "successful": 1, "skipped": 0, "failed": 0 },
          "hits": {
            "total": { "value": 42, "relation": "eq" },
            "max_score": null,
            "hits": [
              {
                "_index": "sample-documents",
                "_id": "order-1",
                "_score": null,
                "_source": { "id": "order-1", "name": "Order 1", "sortKey": 1 }
              }
            ]
          }
        }
        """;

    [Test]
    public async Task IndexAsync_WithSeqNoPrimaryTermAndRefresh_SendsQueryStringParametersAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create());
        IndexDocumentOptions options = new() {
            IfSeqNo = 7,
            IfPrimaryTerm = 2,
            Refresh = Refresh.WaitFor
        };

        IndexResponse response = await sut.IndexAsync(
            "sample-documents",
            CreateDocument(),
            "order-1",
            options,
            CancellationToken.None
        );

        string query = GetQuery(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(GetPath(response), Is.EqualTo("/sample-documents/_doc/order-1"));
            Assert.That(query, Does.Contain("if_seq_no=7"));
            Assert.That(query, Does.Contain("if_primary_term=2"));
            Assert.That(query, Does.Contain("refresh=wait_for"));
        }
    }

    [Test]
    public async Task IndexAsync_WithoutDocumentIdAndWithRefresh_SendsRefreshOnAutoIdPathAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create());
        IndexDocumentOptions options = new() {
            Refresh = Refresh.True
        };

        IndexResponse response = await sut.IndexAsync(
            "sample-documents",
            new { Name = "Order 1" },
            null,
            options,
            CancellationToken.None
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(GetPath(response), Is.EqualTo("/sample-documents/_doc"));
            Assert.That(GetQuery(response), Does.Contain("refresh=true"));
        }
    }

    [Test]
    public async Task IndexAsync_WithoutOptions_DoesNotSendOptionParametersAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create());

        IndexResponse response = await sut.IndexAsync(
            "sample-documents",
            CreateDocument(),
            "order-1",
            null,
            CancellationToken.None
        );

        AssertNoOptionParameters(GetQuery(response));
    }

    [Test]
    public async Task IndexAsync_WithEmptyOptions_DoesNotSendOptionParametersAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create());

        IndexResponse response = await sut.IndexAsync(
            "sample-documents",
            CreateDocument(),
            null,
            new IndexDocumentOptions(),
            CancellationToken.None
        );

        AssertNoOptionParameters(GetQuery(response));
    }

    [Test]
    public async Task UpdateAsync_WithRetryOnConflict_SendsRetryOnConflictAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create(UpdateResponseBody));
        UpdateDocumentOptions options = new() {
            RetryOnConflict = 3,
            Refresh = Refresh.True
        };

        UpdateResponse<SampleDocument> response = await sut.UpdateAsync<SampleDocument, object>(
            "sample-documents",
            "order-1",
            new { Name = "Updated" },
            options,
            CancellationToken.None
        );

        string query = GetQuery(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(GetPath(response), Is.EqualTo("/sample-documents/_update/order-1"));
            Assert.That(query, Does.Contain("retry_on_conflict=3"));
            Assert.That(query, Does.Contain("refresh=true"));
            Assert.That(query, Does.Not.Contain("if_seq_no"));
        }
    }

    [Test]
    public async Task UpdateAsync_WithSeqNoAndPrimaryTerm_SendsConcurrencyParametersAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create(UpdateResponseBody));
        UpdateDocumentOptions options = new() {
            IfSeqNo = 9,
            IfPrimaryTerm = 2
        };

        UpdateResponse<SampleDocument> response = await sut.UpdateAsync<SampleDocument, object>(
            "sample-documents",
            "order-1",
            new { Name = "Updated" },
            options,
            CancellationToken.None
        );

        string query = GetQuery(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(query, Does.Contain("if_seq_no=9"));
            Assert.That(query, Does.Contain("if_primary_term=2"));
            Assert.That(query, Does.Not.Contain("retry_on_conflict"));
        }
    }

    [Test]
    public async Task UpdateAsync_WithoutOptions_DoesNotSendOptionParametersAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create(UpdateResponseBody));

        UpdateResponse<SampleDocument> response = await sut.UpdateAsync<SampleDocument, object>(
            "sample-documents",
            "order-1",
            new { Name = "Updated" },
            null,
            CancellationToken.None
        );

        AssertNoOptionParameters(GetQuery(response));
    }

    [Test]
    public async Task UpdateAsync_WithZeroRetryOnConflict_DoesNotSendRetryOnConflictAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create(UpdateResponseBody));

        UpdateResponse<SampleDocument> response = await sut.UpdateAsync<SampleDocument, object>(
            "sample-documents",
            "order-1",
            new { Name = "Updated" },
            new UpdateDocumentOptions { RetryOnConflict = 0 },
            CancellationToken.None
        );

        AssertNoOptionParameters(GetQuery(response));
    }

    [Test]
    public async Task DeleteAsync_WithRefreshTrue_SendsRefreshAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create(DeleteResponseBody));

        DeleteResponse response = await sut.DeleteAsync<SampleDocument>(
            "sample-documents",
            "order-1",
            new DocumentWriteOptions { Refresh = Refresh.True },
            CancellationToken.None
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(GetPath(response), Is.EqualTo("/sample-documents/_doc/order-1"));
            Assert.That(GetQuery(response), Does.Contain("refresh=true"));
        }
    }

    [Test]
    public async Task DeleteAsync_WithoutOptions_DoesNotSendOptionParametersAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create(DeleteResponseBody));

        DeleteResponse response = await sut.DeleteAsync<SampleDocument>(
            "sample-documents",
            "order-1",
            null,
            CancellationToken.None
        );

        AssertNoOptionParameters(GetQuery(response));
    }

    [Test]
    public async Task CreateAsync_WithDocumentId_SendsCreateOpTypeAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create());

        IndexResponse response = await sut.CreateAsync(
            "sample-documents",
            CreateDocument(),
            "order-1",
            null,
            CancellationToken.None
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(GetPath(response), Is.EqualTo("/sample-documents/_doc/order-1"));
            Assert.That(GetQuery(response), Does.Contain("op_type=create"));
        }
    }

    [Test]
    public async Task CreateAsync_WithoutDocumentId_SendsCreateOpTypeOnAutoIdPathAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create());

        IndexResponse response = await sut.CreateAsync(
            "sample-documents",
            new { Name = "Order 1" },
            null,
            new DocumentWriteOptions { Refresh = Refresh.WaitFor },
            CancellationToken.None
        );

        string query = GetQuery(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(GetPath(response), Is.EqualTo("/sample-documents/_doc"));
            Assert.That(query, Does.Contain("op_type=create"));
            Assert.That(query, Does.Contain("refresh=wait_for"));
        }
    }

    [Test]
    public async Task CreateAsync_WithoutOptions_DoesNotSendConcurrencyOrRefreshParametersAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create());

        IndexResponse response = await sut.CreateAsync(
            "sample-documents",
            CreateDocument(),
            "order-1",
            null,
            CancellationToken.None
        );

        string query = GetQuery(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(query, Does.Not.Contain("if_seq_no"));
            Assert.That(query, Does.Not.Contain("if_primary_term"));
            Assert.That(query, Does.Not.Contain("refresh"));
        }
    }

    [Test]
    public async Task UpdateByScriptAsync_WithScriptAndRefresh_SendsScriptBodyAndRefreshAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create(UpdateResponseBody));
        Script script = new() {
            Source = "ctx._source.sortKey += params.step",
            Params = new Dictionary<string, object> {
                ["step"] = 2
            }
        };

        UpdateResponse<SampleDocument> response = await sut.UpdateByScriptAsync<SampleDocument>(
            "sample-documents",
            "order-1",
            script,
            new DocumentWriteOptions { Refresh = Refresh.True },
            CancellationToken.None
        );

        using JsonDocument body = JsonDocument.Parse(GetRequestBody(response));
        JsonElement scriptElement = body.RootElement.GetProperty("script");

        using (Assert.EnterMultipleScope()) {
            Assert.That(GetPath(response), Is.EqualTo("/sample-documents/_update/order-1"));
            Assert.That(GetQuery(response), Does.Contain("refresh=true"));
            Assert.That(scriptElement.GetProperty("source").GetString(), Is.EqualTo("ctx._source.sortKey += params.step"));
            Assert.That(scriptElement.GetProperty("params").GetProperty("step").GetInt32(), Is.EqualTo(2));
        }
    }

    [Test]
    public async Task UpdateByScriptAsync_WithoutOptions_DoesNotSendRefreshAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create(UpdateResponseBody));

        UpdateResponse<SampleDocument> response = await sut.UpdateByScriptAsync<SampleDocument>(
            "sample-documents",
            "order-1",
            new Script { Source = "ctx._source.sortKey = 1" },
            null,
            CancellationToken.None
        );

        AssertNoOptionParameters(GetQuery(response));
    }

    [Test]
    public async Task UpdateByQueryAsync_WithFiltersAndScript_SendsQueryScriptAndDefaultParametersAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create(UpdateByQueryResponseBody));
        ElasticsearchQueryFilterBuilder<SampleDocument> filters = new();
        filters.Term(document => document.Name, "Order 1");
        UpdateByQueryRequest<SampleDocument> request = new() {
            Filters = filters,
            Script = new Script { Source = "ctx._source.sortKey = 2" }
        };

        UpdateByQueryResponse response = await sut.UpdateByQueryAsync("sample-documents", request, CancellationToken.None);

        string query = GetQuery(response);
        using JsonDocument body = JsonDocument.Parse(GetRequestBody(response));

        using (Assert.EnterMultipleScope()) {
            Assert.That(GetPath(response), Is.EqualTo("/sample-documents/_update_by_query"));
            Assert.That(query, Does.Contain("refresh=true"));
            Assert.That(query, Does.Contain("wait_for_completion=true"));
            Assert.That(body.RootElement.GetProperty("query").GetProperty("bool").TryGetProperty("filter", out _), Is.True);
            Assert.That(body.RootElement.GetProperty("script").GetProperty("source").GetString(), Is.EqualTo("ctx._source.sortKey = 2"));
        }
    }

    [Test]
    public async Task UpdateByQueryAsync_WithRefreshFalseAndNoScript_SendsRefreshFalseWithoutScriptAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create(UpdateByQueryResponseBody));
        ElasticsearchQueryFilterBuilder<SampleDocument> filters = new();
        filters.Term(document => document.Name, "Order 1");

        UpdateByQueryResponse response = await sut.UpdateByQueryAsync(
            "sample-documents",
            new UpdateByQueryRequest<SampleDocument> {
                Filters = filters,
                Refresh = false
            },
            CancellationToken.None
        );

        using JsonDocument body = JsonDocument.Parse(GetRequestBody(response));

        using (Assert.EnterMultipleScope()) {
            Assert.That(GetQuery(response), Does.Contain("refresh=false"));
            Assert.That(body.RootElement.TryGetProperty("script", out _), Is.False);
        }
    }

    [Test]
    public async Task UpdateByQueryAsync_WithConfigureRequestOverride_AppliesDelegateAfterDefaultsAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create(UpdateByQueryResponseBody));
        ElasticsearchQueryFilterBuilder<SampleDocument> filters = new();
        filters.Term(document => document.Name, "Order 1");

        UpdateByQueryResponse response = await sut.UpdateByQueryAsync(
            "sample-documents",
            new UpdateByQueryRequest<SampleDocument> {
                Filters = filters,
                ConfigureRequest = descriptor => descriptor.Conflicts(Conflicts.Proceed).WaitForCompletion(false)
            },
            CancellationToken.None
        );

        string query = GetQuery(response);
        using JsonDocument body = JsonDocument.Parse(GetRequestBody(response));

        using (Assert.EnterMultipleScope()) {
            Assert.That(body.RootElement.GetProperty("conflicts").GetString(), Is.EqualTo("proceed"));
            Assert.That(query, Does.Contain("wait_for_completion=false"));
            Assert.That(query, Does.Not.Contain("wait_for_completion=true"));
        }
    }

    [Test]
    public async Task UpdateByQueryAsync_WithoutFiltersAndWithMatchAllDelegate_SendsRequestAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create(UpdateByQueryResponseBody));

        UpdateByQueryResponse response = await sut.UpdateByQueryAsync(
            "sample-documents",
            new UpdateByQueryRequest<SampleDocument> {
                ConfigureRequest = descriptor => descriptor.Query(query => query.MatchAll(new MatchAllQuery()))
            },
            CancellationToken.None
        );

        using JsonDocument body = JsonDocument.Parse(GetRequestBody(response));

        using (Assert.EnterMultipleScope()) {
            Assert.That(response.IsValidResponse, Is.True);
            Assert.That(body.RootElement.GetProperty("query").TryGetProperty("match_all", out _), Is.True);
        }
    }

    [Test]
    public void UpdateByQueryAsync_WithoutFiltersAndWithEmptyDelegate_ThrowsArgumentExceptionBeforeSending() {
        int requestCount = 0;
        ElasticsearchGateway sut = new(
            RecordingClientFactory.Create(UpdateByQueryResponseBody, onRequestCompleted: _ => requestCount++)
        );

        ArgumentException? exception = Assert.Throws<ArgumentException>(
            () => sut.UpdateByQueryAsync(
                "sample-documents",
                new UpdateByQueryRequest<SampleDocument> {
                    ConfigureRequest = _ => { }
                },
                CancellationToken.None
            )
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(exception?.ParamName, Is.EqualTo("request"));
            Assert.That(requestCount, Is.EqualTo(0));
        }
    }

    [Test]
    public void UpdateByQueryAsync_WithEmptyFiltersAndWithoutDelegate_ThrowsArgumentExceptionBeforeSending() {
        int requestCount = 0;
        ElasticsearchGateway sut = new(
            RecordingClientFactory.Create(UpdateByQueryResponseBody, onRequestCompleted: _ => requestCount++)
        );

        Assert.Throws<ArgumentException>(
            () => sut.UpdateByQueryAsync(
                "sample-documents",
                new UpdateByQueryRequest<SampleDocument> {
                    Filters = new ElasticsearchQueryFilterBuilder<SampleDocument>()
                },
                CancellationToken.None
            )
        );
        Assert.That(requestCount, Is.EqualTo(0));
    }

    [Test]
    public async Task DeleteByQueryAsync_WithFilters_SendsQueryAndDefaultParametersAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create(DeleteByQueryResponseBody));
        ElasticsearchQueryFilterBuilder<SampleDocument> filters = new();
        filters.Term(document => document.Name, "Order 1");

        DeleteByQueryResponse response = await sut.DeleteByQueryAsync(
            "sample-documents",
            new DeleteByQueryRequest<SampleDocument> {
                Filters = filters
            },
            CancellationToken.None
        );

        string query = GetQuery(response);
        using JsonDocument body = JsonDocument.Parse(GetRequestBody(response));

        using (Assert.EnterMultipleScope()) {
            Assert.That(GetPath(response), Is.EqualTo("/sample-documents/_delete_by_query"));
            Assert.That(query, Does.Contain("refresh=true"));
            Assert.That(query, Does.Contain("wait_for_completion=true"));
            Assert.That(body.RootElement.GetProperty("query").GetProperty("bool").TryGetProperty("filter", out _), Is.True);
        }
    }

    [Test]
    public async Task DeleteByQueryAsync_WithoutFiltersAndWithMatchAllDelegate_SendsRequestAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create(DeleteByQueryResponseBody));

        DeleteByQueryResponse response = await sut.DeleteByQueryAsync(
            "sample-documents",
            new DeleteByQueryRequest<SampleDocument> {
                ConfigureRequest = descriptor => descriptor.Query(query => query.MatchAll(new MatchAllQuery()))
            },
            CancellationToken.None
        );

        using JsonDocument body = JsonDocument.Parse(GetRequestBody(response));

        using (Assert.EnterMultipleScope()) {
            Assert.That(response.IsValidResponse, Is.True);
            Assert.That(body.RootElement.GetProperty("query").TryGetProperty("match_all", out _), Is.True);
        }
    }

    [Test]
    public void DeleteByQueryAsync_WithoutFiltersAndWithEmptyDelegate_ThrowsArgumentExceptionBeforeSending() {
        int requestCount = 0;
        ElasticsearchGateway sut = new(
            RecordingClientFactory.Create(DeleteByQueryResponseBody, onRequestCompleted: _ => requestCount++)
        );

        ArgumentException? exception = Assert.Throws<ArgumentException>(
            () => sut.DeleteByQueryAsync(
                "sample-documents",
                new DeleteByQueryRequest<SampleDocument> {
                    ConfigureRequest = _ => { }
                },
                CancellationToken.None
            )
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(exception?.ParamName, Is.EqualTo("request"));
            Assert.That(requestCount, Is.EqualTo(0));
        }
    }

    [Test]
    public void DeleteByQueryAsync_WithoutFiltersAndWithoutDelegate_ThrowsArgumentExceptionBeforeSending() {
        int requestCount = 0;
        ElasticsearchGateway sut = new(
            RecordingClientFactory.Create(DeleteByQueryResponseBody, onRequestCompleted: _ => requestCount++)
        );

        Assert.Throws<ArgumentException>(
            () => sut.DeleteByQueryAsync(
                "sample-documents",
                new DeleteByQueryRequest<SampleDocument>(),
                CancellationToken.None
            )
        );
        Assert.That(requestCount, Is.EqualTo(0));
    }

    [Test]
    public async Task CountAsync_WithFilters_SendsFilterQueryAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create(CountResponseBody));
        ElasticsearchQueryFilterBuilder<SampleDocument> filters = new();
        filters.Term(document => document.Name, "Order 1");

        CountResponse response = await sut.CountAsync("sample-documents", filters, CancellationToken.None);

        using JsonDocument body = JsonDocument.Parse(GetRequestBody(response));

        using (Assert.EnterMultipleScope()) {
            Assert.That(GetPath(response), Is.EqualTo("/sample-documents/_count"));
            Assert.That(body.RootElement.GetProperty("query").GetProperty("bool").TryGetProperty("filter", out _), Is.True);
            Assert.That(response.Count, Is.EqualTo(5));
        }
    }

    [Test]
    public async Task CountAsync_WithEmptyBuilder_SendsRequestWithoutQueryAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create(CountResponseBody));

        CountResponse response = await sut.CountAsync(
            "sample-documents",
            new ElasticsearchQueryFilterBuilder<SampleDocument>(),
            CancellationToken.None
        );

        string body = GetRequestBody(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(response.IsValidResponse, Is.True);
            Assert.That(GetPath(response), Is.EqualTo("/sample-documents/_count"));
            Assert.That(body, Does.Not.Contain("\"query\""));
        }
    }

    [Test]
    public async Task SearchPageAsync_WithPageNumberAndSize_SendsFromSizeAndTrackTotalHitsAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create(SearchResponseBody));
        DocumentPageRequest<SampleDocument> request = new() {
            PageNumber = 3,
            PageSize = 20
        };

        SearchResponse<SampleDocument> response = await sut.SearchPageAsync(
            "sample-documents",
            request,
            CancellationToken.None
        );

        using JsonDocument body = JsonDocument.Parse(GetRequestBody(response));

        using (Assert.EnterMultipleScope()) {
            Assert.That(GetPath(response), Is.EqualTo("/sample-documents/_search"));
            Assert.That(body.RootElement.GetProperty("from").GetInt32(), Is.EqualTo(40));
            Assert.That(body.RootElement.GetProperty("size").GetInt32(), Is.EqualTo(20));
            Assert.That(body.RootElement.GetProperty("track_total_hits").GetBoolean(), Is.True);
            Assert.That(body.RootElement.TryGetProperty("query", out _), Is.False);
            Assert.That(response.Total, Is.EqualTo(42));
        }
    }

    [Test]
    public async Task SearchPageAsync_WithTrackTotalHitsDisabled_SendsFalseAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create(SearchResponseBody));
        DocumentPageRequest<SampleDocument> request = new() {
            PageNumber = 1,
            PageSize = 10,
            TrackTotalHits = false
        };

        SearchResponse<SampleDocument> response = await sut.SearchPageAsync(
            "sample-documents",
            request,
            CancellationToken.None
        );

        using JsonDocument body = JsonDocument.Parse(GetRequestBody(response));

        using (Assert.EnterMultipleScope()) {
            Assert.That(body.RootElement.GetProperty("from").GetInt32(), Is.EqualTo(0));
            Assert.That(body.RootElement.GetProperty("track_total_hits").GetBoolean(), Is.False);
        }
    }

    [Test]
    public async Task SearchPageAsync_WithFiltersAndConfigureSearch_AppliesConfigureSearchLastAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create(SearchResponseBody));
        ElasticsearchQueryFilterBuilder<SampleDocument> filters = new();
        filters.Term(document => document.Name, "Order 1");
        DocumentPageRequest<SampleDocument> request = new() {
            PageNumber = 2,
            PageSize = 5,
            Filters = filters,
            ConfigureSearch = descriptor => descriptor
                .Size(99)
                .TrackTotalHits(new TrackHits(false))
                .Sort(sort => sort.Field(document => document.SortKey, field => field.Order(SortOrder.Desc)))
        };

        SearchResponse<SampleDocument> response = await sut.SearchPageAsync(
            "sample-documents",
            request,
            CancellationToken.None
        );

        using JsonDocument body = JsonDocument.Parse(GetRequestBody(response));

        using (Assert.EnterMultipleScope()) {
            Assert.That(body.RootElement.GetProperty("from").GetInt32(), Is.EqualTo(5));
            Assert.That(body.RootElement.GetProperty("size").GetInt32(), Is.EqualTo(99));
            Assert.That(body.RootElement.GetProperty("track_total_hits").GetBoolean(), Is.False);
            Assert.That(body.RootElement.GetProperty("query").GetProperty("bool").TryGetProperty("filter", out _), Is.True);
            Assert.That(body.RootElement.TryGetProperty("sort", out _), Is.True);
        }
    }

    [Test]
    public void SearchPageAsync_WithOffsetOverflow_ThrowsBeforeSendingRequest() {
        int requestCount = 0;
        ElasticsearchGateway sut = new(
            RecordingClientFactory.Create(SearchResponseBody, onRequestCompleted: _ => requestCount++)
        );
        DocumentPageRequest<SampleDocument> request = new() {
            PageNumber = int.MaxValue,
            PageSize = 2
        };

        Assert.Throws<ArgumentOutOfRangeException>(
            () => sut.SearchPageAsync("sample-documents", request, CancellationToken.None)
        );
        Assert.That(requestCount, Is.EqualTo(0));
    }

    [Test]
    public async Task SearchAsync_WithSize_SendsSizeAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create(SearchResponseBody));
        DocumentSearchRequest<SampleDocument> request = new() {
            Size = 5
        };

        SearchResponse<SampleDocument> response = await sut.SearchAsync(
            "sample-documents",
            request,
            CancellationToken.None
        );

        using JsonDocument body = JsonDocument.Parse(GetRequestBody(response));

        using (Assert.EnterMultipleScope()) {
            Assert.That(GetPath(response), Is.EqualTo("/sample-documents/_search"));
            Assert.That(body.RootElement.GetProperty("size").GetInt32(), Is.EqualTo(5));
            Assert.That(body.RootElement.TryGetProperty("from", out _), Is.False);
            Assert.That(body.RootElement.TryGetProperty("track_total_hits", out _), Is.False);
        }
    }

    [Test]
    public async Task SearchAsync_WithoutSizeAndFilters_SendsNeitherSizeNorQueryAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create(SearchResponseBody));

        SearchResponse<SampleDocument> response = await sut.SearchAsync(
            "sample-documents",
            new DocumentSearchRequest<SampleDocument>(),
            CancellationToken.None
        );

        string body = GetRequestBody(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(response.IsValidResponse, Is.True);
            Assert.That(body, Does.Not.Contain("\"size\""));
            Assert.That(body, Does.Not.Contain("\"query\""));
        }
    }

    [Test]
    public async Task SearchAsync_WithFiltersAndConfigureSearch_AppliesConfigureSearchLastAsync() {
        ElasticsearchGateway sut = new(RecordingClientFactory.Create(SearchResponseBody));
        ElasticsearchQueryFilterBuilder<SampleDocument> filters = new();
        filters.Term(document => document.Name, "Order 1");
        DocumentSearchRequest<SampleDocument> request = new() {
            Size = 5,
            Filters = filters,
            ConfigureSearch = descriptor => descriptor.Size(7).MinScore(1.5)
        };

        SearchResponse<SampleDocument> response = await sut.SearchAsync(
            "sample-documents",
            request,
            CancellationToken.None
        );

        using JsonDocument body = JsonDocument.Parse(GetRequestBody(response));

        using (Assert.EnterMultipleScope()) {
            Assert.That(body.RootElement.GetProperty("size").GetInt32(), Is.EqualTo(7));
            Assert.That(body.RootElement.GetProperty("min_score").GetDouble(), Is.EqualTo(1.5));
            Assert.That(body.RootElement.GetProperty("query").GetProperty("bool").TryGetProperty("filter", out _), Is.True);
        }
    }

    private static SampleDocument CreateDocument() {
        return new SampleDocument {
            Id = "order-1",
            Name = "Order 1",
            SortKey = 1
        };
    }

    private static string GetPath(TransportResponse response) {
        return response.ApiCallDetails.Uri?.AbsolutePath ?? string.Empty;
    }

    private static string GetRequestBody(TransportResponse response) {
        byte[]? bytes = response.ApiCallDetails.RequestBodyInBytes;

        return bytes is null ? string.Empty : Encoding.UTF8.GetString(bytes);
    }

    private static string GetQuery(TransportResponse response) {
        return response.ApiCallDetails.Uri?.Query ?? string.Empty;
    }

    private static void AssertNoOptionParameters(string query) {
        using (Assert.EnterMultipleScope()) {
            Assert.That(query, Does.Not.Contain("refresh"));
            Assert.That(query, Does.Not.Contain("if_seq_no"));
            Assert.That(query, Does.Not.Contain("if_primary_term"));
            Assert.That(query, Does.Not.Contain("retry_on_conflict"));
        }
    }
}
