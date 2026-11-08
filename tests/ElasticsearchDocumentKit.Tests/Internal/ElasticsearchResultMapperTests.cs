using CloudyWing.ElasticsearchDocumentKit.Internal;
using CloudyWing.ElasticsearchDocumentKit.Indexing;
using CloudyWing.ElasticsearchDocumentKit.Search;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Bulk;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.IndexManagement;
using IndexExistsResponse = Elastic.Clients.Elasticsearch.IndexManagement.ExistsResponse;

namespace CloudyWing.ElasticsearchDocumentKit.Tests.Internal;

[TestFixture]
internal sealed class ElasticsearchResultMapperTests {
    [Test]
    public void FromIndexResponse_WithServerError_MapsErrorDetails() {
        IndexResponse response = TestResponses.Failure(
            new IndexResponse {
                Index = "sample-documents",
                Id = "order-1",
                Version = 3
            },
            409,
            "version_conflict_engine_exception",
            "document conflict"
        );

        DocumentOperationResult result = ElasticsearchResultMapper.FromIndexResponse(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.Operation, Is.EqualTo("index"));
            Assert.That(result.StatusCode, Is.EqualTo(409));
            Assert.That(result.ErrorType, Is.EqualTo("version_conflict_engine_exception"));
            Assert.That(result.ErrorReason, Is.EqualTo("document conflict"));
        }
    }

    [Test]
    public void FromDeleteResponse_WithServerError_MapsErrorDetails() {
        DeleteResponse response = TestResponses.Failure(
            new DeleteResponse {
                Index = "sample-documents",
                Id = "order-1",
                Version = 4
            },
            404,
            "document_missing_exception",
            "document not found"
        );

        DocumentOperationResult result = ElasticsearchResultMapper.FromDeleteResponse(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.Operation, Is.EqualTo("delete"));
            Assert.That(result.StatusCode, Is.EqualTo(404));
            Assert.That(result.ErrorType, Is.EqualTo("document_missing_exception"));
            Assert.That(result.ErrorReason, Is.EqualTo("document not found"));
        }
    }

    [Test]
    public void FromGetResponse_WithMissingDocument_MapsResponseFields() {
        GetResponse<SampleDocument> response = TestResponses.Failure(
            new GetResponse<SampleDocument> {
                Index = "sample-documents",
                Id = "order-1",
                Found = false
            },
            404,
            "document_missing_exception",
            "document not found"
        );

        DocumentGetResult<SampleDocument> result = ElasticsearchResultMapper.FromGetResponse(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.Found, Is.False);
            Assert.That(result.DocumentId, Is.EqualTo("order-1"));
            Assert.That(result.IndexName, Is.EqualTo("sample-documents"));
            Assert.That(result.ErrorType, Is.EqualTo("document_missing_exception"));
            Assert.That(result.ErrorReason, Is.EqualTo("document not found"));
        }
    }

    [Test]
    public void FromUpdateResponse_WithServerError_MapsErrorDetails() {
        UpdateResponse<SampleDocument> response = TestResponses.Failure(
            new UpdateResponse<SampleDocument> {
                Index = "sample-documents",
                Id = "order-1",
                Version = 5
            },
            400,
            "mapper_parsing_exception",
            "invalid mapping"
        );

        DocumentOperationResult result = ElasticsearchResultMapper.FromUpdateResponse(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.Operation, Is.EqualTo("update"));
            Assert.That(result.StatusCode, Is.EqualTo(400));
            Assert.That(result.ErrorType, Is.EqualTo("mapper_parsing_exception"));
            Assert.That(result.ErrorReason, Is.EqualTo("invalid mapping"));
        }
    }

    [Test]
    public void FromGetResponse_WithSeqNoAndPrimaryTerm_MapsConcurrencyToken() {
        GetResponse<SampleDocument> response = TestResponses.Success(new GetResponse<SampleDocument> {
            Index = "sample-documents",
            Id = "order-1",
            Found = true,
            SeqNo = 7,
            PrimaryTerm = 2
        });

        DocumentGetResult<SampleDocument> result = ElasticsearchResultMapper.FromGetResponse(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.SeqNo, Is.EqualTo(7));
            Assert.That(result.PrimaryTerm, Is.EqualTo(2));
        }
    }

    [Test]
    public void FromGetResponse_WithoutSeqNo_LeavesConcurrencyTokenNull() {
        GetResponse<SampleDocument> response = TestResponses.Failure(
            new GetResponse<SampleDocument> {
                Index = "sample-documents",
                Id = "order-1",
                Found = false
            },
            404
        );

        DocumentGetResult<SampleDocument> result = ElasticsearchResultMapper.FromGetResponse(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.SeqNo, Is.Null);
            Assert.That(result.PrimaryTerm, Is.Null);
        }
    }

    [Test]
    public void FromIndexResponse_WithSeqNoAndPrimaryTerm_MapsConcurrencyToken() {
        IndexResponse response = TestResponses.Success(new IndexResponse {
            Index = "sample-documents",
            Id = "order-1",
            Version = 3,
            SeqNo = 8,
            PrimaryTerm = 2
        });

        DocumentOperationResult result = ElasticsearchResultMapper.FromIndexResponse(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.SeqNo, Is.EqualTo(8));
            Assert.That(result.PrimaryTerm, Is.EqualTo(2));
        }
    }

    [Test]
    public void FromUpdateResponse_WithSeqNoAndPrimaryTerm_MapsConcurrencyToken() {
        UpdateResponse<SampleDocument> response = TestResponses.Success(new UpdateResponse<SampleDocument> {
            Index = "sample-documents",
            Id = "order-1",
            Version = 5,
            SeqNo = 10,
            PrimaryTerm = 2
        });

        DocumentOperationResult result = ElasticsearchResultMapper.FromUpdateResponse(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.SeqNo, Is.EqualTo(10));
            Assert.That(result.PrimaryTerm, Is.EqualTo(2));
        }
    }

    [Test]
    public void FromDeleteResponse_WithSeqNoAndPrimaryTerm_MapsConcurrencyToken() {
        DeleteResponse response = TestResponses.Success(new DeleteResponse {
            Index = "sample-documents",
            Id = "order-1",
            Version = 4,
            SeqNo = 9,
            PrimaryTerm = 2
        });

        DocumentOperationResult result = ElasticsearchResultMapper.FromDeleteResponse(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.SeqNo, Is.EqualTo(9));
            Assert.That(result.PrimaryTerm, Is.EqualTo(2));
        }
    }

    [Test]
    public void FromIndexResponse_WithCreateOperation_MapsOperationName() {
        IndexResponse response = TestResponses.Success(
            new IndexResponse {
                Index = "sample-documents",
                Id = "order-1",
                Version = 1
            },
            201
        );

        DocumentOperationResult result = ElasticsearchResultMapper.FromIndexResponse(response, "create");

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Operation, Is.EqualTo("create"));
            Assert.That(result.DocumentId, Is.EqualTo("order-1"));
        }
    }

    [Test]
    public void FromIndexResponse_WithCreateOperationAndConflict_MapsErrorDetails() {
        IndexResponse response = TestResponses.Failure(
            new IndexResponse {
                Index = "sample-documents",
                Id = "order-1"
            },
            409,
            "version_conflict_engine_exception",
            "document already exists"
        );

        DocumentOperationResult result = ElasticsearchResultMapper.FromIndexResponse(response, "create");

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.Operation, Is.EqualTo("create"));
            Assert.That(result.StatusCode, Is.EqualTo(409));
            Assert.That(result.ErrorType, Is.EqualTo("version_conflict_engine_exception"));
        }
    }

    [Test]
    public void FromUpdateResponse_WithScriptOperation_MapsOperationName() {
        UpdateResponse<SampleDocument> response = TestResponses.Success(new UpdateResponse<SampleDocument> {
            Index = "sample-documents",
            Id = "order-1",
            Version = 6
        });

        DocumentOperationResult result = ElasticsearchResultMapper.FromUpdateResponse(response, "update_by_script");

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Operation, Is.EqualTo("update_by_script"));
            Assert.That(result.Version, Is.EqualTo(6));
        }
    }

    [Test]
    public void FromIndexResponse_WithoutOperationArgument_UsesIndexOperation() {
        IndexResponse response = TestResponses.Success(new IndexResponse {
            Index = "sample-documents",
            Id = "order-1"
        });

        DocumentOperationResult result = ElasticsearchResultMapper.FromIndexResponse(response);

        Assert.That(result.Operation, Is.EqualTo("index"));
    }

    [Test]
    public void FromUpdateResponse_WithoutOperationArgument_UsesUpdateOperation() {
        UpdateResponse<SampleDocument> response = TestResponses.Success(new UpdateResponse<SampleDocument> {
            Index = "sample-documents",
            Id = "order-1"
        });

        DocumentOperationResult result = ElasticsearchResultMapper.FromUpdateResponse(response);

        Assert.That(result.Operation, Is.EqualTo("update"));
    }

    [Test]
    public void FromUpdateByQueryResponse_WithSuccessfulResponse_MapsCounts() {
        UpdateByQueryResponse response = TestResponses.Success(new UpdateByQueryResponse {
            Updated = 7,
            Deleted = 1,
            VersionConflicts = 2
        });

        ByQueryOperationResult result = ElasticsearchResultMapper.FromUpdateByQueryResponse(
            "sample-documents",
            response
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Operation, Is.EqualTo("update_by_query"));
            Assert.That(result.IndexName, Is.EqualTo("sample-documents"));
            Assert.That(result.Updated, Is.EqualTo(7));
            Assert.That(result.Deleted, Is.EqualTo(0));
            Assert.That(result.VersionConflicts, Is.EqualTo(2));
            Assert.That(result.ErrorType, Is.Null);
        }
    }

    [Test]
    public void FromUpdateByQueryResponse_WithNullCounts_MapsZero() {
        UpdateByQueryResponse response = TestResponses.Success(new UpdateByQueryResponse());

        ByQueryOperationResult result = ElasticsearchResultMapper.FromUpdateByQueryResponse(
            "sample-documents",
            response
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Updated, Is.EqualTo(0));
            Assert.That(result.VersionConflicts, Is.EqualTo(0));
        }
    }

    [Test]
    public void FromUpdateByQueryResponse_WithFailures_ReturnsFailureWithFirstCause() {
        UpdateByQueryResponse response = TestResponses.Success(new UpdateByQueryResponse {
            Updated = 1,
            Failures = [
                new BulkIndexByScrollFailure {
                    Id = "order-1",
                    Index = "sample-documents",
                    Status = 400,
                    Cause = new ErrorCause {
                        Type = "script_exception",
                        Reason = "script failed"
                    }
                }
            ]
        });

        ByQueryOperationResult result = ElasticsearchResultMapper.FromUpdateByQueryResponse(
            "sample-documents",
            response
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo("script_exception"));
            Assert.That(result.ErrorReason, Is.EqualTo("script failed"));
            Assert.That(result.Updated, Is.EqualTo(1));
        }
    }

    [Test]
    public void FromUpdateByQueryResponse_WithServerError_MapsErrorDetails() {
        UpdateByQueryResponse response = TestResponses.Failure(
            new UpdateByQueryResponse(),
            409,
            "version_conflict_engine_exception",
            "version conflict"
        );

        ByQueryOperationResult result = ElasticsearchResultMapper.FromUpdateByQueryResponse(
            "sample-documents",
            response
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(409));
            Assert.That(result.ErrorType, Is.EqualTo("version_conflict_engine_exception"));
            Assert.That(result.Updated, Is.EqualTo(0));
        }
    }

    [Test]
    public void FromDeleteByQueryResponse_WithSuccessfulResponse_MapsCounts() {
        DeleteByQueryResponse response = TestResponses.Success(new DeleteByQueryResponse {
            Deleted = 5,
            VersionConflicts = 3
        });

        ByQueryOperationResult result = ElasticsearchResultMapper.FromDeleteByQueryResponse(
            "sample-documents",
            response
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Operation, Is.EqualTo("delete_by_query"));
            Assert.That(result.IndexName, Is.EqualTo("sample-documents"));
            Assert.That(result.Deleted, Is.EqualTo(5));
            Assert.That(result.Updated, Is.EqualTo(0));
            Assert.That(result.VersionConflicts, Is.EqualTo(3));
        }
    }

    [Test]
    public void FromDeleteByQueryResponse_WithFailures_ReturnsFailureWithFirstCause() {
        DeleteByQueryResponse response = TestResponses.Success(new DeleteByQueryResponse {
            Deleted = 1,
            Failures = [
                new BulkIndexByScrollFailure {
                    Id = "order-1",
                    Index = "sample-documents",
                    Status = 500,
                    Cause = new ErrorCause {
                        Type = "es_rejected_execution_exception",
                        Reason = "rejected"
                    }
                }
            ]
        });

        ByQueryOperationResult result = ElasticsearchResultMapper.FromDeleteByQueryResponse(
            "sample-documents",
            response
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo("es_rejected_execution_exception"));
            Assert.That(result.ErrorReason, Is.EqualTo("rejected"));
        }
    }

    [Test]
    public void FromCountResponse_WithSuccessfulResponse_MapsCount() {
        CountResponse response = TestResponses.Success(new CountResponse {
            Count = 42
        });

        DocumentCountResult result = ElasticsearchResultMapper.FromCountResponse("sample-documents", response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.IndexName, Is.EqualTo("sample-documents"));
            Assert.That(result.Count, Is.EqualTo(42));
            Assert.That(result.ErrorType, Is.Null);
        }
    }

    [Test]
    public void FromCountResponse_WithServerError_ReturnsZeroCountAndErrorDetails() {
        CountResponse response = TestResponses.Failure(
            new CountResponse {
                Count = 42
            },
            404,
            "index_not_found_exception",
            "no such index"
        );

        DocumentCountResult result = ElasticsearchResultMapper.FromCountResponse("sample-documents", response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.Count, Is.EqualTo(0));
            Assert.That(result.StatusCode, Is.EqualTo(404));
            Assert.That(result.ErrorType, Is.EqualTo("index_not_found_exception"));
            Assert.That(result.IndexName, Is.EqualTo("sample-documents"));
        }
    }

    [Test]
    public void FromIndexResponse_WithSuccessfulResponse_LeavesErrorDetailsEmpty() {
        IndexResponse response = TestResponses.Success(new IndexResponse {
            Index = "sample-documents",
            Id = "order-1",
            Version = 3
        });

        DocumentOperationResult result = ElasticsearchResultMapper.FromIndexResponse(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.ErrorType, Is.Null);
            Assert.That(result.ErrorReason, Is.Null);
        }
    }

    [Test]
    public void FromDeleteResponse_WithSuccessfulResponse_LeavesErrorDetailsEmpty() {
        DeleteResponse response = TestResponses.Success(new DeleteResponse {
            Index = "sample-documents",
            Id = "order-1",
            Version = 4
        });

        DocumentOperationResult result = ElasticsearchResultMapper.FromDeleteResponse(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.ErrorType, Is.Null);
            Assert.That(result.ErrorReason, Is.Null);
        }
    }

    [Test]
    public void FromGetResponse_WithSuccessfulResponse_LeavesErrorDetailsEmpty() {
        GetResponse<SampleDocument> response = TestResponses.Success(new GetResponse<SampleDocument> {
            Index = "sample-documents",
            Id = "order-1",
            Found = true,
            Source = new SampleDocument { Id = "order-1", Name = "Order 1", SortKey = 1 }
        });

        DocumentGetResult<SampleDocument> result = ElasticsearchResultMapper.FromGetResponse(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.ErrorType, Is.Null);
            Assert.That(result.ErrorReason, Is.Null);
        }
    }

    [Test]
    public void FromUpdateResponse_WithSuccessfulResponse_LeavesErrorDetailsEmpty() {
        UpdateResponse<SampleDocument> response = TestResponses.Success(new UpdateResponse<SampleDocument> {
            Index = "sample-documents",
            Id = "order-1",
            Version = 5
        });

        DocumentOperationResult result = ElasticsearchResultMapper.FromUpdateResponse(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.ErrorType, Is.Null);
            Assert.That(result.ErrorReason, Is.Null);
        }
    }

    [Test]
    public void FromBulkResponse_WithEmptyInvalidResponse_MapsEveryRequestedDocumentFailure() {
        BulkResponse response = TestResponses.Failure(
            new BulkResponse {
                Items = []
            },
            503,
            "service_unavailable",
            "cluster unavailable"
        );

        BulkOperationResult result = ElasticsearchResultMapper.FromBulkResponse(response, 2);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.TotalCount, Is.EqualTo(2));
            Assert.That(result.SucceededCount, Is.Zero);
            Assert.That(result.FailedCount, Is.EqualTo(2));
            Assert.That(result.Failures.Select(failure => failure.ItemIndex), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(result.Failures.Select(failure => failure.StatusCode), Is.All.EqualTo(503));
            Assert.That(result.ErrorType, Is.EqualTo("service_unavailable"));
        }
    }

    [Test]
    public void FromBulkResponse_WithValidEmptyResponse_UsesRequestedCount() {
        BulkResponse response = TestResponses.Success(new BulkResponse {
            Items = []
        });

        BulkOperationResult result = ElasticsearchResultMapper.FromBulkResponse(response, 2);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.HasErrors, Is.False);
            Assert.That(result.TotalCount, Is.EqualTo(2));
            Assert.That(result.SucceededCount, Is.EqualTo(2));
            Assert.That(result.FailedCount, Is.Zero);
        }
    }

    [Test]
    public void FromBulkResponse_WithItemWithoutError_MapsFailureWithoutErrorDetails() {
        BulkResponse response = TestResponses.Success(new BulkResponse {
            Errors = true,
            Items = [
                TestResponses.BulkIndexItem("order-1", "sample-documents", 400)
            ]
        });

        BulkOperationResult result = ElasticsearchResultMapper.FromBulkResponse(response, 1);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.FailedCount, Is.EqualTo(1));
            Assert.That(result.Failures.Single().DocumentId, Is.EqualTo("order-1"));
            Assert.That(result.Failures.Single().ErrorType, Is.Null);
            Assert.That(result.Failures.Single().ErrorReason, Is.Null);
        }
    }

    [Test]
    public void FromBulkResponse_WithMissingApiCallDetails_UsesZeroStatusCode() {
        BulkOperationResult result = ElasticsearchResultMapper.FromBulkResponse(
            new BulkResponse { Items = [] },
            1
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.FailedCount, Is.EqualTo(1));
            Assert.That(result.Failures.Single().StatusCode, Is.Zero);
        }
    }

    [Test]
    public void FromExistingIndex_WithServerError_MapsFailureResult() {
        IndexExistsResponse response = TestResponses.Failure(
            new IndexExistsResponse(),
            500,
            "index_unavailable",
            "index unavailable"
        );

        IndexInitializationResult result = ElasticsearchResultMapper.FromExistingIndex("sample-documents", response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.AlreadyExists, Is.False);
            Assert.That(result.Created, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo("index_unavailable"));
            Assert.That(result.ErrorReason, Is.EqualTo("index unavailable"));
        }
    }

    [Test]
    public void FromCreateIndexResponse_WhenNotAcknowledged_ReturnsFailureResult() {
        CreateIndexResponse response = TestResponses.Success(new CreateIndexResponse {
            Acknowledged = false
        });

        IndexInitializationResult result = ElasticsearchResultMapper.FromCreateIndexResponse("sample-documents", response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.Created, Is.False);
            Assert.That(result.AlreadyExists, Is.False);
        }
    }

    [Test]
    public void FromCreateIndexResponse_WhenResponseFails_ReturnsErrorDetails() {
        CreateIndexResponse response = TestResponses.Failure(
            new CreateIndexResponse { Acknowledged = true },
            500,
            "create_index_exception",
            "index creation failed"
        );

        IndexInitializationResult result = ElasticsearchResultMapper.FromCreateIndexResponse("sample-documents", response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.Created, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo("create_index_exception"));
            Assert.That(result.ErrorReason, Is.EqualTo("index creation failed"));
        }
    }

    [Test]
    public void FromConcurrentIndexCreation_ReturnsAlreadyExistsResult() {
        CreateIndexResponse response = TestResponses.Failure(new CreateIndexResponse(), 409);

        IndexInitializationResult result = ElasticsearchResultMapper.FromConcurrentIndexCreation(
            "sample-documents",
            response
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Created, Is.False);
            Assert.That(result.AlreadyExists, Is.True);
            Assert.That(result.ErrorType, Is.Null);
        }
    }

    [Test]
    public void FromSearchResponse_WithNoHits_ReturnsEmptySearchAfterValues() {
        SearchResponse<SampleDocument> response = TestResponses.Success(new SearchResponse<SampleDocument> {
            HitsMetadata = new HitsMetadata<SampleDocument>([])
        });

        SearchAfterPage<SampleDocument> result = ElasticsearchResultMapper.FromSearchResponse(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Documents, Is.Empty);
            Assert.That(result.NextSearchAfter, Is.Empty);
            Assert.That(result.TotalCount, Is.EqualTo(-1));
        }
    }

    [Test]
    public void FromSearchResponse_WithHitWithoutSort_ReturnsEmptySearchAfterValues() {
        SearchResponse<SampleDocument> response = TestResponses.Success(new SearchResponse<SampleDocument> {
            HitsMetadata = new HitsMetadata<SampleDocument>([
                new Hit<SampleDocument> {
                    Id = "order-1",
                    Index = "sample-documents",
                    Source = new SampleDocument { Id = "order-1", Name = "Order 1", SortKey = 1 }
                }
            ])
        });

        SearchAfterPage<SampleDocument> result = ElasticsearchResultMapper.FromSearchResponse(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Documents, Has.Count.EqualTo(1));
            Assert.That(result.NextSearchAfter, Is.Empty);
        }
    }

    [Test]
    public void FromDocumentSearchResponse_WithHits_ReturnsDocuments() {
        SearchResponse<SampleDocument> response = CreateSearchResponse(total: 2, "order-1", "order-2");

        DocumentSearchResult<SampleDocument> result = ElasticsearchResultMapper.FromDocumentSearchResponse(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Documents.Select(document => document.Id), Is.EqualTo(new[] { "order-1", "order-2" }));
            Assert.That(result.ErrorType, Is.Null);
        }
    }

    [Test]
    public void FromDocumentSearchResponse_WithNoHits_ReturnsSuccessfulEmptyResult() {
        SearchResponse<SampleDocument> response = CreateSearchResponse(total: 0);

        DocumentSearchResult<SampleDocument> result = ElasticsearchResultMapper.FromDocumentSearchResponse(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Documents, Is.Empty);
        }
    }

    [Test]
    public void FromDocumentSearchResponse_WithServerError_ReturnsFailureWithErrorDetails() {
        SearchResponse<SampleDocument> response = TestResponses.Failure(
            new SearchResponse<SampleDocument>(),
            400,
            "search_phase_execution_exception",
            "failed to execute"
        );

        DocumentSearchResult<SampleDocument> result = ElasticsearchResultMapper.FromDocumentSearchResponse(response);

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.Documents, Is.Empty);
            Assert.That(result.ErrorType, Is.EqualTo("search_phase_execution_exception"));
            Assert.That(result.ErrorReason, Is.EqualTo("failed to execute"));
        }
    }

    [Test]
    public void FromDocumentPageResponse_WithTrackTotalHits_ReturnsDocumentsTotalAndPageInfo() {
        SearchResponse<SampleDocument> response = CreateSearchResponse(total: 42, "order-1", "order-2");
        DocumentPageRequest<SampleDocument> request = new() {
            PageNumber = 3,
            PageSize = 2
        };

        DocumentPageResult<SampleDocument> result = ElasticsearchResultMapper.FromDocumentPageResponse(
            response,
            request
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Documents.Select(document => document.Id), Is.EqualTo(new[] { "order-1", "order-2" }));
            Assert.That(result.TotalCount, Is.EqualTo(42));
            Assert.That(result.PageNumber, Is.EqualTo(3));
            Assert.That(result.PageSize, Is.EqualTo(2));
        }
    }

    [Test]
    public void FromDocumentPageResponse_WithTrackTotalHitsDisabled_ReturnsNullTotalCount() {
        SearchResponse<SampleDocument> response = CreateSearchResponse(total: 42, "order-1");
        DocumentPageRequest<SampleDocument> request = new() {
            PageNumber = 1,
            PageSize = 10,
            TrackTotalHits = false
        };

        DocumentPageResult<SampleDocument> result = ElasticsearchResultMapper.FromDocumentPageResponse(
            response,
            request
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.TotalCount, Is.Null);
            Assert.That(result.Documents, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void FromDocumentPageResponse_WithPageBeyondLastPage_ReturnsEmptyDocumentsAndActualTotal() {
        SearchResponse<SampleDocument> response = CreateSearchResponse(total: 5);
        DocumentPageRequest<SampleDocument> request = new() {
            PageNumber = 9,
            PageSize = 10
        };

        DocumentPageResult<SampleDocument> result = ElasticsearchResultMapper.FromDocumentPageResponse(
            response,
            request
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Documents, Is.Empty);
            Assert.That(result.TotalCount, Is.EqualTo(5));
        }
    }

    [Test]
    public void FromDocumentPageResponse_WithServerError_ReturnsFailureWithNullTotalCount() {
        SearchResponse<SampleDocument> response = TestResponses.Failure(
            new SearchResponse<SampleDocument>(),
            400,
            "search_phase_execution_exception",
            "Result window is too large"
        );
        DocumentPageRequest<SampleDocument> request = new() {
            PageNumber = 2,
            PageSize = 7
        };

        DocumentPageResult<SampleDocument> result = ElasticsearchResultMapper.FromDocumentPageResponse(
            response,
            request
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.Documents, Is.Empty);
            Assert.That(result.TotalCount, Is.Null);
            Assert.That(result.PageNumber, Is.EqualTo(2));
            Assert.That(result.PageSize, Is.EqualTo(7));
            Assert.That(result.ErrorType, Is.EqualTo("search_phase_execution_exception"));
            Assert.That(result.ErrorReason, Is.EqualTo("Result window is too large"));
        }
    }

    private static SearchResponse<SampleDocument> CreateSearchResponse(long total, params string[] documentIds) {
        List<Hit<SampleDocument>> hits = documentIds
            .Select((id, index) => new Hit<SampleDocument> {
                Id = id,
                Index = "sample-documents",
                Source = new SampleDocument { Id = id, Name = $"Order {index + 1}", SortKey = index + 1 }
            })
            .ToList();

        return TestResponses.Success(new SearchResponse<SampleDocument> {
            HitsMetadata = new HitsMetadata<SampleDocument>(hits) {
                Total = new TotalHits {
                    Value = total,
                    Relation = TotalHitsRelation.Eq
                }
            }
        });
    }
}
