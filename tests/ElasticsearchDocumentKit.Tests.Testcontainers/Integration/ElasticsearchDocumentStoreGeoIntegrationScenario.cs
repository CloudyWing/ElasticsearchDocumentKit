using CloudyWing.ElasticsearchDocumentKit.Geo;
using CloudyWing.ElasticsearchDocumentKit.Querying;
using CloudyWing.ElasticsearchDocumentKit.Search;
using Elastic.Clients.Elasticsearch;

namespace CloudyWing.ElasticsearchDocumentKit.Tests.Testcontainers.Integration;

internal static class ElasticsearchDocumentStoreGeoIntegrationScenario {
    public static async Task RunAsync(ElasticsearchClient client) {
        await using IntegrationStoreContext<GeoIntegrationDocument> context = await IntegrationStoreContext<GeoIntegrationDocument>
            .CreateAsync(
                client,
                mapping => mapping.Properties(properties => properties
                    .Keyword(field => field.Id)
                    .GeoShape(field => field.Location)
                ),
                document => document.Id
            )
            .ConfigureAwait(false);
        ElasticsearchDocumentStore<GeoIntegrationDocument> store = context.Store;

        // inside 位於多邊形內，outside 位於多邊形外
        DocumentOperationResult insideResult = await store.IndexAsync(
            new GeoIntegrationDocument { Id = "inside", Location = new GeoPoint(121.5, 25.5) },
            "inside",
            new IndexDocumentOptions { Refresh = Refresh.True }
        ).ConfigureAwait(false);
        DocumentOperationResult outsideResult = await store.IndexAsync(
            new GeoIntegrationDocument { Id = "outside", Location = new GeoPoint(130.0, 40.0) },
            "outside",
            new IndexDocumentOptions { Refresh = Refresh.True }
        ).ConfigureAwait(false);
        DocumentGetResult<GeoIntegrationDocument> insideDocument = await store.GetAsync("inside").ConfigureAwait(false);

        GeoPolygon polygon = new([
            new GeoPoint(121.0, 25.0),
            new GeoPoint(122.0, 25.0),
            new GeoPoint(122.0, 26.0),
            new GeoPoint(121.0, 26.0)
        ]);
        IEnumerable<(double Longitude, double Latitude)> tuplePolygon = [
            (121.0, 25.0),
            (122.0, 25.0),
            (122.0, 26.0),
            (121.0, 26.0)
        ];

        IReadOnlyList<string> within = await SearchIdsAsync(
            store,
            filters => filters.GeoPolygonWithRelation("location", polygon, GeoShapeRelation.Within)
        ).ConfigureAwait(false);
        IReadOnlyList<string> intersects = await SearchIdsAsync(
            store,
            filters => filters.GeoPolygonWithRelation("location", polygon, GeoShapeRelation.Intersects)
        ).ConfigureAwait(false);
        IReadOnlyList<string> disjoint = await SearchIdsAsync(
            store,
            filters => filters.GeoPolygonWithRelation("location", polygon, GeoShapeRelation.Disjoint)
        ).ConfigureAwait(false);
        IReadOnlyList<string> contains = await SearchIdsAsync(
            store,
            filters => filters.GeoPolygonWithRelation("location", polygon, GeoShapeRelation.Contains)
        ).ConfigureAwait(false);
        IReadOnlyList<string> withinByExpression = await SearchIdsAsync(
            store,
            filters => filters.GeoPolygonWithRelation(
                document => document.Location,
                polygon,
                GeoShapeRelation.Within
            )
        ).ConfigureAwait(false);
        IReadOnlyList<string> disjointByExpression = await SearchIdsAsync(
            store,
            filters => filters.GeoPolygonWithRelation(
                document => document.Location,
                polygon,
                GeoShapeRelation.Disjoint
            )
        ).ConfigureAwait(false);
        IReadOnlyList<string> tupleWithin = await SearchIdsAsync(
            store,
            filters => filters.GeoPolygonWithin("location", tuplePolygon)
        ).ConfigureAwait(false);
        IReadOnlyList<string> tupleWithinByExpression = await SearchIdsAsync(
            store,
            filters => filters.GeoPolygonWithin(document => document.Location, tuplePolygon)
        ).ConfigureAwait(false);

        using (Assert.EnterMultipleScope()) {
            Assert.That(insideResult.IsSuccessful, Is.True);
            Assert.That(outsideResult.IsSuccessful, Is.True);
            Assert.That(insideDocument.Document?.Location, Is.EqualTo(new GeoPoint(121.5, 25.5)));
            Assert.That(within, Is.EqualTo(["inside"]));
            Assert.That(intersects, Is.EqualTo(["inside"]));
            Assert.That(disjoint, Is.EqualTo(["outside"]));
            Assert.That(contains, Is.Empty);
            Assert.That(withinByExpression, Is.EqualTo(within));
            Assert.That(disjointByExpression, Is.EqualTo(disjoint));
            Assert.That(tupleWithin, Is.EqualTo(within));
            Assert.That(tupleWithinByExpression, Is.EqualTo(within));
        }
    }

    private static async Task<IReadOnlyList<string>> SearchIdsAsync(
        ElasticsearchDocumentStore<GeoIntegrationDocument> store,
        Action<ElasticsearchQueryFilterBuilder<GeoIntegrationDocument>> configureFilters
    ) {
        ElasticsearchQueryFilterBuilder<GeoIntegrationDocument> filters = new();
        configureFilters(filters);
        DocumentSearchResult<GeoIntegrationDocument> result = await store
            .SearchAsync(new DocumentSearchRequest<GeoIntegrationDocument> { Filters = filters })
            .ConfigureAwait(false);

        Assert.That(result.IsSuccessful, Is.True, result.ErrorReason);

        return result.Documents.Select(document => document.Id).Order(StringComparer.Ordinal).ToList();
    }

    [ElasticsearchIndexName("geo-integration-documents")]
    internal sealed class GeoIntegrationDocument {
        public required string Id { get; init; }

        public GeoPoint? Location { get; init; }
    }
}
