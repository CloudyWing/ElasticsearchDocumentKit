using System.Text;
using System.Text.Json;
using CloudyWing.ElasticsearchDocumentKit.Querying;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

namespace CloudyWing.ElasticsearchDocumentKit.Tests.Querying;

[TestFixture]
internal sealed class ElasticsearchSearchRequestDescriptorExtensionsTests {
    [Test]
    public void QueryFilters_WithoutConfiguredFilters_ReturnsSameDescriptor() {
        SearchRequestDescriptor<SampleDocument> descriptor = new();

        SearchRequestDescriptor<SampleDocument> result = descriptor.QueryFilters(_ => { });

        Assert.That(result, Is.EqualTo(descriptor));
    }

    [Test]
    public void SourceIncludes_WithoutFields_ReturnsSameDescriptor() {
        SearchRequestDescriptor<SampleDocument> descriptor = new();

        SearchRequestDescriptor<SampleDocument> result =
            ElasticsearchSearchRequestDescriptorExtensions.SourceIncludes(descriptor);

        Assert.That(result, Is.EqualTo(descriptor));
    }

    [Test]
    public async Task SourceIncludes_WithFields_SerializesRequestedFieldsAsync() {
        ApiCallDetails apiCallDetails = await CaptureSearchApiCallDetailsAsync(
            descriptor => ElasticsearchSearchRequestDescriptorExtensions.SourceIncludes(
                descriptor,
                document => document.Id
            )
        );

        Assert.That(
            apiCallDetails.Uri?.Query,
            Does.Contain("_source_includes=id"),
            apiCallDetails.Uri?.OriginalString
        );
    }

    [Test]
    public void SourceExcludes_WithoutFields_ReturnsSameDescriptor() {
        SearchRequestDescriptor<SampleDocument> descriptor = new();

        SearchRequestDescriptor<SampleDocument> result =
            ElasticsearchSearchRequestDescriptorExtensions.SourceExcludes(descriptor);

        Assert.That(result, Is.EqualTo(descriptor));
    }

    [Test]
    public async Task SourceExcludes_WithFields_SerializesRequestedFieldsAsync() {
        ApiCallDetails apiCallDetails = await CaptureSearchApiCallDetailsAsync(
            descriptor => ElasticsearchSearchRequestDescriptorExtensions.SourceExcludes(
                descriptor,
                document => document.Name
            )
        );

        Assert.That(
            apiCallDetails.Uri?.Query,
            Does.Contain("_source_excludes=name"),
            apiCallDetails.Uri?.OriginalString
        );
    }

    [Test]
    public async Task DisableSource_SerializesDisabledSourceAsync() {
        ApiCallDetails apiCallDetails = await CaptureSearchApiCallDetailsAsync(
            descriptor => descriptor.DisableSource()
        );
        byte[] requestBody = apiCallDetails.RequestBodyInBytes
            ?? throw new InvalidOperationException("Search request body was not captured.");
        using JsonDocument request = JsonDocument.Parse(requestBody);

        Assert.That(request.RootElement.GetProperty("_source").GetBoolean(), Is.False);
    }

    private static async Task<ApiCallDetails> CaptureSearchApiCallDetailsAsync(
        Func<SearchRequestDescriptor<SampleDocument>, SearchRequestDescriptor<SampleDocument>> configure
    ) {
        byte[] responseBody = Encoding.UTF8.GetBytes(
            """
            {
              "took": 0,
              "timed_out": false,
              "_shards": {
                "total": 0,
                "successful": 0,
                "skipped": 0,
                "failed": 0
              },
              "hits": {
                "total": {
                  "value": 0,
                  "relation": "eq"
                },
                "hits": []
              }
            }
            """
        );
        Dictionary<string, IEnumerable<string>> headers = new(StringComparer.OrdinalIgnoreCase) {
            ["X-Elastic-Product"] = ["Elasticsearch"]
        };
        InMemoryRequestInvoker requestInvoker = new(responseBody, 200, null, "application/json", headers);
        ElasticsearchClientSettings settings = new(requestInvoker);
        settings.DisableDirectStreaming();
        ElasticsearchClient client = new(settings);
        SearchResponse<SampleDocument> response = await client
            .SearchAsync<SampleDocument>(descriptor => configure(descriptor))
            .ConfigureAwait(false);

        return response.ApiCallDetails ?? throw new InvalidOperationException("Search API call details were not captured.");
    }
}
