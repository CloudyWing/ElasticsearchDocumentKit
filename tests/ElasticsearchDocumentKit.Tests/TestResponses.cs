using System.Reflection;
using System.Text;
using System.Text.Json;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Bulk;
using Elastic.Transport;
using ElasticsearchServerError = Elastic.Transport.Products.Elasticsearch.ElasticsearchServerError;

namespace CloudyWing.ElasticsearchDocumentKit.Tests;

internal static class TestResponses {
    internal static T Success<T>(T response, int statusCode = 200)
        where T : TransportResponse {
        return TestableResponseFactory.CreateSuccessfulResponse(response, statusCode);
    }

    internal static T Failure<T>(
        T response,
        int statusCode = 500,
        string? errorType = null,
        string? errorReason = null
    )
        where T : TransportResponse {
        T result = TestableResponseFactory.CreateResponse(response, statusCode, false);

        if (errorType is null && errorReason is null) {
            return result;
        }

        string errorJson = $$"""
            {
              "error": {
                "type": {{JsonSerializer.Serialize(errorType)}},
                "reason": {{JsonSerializer.Serialize(errorReason)}}
              },
              "status": {{statusCode}}
            }
            """;
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(errorJson));
        ElasticsearchServerError serverError = ElasticsearchServerError.Create(stream);
        PropertyInfo? productErrorProperty = typeof(ApiCallDetails).GetProperty(
            nameof(ApiCallDetails.ProductError),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );
        MethodInfo? productErrorSetter = productErrorProperty?.GetSetMethod(nonPublic: true);
        if (productErrorSetter is null) {
            throw new InvalidOperationException("ApiCallDetails does not expose a product error setter.");
        }

        productErrorSetter.Invoke(result.ApiCallDetails, [serverError]);
        return result;
    }

    internal static ResponseItem BulkIndexItem(
        string documentId,
        string indexName,
        int statusCode,
        ErrorCause? error = null
    ) {
        Type sentinelType = typeof(BulkIndexResponseItem).Assembly.GetType(
            "Elastic.Clients.Elasticsearch.Serialization.JsonConstructorSentinel",
            throwOnError: true
        ) ?? throw new InvalidOperationException("JsonConstructorSentinel type was not found.");
        ConstructorInfo constructor = typeof(BulkIndexResponseItem).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [sentinelType],
            modifiers: null
        ) ?? throw new InvalidOperationException("BulkIndexResponseItem constructor was not found.");

        BulkIndexResponseItem item = (BulkIndexResponseItem)constructor.Invoke([null]);
        item.Id = documentId;
        item.Index = indexName;
        item.Status = statusCode;
        item.Error = error;

        return item;
    }
}
