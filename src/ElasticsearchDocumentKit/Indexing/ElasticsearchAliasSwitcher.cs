using System.Diagnostics;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;

namespace CloudyWing.ElasticsearchDocumentKit.Indexing;

/// <summary>
/// 使用官方 <see cref="ElasticsearchClient"/> 執行 alias 切換。
/// </summary>
public sealed class ElasticsearchAliasSwitcher : IElasticsearchAliasSwitcher {
    private readonly ElasticsearchClient client;

    /// <summary>
    /// 初始化 <see cref="ElasticsearchAliasSwitcher"/> 類別的新執行個體。
    /// </summary>
    /// <param name="client">官方 Elasticsearch client。</param>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> 為 <see langword="null"/>。</exception>
    public ElasticsearchAliasSwitcher(ElasticsearchClient client) {
        ArgumentNullException.ThrowIfNull(client);
        this.client = client;
    }

    /// <summary>
    /// 將 alias 切換到指定 index，並從指定舊 index 移除同一 alias。
    /// </summary>
    /// <param name="aliasName">alias 名稱。</param>
    /// <param name="newIndexName">新的 index 名稱。</param>
    /// <param name="oldIndexNames">要移除 alias 的舊 index 名稱集合。</param>
    /// <param name="cancellationToken">取消權杖。</param>
    /// <returns>alias 切換結果。</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="aliasName"/> 或 <paramref name="newIndexName"/> 為空白字串。
    /// </exception>
    public async Task<AliasSwitchResult> SwitchAsync(
        string aliasName,
        string newIndexName,
        IEnumerable<string>? oldIndexNames = null,
        CancellationToken cancellationToken = default
    ) {
        ArgumentException.ThrowIfNullOrWhiteSpace(aliasName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newIndexName);

        List<string> materializedOldIndexNames = oldIndexNames?
            .Where(indexName => !string.IsNullOrWhiteSpace(indexName))
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? [];
        List<IndexUpdateAliasesAction> actions = materializedOldIndexNames
            .Select(indexName => new IndexUpdateAliasesAction {
                Remove = new RemoveAction {
                    Index = (IndexName)indexName,
                    Alias = (IndexAlias)aliasName,
                    MustExist = false
                }
            })
            .ToList();
        actions.Add(new IndexUpdateAliasesAction {
            Add = new AddAction {
                Index = (IndexName)newIndexName,
                Alias = (IndexAlias)aliasName,
                IsWriteIndex = true
            }
        });

        using Activity? activity = ElasticsearchDocumentsTelemetry.ActivitySource.StartActivity(
            "elasticsearch.alias_switch",
            ActivityKind.Client
        );
        activity?.SetTag("db.system", "elasticsearch");
        activity?.SetTag("db.operation", "alias_switch");
        activity?.SetTag("elasticsearch.alias", aliasName);
        activity?.SetTag("elasticsearch.index", newIndexName);
        activity?.SetTag("elasticsearch.removed_index_count", materializedOldIndexNames.Count);

        UpdateAliasesResponse response = await client.Indices.UpdateAliasesAsync(
            new UpdateAliasesRequest {
                Actions = actions
            },
            cancellationToken
        ).ConfigureAwait(false);
        AliasSwitchResult result = ToResult(aliasName, newIndexName, materializedOldIndexNames, response);

        activity?.SetTag("elasticsearch.success", result.IsSuccessful);

        if (!string.IsNullOrWhiteSpace(result.ErrorType)) {
            activity?.SetTag("error.type", result.ErrorType);
        }

        return result;
    }

    private static AliasSwitchResult ToResult(
        string aliasName,
        string newIndexName,
        IReadOnlyList<string> oldIndexNames,
        UpdateAliasesResponse response
    ) {
        Elastic.Transport.Products.Elasticsearch.ElasticsearchServerError? error = response.ElasticsearchServerError;

        return new AliasSwitchResult {
            IsSuccessful = response.IsValidResponse,
            AliasName = aliasName,
            AddedIndexName = newIndexName,
            RemovedIndexNames = oldIndexNames,
            StatusCode = response.ApiCallDetails?.HttpStatusCode,
            ErrorType = error?.Error?.Type,
            ErrorReason = error?.Error?.Reason,
            ResponseSummary = response.DebugInformation
        };
    }
}
