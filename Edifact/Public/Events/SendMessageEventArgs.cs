namespace Net.Leksi.Edifact;

public class SendMessageEventArgs: EventArgs
{
    public MessageHeader Header { get; } = null!;
    public Stream Output { get; } = null!;

}
