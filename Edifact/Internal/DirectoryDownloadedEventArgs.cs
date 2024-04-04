namespace Net.Leksi.Edifact;

internal class DirectoryDownloadedEventArgs
{
    internal string? Directory { get; set; }
    internal string? BaseFolder { get; set; }
    internal string[]? Files { get; set; }
}
