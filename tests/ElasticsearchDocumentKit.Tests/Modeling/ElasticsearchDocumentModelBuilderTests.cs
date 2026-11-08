using CloudyWing.ElasticsearchDocumentKit.Modeling;
using Elastic.Clients.Elasticsearch.IndexManagement;

namespace CloudyWing.ElasticsearchDocumentKit.Tests.Modeling;

[TestFixture]
internal sealed class ElasticsearchDocumentModelBuilderTests {
    [Test]
    public void ConfigureDocuments_MultipleDocumentTypes_ResolvesConfiguredModels() {
        ElasticsearchDocumentsOptions options = new();
        options.ConfigureDocuments(builder => {
            builder.Document<SampleDocument>()
                .ToIndex("sample-documents")
                .HasId(document => document.Id);
            builder.Document<OtherDocument>().ToIndex("other-documents");
        });
        DefaultElasticsearchDocumentModelResolver sut = new(options);

        ElasticsearchDocumentModel<SampleDocument> sampleModel = sut.Resolve<SampleDocument>();
        ElasticsearchDocumentModel<OtherDocument> otherModel = sut.Resolve<OtherDocument>();

        using (Assert.EnterMultipleScope()) {
            Assert.That(sampleModel.IndexName, Is.EqualTo("sample-documents"));
            Assert.That(
                sampleModel.IdSelector?.Invoke(new SampleDocument { Id = "sample-1", Name = "Sample", SortKey = 1 }),
                Is.EqualTo("sample-1")
            );
            Assert.That(otherModel.IndexName, Is.EqualTo("other-documents"));
        }
    }

    [Test]
    public void ConfigureDocument_MultipleIndexActions_PreservesRegistrationOrder() {
        ElasticsearchDocumentsOptions options = new();
        List<string> invocationOrder = [];
        options.ConfigureDocument<SampleDocument>(builder => builder
            .ConfigureIndex(_ => invocationOrder.Add("index"))
            .ConfigureMapping(_ => invocationOrder.Add("mapping"))
        );
        DefaultElasticsearchDocumentModelResolver sut = new(options);
        ElasticsearchDocumentModel<SampleDocument> model = sut.Resolve<SampleDocument>();

        model.ConfigureIndex?.Invoke(new CreateIndexRequestDescriptor<SampleDocument>("sample-documents"));

        Assert.That(invocationOrder, Is.EqualTo(new[] { "index", "mapping" }));
    }

    [Test]
    public void ConfigureDocument_NullDelegates_ThrowsArgumentNullException() {
        ElasticsearchDocumentsOptions options = new();
        ElasticsearchDocumentModelBuilder<SampleDocument>? builder = null;
        options.ConfigureDocument<SampleDocument>(configuredBuilder => builder = configuredBuilder);

        using (Assert.EnterMultipleScope()) {
            Assert.That(
                () => options.ConfigureDocuments(null!),
                Throws.TypeOf<ArgumentNullException>()
                    .With.Property(nameof(ArgumentNullException.ParamName)).EqualTo("configure")
            );
            Assert.That(
                () => builder!.HasId(null!),
                Throws.TypeOf<ArgumentNullException>()
                    .With.Property(nameof(ArgumentNullException.ParamName)).EqualTo("idSelector")
            );
            Assert.That(
                () => builder!.ConfigureIndex(null!),
                Throws.TypeOf<ArgumentNullException>()
                    .With.Property(nameof(ArgumentNullException.ParamName)).EqualTo("configureIndex")
            );
            Assert.That(
                () => builder!.ConfigureMapping(null!),
                Throws.TypeOf<ArgumentNullException>()
                    .With.Property(nameof(ArgumentNullException.ParamName)).EqualTo("configureMapping")
            );
        }
    }

    private sealed class OtherDocument;
}
