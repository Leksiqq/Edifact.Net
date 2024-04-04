namespace Net.Leksi.Edifact;

internal class DirectoryNotFoundEventArgs: EventArgs
{
    internal string? Directory { get; set; }
    internal string? Url { get; set; }
}
