using CloudyWing.ElasticsearchDocumentKit.Indexing;
using CloudyWing.ElasticsearchDocumentKit.Modeling;
using CloudyWing.ElasticsearchDocumentKit.Querying;
using CloudyWing.ElasticsearchDocumentKit.Search;
using Elastic.Clients.Elasticsearch;

namespace CloudyWing.ElasticsearchDocumentKit.Tests.Testcontainers.Integration;

internal static class ElasticsearchDocumentStoreIntegrationScenario {
    public static async Task RunAsync(ElasticsearchClient client) {
        ElasticsearchDocumentsOptions options = new() {
            IndexPrefix = CreateIndexPrefix()
        };
        options.ConfigureDocument<IntegrationDocument>(model => model
            .ConfigureMapping(mapping => mapping.Properties(
                properties => properties
                    .Keyword(field => field.Id)
                    .Text(field => field.Name)
                    .IntegerNumber(field => field.SortKey)
            ))
            .HasId(document => document.Id)
        );
        DefaultIndexNameResolver resolver = new(options);
        DefaultElasticsearchDocumentModelResolver modelResolver = new(options, resolver);
        string indexName = resolver.Resolve<IntegrationDocument>();
        string aliasName = $"{indexName}-alias";
        ElasticsearchIndexInitializer<IntegrationDocument> initializer = new(client, modelResolver);
        ElasticsearchDocumentStore<IntegrationDocument> store = new(client, modelResolver);
        ElasticsearchAliasSwitcher aliasSwitcher = new(client);

        try {
            await DeleteIndexIfExistsAsync(client, indexName).ConfigureAwait(false);

            IndexInitializationResult initializationResult = await initializer.InitializeAsync().ConfigureAwait(false);
            DocumentOperationResult firstIndexResult = await store.IndexAsync(new IntegrationDocument {
                Id = "order-1",
                Name = "Order 1",
                SortKey = 1
            }).ConfigureAwait(false);
            DocumentOperationResult secondIndexResult = await store.IndexAsync(new IntegrationDocument {
                Id = "order-2",
                Name = "Order 2",
                SortKey = 2
            }).ConfigureAwait(false);
            BulkOperationResult bulkResult = await store.BulkIndexAsync([
                new IntegrationDocument {
                    Id = "order-3",
                    Name = "Order 3",
                    SortKey = 3
                },
                new IntegrationDocument {
                    Id = "order-invalid",
                    Name = "Order Invalid",
                    SortKey = "not-a-number"
                }
            ]).ConfigureAwait(false);

            await client.Indices.RefreshAsync((Indices)indexName).ConfigureAwait(false);

            AliasSwitchResult aliasSwitchResult = await aliasSwitcher
                .SwitchAsync(aliasName, indexName)
                .ConfigureAwait(false);
            ElasticsearchDocumentStore<IntegrationDocument> aliasStore = CreateStore(client, aliasName);
            DocumentGetResult<IntegrationDocument> aliasedDocumentResult = await aliasStore
                .GetAsync("order-1")
                .ConfigureAwait(false);

            SearchAfterPage<IntegrationDocument> firstPage = await store
                .SearchAfterAsync(CreatePageRequest())
                .ConfigureAwait(false);
            SearchAfterPage<IntegrationDocument> secondPage = await store.SearchAfterAsync(
                CreatePageRequest(firstPage.NextSearchAfter)
            ).ConfigureAwait(false);

            using (Assert.EnterMultipleScope()) {
                Assert.That(initializationResult.Created, Is.True);
                Assert.That(firstIndexResult.IsSuccessful, Is.True);
                Assert.That(secondIndexResult.IsSuccessful, Is.True);
                Assert.That(bulkResult.IsSuccessful, Is.False);
                Assert.That(bulkResult.SucceededCount, Is.EqualTo(1));
                Assert.That(bulkResult.FailedCount, Is.EqualTo(1));
                Assert.That(bulkResult.Failures.Single().DocumentId, Is.EqualTo("order-invalid"));
                Assert.That(aliasSwitchResult.IsSuccessful, Is.True);
                Assert.That(aliasSwitchResult.AliasName, Is.EqualTo(aliasName));
                Assert.That(aliasSwitchResult.AddedIndexName, Is.EqualTo(indexName));
                Assert.That(aliasedDocumentResult.IsSuccessful, Is.True);
                Assert.That(aliasedDocumentResult.Found, Is.True);
                Assert.That(aliasedDocumentResult.Document?.Id, Is.EqualTo("order-1"));
                Assert.That(firstPage.Documents.Select(document => document.Id), Is.EqualTo(["order-1"]));
                Assert.That(secondPage.Documents.Select(document => document.Id), Is.EqualTo(["order-2"]));
            }
        } finally {
            await DeleteIndexIfExistsAsync(client, indexName).ConfigureAwait(false);
        }

        await RunWriteOperationsAsync(client).ConfigureAwait(false);
        await RunQueryOperationsAsync(client).ConfigureAwait(false);
    }

