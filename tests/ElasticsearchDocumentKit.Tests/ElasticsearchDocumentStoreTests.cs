using System.Reflection;
using CloudyWing.ElasticsearchDocumentKit.Internal;
using CloudyWing.ElasticsearchDocumentKit.Indexing;
using CloudyWing.ElasticsearchDocumentKit.Modeling;
using CloudyWing.ElasticsearchDocumentKit.Querying;
using CloudyWing.ElasticsearchDocumentKit.Search;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Bulk;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Transport;

namespace CloudyWing.ElasticsearchDocumentKit.Tests;

[TestFixture]
internal sealed class ElasticsearchDocumentStoreTests {
    [Test]
    public async Task IndexAsync_WithDocumentIdSelector_ShouldSendResolvedIdAsync() {
        SampleDocument document = new() {
            Id = "order-1",
            Name = "Order 1",
            SortKey = 1
        };
        FakeGateway gateway = new() {
            IndexResponse = TestResponses.Success(new IndexResponse {
                Id = "order-1",
                Index = "sample-documents",
                Version = 3
            }, 201)
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents", x => x.Id));

        DocumentOperationResult result = await sut.IndexAsync(document);

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.IndexedDocumentId, Is.EqualTo("order-1"));
            Assert.That(gateway.IndexedIndexName, Is.EqualTo("sample-documents"));
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.DocumentId, Is.EqualTo("order-1"));
            Assert.That(result.Version, Is.EqualTo(3));
        }
    }

    [Test]
    public async Task DeleteAsync_WithDocumentId_ShouldSendDeleteRequestAsync() {
        FakeGateway gateway = new() {
            DeleteResponse = TestResponses.Success(new DeleteResponse {
                Id = "order-1",
                Index = "sample-documents",
                Version = 4
            })
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        DocumentOperationResult result = await sut.DeleteAsync("order-1");

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.DeletedDocumentId, Is.EqualTo("order-1"));
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Operation, Is.EqualTo("delete"));
        }
    }

    [Test]
    public async Task GetAsync_WithDocumentId_ShouldReturnDocumentAsync() {
        SampleDocument document = new() {
            Id = "order-1",
            Name = "Order 1",
            SortKey = 1
        };
        FakeGateway gateway = new() {
            GetResponse = TestResponses.Success(new GetResponse<SampleDocument> {
                Id = "order-1",
                Index = "sample-documents",
                Found = true,
                Source = document,
                Version = 2
            })
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        DocumentGetResult<SampleDocument> result = await sut.GetAsync("order-1");

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.GotDocumentId, Is.EqualTo("order-1"));
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Found, Is.True);
            Assert.That(result.Document, Is.SameAs(document));
            Assert.That(result.Version, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task UpdatePartialAsync_WithPartialDocument_ShouldSendUpdateRequestAsync() {
        SampleDocumentPatch partialDocument = new() {
            Name = "Updated"
        };
        FakeGateway gateway = new() {
            UpdateResponse = TestResponses.Success(new UpdateResponse<SampleDocument> {
                Id = "order-1",
                Index = "sample-documents",
                Version = 5
            })
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        DocumentOperationResult result = await sut.UpdatePartialAsync("order-1", partialDocument);

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.UpdatedDocumentId, Is.EqualTo("order-1"));
            Assert.That(gateway.UpdatedPartialDocument, Is.SameAs(partialDocument));
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Operation, Is.EqualTo("update"));
            Assert.That(result.Version, Is.EqualTo(5));
        }
    }

    [Test]
    public async Task IndexAsync_WithOptions_ShouldForwardOptionsAndMapSeqNoAsync() {
        SampleDocument document = new() {
            Id = "order-1",
            Name = "Order 1",
            SortKey = 1
        };
        IndexDocumentOptions options = new() {
            Refresh = Refresh.WaitFor,
            IfSeqNo = 7,
            IfPrimaryTerm = 2
        };
        FakeGateway gateway = new() {
            IndexResponse = TestResponses.Success(new IndexResponse {
                Id = "order-1",
                Index = "sample-documents",
                Version = 3,
                SeqNo = 8,
                PrimaryTerm = 2
            })
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents", x => x.Id));

        DocumentOperationResult result = await sut.IndexAsync(document, "order-1", options);

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.IndexedOptions, Is.SameAs(options));
            Assert.That(gateway.IndexedDocumentId, Is.EqualTo("order-1"));
            Assert.That(result.SeqNo, Is.EqualTo(8));
            Assert.That(result.PrimaryTerm, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task IndexAsync_WithoutOptions_ShouldPassNullOptionsToGatewayAsync() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        await sut.IndexAsync(new SampleDocument {
            Id = "order-1",
            Name = "Order 1",
            SortKey = 1
        });

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.IndexCallCount, Is.EqualTo(1));
            Assert.That(gateway.IndexedOptions, Is.Null);
        }
    }

    [Test]
    public void IndexAsync_WithInvalidOptions_ShouldNotCallGateway() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));
        SampleDocument document = new() {
            Id = "order-1",
            Name = "Order 1",
            SortKey = 1
        };

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await sut.IndexAsync(document, "order-1", new IndexDocumentOptions { IfSeqNo = 7 })
        );
        Assert.That(gateway.IndexCallCount, Is.EqualTo(0));
    }

    [Test]
    public void IndexAsync_WithNullOptions_ThrowsArgumentNullException() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));
        SampleDocument document = new() {
            Id = "order-1",
            Name = "Order 1",
            SortKey = 1
        };

        Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sut.IndexAsync(document, "order-1", (IndexDocumentOptions)null!)
        );
        Assert.That(gateway.IndexCallCount, Is.EqualTo(0));
    }

    [Test]
    public async Task DeleteAsync_WithOptions_ShouldForwardOptionsToGatewayAsync() {
        DocumentWriteOptions options = new() {
            Refresh = Refresh.True
        };
        FakeGateway gateway = new() {
            DeleteResponse = TestResponses.Success(new DeleteResponse {
                Id = "order-1",
                Index = "sample-documents",
                Version = 4,
                SeqNo = 9,
                PrimaryTerm = 2
            })
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        DocumentOperationResult result = await sut.DeleteAsync("order-1", options);

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.DeletedOptions, Is.SameAs(options));
            Assert.That(gateway.DeletedDocumentId, Is.EqualTo("order-1"));
            Assert.That(result.SeqNo, Is.EqualTo(9));
            Assert.That(result.PrimaryTerm, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task DeleteAsync_WithoutOptions_ShouldPassNullOptionsToGatewayAsync() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        await sut.DeleteAsync("order-1");

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.DeleteCallCount, Is.EqualTo(1));
            Assert.That(gateway.DeletedOptions, Is.Null);
        }
    }

    [Test]
    public void DeleteAsync_WithNullOptions_ThrowsArgumentNullException() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sut.DeleteAsync("order-1", (DocumentWriteOptions)null!)
        );
        Assert.That(gateway.DeleteCallCount, Is.EqualTo(0));
    }

    [Test]
    public async Task UpdatePartialAsync_WithOptions_ShouldForwardOptionsToGatewayAsync() {
        SampleDocumentPatch partialDocument = new() {
            Name = "Updated"
        };
        UpdateDocumentOptions options = new() {
            Refresh = Refresh.WaitFor,
            RetryOnConflict = 3
        };
        FakeGateway gateway = new() {
            UpdateResponse = TestResponses.Success(new UpdateResponse<SampleDocument> {
                Id = "order-1",
                Index = "sample-documents",
                Version = 5,
                SeqNo = 10,
                PrimaryTerm = 2
            })
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        DocumentOperationResult result = await sut.UpdatePartialAsync("order-1", partialDocument, options);

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.UpdatedOptions, Is.SameAs(options));
            Assert.That(gateway.UpdatedPartialDocument, Is.SameAs(partialDocument));
            Assert.That(result.SeqNo, Is.EqualTo(10));
            Assert.That(result.PrimaryTerm, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task UpdatePartialAsync_WithoutOptions_ShouldPassNullOptionsToGatewayAsync() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        await sut.UpdatePartialAsync("order-1", new SampleDocumentPatch { Name = "Updated" });

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.UpdateCallCount, Is.EqualTo(1));
            Assert.That(gateway.UpdatedOptions, Is.Null);
        }
    }

    [Test]
    public void UpdatePartialAsync_WithRetryOnConflictAndSeqNo_ShouldNotCallGateway() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));
        UpdateDocumentOptions options = new() {
            RetryOnConflict = 1,
            IfSeqNo = 7,
            IfPrimaryTerm = 2
        };

        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await sut.UpdatePartialAsync("order-1", new SampleDocumentPatch { Name = "Updated" }, options)
        );
        Assert.That(gateway.UpdateCallCount, Is.EqualTo(0));
    }

    [Test]
    public void UpdatePartialAsync_WithNullOptions_ThrowsArgumentNullException() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        Assert.ThrowsAsync<ArgumentNullException>(
            async () => await sut.UpdatePartialAsync(
                "order-1",
                new SampleDocumentPatch { Name = "Updated" },
                (UpdateDocumentOptions)null!
            )
        );
        Assert.That(gateway.UpdateCallCount, Is.EqualTo(0));
    }

    [Test]
    public async Task CreateAsync_WithExplicitDocumentId_ShouldPreferExplicitIdAsync() {
        FakeGateway gateway = new() {
            CreateResponse = TestResponses.Success(
                new IndexResponse {
                    Id = "explicit-id",
                    Index = "sample-documents",
                    Version = 1
                },
                201
            )
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents", x => x.Id));
        DocumentWriteOptions options = new() {
            Refresh = Refresh.True
        };

        DocumentOperationResult result = await sut.CreateAsync(
            new SampleDocument {
                Id = "selector-id",
                Name = "Order 1",
                SortKey = 1
            },
            "explicit-id",
            options
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.CreatedDocumentId, Is.EqualTo("explicit-id"));
            Assert.That(gateway.CreatedIndexName, Is.EqualTo("sample-documents"));
            Assert.That(gateway.CreatedOptions, Is.SameAs(options));
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Operation, Is.EqualTo("create"));
        }
    }

    [Test]
    public async Task CreateAsync_WithoutDocumentIdAndWithIdSelector_ShouldUseSelectorIdAsync() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents", x => x.Id));

        await sut.CreateAsync(new SampleDocument {
            Id = "selector-id",
            Name = "Order 1",
            SortKey = 1
        });

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.CreatedDocumentId, Is.EqualTo("selector-id"));
            Assert.That(gateway.CreatedOptions, Is.Null);
        }
    }

    [Test]
    public async Task CreateAsync_WithoutDocumentIdAndIdSelector_ShouldPassNullIdAsync() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        await sut.CreateAsync(new SampleDocument {
            Id = "order-1",
            Name = "Order 1",
            SortKey = 1
        });

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.CreateCallCount, Is.EqualTo(1));
            Assert.That(gateway.CreatedDocumentId, Is.Null);
        }
    }

    [Test]
    public void CreateAsync_WithNullDocument_ThrowsArgumentNullException() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        Assert.ThrowsAsync<ArgumentNullException>(async () => await sut.CreateAsync(null!));
        Assert.That(gateway.CreateCallCount, Is.EqualTo(0));
    }

    [Test]
    public async Task CreateAsync_WithExistingDocumentId_ShouldReturnConflictFailureAsync() {
        FakeGateway gateway = new() {
            CreateResponse = TestResponses.Failure(
                new IndexResponse {
                    Id = "order-1",
                    Index = "sample-documents"
                },
                409,
                "version_conflict_engine_exception",
                "document already exists"
            )
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        DocumentOperationResult result = await sut.CreateAsync(
            new SampleDocument {
                Id = "order-1",
                Name = "Order 1",
                SortKey = 1
            },
            "order-1"
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.Operation, Is.EqualTo("create"));
            Assert.That(result.StatusCode, Is.EqualTo(409));
            Assert.That(result.ErrorType, Is.EqualTo("version_conflict_engine_exception"));
        }
    }

    [Test]
    public async Task UpdateByScriptAsync_WithScript_ShouldForwardDocumentIdScriptAndOptionsAsync() {
        Script script = new() {
            Source = "ctx._source.sortKey += params.step"
        };
        DocumentWriteOptions options = new() {
            Refresh = Refresh.WaitFor
        };
        FakeGateway gateway = new() {
            UpdateByScriptResponse = TestResponses.Success(new UpdateResponse<SampleDocument> {
                Id = "order-1",
                Index = "sample-documents",
                Version = 6,
                SeqNo = 11,
                PrimaryTerm = 2
            })
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        DocumentOperationResult result = await sut.UpdateByScriptAsync("order-1", script, options);

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.ScriptUpdatedDocumentId, Is.EqualTo("order-1"));
            Assert.That(gateway.ScriptUpdatedScript, Is.SameAs(script));
            Assert.That(gateway.ScriptUpdatedOptions, Is.SameAs(options));
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Operation, Is.EqualTo("update_by_script"));
            Assert.That(result.SeqNo, Is.EqualTo(11));
        }
    }

    [TestCase("")]
    [TestCase("   ")]
    public void UpdateByScriptAsync_WithBlankDocumentId_ThrowsArgumentException(string documentId) {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        Assert.ThrowsAsync<ArgumentException>(
            async () => await sut.UpdateByScriptAsync(documentId, new Script { Source = "ctx._source.sortKey = 1" })
        );
        Assert.That(gateway.UpdateByScriptCallCount, Is.EqualTo(0));
    }

    [Test]
    public void UpdateByScriptAsync_WithNullScript_ThrowsArgumentNullException() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        Assert.ThrowsAsync<ArgumentNullException>(async () => await sut.UpdateByScriptAsync("order-1", null!));
        Assert.That(gateway.UpdateByScriptCallCount, Is.EqualTo(0));
    }

    [Test]
    public async Task UpdateByScriptAsync_WithMissingDocument_ShouldReturnNotFoundFailureAsync() {
        FakeGateway gateway = new() {
            UpdateByScriptResponse = TestResponses.Failure(
                new UpdateResponse<SampleDocument> {
                    Id = "order-1",
                    Index = "sample-documents"
                },
                404,
                "document_missing_exception",
                "document missing"
            )
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        DocumentOperationResult result = await sut.UpdateByScriptAsync(
            "order-1",
            new Script { Source = "ctx._source.sortKey = 1" }
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(404));
            Assert.That(result.ErrorType, Is.EqualTo("document_missing_exception"));
        }
    }

    [Test]
    public async Task UpdateByQueryAsync_WithFiltersAndScript_ShouldForwardRequestAndMapResultAsync() {
        ElasticsearchQueryFilterBuilder<SampleDocument> filters = new();
        filters.Term(document => document.Name, "Order 1");
        UpdateByQueryRequest<SampleDocument> request = new() {
            Filters = filters,
            Script = new Script { Source = "ctx._source.sortKey = 2" }
        };
        FakeGateway gateway = new() {
            UpdateByQueryResponse = TestResponses.Success(new UpdateByQueryResponse {
                Updated = 4,
                VersionConflicts = 1
            })
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        ByQueryOperationResult result = await sut.UpdateByQueryAsync(request);

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.UpdateByQueryRequest, Is.SameAs(request));
            Assert.That(gateway.ByQueryIndexName, Is.EqualTo("sample-documents"));
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Updated, Is.EqualTo(4));
            Assert.That(result.VersionConflicts, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task UpdateByQueryAsync_WithoutFiltersAndWithConfigureRequest_ShouldForwardToGatewayAsync() {
        UpdateByQueryRequest<SampleDocument> request = new() {
            ConfigureRequest = _ => { }
        };
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        await sut.UpdateByQueryAsync(request);

        Assert.That(gateway.UpdateByQueryCallCount, Is.EqualTo(1));
    }

    [Test]
    public void UpdateByQueryAsync_WithoutFiltersAndConfigureRequest_ThrowsArgumentExceptionWithoutCallingGateway() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        ArgumentException? exception = Assert.ThrowsAsync<ArgumentException>(
            async () => await sut.UpdateByQueryAsync(new UpdateByQueryRequest<SampleDocument>())
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(exception?.ParamName, Is.EqualTo("request"));
            Assert.That(gateway.UpdateByQueryCallCount, Is.EqualTo(0));
        }
    }

    [Test]
    public void UpdateByQueryAsync_WithEmptyFilterBuilder_ThrowsArgumentExceptionWithoutCallingGateway() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        Assert.ThrowsAsync<ArgumentException>(
            async () => await sut.UpdateByQueryAsync(new UpdateByQueryRequest<SampleDocument> {
                Filters = new ElasticsearchQueryFilterBuilder<SampleDocument>()
            })
        );
        Assert.That(gateway.UpdateByQueryCallCount, Is.EqualTo(0));
    }

    [Test]
    public void UpdateByQueryAsync_WithNullRequest_ThrowsArgumentNullException() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        Assert.ThrowsAsync<ArgumentNullException>(async () => await sut.UpdateByQueryAsync(null!));
        Assert.That(gateway.UpdateByQueryCallCount, Is.EqualTo(0));
    }

    [Test]
    public async Task DeleteByQueryAsync_WithFilters_ShouldForwardRequestAndMapResultAsync() {
        ElasticsearchQueryFilterBuilder<SampleDocument> filters = new();
        filters.Term(document => document.Name, "Order 1");
        DeleteByQueryRequest<SampleDocument> request = new() {
            Filters = filters
        };
        FakeGateway gateway = new() {
            DeleteByQueryResponse = TestResponses.Success(new DeleteByQueryResponse {
                Deleted = 6
            })
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        ByQueryOperationResult result = await sut.DeleteByQueryAsync(request);

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.DeleteByQueryRequest, Is.SameAs(request));
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Operation, Is.EqualTo("delete_by_query"));
            Assert.That(result.Deleted, Is.EqualTo(6));
        }
    }

    [Test]
    public void DeleteByQueryAsync_WithoutFiltersAndConfigureRequest_ThrowsArgumentExceptionWithoutCallingGateway() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        Assert.ThrowsAsync<ArgumentException>(
            async () => await sut.DeleteByQueryAsync(new DeleteByQueryRequest<SampleDocument>())
        );
        Assert.That(gateway.DeleteByQueryCallCount, Is.EqualTo(0));
    }

    [Test]
    public void DeleteByQueryAsync_WithEmptyFilterBuilder_ThrowsArgumentExceptionWithoutCallingGateway() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        Assert.ThrowsAsync<ArgumentException>(
            async () => await sut.DeleteByQueryAsync(new DeleteByQueryRequest<SampleDocument> {
                Filters = new ElasticsearchQueryFilterBuilder<SampleDocument>()
            })
        );
        Assert.That(gateway.DeleteByQueryCallCount, Is.EqualTo(0));
    }

    [Test]
    public void DeleteByQueryAsync_WithNullRequest_ThrowsArgumentNullException() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        Assert.ThrowsAsync<ArgumentNullException>(async () => await sut.DeleteByQueryAsync(null!));
        Assert.That(gateway.DeleteByQueryCallCount, Is.EqualTo(0));
    }

    [Test]
    public async Task DeleteByQueryAsync_WithConflictFailure_ShouldReturnFailureResultAsync() {
        ElasticsearchQueryFilterBuilder<SampleDocument> filters = new();
        filters.Term(document => document.Name, "Order 1");
        FakeGateway gateway = new() {
            DeleteByQueryResponse = TestResponses.Failure(
                new DeleteByQueryResponse(),
                409,
                "version_conflict_engine_exception",
                "version conflict"
            )
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        ByQueryOperationResult result = await sut.DeleteByQueryAsync(new DeleteByQueryRequest<SampleDocument> {
            Filters = filters
        });

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(409));
            Assert.That(result.Deleted, Is.EqualTo(0));
        }
    }

    [Test]
    public async Task CountAsync_WithFilters_ShouldForwardFiltersAndMapCountAsync() {
        ElasticsearchQueryFilterBuilder<SampleDocument> filters = new();
        filters.Term(document => document.Name, "Order 1");
        FakeGateway gateway = new() {
            CountResponse = TestResponses.Success(new CountResponse {
                Count = 12
            })
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        DocumentCountResult result = await sut.CountAsync(filters);

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.CountFilters, Is.SameAs(filters));
            Assert.That(gateway.ByQueryIndexName, Is.EqualTo("sample-documents"));
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Count, Is.EqualTo(12));
        }
    }

    [Test]
    public async Task CountAsync_WithEmptyBuilder_ShouldForwardToGatewayWithoutThrowingAsync() {
        ElasticsearchQueryFilterBuilder<SampleDocument> filters = new();
        FakeGateway gateway = new() {
            CountResponse = TestResponses.Success(new CountResponse {
                Count = 100
            })
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        DocumentCountResult result = await sut.CountAsync(filters);

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.CountCallCount, Is.EqualTo(1));
            Assert.That(result.Count, Is.EqualTo(100));
        }
    }

    [Test]
    public void CountAsync_WithNullFilters_ThrowsArgumentNullException() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        Assert.ThrowsAsync<ArgumentNullException>(async () => await sut.CountAsync(null!));
        Assert.That(gateway.CountCallCount, Is.EqualTo(0));
    }

    [Test]
    public async Task CountAsync_WithServerError_ShouldReturnFailureResultAsync() {
        FakeGateway gateway = new() {
            CountResponse = TestResponses.Failure(
                new CountResponse(),
                404,
                "index_not_found_exception",
                "no such index"
            )
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        DocumentCountResult result = await sut.CountAsync(new ElasticsearchQueryFilterBuilder<SampleDocument>());

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.Count, Is.EqualTo(0));
            Assert.That(result.ErrorType, Is.EqualTo("index_not_found_exception"));
        }
    }

    [Test]
    public async Task UpdateByQueryAsync_WithFilters_ShouldSetIndexNameOnResultAsync() {
        ElasticsearchQueryFilterBuilder<SampleDocument> filters = new();
        filters.Term(document => document.Name, "Order 1");
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("resolved-index"));

        ByQueryOperationResult result = await sut.UpdateByQueryAsync(new UpdateByQueryRequest<SampleDocument> {
            Filters = filters
        });

        Assert.That(result.IndexName, Is.EqualTo("resolved-index"));
    }

    [Test]
    public async Task DeleteByQueryAsync_WithFilters_ShouldSetIndexNameOnResultAsync() {
        ElasticsearchQueryFilterBuilder<SampleDocument> filters = new();
        filters.Term(document => document.Name, "Order 1");
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("resolved-index"));

        ByQueryOperationResult result = await sut.DeleteByQueryAsync(new DeleteByQueryRequest<SampleDocument> {
            Filters = filters
        });

        Assert.That(result.IndexName, Is.EqualTo("resolved-index"));
    }

    [Test]
    public async Task CountAsync_WithEmptyBuilder_ShouldSetIndexNameOnResultAsync() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("resolved-index"));

        DocumentCountResult result = await sut.CountAsync(new ElasticsearchQueryFilterBuilder<SampleDocument>());

        Assert.That(result.IndexName, Is.EqualTo("resolved-index"));
    }

    [TestCaseSource(nameof(CancellationForwardingCases))]
    public async Task StoreMethod_WithCancellationToken_ForwardsTokenToGatewayAsync(
        Func<ElasticsearchDocumentStore<SampleDocument>, CancellationToken, Task> invoke
    ) {
        using CancellationTokenSource cts = new();
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        await invoke(sut, cts.Token);

        Assert.That(gateway.LastCancellationToken, Is.EqualTo(cts.Token));
    }

    private static IEnumerable<TestCaseData> CancellationForwardingCases() {
        SampleDocument document = new() {
            Id = "order-1",
            Name = "Order 1",
            SortKey = 1
        };
        Script script = new() { Source = "ctx._source.sortKey = 2" };

        yield return CreateCancellationCase(
            "CreateAsync",
            (sut, token) => sut.CreateAsync(document, "order-1", null, token)
        );
        yield return CreateCancellationCase(
            "UpdateByScriptAsync",
            (sut, token) => sut.UpdateByScriptAsync("order-1", script, null, token)
        );
        yield return CreateCancellationCase(
            "IndexAsync with options",
            (sut, token) => sut.IndexAsync(document, "order-1", new IndexDocumentOptions(), token)
        );
        yield return CreateCancellationCase(
            "UpdatePartialAsync with options",
            (sut, token) => sut.UpdatePartialAsync(
                "order-1",
                new SampleDocumentPatch { Name = "Order 2" },
                new UpdateDocumentOptions(),
                token
            )
        );
        yield return CreateCancellationCase(
            "DeleteAsync with options",
            (sut, token) => sut.DeleteAsync("order-1", new DocumentWriteOptions(), token)
        );
        yield return CreateCancellationCase(
            "UpdateByQueryAsync",
            (sut, token) => sut.UpdateByQueryAsync(
                new UpdateByQueryRequest<SampleDocument> {
                    ConfigureRequest = _ => { }
                },
                token
            )
        );
        yield return CreateCancellationCase(
            "DeleteByQueryAsync",
            (sut, token) => sut.DeleteByQueryAsync(
                new DeleteByQueryRequest<SampleDocument> {
                    ConfigureRequest = _ => { }
                },
                token
            )
        );
        yield return CreateCancellationCase(
            "CountAsync",
            (sut, token) => sut.CountAsync(new ElasticsearchQueryFilterBuilder<SampleDocument>(), token)
        );
        yield return CreateCancellationCase(
            "SearchAsync",
            (sut, token) => sut.SearchAsync(new DocumentSearchRequest<SampleDocument>(), token)
        );
        yield return CreateCancellationCase(
            "SearchPageAsync",
            (sut, token) => sut.SearchPageAsync(
                new DocumentPageRequest<SampleDocument> {
                    PageNumber = 1,
                    PageSize = 10
                },
                token
            )
        );
    }

    private static TestCaseData CreateCancellationCase(
        string name,
        Func<ElasticsearchDocumentStore<SampleDocument>, CancellationToken, Task> invoke
    ) {
        return new TestCaseData(invoke).SetName($"StoreMethod_WithCancellationToken_ForwardsTokenToGateway({name})");
    }

    [Test]
    public async Task GetAsync_WithSeqNoAndPrimaryTerm_ShouldExposeConcurrencyTokenAsync() {
        FakeGateway gateway = new() {
            GetResponse = TestResponses.Success(new GetResponse<SampleDocument> {
                Id = "order-1",
                Index = "sample-documents",
                Found = true,
                Version = 2,
                SeqNo = 7,
                PrimaryTerm = 2
            })
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        DocumentGetResult<SampleDocument> result = await sut.GetAsync("order-1");

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.SeqNo, Is.EqualTo(7));
            Assert.That(result.PrimaryTerm, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task BulkIndexAsync_WithPartialFailure_ShouldExposeFailureSummaryAsync() {
        List<SampleDocument> documents = [
            new SampleDocument {
                Id = "order-1",
                Name = "Order 1",
                SortKey = 1
            },
            new SampleDocument {
                Id = "order-2",
                Name = "Order 2",
                SortKey = 2
            }
        ];
        FakeGateway gateway = new() {
            BulkResponse = TestResponses.Success(new BulkResponse {
                Errors = true,
                Items = [
                    TestResponses.BulkIndexItem("order-1", "sample-documents", 201),
                    TestResponses.BulkIndexItem(
                        "order-2",
                        "sample-documents",
                        409,
                        new ErrorCause {
                            Type = "version_conflict_engine_exception",
                            Reason = "document conflict"
                        }
                    )
                ]
            })
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents", x => x.Id));

        BulkOperationResult result = await sut.BulkIndexAsync(documents);

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.BulkDocumentIds, Is.EqualTo(new[] { "order-1", "order-2" }));
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.TotalCount, Is.EqualTo(2));
            Assert.That(result.SucceededCount, Is.EqualTo(1));
            Assert.That(result.FailedCount, Is.EqualTo(1));
            Assert.That(result.Failures.Single().DocumentId, Is.EqualTo("order-2"));
            Assert.That(result.Failures.Single().ErrorType, Is.EqualTo("version_conflict_engine_exception"));
        }
    }

    [Test]
    public async Task BulkIndexAsync_WithOptions_ShouldChunkAndPublishDeadLettersAsync() {
        List<SampleDocument> documents = [
            new SampleDocument {
                Id = "order-1",
                Name = "Order 1",
                SortKey = 1
            },
            new SampleDocument {
                Id = "order-2",
                Name = "Order 2",
                SortKey = 2
            },
            new SampleDocument {
                Id = "order-3",
                Name = "Order 3",
                SortKey = 3
            }
        ];
        List<BulkDeadLetter<SampleDocument>> deadLetters = [];
        FakeGateway gateway = new() {
            BulkResponses = [
                TestResponses.Success(new BulkResponse {
                    Items = [
                        TestResponses.BulkIndexItem("order-1", "sample-documents", 201),
                        TestResponses.BulkIndexItem(
                            "order-2",
                            "sample-documents",
                            400,
                            new ErrorCause {
                                Type = "mapper_parsing_exception",
                                Reason = "invalid mapping"
                            }
                        )
                    ],
                    Errors = true
                }),
                TestResponses.Success(new BulkResponse {
                    Items = [
                        TestResponses.BulkIndexItem("order-3", "sample-documents", 201)
                    ]
                })
            ]
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents", x => x.Id));
        BulkIndexOptions<SampleDocument> options = new() {
            ChunkSize = 2,
            DeadLetterCallback = (deadLetter, _) => {
                deadLetters.Add(deadLetter);
                return Task.CompletedTask;
            }
        };

        BulkOperationResult result = await sut.BulkIndexAsync(documents, options);

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.BulkBatchSizes, Is.EqualTo(new[] { 2, 1 }));
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.TotalCount, Is.EqualTo(3));
            Assert.That(result.SucceededCount, Is.EqualTo(2));
            Assert.That(result.FailedCount, Is.EqualTo(1));
            Assert.That(result.Failures.Single().ItemIndex, Is.EqualTo(1));
            Assert.That(deadLetters.Single().Document.Id, Is.EqualTo("order-2"));
            Assert.That(deadLetters.Single().Failure.ErrorType, Is.EqualTo("mapper_parsing_exception"));
        }
    }

    [Test]
    public async Task BulkIndexAsync_WithStreamingEnumerable_ShouldSendChunkBeforeEnumeratingRemainingDocumentsAsync() {
        int yieldedCount = 0;
        List<int> observedYieldCounts = [];
        FakeGateway gateway = new() {
            BulkCallCallback = () => observedYieldCounts.Add(yieldedCount)
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents", x => x.Id));
        BulkIndexOptions<SampleDocument> options = new() {
            ChunkSize = 2
        };

        BulkOperationResult result = await sut.BulkIndexAsync(
            CreateStreamingDocuments(3, () => yieldedCount++),
            options
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.BulkBatchSizes, Is.EqualTo(new[] { 2, 1 }));
            Assert.That(observedYieldCounts, Is.EqualTo(new[] { 2, 3 }));
            Assert.That(result.TotalCount, Is.EqualTo(3));
        }
    }

    [Test]
    public async Task BulkIndexAsync_WithAsyncEnumerable_ShouldChunkAndIndexAsync() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents", x => x.Id));
        BulkIndexOptions<SampleDocument> options = new() {
            ChunkSize = 2
        };

        BulkOperationResult result = await sut.BulkIndexAsync(CreateAsyncDocuments(3), options);

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.BulkBatchSizes, Is.EqualTo(new[] { 2, 1 }));
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.TotalCount, Is.EqualTo(3));
        }
    }

    [Test]
    public async Task BulkIndexAsync_WithInvalidBulkResponse_ShouldRetryBeforeReturningAsync() {
        List<SampleDocument> documents = [
            new SampleDocument {
                Id = "order-1",
                Name = "Order 1",
                SortKey = 1
            },
            new SampleDocument {
                Id = "order-2",
                Name = "Order 2",
                SortKey = 2
            }
        ];
        FakeGateway gateway = new() {
            BulkResponses = [
                TestResponses.Failure(new BulkResponse {
                    Items = []
                }, 503),
                TestResponses.Success(new BulkResponse {
                    Items = [
                        TestResponses.BulkIndexItem("order-1", "sample-documents", 201),
                        TestResponses.BulkIndexItem("order-2", "sample-documents", 201)
                    ]
                })
            ]
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents", x => x.Id));
        BulkIndexOptions<SampleDocument> options = new() {
            MaxRetryAttempts = 1,
            RetryDelayProvider = _ => TimeSpan.Zero
        };

        BulkOperationResult result = await sut.BulkIndexAsync(documents, options);

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.BulkBatchSizes, Is.EqualTo(new[] { 2, 2 }));
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.HasErrors, Is.False);
            Assert.That(result.SucceededCount, Is.EqualTo(2));
            Assert.That(result.FailedCount, Is.EqualTo(0));
        }
    }

    [Test]
    public async Task BulkIndexAsync_WithInvalidBulkResponseAfterRetries_ShouldFailAllDocumentsAsync() {
        List<SampleDocument> documents = [
            new SampleDocument {
                Id = "order-1",
                Name = "Order 1",
                SortKey = 1
            },
            new SampleDocument {
                Id = "order-2",
                Name = "Order 2",
                SortKey = 2
            }
        ];
        List<BulkDeadLetter<SampleDocument>> deadLetters = [];
        FakeGateway gateway = new() {
            BulkResponses = [
                TestResponses.Failure(new BulkResponse {
                    Items = []
                }, 503),
                TestResponses.Failure(new BulkResponse {
                    Items = []
                }, 503)
            ]
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents", x => x.Id));
        BulkIndexOptions<SampleDocument> options = new() {
            MaxRetryAttempts = 1,
            RetryDelayProvider = _ => TimeSpan.Zero,
            DeadLetterCallback = (deadLetter, _) => {
                deadLetters.Add(deadLetter);
                return Task.CompletedTask;
            }
        };

        BulkOperationResult result = await sut.BulkIndexAsync(documents, options);

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.BulkBatchSizes, Is.EqualTo(new[] { 2, 2 }));
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.HasErrors, Is.True);
            Assert.That(result.TotalCount, Is.EqualTo(2));
            Assert.That(result.SucceededCount, Is.EqualTo(0));
            Assert.That(result.FailedCount, Is.EqualTo(2));
            Assert.That(result.Failures.Select(failure => failure.ItemIndex), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(result.Failures.Select(failure => failure.StatusCode), Is.EqualTo(new[] { 503, 503 }));
            Assert.That(
                deadLetters.Select(deadLetter => deadLetter.Document.Id),
                Is.EqualTo(new[] { "order-1", "order-2" })
            );
            Assert.That(deadLetters.Select(deadLetter => deadLetter.Attempt), Is.EqualTo(new[] { 2, 2 }));
        }
    }

    [Test]
    public async Task BulkIndexAsync_EmptyDocuments_ReturnsSuccessfulEmptyResultAsync() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        BulkOperationResult result = await sut.BulkIndexAsync([]);

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.BulkBatchSizes, Is.Empty);
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.TotalCount, Is.Zero);
            Assert.That(result.Failures, Is.Empty);
        }
    }

    [Test]
    public async Task BulkIndexAsync_GatewayExceptionAfterRetries_MapsEveryDocumentFailureAsync() {
        List<SampleDocument> documents = [
            new SampleDocument { Id = "order-1", Name = "Order 1", SortKey = 1 },
            new SampleDocument { Id = "order-2", Name = "Order 2", SortKey = 2 }
        ];
        FakeGateway gateway = new() {
            BulkException = new InvalidOperationException("gateway unavailable")
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents", x => x.Id));
        BulkIndexOptions<SampleDocument> options = new() {
            MaxRetryAttempts = 1,
            RetryDelayProvider = _ => TimeSpan.Zero
        };

        BulkOperationResult result = await sut.BulkIndexAsync(documents, options);

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.BulkBatchSizes, Is.EqualTo(new[] { 2, 2 }));
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.FailedCount, Is.EqualTo(2));
            Assert.That(result.Failures.Select(failure => failure.DocumentId), Is.EqualTo(new[] { "order-1", "order-2" }));
            Assert.That(result.Failures.Select(failure => failure.ErrorType), Is.All.EqualTo("InvalidOperationException"));
        }
    }

    [Test]
    public void BulkIndexAsync_CallerTokenCancelledWithoutRetry_ThrowsOperationCanceledException() {
        List<SampleDocument> documents = [
            new SampleDocument { Id = "order-1", Name = "Order 1", SortKey = 1 },
            new SampleDocument { Id = "order-2", Name = "Order 2", SortKey = 2 }
        ];
        using CancellationTokenSource cts = new();
        FakeGateway gateway = new() {
            BulkException = new OperationCanceledException(),
            BulkCallCallback = cts.Cancel
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents", x => x.Id));
        BulkIndexOptions<SampleDocument> options = new() {
            MaxRetryAttempts = 0
        };

        Assert.ThrowsAsync<OperationCanceledException>(
            async () => await sut.BulkIndexAsync(documents, options, cts.Token)
        );

        Assert.That(gateway.BulkBatchSizes, Has.Count.EqualTo(1));
    }

    [Test]
    public void BulkIndexAsync_CallerTokenCancelledWithZeroDelayRetry_DoesNotRetry() {
        List<SampleDocument> documents = [
            new SampleDocument { Id = "order-1", Name = "Order 1", SortKey = 1 },
            new SampleDocument { Id = "order-2", Name = "Order 2", SortKey = 2 }
        ];
        using CancellationTokenSource cts = new();
        FakeGateway gateway = new() {
            BulkException = new OperationCanceledException(),
            BulkCallCallback = cts.Cancel
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents", x => x.Id));
        BulkIndexOptions<SampleDocument> options = new() {
            MaxRetryAttempts = 1,
            RetryDelayProvider = _ => TimeSpan.Zero
        };

        Assert.ThrowsAsync<OperationCanceledException>(
            async () => await sut.BulkIndexAsync(documents, options, cts.Token)
        );

        Assert.That(gateway.BulkBatchSizes, Has.Count.EqualTo(1));
    }

    [Test]
    public void BulkIndexAsync_AsyncDocumentsCallerTokenCancelled_ThrowsOperationCanceledException() {
        using CancellationTokenSource cts = new();
        FakeGateway gateway = new() {
            BulkException = new OperationCanceledException(),
            BulkCallCallback = cts.Cancel
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents", x => x.Id));
        BulkIndexOptions<SampleDocument> options = new() {
            MaxRetryAttempts = 1,
            RetryDelayProvider = _ => TimeSpan.Zero
        };

        Assert.ThrowsAsync<OperationCanceledException>(
            async () => await sut.BulkIndexAsync(CreateAsyncDocuments(2), options, cts.Token)
        );

        Assert.That(gateway.BulkBatchSizes, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task BulkIndexAsync_CancellationNotFromCallerToken_RetriesAndMapsFailureAsync() {
        List<SampleDocument> documents = [
            new SampleDocument { Id = "order-1", Name = "Order 1", SortKey = 1 },
            new SampleDocument { Id = "order-2", Name = "Order 2", SortKey = 2 }
        ];
        FakeGateway gateway = new() {
            BulkException = new TaskCanceledException("http timeout")
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents", x => x.Id));
        BulkIndexOptions<SampleDocument> options = new() {
            MaxRetryAttempts = 1,
            RetryDelayProvider = _ => TimeSpan.Zero
        };

        BulkOperationResult result = await sut.BulkIndexAsync(documents, options);

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.BulkBatchSizes, Is.EqualTo(new[] { 2, 2 }));
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.FailedCount, Is.EqualTo(2));
        }
    }

    [Test]
    public void BulkIndexAsync_CallerTokenCancelledWithWrappedExceptionWithoutRetry_ThrowsOperationCanceledException() {
        List<SampleDocument> documents = [
            new SampleDocument { Id = "order-1", Name = "Order 1", SortKey = 1 },
            new SampleDocument { Id = "order-2", Name = "Order 2", SortKey = 2 }
        ];
        using CancellationTokenSource cts = new();
        InvalidOperationException wrapped = new("wrapped");
        FakeGateway gateway = new() {
            BulkException = wrapped,
            BulkCallCallback = cts.Cancel
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents", x => x.Id));
        BulkIndexOptions<SampleDocument> options = new() {
            MaxRetryAttempts = 0
        };

        OperationCanceledException? exception = Assert.ThrowsAsync<OperationCanceledException>(
            async () => await sut.BulkIndexAsync(documents, options, cts.Token)
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(exception!.InnerException, Is.SameAs(wrapped));
            Assert.That(exception.CancellationToken, Is.EqualTo(cts.Token));
            Assert.That(gateway.BulkBatchSizes, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void BulkIndexAsync_CallerTokenCancelledWithWrappedExceptionAndZeroDelayRetry_DoesNotRetry() {
        List<SampleDocument> documents = [
            new SampleDocument { Id = "order-1", Name = "Order 1", SortKey = 1 },
            new SampleDocument { Id = "order-2", Name = "Order 2", SortKey = 2 }
        ];
        using CancellationTokenSource cts = new();
        InvalidOperationException wrapped = new("wrapped");
        FakeGateway gateway = new() {
            BulkException = wrapped,
            BulkCallCallback = cts.Cancel
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents", x => x.Id));
        BulkIndexOptions<SampleDocument> options = new() {
            MaxRetryAttempts = 1,
            RetryDelayProvider = _ => TimeSpan.Zero
        };

        OperationCanceledException? exception = Assert.ThrowsAsync<OperationCanceledException>(
            async () => await sut.BulkIndexAsync(documents, options, cts.Token)
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(exception!.InnerException, Is.SameAs(wrapped));
            Assert.That(exception.CancellationToken, Is.EqualTo(cts.Token));
            Assert.That(gateway.BulkBatchSizes, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void BulkIndexAsync_CallerTokenCancelledWithEmptyInvalidResponse_ThrowsOperationCanceledException() {
        List<SampleDocument> documents = [
            new SampleDocument { Id = "order-1", Name = "Order 1", SortKey = 1 },
            new SampleDocument { Id = "order-2", Name = "Order 2", SortKey = 2 }
        ];
        using CancellationTokenSource cts = new();
        BulkResponse invalidResponse = TestResponses.Failure(new BulkResponse {
            Items = []
        }, 503);
        FakeGateway gateway = new() {
            BulkResponse = invalidResponse,
            BulkCallCallback = cts.Cancel
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents", x => x.Id));
        BulkIndexOptions<SampleDocument> options = new() {
            MaxRetryAttempts = 1,
            RetryDelayProvider = _ => TimeSpan.Zero
        };

        Assert.ThrowsAsync<OperationCanceledException>(
            async () => await sut.BulkIndexAsync(documents, options, cts.Token)
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(invalidResponse.IsValidResponse, Is.False);
            Assert.That(invalidResponse.Items, Is.Empty);
            Assert.That(gateway.BulkBatchSizes, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void BulkIndexAsync_CallerTokenCancelledWithInvalidResponseOriginalException_PreservesInnerException() {
        List<SampleDocument> documents = [
            new SampleDocument { Id = "order-1", Name = "Order 1", SortKey = 1 }
        ];
        using CancellationTokenSource cts = new();
        BulkResponse invalidResponse = TestResponses.Failure(new BulkResponse {
            Items = []
        }, 503);
        HttpRequestException original = new("connection reset");
        MethodInfo originalExceptionSetter = typeof(ApiCallDetails)
            .GetProperty(nameof(ApiCallDetails.OriginalException))!
            .GetSetMethod(nonPublic: true)!;
        originalExceptionSetter.Invoke(invalidResponse.ApiCallDetails, [original]);
        FakeGateway gateway = new() {
            BulkResponse = invalidResponse,
            BulkCallCallback = cts.Cancel
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents", x => x.Id));
        BulkIndexOptions<SampleDocument> options = new() {
            MaxRetryAttempts = 1,
            RetryDelayProvider = _ => TimeSpan.Zero
        };

        OperationCanceledException? exception = Assert.ThrowsAsync<OperationCanceledException>(
            async () => await sut.BulkIndexAsync(documents, options, cts.Token)
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(exception!.InnerException, Is.SameAs(original));
            Assert.That(exception.CancellationToken, Is.EqualTo(cts.Token));
            Assert.That(gateway.BulkBatchSizes, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void BulkIndexAsync_CallerTokenCancelledAfterPartialItemFailure_PublishesDeadLetterThenThrows() {
        List<SampleDocument> documents = [
            new SampleDocument { Id = "order-1", Name = "Order 1", SortKey = 1 },
            new SampleDocument { Id = "order-2", Name = "Order 2", SortKey = 2 }
        ];
        using CancellationTokenSource cts = new();
        List<bool> callbackTokenCancelled = [];
        FakeGateway gateway = new() {
            BulkResponse = TestResponses.Success(new BulkResponse {
                Items = [
                    TestResponses.BulkIndexItem(
                        "order-1",
                        "sample-documents",
                        400,
                        new ErrorCause {
                            Type = "mapper_parsing_exception",
                            Reason = "invalid mapping"
                        }
                    )
                ],
                Errors = true
            }),
            BulkCallCallback = cts.Cancel
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents", x => x.Id));
        BulkIndexOptions<SampleDocument> options = new() {
            ChunkSize = 1,
            DeadLetterCallback = (_, token) => {
                callbackTokenCancelled.Add(token.IsCancellationRequested);
                return Task.CompletedTask;
            }
        };

        Assert.ThrowsAsync<OperationCanceledException>(
            async () => await sut.BulkIndexAsync(documents, options, cts.Token)
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(callbackTokenCancelled, Is.EqualTo(new[] { true }));
            Assert.That(gateway.BulkBatchSizes, Is.EqualTo(new[] { 1 }));
        }
    }

    [Test]
    public void BulkIndexAsync_RetryWithoutIdSelector_ThrowsInvalidOperationExceptionBeforeGatewayCall() {
        List<SampleDocument> documents = [
            new SampleDocument { Id = "order-1", Name = "Order 1", SortKey = 1 }
        ];
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));
        BulkIndexOptions<SampleDocument> options = new() {
            MaxRetryAttempts = 1
        };

        using (Assert.EnterMultipleScope()) {
            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await sut.BulkIndexAsync(documents, options)
            );
            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await sut.BulkIndexAsync(CreateAsyncDocuments(1), options)
            );
            Assert.That(gateway.BulkBatchSizes, Is.Empty);
        }
    }

    [Test]
    public async Task BulkIndexAsync_RetryWithoutIdSelectorAndAllowed_CallsGatewayAsync() {
        List<SampleDocument> documents = [
            new SampleDocument { Id = "order-1", Name = "Order 1", SortKey = 1 }
        ];
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));
        BulkIndexOptions<SampleDocument> options = new() {
            MaxRetryAttempts = 1,
            AllowRetryWithoutIdSelector = true
        };

        await sut.BulkIndexAsync(documents, options);

        Assert.That(gateway.BulkBatchSizes, Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public async Task BulkIndexAsync_NoRetryWithoutIdSelector_CallsGatewayAsync() {
        List<SampleDocument> documents = [
            new SampleDocument { Id = "order-1", Name = "Order 1", SortKey = 1 }
        ];
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));
        BulkIndexOptions<SampleDocument> options = new() {
            MaxRetryAttempts = 0
        };

        await sut.BulkIndexAsync(documents, options);

        Assert.That(gateway.BulkBatchSizes, Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public async Task BulkIndexAsync_SecondChunkFailure_OffsetsFailureIndexAsync() {
        List<SampleDocument> documents = [
            new SampleDocument { Id = "order-1", Name = "Order 1", SortKey = 1 },
            new SampleDocument { Id = "order-2", Name = "Order 2", SortKey = 2 },
            new SampleDocument { Id = "order-3", Name = "Order 3", SortKey = 3 }
        ];
        FakeGateway gateway = new() {
            BulkResponses = [
                TestResponses.Success(new BulkResponse {
                    Items = [
                        TestResponses.BulkIndexItem("order-1", "sample-documents", 201),
                        TestResponses.BulkIndexItem("order-2", "sample-documents", 201)
                    ]
                }),
                TestResponses.Success(new BulkResponse {
                    Errors = true,
                    Items = [
                        TestResponses.BulkIndexItem(
                            "order-3",
                            "sample-documents",
                            400,
                            new ErrorCause { Type = "mapper_parsing_exception", Reason = "invalid mapping" }
                        )
                    ]
                })
            ]
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents", x => x.Id));

        BulkOperationResult result = await sut.BulkIndexAsync(
            documents,
            new BulkIndexOptions<SampleDocument> { ChunkSize = 2 }
        );

        Assert.That(result.Failures.Single().ItemIndex, Is.EqualTo(2));
    }

    [Test]
    public async Task SearchAfterAsync_WithHits_ShouldReturnDocumentsAndNextSortValuesAsync() {
        SampleDocument first = new() {
            Id = "order-1",
            Name = "Order 1",
            SortKey = 1
        };
        SampleDocument second = new() {
            Id = "order-2",
            Name = "Order 2",
            SortKey = 2
        };
        FakeGateway gateway = new() {
            SearchResponse = TestResponses.Success(new SearchResponse<SampleDocument> {
                HitsMetadata = new HitsMetadata<SampleDocument>([
                    new Hit<SampleDocument> {
                        Id = first.Id,
                        Index = "sample-documents",
                        Source = first,
                        Sort = [first.SortKey, first.Id]
                    },
                    new Hit<SampleDocument> {
                        Id = second.Id,
                        Index = "sample-documents",
                        Source = second,
                        Sort = [second.SortKey, second.Id]
                    }
                ])
            })
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));
        SearchAfterRequest<SampleDocument> request = new() {
            Size = 2,
            SearchAfter = [1, "order-1"]
        };

        SearchAfterPage<SampleDocument> result = await sut.SearchAfterAsync(request);

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.SearchRequest, Is.SameAs(request));
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Documents.Select(x => x.Id), Is.EqualTo(new[] { "order-1", "order-2" }));
            Assert.That(result.NextSearchAfter, Is.EqualTo(new FieldValue[] { 2, "order-2" }));
        }
    }

    [Test]
    public void SearchAfterAsync_WithInvalidSize_ShouldThrowArgumentOutOfRangeException() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));
        SearchAfterRequest<SampleDocument> request = new() {
            Size = 0
        };

        Assert.That(
            async () => await sut.SearchAfterAsync(request),
            Throws.TypeOf<ArgumentOutOfRangeException>()
        );
    }

    [Test]
    public async Task SearchAsync_WithFiltersAndSize_ShouldForwardRequestAndMapDocumentsAsync() {
        ElasticsearchQueryFilterBuilder<SampleDocument> filters = new();
        filters.Term(document => document.Name, "Order 1");
        FakeGateway gateway = new() {
            SearchResponse = CreateSearchResponse(total: 2, "order-1", "order-2")
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));
        DocumentSearchRequest<SampleDocument> request = new() {
            Filters = filters,
            Size = 2
        };

        DocumentSearchResult<SampleDocument> result = await sut.SearchAsync(request);

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.DocumentSearchRequest, Is.SameAs(request));
            Assert.That(gateway.DocumentSearchIndexName, Is.EqualTo("sample-documents"));
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Documents.Select(x => x.Id), Is.EqualTo(new[] { "order-1", "order-2" }));
        }
    }

    [Test]
    public async Task SearchAsync_WithoutSize_ShouldForwardToGatewayAsync() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        DocumentSearchResult<SampleDocument> result = await sut.SearchAsync(new DocumentSearchRequest<SampleDocument>());

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.DocumentSearchCallCount, Is.EqualTo(1));
            Assert.That(gateway.DocumentSearchRequest!.Size, Is.Null);
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Documents, Is.Empty);
        }
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void SearchAsync_WithSizeLessThanOne_ThrowsArgumentOutOfRangeException(int size) {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await sut.SearchAsync(new DocumentSearchRequest<SampleDocument> { Size = size })
        );
        Assert.That(gateway.DocumentSearchCallCount, Is.EqualTo(0));
    }

    [Test]
    public void SearchAsync_WithNullRequest_ThrowsArgumentNullException() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        Assert.ThrowsAsync<ArgumentNullException>(async () => await sut.SearchAsync(null!));
        Assert.That(gateway.DocumentSearchCallCount, Is.EqualTo(0));
    }

    [Test]
    public async Task SearchAsync_WithServerError_ShouldReturnFailureResultAsync() {
        FakeGateway gateway = new() {
            SearchResponse = TestResponses.Failure(
                new SearchResponse<SampleDocument>(),
                404,
                "index_not_found_exception",
                "no such index"
            )
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        DocumentSearchResult<SampleDocument> result = await sut.SearchAsync(new DocumentSearchRequest<SampleDocument>());

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.Documents, Is.Empty);
            Assert.That(result.ErrorType, Is.EqualTo("index_not_found_exception"));
        }
    }

    [Test]
    public async Task SearchPageAsync_WithFilters_ShouldForwardRequestAndMapPageAsync() {
        ElasticsearchQueryFilterBuilder<SampleDocument> filters = new();
        filters.Term(document => document.Name, "Order 1");
        FakeGateway gateway = new() {
            SearchResponse = CreateSearchResponse(total: 42, "order-1", "order-2")
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));
        DocumentPageRequest<SampleDocument> request = new() {
            PageNumber = 2,
            PageSize = 2,
            Filters = filters
        };

        DocumentPageResult<SampleDocument> result = await sut.SearchPageAsync(request);

        using (Assert.EnterMultipleScope()) {
            Assert.That(gateway.DocumentPageRequest, Is.SameAs(request));
            Assert.That(gateway.DocumentSearchIndexName, Is.EqualTo("sample-documents"));
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Documents.Select(x => x.Id), Is.EqualTo(new[] { "order-1", "order-2" }));
            Assert.That(result.TotalCount, Is.EqualTo(42));
            Assert.That(result.PageNumber, Is.EqualTo(2));
            Assert.That(result.PageSize, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task SearchPageAsync_WithTrackTotalHitsDisabled_ShouldReturnNullTotalCountAsync() {
        FakeGateway gateway = new() {
            SearchResponse = CreateSearchResponse(total: 42, "order-1")
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        DocumentPageResult<SampleDocument> result = await sut.SearchPageAsync(new DocumentPageRequest<SampleDocument> {
            PageNumber = 1,
            PageSize = 10,
            TrackTotalHits = false
        });

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.TotalCount, Is.Null);
        }
    }

    [TestCase(0, 10)]
    [TestCase(-1, 10)]
    [TestCase(1, 0)]
    [TestCase(1, -5)]
    [TestCase(int.MaxValue, 2)]
    public void SearchPageAsync_WithInvalidPaging_ThrowsArgumentOutOfRangeException(int pageNumber, int pageSize) {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));
        DocumentPageRequest<SampleDocument> request = new() {
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await sut.SearchPageAsync(request));
        Assert.That(gateway.DocumentSearchPageCallCount, Is.EqualTo(0));
    }

    [Test]
    public void SearchPageAsync_WithNullRequest_ThrowsArgumentNullException() {
        FakeGateway gateway = new();
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        Assert.ThrowsAsync<ArgumentNullException>(async () => await sut.SearchPageAsync(null!));
        Assert.That(gateway.DocumentSearchPageCallCount, Is.EqualTo(0));
    }

    [Test]
    public async Task SearchPageAsync_WithServerError_ShouldReturnFailureResultAsync() {
        FakeGateway gateway = new() {
            SearchResponse = TestResponses.Failure(
                new SearchResponse<SampleDocument>(),
                400,
                "search_phase_execution_exception",
                "Result window is too large"
            )
        };
        ElasticsearchDocumentStore<SampleDocument> sut = new(gateway, CreateModelResolver("sample-documents"));

        DocumentPageResult<SampleDocument> result = await sut.SearchPageAsync(new DocumentPageRequest<SampleDocument> {
            PageNumber = 2000,
            PageSize = 10
        });

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.Documents, Is.Empty);
            Assert.That(result.TotalCount, Is.Null);
            Assert.That(result.ErrorType, Is.EqualTo("search_phase_execution_exception"));
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

    private static IElasticsearchDocumentModelResolver CreateModelResolver(
        string indexName,
        Func<SampleDocument, string?>? idSelector = null
    ) {
        IElasticsearchDocumentModelResolver resolver = Substitute.For<IElasticsearchDocumentModelResolver>();
        resolver.Resolve<SampleDocument>().Returns(new ElasticsearchDocumentModel<SampleDocument>(indexName, idSelector));

        return resolver;
    }

    private sealed class FakeGateway : IElasticsearchGateway {
        public IndexResponse IndexResponse { get; init; } = TestResponses.Success(new IndexResponse());

        public DeleteResponse DeleteResponse { get; init; } = TestResponses.Success(new DeleteResponse());

        public GetResponse<SampleDocument> GetResponse { get; init; } = TestResponses.Success(
            new GetResponse<SampleDocument>()
        );

        public UpdateResponse<SampleDocument> UpdateResponse { get; init; } = TestResponses.Success(
            new UpdateResponse<SampleDocument>()
        );

        public BulkResponse BulkResponse { get; init; } = TestResponses.Success(new BulkResponse {
            Items = []
        });

        public IReadOnlyList<BulkResponse> BulkResponses { get; init; } = [];

        public Exception? BulkException { get; init; }

        public Action? BulkCallCallback { get; init; }

        public SearchResponse<SampleDocument> SearchResponse { get; init; } = TestResponses.Success(
            new SearchResponse<SampleDocument> {
                HitsMetadata = new HitsMetadata<SampleDocument>([])
            }
        );

        public string? IndexedIndexName { get; private set; }

        public string? IndexedDocumentId { get; private set; }

        public string? DeletedDocumentId { get; private set; }

        public string? GotDocumentId { get; private set; }

        public string? UpdatedDocumentId { get; private set; }

        public object? UpdatedPartialDocument { get; private set; }

        public IndexDocumentOptions? IndexedOptions { get; private set; }

        public DocumentWriteOptions? DeletedOptions { get; private set; }

        public UpdateDocumentOptions? UpdatedOptions { get; private set; }

        public int IndexCallCount { get; private set; }

        public int DeleteCallCount { get; private set; }

        public int UpdateCallCount { get; private set; }

        public IndexResponse CreateResponse { get; init; } = TestResponses.Success(new IndexResponse());

        public UpdateResponse<SampleDocument> UpdateByScriptResponse { get; init; } = TestResponses.Success(
            new UpdateResponse<SampleDocument>()
        );

        public string? CreatedIndexName { get; private set; }

        public string? CreatedDocumentId { get; private set; }

        public DocumentWriteOptions? CreatedOptions { get; private set; }

        public int CreateCallCount { get; private set; }

        public string? ScriptUpdatedDocumentId { get; private set; }

        public Script? ScriptUpdatedScript { get; private set; }

        public DocumentWriteOptions? ScriptUpdatedOptions { get; private set; }

        public int UpdateByScriptCallCount { get; private set; }

        public UpdateByQueryResponse UpdateByQueryResponse { get; init; } = TestResponses.Success(
            new UpdateByQueryResponse()
        );

        public DeleteByQueryResponse DeleteByQueryResponse { get; init; } = TestResponses.Success(
            new DeleteByQueryResponse()
        );

        public CountResponse CountResponse { get; init; } = TestResponses.Success(new CountResponse());

        public UpdateByQueryRequest<SampleDocument>? UpdateByQueryRequest { get; private set; }

        public DeleteByQueryRequest<SampleDocument>? DeleteByQueryRequest { get; private set; }

        public ElasticsearchQueryFilterBuilder<SampleDocument>? CountFilters { get; private set; }

        public string? ByQueryIndexName { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public int UpdateByQueryCallCount { get; private set; }

        public int DeleteByQueryCallCount { get; private set; }

        public int CountCallCount { get; private set; }

        public DocumentSearchRequest<SampleDocument>? DocumentSearchRequest { get; private set; }

        public DocumentPageRequest<SampleDocument>? DocumentPageRequest { get; private set; }

        public string? DocumentSearchIndexName { get; private set; }

        public int DocumentSearchCallCount { get; private set; }

        public int DocumentSearchPageCallCount { get; private set; }

        public IReadOnlyList<string?> BulkDocumentIds { get; private set; } = [];

        public IReadOnlyList<int> BulkBatchSizes { get; private set; } = [];

        private List<int> MutableBulkBatchSizes { get; } = [];

        private int BulkCallCount { get; set; }

        public SearchAfterRequest<SampleDocument>? SearchRequest { get; private set; }

        public Task<IndexResponse> IndexAsync<TDocument>(
            string indexName,
            TDocument document,
            string? documentId,
            IndexDocumentOptions? options,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            IndexCallCount++;
            LastCancellationToken = cancellationToken;
            IndexedIndexName = indexName;
            IndexedDocumentId = documentId;
            IndexedOptions = options;
            return Task.FromResult(IndexResponse);
        }

        public Task<IndexResponse> CreateAsync<TDocument>(
            string indexName,
            TDocument document,
            string? documentId,
            DocumentWriteOptions? options,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            CreateCallCount++;
            LastCancellationToken = cancellationToken;
            CreatedIndexName = indexName;
            CreatedDocumentId = documentId;
            CreatedOptions = options;
            return Task.FromResult(CreateResponse);
        }

        public Task<UpdateResponse<TDocument>> UpdateByScriptAsync<TDocument>(
            string indexName,
            string documentId,
            Script script,
            DocumentWriteOptions? options,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            UpdateByScriptCallCount++;
            LastCancellationToken = cancellationToken;
            ScriptUpdatedDocumentId = documentId;
            ScriptUpdatedScript = script;
            ScriptUpdatedOptions = options;
            return Task.FromResult((UpdateResponse<TDocument>)(object)UpdateByScriptResponse);
        }

        public Task<UpdateByQueryResponse> UpdateByQueryAsync<TDocument>(
            string indexName,
            UpdateByQueryRequest<TDocument> request,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            UpdateByQueryCallCount++;
            LastCancellationToken = cancellationToken;
            ByQueryIndexName = indexName;
            UpdateByQueryRequest = request as UpdateByQueryRequest<SampleDocument>;
            return Task.FromResult(UpdateByQueryResponse);
        }

        public Task<DeleteByQueryResponse> DeleteByQueryAsync<TDocument>(
            string indexName,
            DeleteByQueryRequest<TDocument> request,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            DeleteByQueryCallCount++;
            LastCancellationToken = cancellationToken;
            ByQueryIndexName = indexName;
            DeleteByQueryRequest = request as DeleteByQueryRequest<SampleDocument>;
            return Task.FromResult(DeleteByQueryResponse);
        }

        public Task<CountResponse> CountAsync<TDocument>(
            string indexName,
            ElasticsearchQueryFilterBuilder<TDocument>? filters,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            CountCallCount++;
            LastCancellationToken = cancellationToken;
            ByQueryIndexName = indexName;
            CountFilters = filters as ElasticsearchQueryFilterBuilder<SampleDocument>;
            return Task.FromResult(CountResponse);
        }

        public Task<DeleteResponse> DeleteAsync<TDocument>(
            string indexName,
            string documentId,
            DocumentWriteOptions? options,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            DeleteCallCount++;
            LastCancellationToken = cancellationToken;
            DeletedDocumentId = documentId;
            DeletedOptions = options;
            return Task.FromResult(DeleteResponse);
        }

        public Task<GetResponse<TDocument>> GetAsync<TDocument>(
            string indexName,
            string documentId,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            GotDocumentId = documentId;
            return Task.FromResult((GetResponse<TDocument>)(object)GetResponse);
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
            UpdateCallCount++;
            LastCancellationToken = cancellationToken;
            UpdatedDocumentId = documentId;
            UpdatedPartialDocument = partialDocument;
            UpdatedOptions = options;
            return Task.FromResult((UpdateResponse<TDocument>)(object)UpdateResponse);
        }

        public Task<BulkResponse> BulkIndexAsync<TDocument>(
            string indexName,
            IReadOnlyList<TDocument> documents,
            Func<TDocument, string?>? documentIdSelector,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            BulkCallCount++;
            MutableBulkBatchSizes.Add(documents.Count);
            BulkBatchSizes = MutableBulkBatchSizes.ToList();
            BulkDocumentIds = documents.Select(document => documentIdSelector?.Invoke(document)).ToList();
            BulkCallCallback?.Invoke();

            if (BulkException is not null) {
                return Task.FromException<BulkResponse>(BulkException);
            }

            if (BulkResponses.Count > 0) {
                return Task.FromResult(BulkResponses[BulkCallCount - 1]);
            }

            return Task.FromResult(BulkResponse);
        }

        public Task<SearchResponse<TDocument>> SearchAsync<TDocument>(
            string indexName,
            DocumentSearchRequest<TDocument> request,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            DocumentSearchCallCount++;
            LastCancellationToken = cancellationToken;
            DocumentSearchIndexName = indexName;
            DocumentSearchRequest = request as DocumentSearchRequest<SampleDocument>;
            return Task.FromResult((SearchResponse<TDocument>)(object)SearchResponse);
        }

        public Task<SearchResponse<TDocument>> SearchPageAsync<TDocument>(
            string indexName,
            DocumentPageRequest<TDocument> request,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            DocumentSearchPageCallCount++;
            LastCancellationToken = cancellationToken;
            DocumentSearchIndexName = indexName;
            DocumentPageRequest = request as DocumentPageRequest<SampleDocument>;
            return Task.FromResult((SearchResponse<TDocument>)(object)SearchResponse);
        }

        public Task<SearchResponse<TDocument>> SearchAfterAsync<TDocument>(
            string indexName,
            SearchAfterRequest<TDocument> request,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            SearchRequest = request as SearchAfterRequest<SampleDocument>;
            return Task.FromResult((SearchResponse<TDocument>)(object)SearchResponse);
        }

        public Task<Elastic.Clients.Elasticsearch.IndexManagement.ExistsResponse> IndexExistsAsync(
            string indexName,
            CancellationToken cancellationToken
        ) {
            throw new NotSupportedException();
        }

        public Task<Elastic.Clients.Elasticsearch.IndexManagement.CreateIndexResponse> CreateIndexAsync<TDocument>(
            string indexName,
            Action<Elastic.Clients.Elasticsearch.IndexManagement.CreateIndexRequestDescriptor<TDocument>>? configureIndex,
            CancellationToken cancellationToken
        )
            where TDocument : class {
            throw new NotSupportedException();
        }
    }

    private sealed class SampleDocumentPatch {
        public required string Name { get; init; }
    }

    private static IEnumerable<SampleDocument> CreateStreamingDocuments(int count, Action onYield) {
        for (int i = 1; i <= count; i++) {
            onYield();
            yield return new SampleDocument {
                Id = $"order-{i}",
                Name = $"Order {i}",
                SortKey = i
            };
        }
    }

    private static async IAsyncEnumerable<SampleDocument> CreateAsyncDocuments(int count) {
        for (int i = 1; i <= count; i++) {
            await Task.Yield();
            yield return new SampleDocument {
                Id = $"order-{i}",
                Name = $"Order {i}",
                SortKey = i
            };
        }
    }
}
