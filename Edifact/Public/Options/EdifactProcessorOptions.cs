using System.Text;

namespace Net.Leksi.Edifact;

public class EdifactProcessorOptions
{
    public string? SchemasUri { get; set; }
    public Encoding? Encoding { get; set; } = Encoding.UTF8;
    public Dictionary<string, string>? MessagesSuffixes { get; set; }
}