    private static async Task RunWriteOperationsAsync(ElasticsearchClient client) {
        await using IntegrationStoreContext<ScenarioDocument> context = await CreateScenarioContextAsync(client)
            .ConfigureAwait(false);
        ElasticsearchDocumentStore<ScenarioDocument> store = context.Store;
        DocumentWriteOptions refreshTrue = new() { Refresh = Refresh.True };

        // CreateAsync：新 ID 成功，已存在的 ID 為 409 失敗結果且不覆寫原文件
        DocumentOperationResult created = await store.CreateAsync(
            CreateScenarioDocument(1, "Created"),
            null,
            refreshTrue
        ).ConfigureAwait(false);
        DocumentOperationResult duplicate = await store.CreateAsync(
            CreateScenarioDocument(1, "Overwritten"),
            null,
            refreshTrue
        ).ConfigureAwait(false);
        DocumentGetResult<ScenarioDocument> afterDuplicate = await store
            .GetAsync("s-01")
            .ConfigureAwait(false);

        using (Assert.EnterMultipleScope()) {
            Assert.That(created.IsSuccessful, Is.True);
            Assert.That(created.Operation, Is.EqualTo("create"));
            Assert.That(duplicate.IsSuccessful, Is.False);
            Assert.That(duplicate.StatusCode, Is.EqualTo(409));
            Assert.That(duplicate.ErrorType, Is.EqualTo("version_conflict_engine_exception"));
            Assert.That(afterDuplicate.Document?.Name, Is.EqualTo("Created"));
            Assert.That(afterDuplicate.SeqNo, Is.Not.Null);
            Assert.That(afterDuplicate.PrimaryTerm, Is.Not.Null);
        }

        // 樂觀鎖：帶入 GetAsync 取得的 SeqNo／PrimaryTerm 可寫入，過期值為 409 失敗結果且文件未變
        long seqNo = afterDuplicate.SeqNo!.Value;
        long primaryTerm = afterDuplicate.PrimaryTerm!.Value;
        DocumentOperationResult optimisticWrite = await store.IndexAsync(
            CreateScenarioDocument(1, "Updated"),
            "s-01",
            new IndexDocumentOptions {
                IfSeqNo = seqNo,
                IfPrimaryTerm = primaryTerm,
                Refresh = Refresh.True
            }
        ).ConfigureAwait(false);
        DocumentOperationResult staleWrite = await store.IndexAsync(
            CreateScenarioDocument(1, "Stale"),
            "s-01",
            new IndexDocumentOptions { IfSeqNo = seqNo, IfPrimaryTerm = primaryTerm }
        ).ConfigureAwait(false);
        DocumentGetResult<ScenarioDocument> afterStaleWrite = await store.GetAsync("s-01").ConfigureAwait(false);

        using (Assert.EnterMultipleScope()) {
            Assert.That(optimisticWrite.IsSuccessful, Is.True);
            Assert.That(optimisticWrite.SeqNo, Is.GreaterThan(seqNo));
            Assert.That(staleWrite.IsSuccessful, Is.False);
            Assert.That(staleWrite.StatusCode, Is.EqualTo(409));
            Assert.That(staleWrite.ErrorType, Is.EqualTo("version_conflict_engine_exception"));
            Assert.That(afterStaleWrite.Document?.Name, Is.EqualTo("Updated"));
        }

        // UpdatePartialAsync：過期 SeqNo 失敗，設定 RetryOnConflict 的更新成功
        DocumentOperationResult staleUpdate = await store.UpdatePartialAsync(
            "s-01",
            new { Counter = 99 },
            new UpdateDocumentOptions { IfSeqNo = seqNo, IfPrimaryTerm = primaryTerm }
        ).ConfigureAwait(false);
        DocumentOperationResult retryUpdate = await store.UpdatePartialAsync(
            "s-01",
            new { Counter = 5 },
            new UpdateDocumentOptions { RetryOnConflict = 3, Refresh = Refresh.True }
        ).ConfigureAwait(false);
        DocumentGetResult<ScenarioDocument> afterUpdates = await store.GetAsync("s-01").ConfigureAwait(false);

        using (Assert.EnterMultipleScope()) {
            Assert.That(staleUpdate.IsSuccessful, Is.False);
            Assert.That(staleUpdate.StatusCode, Is.EqualTo(409));
            Assert.That(retryUpdate.IsSuccessful, Is.True);
            Assert.That(afterUpdates.Document?.Counter, Is.EqualTo(5));
        }

        // UpdateByScriptAsync：修改指定欄位
        DocumentOperationResult scriptUpdate = await store.UpdateByScriptAsync(
            "s-01",
            new Script {
                Source = "ctx._source.counter += params.delta",
                Params = new Dictionary<string, object> { ["delta"] = 10 }
            },
            refreshTrue
        ).ConfigureAwait(false);
        DocumentGetResult<ScenarioDocument> afterScript = await store.GetAsync("s-01").ConfigureAwait(false);

        using (Assert.EnterMultipleScope()) {
            Assert.That(scriptUpdate.IsSuccessful, Is.True);
            Assert.That(scriptUpdate.Operation, Is.EqualTo("update_by_script"));
            Assert.That(afterScript.Document?.Counter, Is.EqualTo(15));
            Assert.That(afterScript.Document?.Name, Is.EqualTo("Updated"));
        }

        // refresh：寫入後不必另外 refresh，立即可由 CountAsync 看見
        DocumentCountResult countAfterTrue = await store
            .CountAsync(new ElasticsearchQueryFilterBuilder<ScenarioDocument>())
            .ConfigureAwait(false);
        DocumentOperationResult waitForWrite = await store.IndexAsync(
            CreateScenarioDocument(2, "WaitFor"),
            "s-02",
            new IndexDocumentOptions { Refresh = Refresh.WaitFor }
        ).ConfigureAwait(false);
        DocumentCountResult countAfterWaitFor = await store
            .CountAsync(new ElasticsearchQueryFilterBuilder<ScenarioDocument>())
            .ConfigureAwait(false);

        using (Assert.EnterMultipleScope()) {
            Assert.That(countAfterTrue.Count, Is.EqualTo(1));
            Assert.That(waitForWrite.IsSuccessful, Is.True);
            Assert.That(countAfterWaitFor.Count, Is.EqualTo(2));
        }

        // 不存在的 ID：更新為 404 失敗結果，不補建文件
        DocumentOperationResult missingUpdate = await store.UpdatePartialAsync(
            "s-99",
            new { Counter = 1 },
            new UpdateDocumentOptions { Refresh = Refresh.True }
        ).ConfigureAwait(false);
        DocumentCountResult countAfterMissing = await store
            .CountAsync(new ElasticsearchQueryFilterBuilder<ScenarioDocument>())
            .ConfigureAwait(false);

        using (Assert.EnterMultipleScope()) {
            Assert.That(missingUpdate.IsSuccessful, Is.False);
            Assert.That(missingUpdate.StatusCode, Is.EqualTo(404));
            Assert.That(countAfterMissing.Count, Is.EqualTo(2));
        }
    }

