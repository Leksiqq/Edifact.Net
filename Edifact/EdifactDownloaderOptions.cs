using System.Net;

namespace Net.Leksi.Edifact;

public class EdifactDownloaderOptions
{
    public string? Message { get; set; }
    public string? Directory { get; set; }
    public string? Namespace { get; set; }
    public WebProxy? Proxy { get; set; }
    public Uri? TargetUri { get; set; }
    public string? TmpFolder { get; set; }
    public string? ExternalUnzipCommandLineFormat {  get; set; }
}
