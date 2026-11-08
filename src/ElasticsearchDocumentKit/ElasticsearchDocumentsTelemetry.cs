using System.Diagnostics;

namespace CloudyWing.ElasticsearchDocumentKit;

/// <summary>
/// 提供 Elasticsearch documents 工具庫 OpenTelemetry instrumentation 使用的 ActivitySource。
/// </summary>
public static class ElasticsearchDocumentsTelemetry {
    /// <summary>
    /// 取得 ActivitySource 名稱。
    /// </summary>
    public const string ActivitySourceName = "CloudyWing.ElasticsearchDocumentKit";

    /// <summary>
    /// 取得 ActivitySource 執行個體。
    /// </summary>
    public static ActivitySource ActivitySource { get; } = new(ActivitySourceName);
}
