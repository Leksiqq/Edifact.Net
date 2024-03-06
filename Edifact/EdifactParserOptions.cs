using System.Text;

namespace Net.Leksi.Edifact;

public class EdifactParserOptions
{
    public string? InputUri { get; set; }
    public string? OutputUri { get; set; }
    public string? SchemasUri { get; set; }
    public Encoding? Encoding { get; set; }
    public bool? IsStrict { get; set; }
    public int? BufferLength { get; set; }
}
