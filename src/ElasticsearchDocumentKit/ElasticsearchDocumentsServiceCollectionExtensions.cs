using CloudyWing.ElasticsearchDocumentKit.Modeling;
using CloudyWing.ElasticsearchDocumentKit.Indexing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CloudyWing.ElasticsearchDocumentKit;

/// <summary>
/// 提供 Elasticsearch documents 工具庫的 DI 註冊方法。
/// </summary>
public static class ElasticsearchDocumentsServiceCollectionExtensions {
    /// <summary>
    /// 將 Elasticsearch documents 工具庫的核心服務註冊至 DI 容器。
    /// </summary>
    /// <param name="services">服務集合。</param>
    /// <param name="configure">設定 Elasticsearch documents 選項的委派。</param>
    /// <returns>原始服務集合。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> 為 <see langword="null"/> 時。</exception>
    /// <exception cref="InvalidOperationException">設定值不符合工具庫需求時。</exception>
    public static IServiceCollection AddElasticsearchDocuments(
        this IServiceCollection services,
        Action<ElasticsearchDocumentsOptions>? configure = null
    ) {
        ArgumentNullException.ThrowIfNull(services);

        ElasticsearchDocumentsOptions options = new();
        configure?.Invoke(options);
        options.Validate();

        services.Replace(ServiceDescriptor.Singleton(options));
        services.TryAddSingleton<IIndexNameResolver, DefaultIndexNameResolver>();
        services.TryAddSingleton<IElasticsearchDocumentModelResolver, DefaultElasticsearchDocumentModelResolver>();
        services.TryAddTransient(typeof(IElasticsearchDocumentStore<>), typeof(ElasticsearchDocumentStore<>));
        services.TryAddTransient(typeof(IIndexInitializer<>), typeof(ElasticsearchIndexInitializer<>));
        services.TryAddSingleton<IElasticsearchAliasSwitcher, ElasticsearchAliasSwitcher>();

        return services;
    }
}
