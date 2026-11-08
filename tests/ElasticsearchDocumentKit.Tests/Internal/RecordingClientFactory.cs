using System.Text;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

namespace CloudyWing.ElasticsearchDocumentKit.Tests.Internal;

internal static class RecordingClientFactory {
    internal const string IndexResponseBody = """
        {
          "_index": "sample-documents",
          "_id": "order-1",
          "_version": 2,
          "result": "updated",
          "_shards": { "total": 2, "successful": 1, "failed": 0 },
          "_seq_no": 8,
          "_primary_term": 2
        }
        """;

    /// <summary>
    /// 建立不連線的 client，所有請求以固定內容回應，並保留請求 URI 與 body 供斷言。
    /// </summary>
    /// <param name="responseBody">固定回應的 JSON 內容。</param>
    /// <param name="statusCode">固定回應的 HTTP 狀態碼。</param>
    /// <param name="onRequestCompleted">每次請求完成時的回呼，供確認請求是否送出。</param>
    /// <returns>使用 in-memory 傳輸層的 client。</returns>
    internal static ElasticsearchClient Create(
        string responseBody = IndexResponseBody,
        int statusCode = 200,
        Action<ApiCallDetails>? onRequestCompleted = null
    ) {
        InMemoryRequestInvoker invoker = new(
            Encoding.UTF8.GetBytes(responseBody),
            statusCode,
            null,
            "application/json",
            new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase) {
                ["X-Elastic-Product"] = ["Elasticsearch"]
            }
        );
        ElasticsearchClientSettings settings = new ElasticsearchClientSettings(invoker)
            .DisableDirectStreaming();

        if (onRequestCompleted is not null) {
            settings.OnRequestCompleted(onRequestCompleted);
        }

        return new ElasticsearchClient(settings);
    }

    /// <summary>
    /// 以 client 的 request serializer 將物件序列化為 JSON 字串。
    /// </summary>
    /// <typeparam name="T">物件型別。</typeparam>
    /// <param name="client">client。</param>
    /// <param name="value">要序列化的物件。</param>
    /// <returns>JSON 字串。</returns>
    internal static string Serialize<T>(ElasticsearchClient client, T value) {
        using MemoryStream stream = new();
        client.RequestResponseSerializer.Serialize(value, stream);

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
