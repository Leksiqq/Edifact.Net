using System.Net;

namespace Net.Leksi.Edifact;

internal class EdifactDownloaderOptions
{
    internal string? Message { get; set; }
    internal string? Directories { get; set; }
    internal string? Namespace { get; set; }
    internal WebProxy? Proxy { get; set; }
    internal string? SchemasUri { get; set; }
    internal string? TmpFolder { get; set; }
    internal int? ConnectionTimeout { get; set; }
}
