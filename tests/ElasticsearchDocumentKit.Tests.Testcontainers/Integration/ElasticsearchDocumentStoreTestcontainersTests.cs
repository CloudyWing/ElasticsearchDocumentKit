using CloudyWing.ElasticsearchDocumentKit.Tests.Testcontainers;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Testcontainers.Elasticsearch;

namespace CloudyWing.ElasticsearchDocumentKit.Tests.Testcontainers.Integration;

[TestFixture]
[Category(IntegrationTestCategories.Integration)]
[Explicit("Requires Docker to start provider fixture containers.")]
[NonParallelizable]
internal sealed class ElasticsearchDocumentStoreTestcontainersTests {
    internal const string TestUriEnvironmentVariable = "ELASTICSEARCHDOCUMENTKIT_TEST_URI";

    private const string TestUsernameEnvironmentVariable = "ELASTICSEARCHDOCUMENTKIT_TEST_USERNAME";
    private const string TestPasswordEnvironmentVariable = "ELASTICSEARCHDOCUMENTKIT_TEST_PASSWORD";

    // 需與 Elastic.Clients.Elasticsearch 套件版本一起升版。
    private const string ElasticsearchImage = "elasticsearch:9.2.0";

    private ElasticsearchContainer? container;
    private ElasticsearchClient? client;

    [OneTimeSetUp]
    public async Task SetUpAsync() {
        string? externalUri = Environment.GetEnvironmentVariable(TestUriEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(externalUri)) {
            client = CreateClient(
                new Uri(externalUri),
                Environment.GetEnvironmentVariable(TestUsernameEnvironmentVariable),
                Environment.GetEnvironmentVariable(TestPasswordEnvironmentVariable)
            );
            return;
        }

        container = new ElasticsearchBuilder(ElasticsearchImage).Build();

        await container.StartAsync().ConfigureAwait(false);

        client = CreateClient(container);
    }

    [OneTimeTearDown]
    public async Task TearDownAsync() {
        if (container is not null) {
            await container.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Test]
    public async Task DocumentStore_WithContainerElasticsearch_ShouldInitializeWriteReportBulkFailuresAndPageAsync() {
        await ElasticsearchDocumentStoreIntegrationScenario.RunAsync(client!).ConfigureAwait(false);
    }

    [Test]
    public async Task DocumentStore_WithContainerElasticsearch_ShouldQueryGeoShapesByPolygonRelationAsync() {
        await ElasticsearchDocumentStoreGeoIntegrationScenario.RunAsync(client!).ConfigureAwait(false);
    }

    private static ElasticsearchClient CreateClient(ElasticsearchContainer container) {
        return CreateClient(new Uri(container.GetConnectionString()));
    }

    private static ElasticsearchClient CreateClient(
        Uri uri,
        string? username = null,
        string? password = null
    ) {
        ElasticsearchClientSettings settings = new(uri);
        settings.ServerCertificateValidationCallback(CertificateValidations.AllowAll);

        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password)) {
            settings.Authentication(new BasicAuthentication(username, password));
        }

        return new ElasticsearchClient(settings);
    }
}
