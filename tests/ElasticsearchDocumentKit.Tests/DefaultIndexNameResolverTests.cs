using CloudyWing.ElasticsearchDocumentKit;

namespace CloudyWing.ElasticsearchDocumentKit.Tests;

[TestFixture]
internal sealed class DefaultIndexNameResolverTests {
    [Test]
    public void Resolve_WithDocumentType_ShouldUseKebabCaseAndPrefix() {
        DefaultIndexNameResolver sut = new(CreateOptions("Sales_Prod"));

        string result = sut.Resolve<CustomerOrderDocument>();

        Assert.That(result, Is.EqualTo("sales_prod-customer-order-document"));
    }

    [Test]
    public void Resolve_WithAttribute_ShouldUseAttributeName() {
        DefaultIndexNameResolver sut = new(CreateOptions("dev"));

        string result = sut.Resolve<NamedDocument>();

        Assert.That(result, Is.EqualTo("dev-business-orders"));
    }

    [Test]
    public void Resolve_WithConfiguredModel_ShouldUseConfiguredIndexName() {
        ElasticsearchDocumentsOptions options = CreateOptions("dev");
        options.ConfigureDocument<CustomerOrderDocument>(document => document.ToIndex("configured-orders"));
        DefaultIndexNameResolver sut = new(options);

        string result = sut.Resolve<CustomerOrderDocument>();

        Assert.That(result, Is.EqualTo("dev-configured-orders"));
    }

    [Test]
    public void Resolve_WithAcronymTypeName_ShouldKeepAcronymAsSingleWord() {
        DefaultIndexNameResolver sut = new(CreateOptions(null));

        string result = sut.Resolve<HTTPApiDocument>();

        Assert.That(result, Is.EqualTo("http-api-document"));
    }

    [Test]
    public void Resolve_WithSeparatorOnlyPrefix_ShouldIgnorePrefix() {
        DefaultIndexNameResolver sut = new(CreateOptions("---"));

        string result = sut.Resolve<CustomerOrderDocument>();

        Assert.That(result, Is.EqualTo("customer-order-document"));
    }

    [Test]
    public void Resolve_WithInvalidPrefix_ShouldThrowArgumentException() {
        DefaultIndexNameResolver sut = new(CreateOptions("bad prefix"));

        Assert.That(
            () => sut.Resolve<CustomerOrderDocument>(),
            Throws.TypeOf<ArgumentException>()
                .With.Property(nameof(ArgumentException.ParamName)).EqualTo("documentType")
        );
    }

    [Test]
    public void Resolve_WithInvalidAttributeName_ShouldThrowArgumentException() {
        DefaultIndexNameResolver sut = new(CreateOptions(null));

        Assert.That(
            () => sut.Resolve<InvalidNamedDocument>(),
            Throws.TypeOf<ArgumentException>()
                .With.Property(nameof(ArgumentException.ParamName)).EqualTo("name")
        );
    }

    private static ElasticsearchDocumentsOptions CreateOptions(string? prefix) {
        return new ElasticsearchDocumentsOptions {
            IndexPrefix = prefix
        };
    }

    private sealed class CustomerOrderDocument {
    }

    private sealed class HTTPApiDocument {
    }

    [ElasticsearchIndexName("business-orders")]
    private sealed class NamedDocument {
    }

    [ElasticsearchIndexName("Business Orders")]
    private sealed class InvalidNamedDocument {
    }
}
