namespace Net.Leksi.Edifact;

public class MessageSchemaCustomizerOptions
{
    public string? SchemasUri { get; set; }
    public string? MessageIdentifier { get; set; }
    public MessageSchemaCustomizerAction? Action { get; set;}
    public string? SegmentGroup { get; set; }
    public string? Segment { get; set; }
    public string? Type { get; set; }
    public string? Suffix { get; set; }
}
