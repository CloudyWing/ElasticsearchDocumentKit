using System.Diagnostics;
using CloudyWing.ElasticsearchDocumentKit;
using CloudyWing.ElasticsearchDocumentKit.Indexing;
using CloudyWing.ElasticsearchDocumentKit.Internal;
using CloudyWing.ElasticsearchDocumentKit.Modeling;
using CloudyWing.ElasticsearchDocumentKit.Querying;
using CloudyWing.ElasticsearchDocumentKit.Search;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using IndexExistsResponse = Elastic.Clients.Elasticsearch.IndexManagement.ExistsResponse;

namespace CloudyWing.ElasticsearchDocumentKit.Tests.Indexing;

[TestFixture]
internal sealed class ElasticsearchIndexInitializerTests {
    [Test]
    public async Task InitializeAsync_WhenIndexExists_ShouldReturnExistingResultAsync() {
        FakeGateway gateway = new() {
            ExistsResponse = CreateExistsResponse(200)
        };
        ElasticsearchIndexInitializer<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        IndexInitializationResult result = await sut.InitializeAsync();

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.AlreadyExists, Is.True);
            Assert.That(result.Created, Is.False);
            Assert.That(gateway.CreateIndexCallCount, Is.Zero);
        }
    }

    [Test]
    public async Task InitializeAsync_WhenIndexDoesNotExist_ShouldCreateIndexAsync() {
        FakeGateway gateway = new() {
            ExistsResponse = CreateExistsResponse(404),
            CreateIndexResponse = TestResponses.Success(new CreateIndexResponse {
                Acknowledged = true,
                Index = "sample-documents",
                ShardsAcknowledged = true
            })
        };
        ElasticsearchIndexInitializer<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        IndexInitializationResult result = await sut.InitializeAsync();

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Created, Is.True);
            Assert.That(result.AlreadyExists, Is.False);
            Assert.That(gateway.CreatedIndexName, Is.EqualTo("sample-documents"));
        }
    }

    [Test]
    public async Task InitializeAsync_WithConfiguredModel_ShouldPassConfigureIndexAsync() {
        FakeGateway gateway = new() {
            ExistsResponse = CreateExistsResponse(404),
            CreateIndexResponse = TestResponses.Success(new CreateIndexResponse {
                Acknowledged = true,
                Index = "sample-documents",
                ShardsAcknowledged = true
            })
        };
        ElasticsearchIndexInitializer<SampleDocument> sut = new(
            gateway,
            CreateModelResolver("sample-documents", _ => { })
        );

        await sut.InitializeAsync();

        Assert.That(gateway.ConfigureIndexWasProvided, Is.True);
    }

    [Test]
    public async Task InitializeAsync_WhenExistsCheckFails_ShouldReturnFailureWithoutCreatingIndexAsync() {
        FakeGateway gateway = new() {
            ExistsResponse = TestResponses.Failure(new IndexExistsResponse(), 500)
        };
        ElasticsearchIndexInitializer<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        IndexInitializationResult result = await sut.InitializeAsync();

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.Created, Is.False);
            Assert.That(result.AlreadyExists, Is.False);
            Assert.That(gateway.CreateIndexCallCount, Is.Zero);
        }
    }

    [Test]
    public async Task InitializeAsync_WhenCreateRacesWithAnotherWriter_ShouldReturnExistingResultAsync() {
        FakeGateway gateway = new() {
            ExistsResponse = CreateExistsResponse(404),
            CreateIndexResponse = TestResponses.Failure(
                new CreateIndexResponse(),
                409,
                "resource_already_exists_exception",
                "index already exists"
            )
        };
        ElasticsearchIndexInitializer<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        IndexInitializationResult result = await sut.InitializeAsync();

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.AlreadyExists, Is.True);
            Assert.That(result.Created, Is.False);
            Assert.That(result.ErrorType, Is.Null);
            Assert.That(gateway.CreateIndexCallCount, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task InitializeAsync_WhenExistsCheckFailsWithError_RecordsActivityTagsAsync() {
        Activity? capturedActivity = null;
        using ActivityListener listener = new() {
            ShouldListenTo = source => source.Name == ElasticsearchDocumentsTelemetry.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => capturedActivity = activity
        };
        ActivitySource.AddActivityListener(listener);
        FakeGateway gateway = new() {
            ExistsResponse = TestResponses.Failure(
                new IndexExistsResponse(),
                500,
                "index_unavailable",
                "index unavailable"
            )
        };
        ElasticsearchIndexInitializer<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        IndexInitializationResult result = await sut.InitializeAsync();

        Assert.That(capturedActivity, Is.Not.Null);
        Activity activity = capturedActivity!;

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo("index_unavailable"));
            Assert.That(result.ErrorReason, Is.EqualTo("index unavailable"));
            Assert.That(activity.OperationName, Is.EqualTo("elasticsearch.index_initialize"));
            Assert.That(activity.GetTagItem("db.system"), Is.EqualTo("elasticsearch"));
            Assert.That(activity.GetTagItem("db.operation"), Is.EqualTo("index_initialize"));
            Assert.That(activity.GetTagItem("elasticsearch.index"), Is.EqualTo("sample-documents"));
            Assert.That(activity.GetTagItem("elasticsearch.success"), Is.EqualTo(false));
            Assert.That(activity.GetTagItem("elasticsearch.index_created"), Is.EqualTo(false));
            Assert.That(activity.GetTagItem("elasticsearch.index_already_exists"), Is.EqualTo(false));
            Assert.That(activity.GetTagItem("error.type"), Is.EqualTo("index_unavailable"));
        }
    }

    [Test]
    public async Task InitializeAsync_WhenCreateFailsWithOtherError_ShouldReturnFailureResultAsync() {
        FakeGateway gateway = new() {
            ExistsResponse = CreateExistsResponse(404),
            CreateIndexResponse = TestResponses.Failure(
                new CreateIndexResponse(),
                500,
                "create_index_exception",
                "index creation failed"
            )
        };
        ElasticsearchIndexInitializer<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        IndexInitializationResult result = await sut.InitializeAsync();

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.Created, Is.False);
            Assert.That(result.AlreadyExists, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo("create_index_exception"));
        }
    }

    [Test]
    public void Constructor_WithNullGateway_ShouldThrowArgumentNullException() {
        IElasticsearchDocumentModelResolver resolver = CreateModelResolver("sample-documents");

        ArgumentNullException? exception = Assert.Throws<ArgumentNullException>(
            () => _ = new ElasticsearchIndexInitializer<SampleDocument>((IElasticsearchGateway)null!, resolver)
        );

        Assert.That(exception!.ParamName, Is.EqualTo("gateway"));
    }

    [Test]
    public void Constructor_WithNullModelResolver_ShouldThrowArgumentNullException() {
        FakeGateway gateway = new();

        ArgumentNullException? exception = Assert.Throws<ArgumentNullException>(
            () => _ = new ElasticsearchIndexInitializer<SampleDocument>(gateway, null!)
        );

        Assert.That(exception!.ParamName, Is.EqualTo("documentModelResolver"));
    }

    [TestCase("resource_already_exists_exception", true)]
    [TestCase("RESOURCE_ALREADY_EXISTS_EXCEPTION", false)]
    [TestCase("index_not_found_exception", false)]
    [TestCase(null, false)]
    public void IsResourceAlreadyExistsError_WithErrorType_ShouldClassifyExactMatch(
        string? errorType,
        bool expected
    ) {
        bool result = ElasticsearchIndexInitializer<SampleDocument>.IsResourceAlreadyExistsError(errorType);

        Assert.That(result, Is.EqualTo(expected));
    }

    private static IndexExistsResponse CreateExistsResponse(int statusCode) {
        IndexExistsResponse response = new();

        return statusCode == 200
            ? TestResponses.Success(response, statusCode)
            : TestResponses.Failure(response, statusCode);
    }

    private static IElasticsearchDocumentModelResolver CreateModelResolver(
        string indexName,
        Action<CreateIndexRequestDescriptor<SampleDocument>>? configureIndex = null
    ) {
        IElasticsearchDocumentModelResolver resolver = Substitute.For<IElasticsearchDocumentModelResolver>();
        resolver.Resolve<SampleDocument>().Returns(
            new ElasticsearchDocumentModel<SampleDocument>(indexName, configureIndex: configureIndex)
        );

        return resolver;
    }

    private sealed class FakeGateway : IElasticsearchGateway {
        public IndexExistsResponse ExistsResponse { get; init; } = CreateExistsResponse(404);

        public CreateIndexResponse CreateIndexResponse { get; init; } = TestResponses.Success(
            new CreateIndexResponse()
        );

        public string? CreatedIndexName { get; private set; }

        public int CreateIndexCallCount { get; private set; }

        public bool ConfigureIndexWasProvided { get; private set; }

        public Task<IndexExistsResponse> IndexExistsAsync(string indexName, CancellationToken cancellationToken) {
            return Task.FromResult(ExistsResponse);
        }

        public Task<CreateIndexResponse> CreateIndexAsync<TDocument>(
            string indexName,
            Action<CreateIndexRequestDescriptor<TDocument>>? configureIndex,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            CreatedIndexName = indexName;
            ConfigureIndexWasProvided = configureIndex is not null;
            CreateIndexCallCount++;
            return Task.FromResult(CreateIndexResponse);
        }

        public Task<IndexResponse> IndexAsync<TDocument>(
            string indexName,
            TDocument document,
            string? documentId,
            IndexDocumentOptions? options,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            throw new NotSupportedException();
        }

        public Task<IndexResponse> CreateAsync<TDocument>(
            string indexName,
            TDocument document,
            string? documentId,
            DocumentWriteOptions? options,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            throw new NotSupportedException();
        }

        public Task<UpdateResponse<TDocument>> UpdateByScriptAsync<TDocument>(
            string indexName,
            string documentId,
            Script script,
            DocumentWriteOptions? options,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            throw new NotSupportedException();
        }

        public Task<UpdateByQueryResponse> UpdateByQueryAsync<TDocument>(
            string indexName,
            UpdateByQueryRequest<TDocument> request,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            throw new NotSupportedException();
        }

        public Task<DeleteByQueryResponse> DeleteByQueryAsync<TDocument>(
            string indexName,
            DeleteByQueryRequest<TDocument> request,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            throw new NotSupportedException();
        }

        public Task<CountResponse> CountAsync<TDocument>(
            string indexName,
            ElasticsearchQueryFilterBuilder<TDocument>? filters,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            throw new NotSupportedException();
        }

        public Task<DeleteResponse> DeleteAsync<TDocument>(
            string indexName,
            string documentId,
            DocumentWriteOptions? options,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            throw new NotSupportedException();
        }

        public Task<GetResponse<TDocument>> GetAsync<TDocument>(
            string indexName,
            string documentId,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            throw new NotSupportedException();
        }

        public Task<UpdateResponse<TDocument>> UpdateAsync<TDocument, TPartialDocument>(
            string indexName,
            string documentId,
            TPartialDocument partialDocument,
            UpdateDocumentOptions? options,
            CancellationToken cancellationToken
        )
            where TDocument : class
            where TPartialDocument : class {
            throw new NotSupportedException();
        }

        public Task<BulkResponse> BulkIndexAsync<TDocument>(
            string indexName,
            IReadOnlyList<TDocument> documents,
            Func<TDocument, string?>? documentIdSelector,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            throw new NotSupportedException();
        }

        public Task<SearchResponse<TDocument>> SearchAsync<TDocument>(
            string indexName,
            DocumentSearchRequest<TDocument> request,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            throw new NotSupportedException();
        }

        public Task<SearchResponse<TDocument>> SearchPageAsync<TDocument>(
            string indexName,
            DocumentPageRequest<TDocument> request,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            throw new NotSupportedException();
        }

        public Task<SearchResponse<TDocument>> SearchAfterAsync<TDocument>(
            string indexName,
            SearchAfterRequest<TDocument> request,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            throw new NotSupportedException();
        }
    }
}
