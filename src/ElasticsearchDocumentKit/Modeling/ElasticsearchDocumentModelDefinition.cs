namespace CloudyWing.ElasticsearchDocumentKit.Modeling;

internal sealed class ElasticsearchDocumentModelDefinition {
    public ElasticsearchDocumentModelDefinition(Type documentType) {
        ArgumentNullException.ThrowIfNull(documentType);

        DocumentType = documentType;
    }

    public Type DocumentType { get; }

    public string? IndexName { get; set; }

    public Delegate? IdSelector { get; set; }

    public Delegate? ConfigureIndex { get; set; }
}
