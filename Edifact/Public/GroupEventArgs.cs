namespace Net.Leksi.Edifact;

public class GroupEventArgs: EventArgs
{
    public EventKind EventKind { get; internal set; }
    public GroupHeader Header { get; internal set; } = null!;
}
