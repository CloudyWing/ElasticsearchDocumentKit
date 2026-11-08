using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Bulk;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Transport.Products.Elasticsearch;
using CloudyWing.ElasticsearchDocumentKit.Indexing;
using CloudyWing.ElasticsearchDocumentKit.Search;
using IndexExistsResponse = Elastic.Clients.Elasticsearch.IndexManagement.ExistsResponse;

namespace CloudyWing.ElasticsearchDocumentKit.Internal;

internal static class ElasticsearchResultMapper {
    internal static DocumentOperationResult FromIndexResponse(IndexResponse response, string operation = "index") {
        ArgumentNullException.ThrowIfNull(response);

        ErrorDetails error = GetServerError(response.ElasticsearchServerError);

        return new DocumentOperationResult {
            IsSuccessful = response.IsValidResponse,
            Operation = operation,
            IndexName = response.Index,
            DocumentId = response.Id,
            Version = response.Version,
            SeqNo = response.SeqNo,
            PrimaryTerm = response.PrimaryTerm,
            StatusCode = response.ApiCallDetails?.HttpStatusCode,
            ErrorType = error.Type,
            ErrorReason = error.Reason,
            ResponseSummary = response.DebugInformation
        };
    }

    internal static DocumentOperationResult FromDeleteResponse(DeleteResponse response) {
        ArgumentNullException.ThrowIfNull(response);

        ErrorDetails error = GetServerError(response.ElasticsearchServerError);

        return new DocumentOperationResult {
            IsSuccessful = response.IsValidResponse,
            Operation = "delete",
            IndexName = response.Index,
            DocumentId = response.Id,
            Version = response.Version,
            SeqNo = response.SeqNo,
            PrimaryTerm = response.PrimaryTerm,
            StatusCode = response.ApiCallDetails?.HttpStatusCode,
            ErrorType = error.Type,
            ErrorReason = error.Reason,
            ResponseSummary = response.DebugInformation
        };
    }

    internal static DocumentGetResult<TDocument> FromGetResponse<TDocument>(GetResponse<TDocument> response)
        where TDocument : class {
        ArgumentNullException.ThrowIfNull(response);

        ErrorDetails error = GetServerError(response.ElasticsearchServerError);

        return new DocumentGetResult<TDocument> {
            IsSuccessful = response.IsValidResponse,
            Found = response.Found,
            IndexName = response.Index,
            DocumentId = response.Id,
            Version = response.Version,
            SeqNo = response.SeqNo,
            PrimaryTerm = response.PrimaryTerm,
            Document = response.Source,
            StatusCode = response.ApiCallDetails?.HttpStatusCode,
            ErrorType = error.Type,
            ErrorReason = error.Reason,
            ResponseSummary = response.DebugInformation
        };
    }

    internal static DocumentOperationResult FromUpdateResponse<TDocument>(
        UpdateResponse<TDocument> response,
        string operation = "update"
    )
        where TDocument : class {
        ArgumentNullException.ThrowIfNull(response);

        ErrorDetails error = GetServerError(response.ElasticsearchServerError);

        return new DocumentOperationResult {
            IsSuccessful = response.IsValidResponse,
            Operation = operation,
            IndexName = response.Index,
            DocumentId = response.Id,
            Version = response.Version,
            SeqNo = response.SeqNo,
            PrimaryTerm = response.PrimaryTerm,
            StatusCode = response.ApiCallDetails?.HttpStatusCode,
            ErrorType = error.Type,
            ErrorReason = error.Reason,
            ResponseSummary = response.DebugInformation
        };
    }

    internal static ByQueryOperationResult FromUpdateByQueryResponse(string indexName, UpdateByQueryResponse response) {
        ArgumentNullException.ThrowIfNull(response);

        ErrorDetails error = GetByQueryError(response.ElasticsearchServerError, response.Failures);

        return new ByQueryOperationResult {
            IsSuccessful = response.IsValidResponse && !HasFailures(response.Failures),
            Operation = "update_by_query",
            IndexName = indexName,
            Updated = response.Updated ?? 0,
            VersionConflicts = response.VersionConflicts ?? 0,
            StatusCode = response.ApiCallDetails?.HttpStatusCode,
            ErrorType = error.Type,
            ErrorReason = error.Reason,
            ResponseSummary = response.DebugInformation
        };
    }

