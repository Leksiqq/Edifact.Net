namespace Net.Leksi.Edifact;

public class MessageSchemaCustomizerOptions
{
    public string? OriginalSchemaUri { get; set; }
    public string? CustomSchemaUri { get; set;}
    public MessageSchemaCustomizerAction? Action { get; set;}
    public string? SegmentGroup { get; set; }
    public string? Segment { get; set; }
    public string? Type { get; set; }
    public string? Suffix { get; set; }
}
