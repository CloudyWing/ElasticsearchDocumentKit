using System.Globalization;
using System.Text.Json.Serialization;

namespace CloudyWing.ElasticsearchDocumentKit.Geo;

/// <summary>
/// 表示存放於 <c>geo_shape</c> 欄位的 GeoJSON Point。
/// </summary>
/// <remarks>
/// 序列化時固定輸出 <c>{"type":"Point","coordinates":[longitude,latitude]}</c>（座標順序為經度、緯度），
/// 反序列化時要求相同格式。使用時，文件欄位的 mapping 須以 <c>GeoShape</c> 宣告。
/// </remarks>
[JsonConverter(typeof(GeoPointJsonConverter))]
public sealed class GeoPoint : IEquatable<GeoPoint> {
    /// <summary>
    /// 初始化 <see cref="GeoPoint"/> 類別的新執行個體。
    /// </summary>
    /// <param name="longitude">經度，範圍為 -180 至 180。</param>
    /// <param name="latitude">緯度，範圍為 -90 至 90。</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="longitude"/> 或 <paramref name="latitude"/> 超出範圍，或為 <see cref="double.NaN"/>、無限大。
    /// </exception>
    public GeoPoint(double longitude, double latitude) {
        if (!double.IsFinite(longitude) || longitude < -180 || longitude > 180) {
            throw new ArgumentOutOfRangeException(
                nameof(longitude),
                longitude,
                "Longitude must be a finite number between -180 and 180."
            );
        }

        if (!double.IsFinite(latitude) || latitude < -90 || latitude > 90) {
            throw new ArgumentOutOfRangeException(
                nameof(latitude),
                latitude,
                "Latitude must be a finite number between -90 and 90."
            );
        }

        Longitude = longitude;
        Latitude = latitude;
    }

    /// <summary>
    /// 取得經度。
    /// </summary>
    /// <value>範圍為 -180 至 180。</value>
    public double Longitude { get; }

    /// <summary>
    /// 取得緯度。
    /// </summary>
    /// <value>範圍為 -90 至 90。</value>
    public double Latitude { get; }

    /// <summary>
    /// 判斷指定的 <see cref="GeoPoint"/> 是否與目前執行個體有相同的經緯度。
    /// </summary>
    /// <param name="other">要比較的 <see cref="GeoPoint"/>。</param>
    /// <returns>若經度與緯度皆相同，則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public bool Equals(GeoPoint? other) {
        return other is not null && Longitude == other.Longitude && Latitude == other.Latitude;
    }

    /// <summary>
    /// 判斷指定的物件是否與目前執行個體有相同的經緯度。
    /// </summary>
    /// <param name="obj">要比較的物件。</param>
    /// <returns>若 <paramref name="obj"/> 為經緯度相同的 <see cref="GeoPoint"/>，則為 <see langword="true"/>；否則為 <see langword="false"/>。</returns>
    public override bool Equals(object? obj) {
        return Equals(obj as GeoPoint);
    }

    /// <summary>
    /// 取得以經緯度計算的雜湊碼。
    /// </summary>
    /// <returns>雜湊碼。</returns>
    public override int GetHashCode() {
        return HashCode.Combine(Longitude, Latitude);
    }

    /// <summary>
    /// 以固定文化特性輸出經度與緯度。
    /// </summary>
    /// <returns>格式為 <c>(longitude, latitude)</c> 的字串。</returns>
    public override string ToString() {
        return string.Create(CultureInfo.InvariantCulture, $"({Longitude}, {Latitude})");
    }
}
