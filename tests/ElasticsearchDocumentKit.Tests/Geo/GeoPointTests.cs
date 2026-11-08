using System.Globalization;
using System.Text.Json;
using CloudyWing.ElasticsearchDocumentKit.Geo;

namespace CloudyWing.ElasticsearchDocumentKit.Tests.Geo;

[TestFixture]
internal sealed class GeoPointTests {
    [TestCase(180, 90)]
    [TestCase(-180, -90)]
    [TestCase(0, 0)]
    [TestCase(121.5, 25.05)]
    public void Constructor_WithValidCoordinates_KeepsLongitudeAndLatitude(double longitude, double latitude) {
        GeoPoint sut = new(longitude, latitude);

        using (Assert.EnterMultipleScope()) {
            Assert.That(sut.Longitude, Is.EqualTo(longitude));
            Assert.That(sut.Latitude, Is.EqualTo(latitude));
        }
    }

    [TestCase(180.0001)]
    [TestCase(-180.0001)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void Constructor_WithInvalidLongitude_ThrowsArgumentOutOfRangeException(double longitude) {
        Assert.That(
            () => new GeoPoint(longitude, 0),
            Throws.TypeOf<ArgumentOutOfRangeException>()
                .With.Property(nameof(ArgumentOutOfRangeException.ParamName)).EqualTo("longitude")
        );
    }

    [TestCase(90.0001)]
    [TestCase(-90.0001)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void Constructor_WithInvalidLatitude_ThrowsArgumentOutOfRangeException(double latitude) {
        Assert.That(
            () => new GeoPoint(0, latitude),
            Throws.TypeOf<ArgumentOutOfRangeException>()
                .With.Property(nameof(ArgumentOutOfRangeException.ParamName)).EqualTo("latitude")
        );
    }

    [Test]
    public void Equals_WithSameCoordinates_ReturnsTrueAndSameHashCode() {
        GeoPoint left = new(121.5, 25.05);
        GeoPoint right = new(121.5, 25.05);

        using (Assert.EnterMultipleScope()) {
            Assert.That(left.Equals(right), Is.True);
            Assert.That(left.Equals((object)right), Is.True);
            Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
        }
    }

    [Test]
    public void Equals_WithPositiveAndNegativeZero_ReturnsTrueAndSameHashCode() {
        GeoPoint left = new(0.0, 0.0);
        GeoPoint right = new(-0.0, -0.0);

        using (Assert.EnterMultipleScope()) {
            Assert.That(left.Equals(right), Is.True);
            Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
        }
    }

    [Test]
    public void Equals_WithDifferentCoordinatesOrNull_ReturnsFalse() {
        GeoPoint sut = new(121.5, 25.05);

        using (Assert.EnterMultipleScope()) {
            Assert.That(sut.Equals(new GeoPoint(121.5, 25.06)), Is.False);
            Assert.That(sut.Equals(new GeoPoint(121.6, 25.05)), Is.False);
            Assert.That(sut.Equals(null as GeoPoint), Is.False);
            Assert.That(sut.Equals((object)"not a point"), Is.False);
        }
    }

    [Test]
    public void ToString_UnderNonInvariantCulture_UsesInvariantFormat() {
        CultureInfo original = CultureInfo.CurrentCulture;

        try {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Assert.That(new GeoPoint(121.5, 25.05).ToString(), Is.EqualTo("(121.5, 25.05)"));
        } finally {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Test]
    public void Serialize_WritesGeoJsonPointWithLongitudeFirst() {
        string json = JsonSerializer.Serialize(new GeoPoint(121.5, 25.05));

        Assert.That(json, Is.EqualTo("""{"type":"Point","coordinates":[121.5,25.05]}"""));
    }

    [Test]
    public void SerializeAndDeserialize_RoundTrip_ReturnsEqualPoint() {
        GeoPoint original = new(-179.999999, 89.123456);

        GeoPoint? restored = JsonSerializer.Deserialize<GeoPoint>(JsonSerializer.Serialize(original));

        Assert.That(restored, Is.EqualTo(original));
    }

    [Test]
    public void Deserialize_WithNull_ReturnsNull() {
        GeoPoint? result = JsonSerializer.Deserialize<GeoPoint>("null");

        Assert.That(result, Is.Null);
    }

    [TestCase("""{"type":"point","coordinates":[121.5,25.05]}""")]
    [TestCase("""{"coordinates":[121.5,25.05],"type":"POINT"}""")]
    [TestCase("""{"type":"Point","coordinates":[121.5,25.05,10.5]}""")]
    public void Deserialize_WithValidGeoJson_ReturnsPoint(string json) {
        GeoPoint? result = JsonSerializer.Deserialize<GeoPoint>(json);

        Assert.That(result, Is.EqualTo(new GeoPoint(121.5, 25.05)));
    }

    [TestCase("""{"type":"Polygon","coordinates":[[[121.0,25.0],[122.0,25.0],[122.0,26.0],[121.0,25.0]]]}""")]
    [TestCase("""{"type":"Point","coordinates":[121.5]}""")]
    [TestCase("""{"type":"Point","coordinates":[]}""")]
    [TestCase("""{"type":"Point"}""")]
    [TestCase("""{"coordinates":[121.5,25.05]}""")]
    [TestCase("""{"type":"Point","coordinates":"121.5,25.05"}""")]
    [TestCase("""{"type":"Point","coordinates":["121.5","25.05"]}""")]
    [TestCase("""{"type":"Point","coordinates":[181.0,25.05]}""")]
    [TestCase("""{"type":"Point","coordinates":[121.5,-91.0]}""")]
    [TestCase("""{"type":1,"coordinates":[121.5,25.05]}""")]
    [TestCase("""[121.5,25.05]""")]
    [TestCase("""
        "121.5,25.05"
        """)]
    public void Deserialize_WithInvalidGeoJson_ThrowsJsonException(string json) {
        Assert.That(() => JsonSerializer.Deserialize<GeoPoint>(json), Throws.TypeOf<JsonException>());
    }
}
