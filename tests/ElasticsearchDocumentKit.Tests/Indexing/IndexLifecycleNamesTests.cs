using CloudyWing.ElasticsearchDocumentKit.Indexing;

namespace CloudyWing.ElasticsearchDocumentKit.Tests.Indexing;

[TestFixture]
internal sealed class IndexLifecycleNamesTests {
    [Test]
    public void WithDateSuffix_WithDate_ShouldAppendFormattedDate() {
        DateTimeOffset date = new(2026, 5, 10, 0, 0, 0, TimeSpan.Zero);

        string result = IndexLifecycleNames.WithDateSuffix("Orders", date);

        Assert.That(result, Is.EqualTo("orders-20260510"));
    }

    [Test]
    public void WithVersionSuffix_WithVersion_ShouldAppendNormalizedVersion() {
        string result = IndexLifecycleNames.WithVersionSuffix("Orders", "V2");

        Assert.That(result, Is.EqualTo("orders-v2"));
    }

    [Test]
    public void WithDateSuffix_WithInvalidBaseName_ShouldThrowArgumentException() {
        DateTimeOffset date = new(2026, 5, 10, 0, 0, 0, TimeSpan.Zero);

        Assert.That(
            () => IndexLifecycleNames.WithDateSuffix("bad name", date),
            Throws.TypeOf<ArgumentException>()
                .With.Property(nameof(ArgumentException.ParamName)).EqualTo("baseName")
        );
    }

    [Test]
    public void WithVersionSuffix_WithInvalidVersion_ShouldThrowArgumentException() {
        Assert.That(
            () => IndexLifecycleNames.WithVersionSuffix("orders", "bad#version"),
            Throws.TypeOf<ArgumentException>()
                .With.Property(nameof(ArgumentException.ParamName)).EqualTo("version")
        );
    }
}
