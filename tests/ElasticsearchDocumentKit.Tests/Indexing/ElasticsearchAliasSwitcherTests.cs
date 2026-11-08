using System.Reflection;
using System.Text;
using System.Text.Json;
using CloudyWing.ElasticsearchDocumentKit.Indexing;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Transport;

namespace CloudyWing.ElasticsearchDocumentKit.Tests.Indexing;

[TestFixture]
internal sealed class ElasticsearchAliasSwitcherTests {
    [Test]
    public void Constructor_WithNullClient_ShouldThrowArgumentNullException() {
        ElasticsearchClient? client = null;

        ArgumentNullException? exception = Assert.Throws<ArgumentNullException>(
            () => _ = new ElasticsearchAliasSwitcher(client!)
        );

        Assert.That(exception!.ParamName, Is.EqualTo("client"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void SwitchAsync_WithInvalidAliasName_ShouldThrowArgumentException(string? aliasName) {
        ElasticsearchAliasSwitcher sut = new(new ElasticsearchClient());

        ArgumentException? exception = Assert.CatchAsync<ArgumentException>(
            async () => await sut.SwitchAsync(aliasName!, "orders-v2")
        );

        Assert.That(exception!.ParamName, Is.EqualTo("aliasName"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void SwitchAsync_WithInvalidNewIndexName_ShouldThrowArgumentException(string? newIndexName) {
        ElasticsearchAliasSwitcher sut = new(new ElasticsearchClient());

        ArgumentException? exception = Assert.CatchAsync<ArgumentException>(
            async () => await sut.SwitchAsync("orders-write", newIndexName!)
        );

        Assert.That(exception!.ParamName, Is.EqualTo("newIndexName"));
    }

    [Test]
    public async Task SwitchAsync_DuplicateAndBlankOldIndexes_SendsNormalizedActionsAndReturnsSuccessfulResultAsync() {
        byte[] responseBody = Encoding.UTF8.GetBytes("""{"acknowledged":true}""");
        Dictionary<string, IEnumerable<string>> headers = new(StringComparer.OrdinalIgnoreCase) {
            ["X-Elastic-Product"] = ["Elasticsearch"]
        };
        InMemoryRequestInvoker requestInvoker = new(responseBody, 200, null, "application/json", headers);
        ElasticsearchClientSettings settings = new(requestInvoker);
        byte[]? requestBody = null;
        settings.DisableDirectStreaming();
        settings.OnRequestCompleted(apiCallDetails => requestBody = apiCallDetails.RequestBodyInBytes);
        ElasticsearchAliasSwitcher sut = new(new ElasticsearchClient(settings));

        AliasSwitchResult result = await sut.SwitchAsync(
            "orders-write",
            "orders-v2",
            ["orders-v1", "", "orders-v1", " "]
        );

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.AliasName, Is.EqualTo("orders-write"));
            Assert.That(result.AddedIndexName, Is.EqualTo("orders-v2"));
            Assert.That(result.RemovedIndexNames, Is.EqualTo(new[] { "orders-v1" }));
        }

        byte[] capturedRequestBody = requestBody
            ?? throw new InvalidOperationException("Alias switch request body was not captured.");
        using JsonDocument request = JsonDocument.Parse(capturedRequestBody);
        JsonElement actions = request.RootElement.GetProperty("actions");
        JsonElement remove = actions
            .EnumerateArray()
            .Single(action => action.TryGetProperty("remove", out _))
            .GetProperty("remove");
        JsonElement add = actions
            .EnumerateArray()
            .Single(action => action.TryGetProperty("add", out _))
            .GetProperty("add");

        using (Assert.EnterMultipleScope()) {
            Assert.That(actions.GetArrayLength(), Is.EqualTo(2));
            Assert.That(remove.GetProperty("index").GetString(), Is.EqualTo("orders-v1"));
            Assert.That(remove.GetProperty("alias").GetString(), Is.EqualTo("orders-write"));
            Assert.That(remove.GetProperty("must_exist").GetBoolean(), Is.False);
            Assert.That(add.GetProperty("index").GetString(), Is.EqualTo("orders-v2"));
            Assert.That(add.GetProperty("alias").GetString(), Is.EqualTo("orders-write"));
            Assert.That(add.GetProperty("is_write_index").GetBoolean(), Is.True);
        }
    }

    [Test]
    public async Task SwitchAsync_WithoutOldIndexes_ReturnsSuccessfulResultWithNoRemovedIndexesAsync() {
        byte[] responseBody = Encoding.UTF8.GetBytes("""{"acknowledged":true}""");
        Dictionary<string, IEnumerable<string>> headers = new(StringComparer.OrdinalIgnoreCase) {
            ["X-Elastic-Product"] = ["Elasticsearch"]
        };
        InMemoryRequestInvoker requestInvoker = new(responseBody, 200, null, "application/json", headers);
        ElasticsearchClientSettings settings = new(requestInvoker);
        settings.DisableDirectStreaming();
        ElasticsearchAliasSwitcher sut = new(new ElasticsearchClient(settings));

        AliasSwitchResult result = await sut.SwitchAsync("orders-write", "orders-v2");

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.RemovedIndexNames, Is.Empty);
        }
    }

    [Test]
    public async Task SwitchAsync_WhenResponseFails_ReturnsMappedServerErrorAsync() {
        byte[] responseBody = Encoding.UTF8.GetBytes(
            """
            {
              "error": {
                "type": "alias_switch_exception",
                "reason": "alias update failed"
              },
              "status": 500
            }
            """
        );
        Dictionary<string, IEnumerable<string>> headers = new(StringComparer.OrdinalIgnoreCase) {
            ["X-Elastic-Product"] = ["Elasticsearch"]
        };
        InMemoryRequestInvoker requestInvoker = new(responseBody, 500, null, "application/json", headers);
        ElasticsearchClientSettings settings = new(requestInvoker);
        settings.DisableDirectStreaming();
        ElasticsearchAliasSwitcher sut = new(new ElasticsearchClient(settings));

        AliasSwitchResult result = await sut.SwitchAsync("orders-write", "orders-v2");

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.StatusCode, Is.EqualTo(500));
            Assert.That(result.ErrorType, Is.EqualTo("alias_switch_exception"));
            Assert.That(result.ErrorReason, Is.EqualTo("alias update failed"));
        }
    }

