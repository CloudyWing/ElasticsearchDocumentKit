using CloudyWing.ElasticsearchDocumentKit;

namespace CloudyWing.ElasticsearchDocumentKit.Tests;

[TestFixture]
internal sealed class ElasticsearchIndexNameValidatorTests {
    [TestCase("orders")]
    [TestCase("orders_2026")]
    [TestCase("orders.v1")]
    public void IsValid_WithValidIndexName_ShouldReturnTrue(string indexName) {
        bool result = ElasticsearchIndexNameValidator.IsValid(indexName);

        Assert.That(result, Is.True);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("Orders")]
    [TestCase("orders test")]
    [TestCase("orders,test")]
    [TestCase("orders#test")]
    [TestCase("orders:test")]
    [TestCase("-orders")]
    [TestCase("_orders")]
    [TestCase("+orders")]
    [TestCase(".")]
    [TestCase("..")]
    public void IsValid_WithInvalidIndexName_ShouldReturnFalse(string? indexName) {
        bool result = ElasticsearchIndexNameValidator.IsValid(indexName);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsValid_WithNameLongerThanByteLimit_ShouldReturnFalse() {
        string indexName = new('a', ElasticsearchIndexNameValidator.MaxIndexNameByteLength + 1);

        bool result = ElasticsearchIndexNameValidator.IsValid(indexName);

        Assert.That(result, Is.False);
    }

    [Test]
    public void IsValid_WithNonAsciiNameAtByteLimit_ShouldReturnTrue() {
        string indexName = new('名', 85);

        bool result = ElasticsearchIndexNameValidator.IsValid(indexName);

        Assert.That(result, Is.True);
    }

    [Test]
    public void IsValid_WithNonAsciiNameExceedingByteLimit_ShouldReturnFalse() {
        string indexName = new('名', 86);

        bool result = ElasticsearchIndexNameValidator.IsValid(indexName);

        Assert.That(result, Is.False);
    }

    [Test]
    public void Validate_WithInvalidIndexName_ShouldThrowArgumentException() {
        Assert.That(
            () => ElasticsearchIndexNameValidator.Validate("Orders", "indexName"),
            Throws.TypeOf<ArgumentException>()
                .With.Property(nameof(ArgumentException.ParamName)).EqualTo("indexName")
        );
    }
}
