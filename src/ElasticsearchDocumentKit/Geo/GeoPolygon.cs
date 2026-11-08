namespace CloudyWing.ElasticsearchDocumentKit.Geo;

/// <summary>
/// 表示作為地理查詢條件的多邊形，只含外環，不支援內環。
/// </summary>
/// <remarks>
/// 此型別只用於查詢條件，不提供 JSON converter，不能作為文件屬性。
/// </remarks>
public sealed class GeoPolygon {
    /// <summary>
    /// 初始化 <see cref="GeoPolygon"/> 類別的新執行個體。
    /// </summary>
    /// <param name="points">外環的頂點；首尾不同時自動在尾端補上首點使多邊形閉合。</param>
    /// <exception cref="ArgumentNullException"><paramref name="points"/> 為 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="points"/> 含有 <see langword="null"/> 元素，或不同座標少於三個。
    /// </exception>
    public GeoPolygon(IEnumerable<GeoPoint> points) {
        ArgumentNullException.ThrowIfNull(points);

        List<GeoPoint> ring = points.ToList();

        if (ring.Any(point => point is null)) {
            throw new ArgumentException("Geo polygon must not contain null points.", nameof(points));
        }

        if (ring.Distinct().Count() < 3) {
            throw new ArgumentException(
                "Geo polygon must contain at least three distinct points.",
                nameof(points)
            );
        }

        if (!ring[0].Equals(ring[^1])) {
            ring.Add(ring[0]);
        }

        Points = ring.AsReadOnly();
    }

    /// <summary>
    /// 取得已閉合的頂點集合。
    /// </summary>
    /// <value>首點與末點相同。</value>
    public IReadOnlyList<GeoPoint> Points { get; }
}
