using CloudyWing.ElasticsearchDocumentKit;
using CloudyWing.ElasticsearchDocumentKit.Modeling;
using CloudyWing.ElasticsearchDocumentKit.Indexing;
using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.DependencyInjection;

namespace CloudyWing.ElasticsearchDocumentKit.Tests;

[TestFixture]
internal sealed class ElasticsearchDocumentsServiceCollectionExtensionsTests {
    [Test]
    public void AddElasticsearchDocuments_WithNullServices_ShouldThrowArgumentNullException() {
        IServiceCollection? services = null;

        ArgumentNullException? exception = Assert.Throws<ArgumentNullException>(
            () => services!.AddElasticsearchDocuments()
        );

        Assert.That(exception!.ParamName, Is.EqualTo("services"));
    }

    [Test]
    public void AddElasticsearchDocuments_WithConfiguredPrefix_ShouldRegisterResolvers() {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton(new ElasticsearchClient());
        services.AddElasticsearchDocuments(options => {
            options.IndexPrefix = "orders";
            options.ConfigureDocument<OrderDocument>(model => model
                .ToIndex("documents")
                .HasId(document => document.Id)
            );
        });

        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        IIndexNameResolver resolver = provider.GetRequiredService<IIndexNameResolver>();
        IElasticsearchDocumentModelResolver modelResolver = provider
            .GetRequiredService<IElasticsearchDocumentModelResolver>();
        IElasticsearchDocumentStore<OrderDocument> store = provider
            .GetRequiredService<IElasticsearchDocumentStore<OrderDocument>>();
        IIndexInitializer<OrderDocument> initializer = provider.GetRequiredService<IIndexInitializer<OrderDocument>>();
        IElasticsearchAliasSwitcher aliasSwitcher = provider.GetRequiredService<IElasticsearchAliasSwitcher>();
        string result = resolver.Resolve<OrderDocument>();
        ElasticsearchDocumentModel<OrderDocument> model = modelResolver.Resolve<OrderDocument>();

        using (Assert.EnterMultipleScope()) {
            Assert.That(result, Is.EqualTo("orders-documents"));
            Assert.That(model.IndexName, Is.EqualTo("orders-documents"));
            Assert.That(model.IdSelector?.Invoke(new OrderDocument { Id = "order-1" }), Is.EqualTo("order-1"));
            Assert.That(store, Is.TypeOf<ElasticsearchDocumentStore<OrderDocument>>());
            Assert.That(initializer, Is.TypeOf<ElasticsearchIndexInitializer<OrderDocument>>());
            Assert.That(aliasSwitcher, Is.TypeOf<ElasticsearchAliasSwitcher>());
        }
    }

    [Test]
    public void AddElasticsearchDocuments_WithInvalidPrefix_ShouldThrowInvalidOperationException() {
        IServiceCollection services = new ServiceCollection();

        InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(
            () => services.AddElasticsearchDocuments(options => options.IndexPrefix = " ")
        );

        Assert.That(exception!.Message, Is.EqualTo("Elasticsearch index prefix must be null or non-whitespace."));
    }

    [Test]
    public void AddElasticsearchDocuments_CalledTwice_ShouldReplaceOptions() {
        IServiceCollection services = new ServiceCollection();
        services.AddElasticsearchDocuments(options => options.IndexPrefix = "first");
        services.AddElasticsearchDocuments(options => options.IndexPrefix = "second");

        using ServiceProvider provider = services.BuildServiceProvider();

        ElasticsearchDocumentsOptions options = provider.GetRequiredService<ElasticsearchDocumentsOptions>();
        IIndexNameResolver resolver = provider.GetRequiredService<IIndexNameResolver>();

        using (Assert.EnterMultipleScope()) {
            Assert.That(options.IndexPrefix, Is.EqualTo("second"));
            Assert.That(resolver.Resolve<OrderDocument>(), Is.EqualTo("second-order-document"));
        }
    }

    private sealed class OrderDocument {
        public required string Id { get; init; }
    }
}
