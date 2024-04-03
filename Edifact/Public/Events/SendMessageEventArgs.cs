namespace Net.Leksi.Edifact;

public class SendMessageEventArgs: EventArgs
{
    public MessageHeader Header { get; internal set; } = null!;
    public Stream Output { get; internal set; } = null!;

}
