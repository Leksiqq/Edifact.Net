using System.Text;

namespace Net.Leksi.Edifact;

public class EdifactParserOptions: EdifactProcessorOptions
{
    public string? InputUri { get; set; }
    public string? OutputUri { get; set; }
    public int? BufferSize { get; set; }
}
