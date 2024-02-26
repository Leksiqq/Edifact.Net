namespace Net.Leksi.Edifact;

public class DirectoryDownloadedEventArgs
{
    public string? Directory { get; internal set; }
    public string[]? Files { get; internal set; }
}