    private static async Task RunQueryOperationsAsync(ElasticsearchClient client) {
        await using IntegrationStoreContext<ScenarioDocument> context = await CreateScenarioContextAsync(client)
            .ConfigureAwait(false);
        ElasticsearchDocumentStore<ScenarioDocument> store = context.Store;
        ScenarioDocument[] documents = Enumerable
            .Range(1, 12)
            .Select(number => CreateScenarioDocument(number, $"Order {number}", number <= 7 ? "a" : "b"))
            .ToArray();

        BulkOperationResult bulkResult = await store.BulkIndexAsync(documents).ConfigureAwait(false);
        await client.Indices.RefreshAsync((Indices)context.IndexName).ConfigureAwait(false);

        Assert.That(bulkResult.IsSuccessful, Is.True);

        // SearchAsync：未指定 Size 為 Elasticsearch 預設 10 筆，指定 Size 則限制筆數
        DocumentSearchResult<ScenarioDocument> defaultSizeResult = await store
            .SearchAsync(new DocumentSearchRequest<ScenarioDocument> {
                ConfigureSearch = SortBySequence
            })
            .ConfigureAwait(false);
        ElasticsearchQueryFilterBuilder<ScenarioDocument> categoryA = new();
        categoryA.Term(document => document.Category, "a");
        DocumentSearchResult<ScenarioDocument> limitedResult = await store
            .SearchAsync(new DocumentSearchRequest<ScenarioDocument> {
                Filters = categoryA,
                Size = 3,
                ConfigureSearch = SortBySequence
            })
            .ConfigureAwait(false);

        using (Assert.EnterMultipleScope()) {
            Assert.That(defaultSizeResult.IsSuccessful, Is.True);
            Assert.That(defaultSizeResult.Documents, Has.Count.EqualTo(10));
            Assert.That(limitedResult.IsSuccessful, Is.True);
            Assert.That(limitedResult.Documents.Select(document => document.Id), Is.EqualTo(["s-01", "s-02", "s-03"]));
        }

        // SearchPageAsync：各頁文件與精確 TotalCount，頁碼超出範圍為成功的空頁
        DocumentPageResult<ScenarioDocument> firstPage = await SearchPageAsync(store, 1, 5).ConfigureAwait(false);
        DocumentPageResult<ScenarioDocument> lastPage = await SearchPageAsync(store, 3, 5).ConfigureAwait(false);
        DocumentPageResult<ScenarioDocument> beyondLastPage = await SearchPageAsync(store, 4, 5).ConfigureAwait(false);
        DocumentPageResult<ScenarioDocument> untrackedPage = await store
            .SearchPageAsync(new DocumentPageRequest<ScenarioDocument> {
                PageNumber = 1,
                PageSize = 5,
                TrackTotalHits = false,
                ConfigureSearch = SortBySequence
            })
            .ConfigureAwait(false);

        using (Assert.EnterMultipleScope()) {
            Assert.That(firstPage.IsSuccessful, Is.True);
            Assert.That(
                firstPage.Documents.Select(document => document.Id),
                Is.EqualTo(["s-01", "s-02", "s-03", "s-04", "s-05"])
            );
            Assert.That(firstPage.TotalCount, Is.EqualTo(12));
            Assert.That(lastPage.Documents.Select(document => document.Id), Is.EqualTo(["s-11", "s-12"]));
            Assert.That(lastPage.TotalCount, Is.EqualTo(12));
            Assert.That(beyondLastPage.IsSuccessful, Is.True);
            Assert.That(beyondLastPage.Documents, Is.Empty);
            Assert.That(beyondLastPage.TotalCount, Is.EqualTo(12));
            Assert.That(untrackedPage.IsSuccessful, Is.True);
            Assert.That(untrackedPage.Documents, Has.Count.EqualTo(5));
            Assert.That(untrackedPage.TotalCount, Is.Null);
        }

        // 超過 max_result_window（預設 10000）：失敗結果，資料不受影響
        DocumentPageResult<ScenarioDocument> beyondWindow = await SearchPageAsync(store, 1001, 10)
            .ConfigureAwait(false);

        using (Assert.EnterMultipleScope()) {
            Assert.That(beyondWindow.IsSuccessful, Is.False);
            Assert.That(beyondWindow.Documents, Is.Empty);
            Assert.That(beyondWindow.TotalCount, Is.Null);
            Assert.That(beyondWindow.ErrorType, Is.Not.Null);
        }

        TestContext.Out.WriteLine(
            $"[max_result_window] ErrorType={beyondWindow.ErrorType}; Reason={beyondWindow.ErrorReason}"
        );

        // CountAsync：依條件與無條件計數
        DocumentCountResult countA = await store.CountAsync(categoryA).ConfigureAwait(false);
        DocumentCountResult countAll = await store
            .CountAsync(new ElasticsearchQueryFilterBuilder<ScenarioDocument>())
            .ConfigureAwait(false);

        using (Assert.EnterMultipleScope()) {
            Assert.That(countA.Count, Is.EqualTo(7));
            Assert.That(countAll.Count, Is.EqualTo(12));
        }

        // UpdateByQueryAsync：預設 refresh 並等待完成，結果立即可見
        ByQueryOperationResult updateResult = await store.UpdateByQueryAsync(new UpdateByQueryRequest<ScenarioDocument> {
            Filters = categoryA,
            Script = new Script { Source = "ctx._source.counter = 100" }
        }).ConfigureAwait(false);
        ElasticsearchQueryFilterBuilder<ScenarioDocument> counterIs100 = new();
        counterIs100.Term(document => document.Counter, 100);
        DocumentCountResult countUpdated = await store.CountAsync(counterIs100).ConfigureAwait(false);

        using (Assert.EnterMultipleScope()) {
            Assert.That(updateResult.IsSuccessful, Is.True);
            Assert.That(updateResult.Operation, Is.EqualTo("update_by_query"));
            Assert.That(updateResult.Updated, Is.EqualTo(7));
            Assert.That(countUpdated.Count, Is.EqualTo(7));
        }

        // B2：預設 conflicts=abort 與 Conflicts.Proceed 的實際回應值，只記錄，不斷言 VersionConflicts 大於 0
        ByQueryOperationResult abortModeResult = await store.UpdateByQueryAsync(new UpdateByQueryRequest<ScenarioDocument> {
            Filters = categoryA
        }).ConfigureAwait(false);
        ByQueryOperationResult proceedModeResult = await store.UpdateByQueryAsync(new UpdateByQueryRequest<ScenarioDocument> {
            Filters = categoryA,
            ConfigureRequest = descriptor => descriptor.Conflicts(Conflicts.Proceed)
        }).ConfigureAwait(false);

        TestContext.Out.WriteLine(
            $"[B2 abort] IsSuccessful={abortModeResult.IsSuccessful}; Updated={abortModeResult.Updated}; "
            + $"VersionConflicts={abortModeResult.VersionConflicts}; StatusCode={abortModeResult.StatusCode}; "
            + $"ErrorType={abortModeResult.ErrorType}"
        );
        TestContext.Out.WriteLine(
            $"[B2 proceed] IsSuccessful={proceedModeResult.IsSuccessful}; Updated={proceedModeResult.Updated}; "
            + $"VersionConflicts={proceedModeResult.VersionConflicts}; StatusCode={proceedModeResult.StatusCode}; "
            + $"ErrorType={proceedModeResult.ErrorType}"
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(abortModeResult.IsSuccessful, Is.True);
            Assert.That(abortModeResult.Updated, Is.EqualTo(7));
            Assert.That(proceedModeResult.IsSuccessful, Is.True);
            Assert.That(proceedModeResult.Updated, Is.EqualTo(7));
        }

        // 無條件的 ByQuery 在送出前被拒絕；以 ConfigureRequest 明確指定 match_all 才可執行
        Assert.ThrowsAsync<ArgumentException>(
            async () => await store.DeleteByQueryAsync(new DeleteByQueryRequest<ScenarioDocument>())
        );
        Assert.ThrowsAsync<ArgumentException>(
            async () => await store.UpdateByQueryAsync(new UpdateByQueryRequest<ScenarioDocument> {
                Filters = new ElasticsearchQueryFilterBuilder<ScenarioDocument>()
            })
        );

        DocumentCountResult countAfterRejected = await store
            .CountAsync(new ElasticsearchQueryFilterBuilder<ScenarioDocument>())
            .ConfigureAwait(false);
        ByQueryOperationResult matchAllResult = await store.UpdateByQueryAsync(new UpdateByQueryRequest<ScenarioDocument> {
            ConfigureRequest = descriptor => descriptor.Query(query => query.MatchAll())
        }).ConfigureAwait(false);

        using (Assert.EnterMultipleScope()) {
            Assert.That(countAfterRejected.Count, Is.EqualTo(12));
            Assert.That(matchAllResult.IsSuccessful, Is.True);
            Assert.That(matchAllResult.Updated, Is.EqualTo(12));
        }

        // DeleteByQueryAsync：Deleted 與之後的 CountAsync 一致
        ElasticsearchQueryFilterBuilder<ScenarioDocument> categoryB = new();
        categoryB.Term(document => document.Category, "b");
        ByQueryOperationResult deleteResult = await store.DeleteByQueryAsync(new DeleteByQueryRequest<ScenarioDocument> {
            Filters = categoryB
        }).ConfigureAwait(false);
        DocumentCountResult countAfterDelete = await store
            .CountAsync(new ElasticsearchQueryFilterBuilder<ScenarioDocument>())
            .ConfigureAwait(false);

        using (Assert.EnterMultipleScope()) {
            Assert.That(deleteResult.IsSuccessful, Is.True);
            Assert.That(deleteResult.Operation, Is.EqualTo("delete_by_query"));
            Assert.That(deleteResult.Deleted, Is.EqualTo(5));
            Assert.That(countAfterDelete.Count, Is.EqualTo(7));
        }
    }

