using CloudyWing.ElasticsearchDocumentKit.Geo;

namespace CloudyWing.ElasticsearchDocumentKit.Tests.Geo;

[TestFixture]
internal sealed class GeoPolygonTests {
    [Test]
    public void Constructor_WithOpenRing_AppendsFirstPointToClose() {
        GeoPolygon sut = new([
            new GeoPoint(121.0, 25.0),
            new GeoPoint(122.0, 25.0),
            new GeoPoint(122.0, 26.0)
        ]);

        using (Assert.EnterMultipleScope()) {
            Assert.That(sut.Points, Has.Count.EqualTo(4));
            Assert.That(sut.Points[^1], Is.EqualTo(sut.Points[0]));
        }
    }

    [Test]
    public void Constructor_WithClosedRing_DoesNotAppendAnotherPoint() {
        GeoPolygon sut = new([
            new GeoPoint(121.0, 25.0),
            new GeoPoint(122.0, 25.0),
            new GeoPoint(122.0, 26.0),
            new GeoPoint(121.0, 25.0)
        ]);

        using (Assert.EnterMultipleScope()) {
            Assert.That(sut.Points, Has.Count.EqualTo(4));
            Assert.That(sut.Points[^1], Is.EqualTo(sut.Points[0]));
        }
    }

    [Test]
    public void Constructor_WithMoreThanThreePoints_KeepsAllPointsInOrder() {
        GeoPoint[] points = [
            new GeoPoint(121.0, 25.0),
            new GeoPoint(122.0, 25.0),
            new GeoPoint(122.0, 26.0),
            new GeoPoint(121.0, 26.0)
        ];

        GeoPolygon sut = new(points);

        using (Assert.EnterMultipleScope()) {
            Assert.That(sut.Points, Has.Count.EqualTo(5));
            Assert.That(sut.Points.Take(4), Is.EqualTo(points));
        }
    }

    [Test]
    public void Constructor_WithTwoDistinctPoints_ThrowsArgumentException() {
        Assert.That(
            () => new GeoPolygon([new GeoPoint(121.0, 25.0), new GeoPoint(122.0, 25.0)]),
            Throws.TypeOf<ArgumentException>()
                .With.Property(nameof(ArgumentException.ParamName)).EqualTo("points")
        );
    }

    [Test]
    public void Constructor_WithRepeatedPointsThatAreNotThreeDistinct_ThrowsArgumentException() {
        GeoPoint first = new(121.0, 25.0);
        GeoPoint second = new(122.0, 25.0);

        Assert.That(
            () => new GeoPolygon([first, second, first, second]),
            Throws.TypeOf<ArgumentException>()
                .With.Property(nameof(ArgumentException.ParamName)).EqualTo("points")
        );
    }

    [Test]
    public void Constructor_WithEmptyCollection_ThrowsArgumentException() {
        Assert.That(
            () => new GeoPolygon([]),
            Throws.TypeOf<ArgumentException>()
                .With.Property(nameof(ArgumentException.ParamName)).EqualTo("points")
        );
    }

    [Test]
    public void Constructor_WithNull_ThrowsArgumentNullException() {
        Assert.That(
            () => new GeoPolygon(null!),
            Throws.TypeOf<ArgumentNullException>()
                .With.Property(nameof(ArgumentNullException.ParamName)).EqualTo("points")
        );
    }

    [Test]
    public void Constructor_WithNullElement_ThrowsArgumentException() {
        Assert.That(
            () => new GeoPolygon([new GeoPoint(121.0, 25.0), null!, new GeoPoint(122.0, 26.0)]),
            Throws.TypeOf<ArgumentException>()
                .With.Property(nameof(ArgumentException.ParamName)).EqualTo("points")
        );
    }
}
