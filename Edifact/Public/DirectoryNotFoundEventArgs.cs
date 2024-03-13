namespace Net.Leksi.Edifact;

public class DirectoryNotFoundEventArgs: EventArgs
{
    public string? Directory { get; internal set; }
    public string? Url { get; internal set; }
}
