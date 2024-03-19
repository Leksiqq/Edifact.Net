namespace Net.Leksi.Edifact;

public class GroupEventArgs: EdifactEventArgs
{
    public GroupHeader Header { get; internal set; } = null!;
}