    private static Task<DocumentPageResult<ScenarioDocument>> SearchPageAsync(
        ElasticsearchDocumentStore<ScenarioDocument> store,
        int pageNumber,
        int pageSize
    ) {
        return store.SearchPageAsync(new DocumentPageRequest<ScenarioDocument> {
            PageNumber = pageNumber,
            PageSize = pageSize,
            ConfigureSearch = SortBySequence
        });
    }

    private static void SortBySequence(SearchRequestDescriptor<ScenarioDocument> descriptor) {
        descriptor.Sort(sort => sort.Field(document => document.Sequence, SortOrder.Asc));
    }

    private static Task<IntegrationStoreContext<ScenarioDocument>> CreateScenarioContextAsync(
        ElasticsearchClient client
    ) {
        return IntegrationStoreContext<ScenarioDocument>.CreateAsync(
            client,
            mapping => mapping.Properties(properties => properties
                .Keyword(field => field.Id)
                .Text(field => field.Name)
                .Keyword(field => field.Category)
                .IntegerNumber(field => field.Counter)
                .IntegerNumber(field => field.Sequence)
            ),
            document => document.Id
        );
    }

    private static ScenarioDocument CreateScenarioDocument(int number, string name, string category = "a") {
        return new ScenarioDocument {
            Id = $"s-{number:00}",
            Name = name,
            Category = category,
            Counter = 0,
            Sequence = number
        };
    }