    internal static ByQueryOperationResult FromDeleteByQueryResponse(string indexName, DeleteByQueryResponse response) {
        ArgumentNullException.ThrowIfNull(response);

        ErrorDetails error = GetByQueryError(response.ElasticsearchServerError, response.Failures);

        return new ByQueryOperationResult {
            IsSuccessful = response.IsValidResponse && !HasFailures(response.Failures),
            Operation = "delete_by_query",
            IndexName = indexName,
            Deleted = response.Deleted ?? 0,
            VersionConflicts = response.VersionConflicts ?? 0,
            StatusCode = response.ApiCallDetails?.HttpStatusCode,
            ErrorType = error.Type,
            ErrorReason = error.Reason,
            ResponseSummary = response.DebugInformation
        };
    }

    internal static DocumentCountResult FromCountResponse(string indexName, CountResponse response) {
        ArgumentNullException.ThrowIfNull(response);

        ErrorDetails error = GetServerError(response.ElasticsearchServerError);

        return new DocumentCountResult {
            IsSuccessful = response.IsValidResponse,
            IndexName = indexName,
            Count = response.IsValidResponse ? response.Count : 0,
            StatusCode = response.ApiCallDetails?.HttpStatusCode,
            ErrorType = error.Type,
            ErrorReason = error.Reason,
            ResponseSummary = response.DebugInformation
        };
    }

    internal static BulkOperationResult FromBulkResponse(BulkResponse response, int requestedCount) {
        ArgumentNullException.ThrowIfNull(response);

        int totalCount = response.Items.Count > 0 ? response.Items.Count : requestedCount;
        List<BulkOperationFailure> failures = response.Items
            .Select((item, index) => new BulkItemWithIndex(item, index))
            .Where(x => !x.Item.IsValid)
            .Select(x => FromBulkFailure(x.Item, x.Index))
            .ToList();
        ErrorDetails error = GetServerError(response.ElasticsearchServerError);

        if (!response.IsValidResponse && response.Items.Count == 0 && totalCount > 0) {
            HashSet<int> failedIndexes = failures.Select(failure => failure.ItemIndex).ToHashSet();
            List<BulkOperationFailure> requestFailures = Enumerable
                .Range(0, totalCount)
                .Where(index => !failedIndexes.Contains(index))
                .Select(index => FromRequestFailure(index, response, error))
                .ToList();
            failures.AddRange(requestFailures);
        }

        return new BulkOperationResult {
            IsSuccessful = response.IsValidResponse && failures.Count == 0,
            HasErrors = !response.IsValidResponse || response.Errors || failures.Count > 0,
            TotalCount = totalCount,
            SucceededCount = Math.Max(0, totalCount - failures.Count),
            FailedCount = failures.Count,
            Failures = failures,
            ErrorType = error.Type,
            ErrorReason = error.Reason,
            ResponseSummary = response.DebugInformation
        };
    }

    internal static IndexInitializationResult FromExistingIndex(string indexName, IndexExistsResponse response) {
        ArgumentNullException.ThrowIfNull(response);

        ErrorDetails error = GetServerError(response.ElasticsearchServerError);

        return new IndexInitializationResult {
            IsSuccessful = response.IsValidResponse,
            IndexName = indexName,
            Created = false,
            AlreadyExists = response.Exists,
            ErrorType = error.Type,
            ErrorReason = error.Reason,
            ResponseSummary = response.DebugInformation
        };
    }

    internal static IndexInitializationResult FromCreateIndexResponse(string indexName, CreateIndexResponse response) {
        ArgumentNullException.ThrowIfNull(response);

        ErrorDetails error = GetServerError(response.ElasticsearchServerError);

        return new IndexInitializationResult {
            IsSuccessful = response.IsValidResponse && response.Acknowledged,
            IndexName = indexName,
            Created = response.IsValidResponse && response.Acknowledged,
            AlreadyExists = false,
            ErrorType = error.Type,
            ErrorReason = error.Reason,
            ResponseSummary = response.DebugInformation
        };
    }

    internal static IndexInitializationResult FromConcurrentIndexCreation(
        string indexName,
        CreateIndexResponse response
    ) {
        ArgumentNullException.ThrowIfNull(response);

        return new IndexInitializationResult {
            IsSuccessful = true,
            IndexName = indexName,
            Created = false,
            AlreadyExists = true,
            ResponseSummary = response.DebugInformation
        };
    }

