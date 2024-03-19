namespace Net.Leksi.Edifact;

public class EdifactEventArgs: EventArgs
{
    public EventKind EventKind { get; internal set; }
}
