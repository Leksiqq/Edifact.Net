using System.Text;

namespace Net.Leksi.Edifact;

public class EdifactParserOptions: EdifactProcessorOptions
{
    public string? InputUri { get; set; }
    public bool? IsStrict { get; set; }
    public string? OutputUri { get; set; }
    public int? BufferLength { get; set; }
}
