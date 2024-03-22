using System.Net;

namespace Net.Leksi.Edifact;

public class EdifactDownloaderOptions
{
    public string? Message { get; set; }
    public string? Directories { get; set; }
    public string? Namespace { get; set; }
    public WebProxy? Proxy { get; set; }
    public string? SchemasUri { get; set; }
    public string? TmpFolder { get; set; }
    public int? ConnectionTimeout { get; set; }
}