    internal static SearchAfterPage<TDocument> FromSearchResponse<TDocument>(SearchResponse<TDocument> response)
        where TDocument : class {
        ArgumentNullException.ThrowIfNull(response);

        ErrorDetails error = GetServerError(response.ElasticsearchServerError);
        List<TDocument> documents = response.Documents.ToList();
        IReadOnlyList<FieldValue> nextSearchAfter = response.Hits.LastOrDefault()?.Sort?.ToList() ?? [];

        return new SearchAfterPage<TDocument> {
            IsSuccessful = response.IsValidResponse,
            Documents = documents,
            NextSearchAfter = nextSearchAfter,
            TotalCount = response.Total,
            ErrorType = error.Type,
            ErrorReason = error.Reason,
            ResponseSummary = response.DebugInformation
        };
    }

    internal static DocumentSearchResult<TDocument> FromDocumentSearchResponse<TDocument>(
        SearchResponse<TDocument> response
    )
        where TDocument : class {
        ArgumentNullException.ThrowIfNull(response);

        ErrorDetails error = GetServerError(response.ElasticsearchServerError);

        return new DocumentSearchResult<TDocument> {
            IsSuccessful = response.IsValidResponse,
            Documents = response.IsValidResponse ? response.Documents.ToList() : [],
            ErrorType = error.Type,
            ErrorReason = error.Reason,
            ResponseSummary = response.DebugInformation
        };
    }

    internal static DocumentPageResult<TDocument> FromDocumentPageResponse<TDocument>(
        SearchResponse<TDocument> response,
        DocumentPageRequest<TDocument> request
    )
        where TDocument : class {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(request);

        ErrorDetails error = GetServerError(response.ElasticsearchServerError);
        long? totalCount = response.IsValidResponse && request.TrackTotalHits && response.Total >= 0
            ? response.Total
            : null;

        return new DocumentPageResult<TDocument> {
            IsSuccessful = response.IsValidResponse,
            Documents = response.IsValidResponse ? response.Documents.ToList() : [],
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            ErrorType = error.Type,
            ErrorReason = error.Reason,
            ResponseSummary = response.DebugInformation
        };
    }

    private static BulkOperationFailure FromBulkFailure(ResponseItem item, int index) {
        return new BulkOperationFailure {
            ItemIndex = index,
            Operation = item.Operation,
            IndexName = item.Index,
            DocumentId = item.Id,
            StatusCode = item.Status,
            ErrorType = item.Error?.Type,
            ErrorReason = item.Error?.Reason
        };
    }

    private static BulkOperationFailure FromRequestFailure(
        int index,
        BulkResponse response,
        ErrorDetails error
    ) {
        return new BulkOperationFailure {
            ItemIndex = index,
            Operation = "index",
            StatusCode = response.ApiCallDetails?.HttpStatusCode ?? 0,
            ErrorType = error.Type,
            ErrorReason = error.Reason
        };
    }

    private static bool HasFailures(IReadOnlyCollection<BulkIndexByScrollFailure>? failures) {
        return failures is { Count: > 0 };
    }

    private static ErrorDetails GetByQueryError(
        ElasticsearchServerError? serverError,
        IReadOnlyCollection<BulkIndexByScrollFailure>? failures
    ) {
        ErrorDetails error = GetServerError(serverError);

        if (error.Type is not null || error.Reason is not null) {
            return error;
        }

        Elastic.Clients.Elasticsearch.ErrorCause? cause = failures?.FirstOrDefault()?.Cause;

        return new ErrorDetails(cause?.Type, cause?.Reason);
    }

    private static ErrorDetails GetServerError(ElasticsearchServerError? error) {
        return new ErrorDetails(error?.Error?.Type, error?.Error?.Reason);
    }

    private sealed class BulkItemWithIndex {
        public BulkItemWithIndex(ResponseItem item, int index) {
            Item = item;
            Index = index;
        }

        public ResponseItem Item { get; }

        public int Index { get; }
    }

    private sealed class ErrorDetails {
        public ErrorDetails(string? type, string? reason) {
            Type = type;
            Reason = reason;
        }

        public string? Type { get; }

        public string? Reason { get; }
    }
}