    private static string CreateIndexPrefix() {
        return $"estk-tests-{Guid.NewGuid():N}";
    }

    private static ElasticsearchDocumentStore<IntegrationDocument> CreateStore(
        ElasticsearchClient client,
        string indexName
    ) {
        ElasticsearchDocumentsOptions options = new();
        options.ConfigureDocument<IntegrationDocument>(model => model.ToIndex(indexName));
        DefaultElasticsearchDocumentModelResolver modelResolver = new(options);

        return new ElasticsearchDocumentStore<IntegrationDocument>(client, modelResolver);
    }

    private static SearchAfterRequest<IntegrationDocument> CreatePageRequest(
        IReadOnlyList<FieldValue>? searchAfter = null
    ) {
        return new SearchAfterRequest<IntegrationDocument> {
            Size = 1,
            SearchAfter = searchAfter ?? [],
            ConfigureSearch = descriptor => descriptor
                .QueryFilters(filters => filters.Exists(document => document.Id))
                .SourceIncludes(
                    document => document.Id,
                    document => document.Name,
                    document => document.SortKey
                )
                .Sort(
                    sort => sort.Field(document => document.SortKey, SortOrder.Asc),
                    sort => sort.Field(document => document.Id, SortOrder.Asc)
                )
        };
    }

    private static async Task DeleteIndexIfExistsAsync(ElasticsearchClient client, string indexName) {
        Elastic.Clients.Elasticsearch.IndexManagement.ExistsResponse existsResponse = await client
            .Indices
            .ExistsAsync((Indices)indexName)
            .ConfigureAwait(false);

        if (existsResponse.Exists) {
            await client.Indices.DeleteAsync((Indices)indexName).ConfigureAwait(false);
        }
    }

    [ElasticsearchIndexName("scenario-documents")]
    internal sealed class ScenarioDocument {
        public required string Id { get; init; }

        public required string Name { get; init; }

        public required string Category { get; init; }

        public int Counter { get; init; }

        public int Sequence { get; init; }
    }

    [ElasticsearchIndexName("integration-documents")]
    private sealed class IntegrationDocument {
        public required string Id { get; init; }

        public required string Name { get; init; }

        public required object SortKey { get; init; }
    }
}
