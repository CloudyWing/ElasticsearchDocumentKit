using System.Text.Json;
using System.Text.Json.Serialization;

namespace CloudyWing.ElasticsearchDocumentKit.Geo;

internal sealed class GeoPointJsonConverter : JsonConverter<GeoPoint> {
    public override GeoPoint? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object) {
            throw new JsonException("A GeoJSON Point must be a JSON object.");
        }

        if (!root.TryGetProperty("type", out JsonElement type)
            || type.ValueKind != JsonValueKind.String
            || !string.Equals(type.GetString(), "Point", StringComparison.OrdinalIgnoreCase)
        ) {
            throw new JsonException("The type of a GeoJSON Point must be \"Point\".");
        }

        if (!root.TryGetProperty("coordinates", out JsonElement coordinates)
            || coordinates.ValueKind != JsonValueKind.Array
            || coordinates.GetArrayLength() < 2
        ) {
            throw new JsonException("The coordinates of a GeoJSON Point must be an array of at least two numbers.");
        }

        // GeoJSON 允許第三個元素（高度），GeoPoint 只使用經度與緯度，多餘元素直接忽略
        JsonElement longitudeElement = coordinates[0];
        JsonElement latitudeElement = coordinates[1];

        double longitude = 0;
        double latitude = 0;

        if (longitudeElement.ValueKind != JsonValueKind.Number
            || latitudeElement.ValueKind != JsonValueKind.Number
            || !longitudeElement.TryGetDouble(out longitude)
            || !latitudeElement.TryGetDouble(out latitude)
        ) {
            throw new JsonException("The longitude and latitude of a GeoJSON Point must be numbers.");
        }

        try {
            return new GeoPoint(longitude, latitude);
        } catch (ArgumentOutOfRangeException ex) {
            throw new JsonException(ex.Message, ex);
        }
    }

    public override void Write(Utf8JsonWriter writer, GeoPoint value, JsonSerializerOptions options) {
        writer.WriteStartObject();
        writer.WriteString("type", "Point");
        writer.WriteStartArray("coordinates");
        writer.WriteNumberValue(value.Longitude);
        writer.WriteNumberValue(value.Latitude);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
