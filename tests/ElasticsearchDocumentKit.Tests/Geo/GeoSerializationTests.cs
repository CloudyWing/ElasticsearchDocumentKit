using System.Text;
using System.Text.Json;
using CloudyWing.ElasticsearchDocumentKit.Geo;
using CloudyWing.ElasticsearchDocumentKit.Internal;
using CloudyWing.ElasticsearchDocumentKit.Tests.Internal;
using Elastic.Clients.Elasticsearch;

namespace CloudyWing.ElasticsearchDocumentKit.Tests.Geo;

[TestFixture]
internal sealed class GeoSerializationTests {
    private const string GetResponseBody = """
        {
          "_index": "geo-sample-documents",
          "_id": "order-1",
          "_version": 1,
          "_seq_no": 0,
          "_primary_term": 1,
          "found": true,
          "_source": {
            "id": "order-1",
            "location": { "type": "Point", "coordinates": [121.5, 25.05] }
          }
        }
        """;

    private const string SearchResponseBody = """
        {
          "took": 1,
          "timed_out": false,
          "_shards": { "total": 1, "successful": 1, "skipped": 0, "failed": 0 },
          "hits": {
            "total": { "value": 2, "relation": "eq" },
            "max_score": null,
            "hits": [
              {
                "_index": "geo-sample-documents",
                "_id": "order-1",
                "_score": null,
                "_source": {
                  "id": "order-1",
                  "location": { "type": "Point", "coordinates": [121.5, 25.05] }
                }
              },
              {
                "_index": "geo-sample-documents",
                "_id": "order-2",
                "_score": null,
                "_source": { "id": "order-2" }
              }
            ]
          }
        }
        """;

    [Test]
    public async Task IndexAsync_WithGeoPointProperty_SerializesLocationAsGeoJsonPointAsync() {
        ElasticsearchClient client = RecordingClientFactory.Create();
        GeoSampleDocument document = new() {
            Id = "order-1",
            Location = new GeoPoint(121.5, 25.05)
        };

        IndexResponse response = await client.IndexAsync(
            document,
            descriptor => descriptor.Index((IndexName)"geo-sample-documents")
        );

        using JsonDocument body = JsonDocument.Parse(GetRequestBody(response));
        JsonElement location = body.RootElement.GetProperty("location");

        using (Assert.EnterMultipleScope()) {
            Assert.That(location.GetProperty("type").GetString(), Is.EqualTo("Point"));
            Assert.That(location.GetProperty("coordinates").GetArrayLength(), Is.EqualTo(2));
            Assert.That(location.GetProperty("coordinates")[0].GetDouble(), Is.EqualTo(121.5));
            Assert.That(location.GetProperty("coordinates")[1].GetDouble(), Is.EqualTo(25.05));
            Assert.That(location.TryGetProperty("longitude", out _), Is.False);
            Assert.That(location.TryGetProperty("latitude", out _), Is.False);
        }
    }

    [Test]
    public async Task IndexAsync_ThroughGateway_SerializesLocationAsGeoJsonPointAsync() {
        ElasticsearchGateway gateway = new(RecordingClientFactory.Create());
        GeoSampleDocument document = new() {
            Id = "order-1",
            Location = new GeoPoint(121.5, 25.05)
        };

        IndexResponse response = await gateway.IndexAsync(
            "geo-sample-documents",
            document,
            "order-1",
            null,
            CancellationToken.None
        );

        using JsonDocument body = JsonDocument.Parse(GetRequestBody(response));

        Assert.That(
            body.RootElement.GetProperty("location").GetRawText(),
            Is.EqualTo("""{"type":"Point","coordinates":[121.5,25.05]}""")
        );
    }

    [Test]
    public async Task IndexAsync_WithNullLocation_OmitsLocationAsync() {
        ElasticsearchClient client = RecordingClientFactory.Create();
        GeoSampleDocument document = new() {
            Id = "order-1"
        };

        IndexResponse response = await client.IndexAsync(
            document,
            descriptor => descriptor.Index((IndexName)"geo-sample-documents")
        );

        using JsonDocument body = JsonDocument.Parse(GetRequestBody(response));

        Assert.That(body.RootElement.TryGetProperty("location", out _), Is.False);
    }

    [Test]
    public async Task GetAsync_WithGeoJsonPointSource_DeserializesLocationAsync() {
        ElasticsearchClient client = RecordingClientFactory.Create(GetResponseBody);

        GetResponse<GeoSampleDocument> response = await client.GetAsync<GeoSampleDocument>(
            (IndexName)"geo-sample-documents",
            (Id)"order-1"
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(response.IsValidResponse, Is.True);
            Assert.That(response.Source, Is.Not.Null);
            Assert.That(response.Source!.Location, Is.Not.Null);
            Assert.That(response.Source.Location!.Longitude, Is.EqualTo(121.5));
            Assert.That(response.Source.Location.Latitude, Is.EqualTo(25.05));
        }
    }

    [Test]
    public async Task SearchAsync_WithGeoJsonPointSources_DeserializesLocationsAsync() {
        ElasticsearchClient client = RecordingClientFactory.Create(SearchResponseBody);

        SearchResponse<GeoSampleDocument> response = await client.SearchAsync<GeoSampleDocument>(
            descriptor => descriptor.Indices("geo-sample-documents")
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(response.IsValidResponse, Is.True);
            Assert.That(response.Documents.Count, Is.EqualTo(2));
            Assert.That(response.Documents.ElementAt(0).Location, Is.EqualTo(new GeoPoint(121.5, 25.05)));
            Assert.That(response.Documents.ElementAt(1).Location, Is.Null);
        }
    }

    private static string GetRequestBody(IndexResponse response) {
        byte[]? bytes = response.ApiCallDetails.RequestBodyInBytes;

        return bytes is null ? string.Empty : Encoding.UTF8.GetString(bytes);
    }
}
