using CloudyWing.ElasticsearchDocumentKit;

namespace CloudyWing.ElasticsearchDocumentKit.Tests;

[TestFixture]
internal sealed class ElasticsearchDocumentsOptionsTests {
    [Test]
    public void Validate_WithNullPrefix_ShouldNotThrowException() {
        ElasticsearchDocumentsOptions options = new();

        Assert.That(() => options.Validate(), Throws.Nothing);
    }

    [Test]
    public void Validate_WithWhitespacePrefix_ShouldThrowInvalidOperationException() {
        ElasticsearchDocumentsOptions options = new() {
            IndexPrefix = " "
        };

        InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.That(exception!.Message, Is.EqualTo("Elasticsearch index prefix must be null or non-whitespace."));
    }
}