    [Test]
    public async Task SwitchAsync_WhenServerErrorHasNoReason_ReturnsNullErrorReasonAsync() {
        byte[] responseBody = Encoding.UTF8.GetBytes(
            """
            {
              "error": {
                "type": "alias_switch_exception"
              },
              "status": 500
            }
            """
        );
        Dictionary<string, IEnumerable<string>> headers = new(StringComparer.OrdinalIgnoreCase) {
            ["X-Elastic-Product"] = ["Elasticsearch"]
        };
        InMemoryRequestInvoker requestInvoker = new(responseBody, 500, null, "application/json", headers);
        ElasticsearchClientSettings settings = new(requestInvoker);
        settings.DisableDirectStreaming();
        ElasticsearchAliasSwitcher sut = new(new ElasticsearchClient(settings));

        AliasSwitchResult result = await sut.SwitchAsync("orders-write", "orders-v2");

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.ErrorType, Is.EqualTo("alias_switch_exception"));
            Assert.That(result.ErrorReason, Is.Null);
        }
    }

    [Test]
    public async Task SwitchAsync_WhenServerErrorHasNoType_ReturnsNullErrorTypeAsync() {
        byte[] responseBody = Encoding.UTF8.GetBytes(
            """
            {
              "error": {
                "reason": "alias update failed"
              },
              "status": 500
            }
            """
        );
        Dictionary<string, IEnumerable<string>> headers = new(StringComparer.OrdinalIgnoreCase) {
            ["X-Elastic-Product"] = ["Elasticsearch"]
        };
        InMemoryRequestInvoker requestInvoker = new(responseBody, 500, null, "application/json", headers);
        ElasticsearchClientSettings settings = new(requestInvoker);
        settings.DisableDirectStreaming();
        ElasticsearchAliasSwitcher sut = new(new ElasticsearchClient(settings));

        AliasSwitchResult result = await sut.SwitchAsync("orders-write", "orders-v2");

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.ErrorType, Is.Null);
            Assert.That(result.ErrorReason, Is.EqualTo("alias update failed"));
        }
    }

    [Test]
    public void ToResult_WithResponseWithoutApiCallDetails_ReturnsNullStatusCode() {
        MethodInfo method = typeof(ElasticsearchAliasSwitcher).GetMethod(
            "ToResult",
            BindingFlags.NonPublic | BindingFlags.Static
        ) ?? throw new InvalidOperationException("ToResult method was not found.");
        UpdateAliasesResponse response = new();

        AliasSwitchResult result = (AliasSwitchResult)method.Invoke(
            obj: null,
            parameters: ["orders-write", "orders-v2", Array.Empty<string>(), response]
        )!;

        using (Assert.EnterMultipleScope()) {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.StatusCode, Is.Null);
            Assert.That(result.ErrorType, Is.Null);
            Assert.That(result.ErrorReason, Is.Null);
        }
    }
}
