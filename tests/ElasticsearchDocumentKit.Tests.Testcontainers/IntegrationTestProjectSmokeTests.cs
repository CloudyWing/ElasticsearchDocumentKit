namespace CloudyWing.ElasticsearchDocumentKit.Tests.Testcontainers;

[TestFixture]
public sealed class IntegrationTestProjectSmokeTests {
    [Test]
    public void ElasticsearchDocumentStoreTestcontainersTests_WhenDiscovered_RequireExplicitSelection() {
        ExplicitAttribute? explicitAttribute = Attribute.GetCustomAttribute(
            typeof(Integration.ElasticsearchDocumentStoreTestcontainersTests),
            typeof(ExplicitAttribute)
        ) as ExplicitAttribute;
        CategoryAttribute? categoryAttribute = Attribute.GetCustomAttribute(
            typeof(Integration.ElasticsearchDocumentStoreTestcontainersTests),
            typeof(CategoryAttribute)
        ) as CategoryAttribute;

        using (Assert.EnterMultipleScope()) {
            Assert.That(explicitAttribute, Is.Not.Null);
            Assert.That(categoryAttribute?.Name, Is.EqualTo(IntegrationTestCategories.Integration));
        }
    }
}
