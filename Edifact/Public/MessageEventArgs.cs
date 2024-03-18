using System.Xml;

namespace Net.Leksi.Edifact;

public class MessageEventArgs: EventArgs
{
    public EventKind EventKind { get; internal set; }
    public XmlDocument? InterchangeHeader { get; internal set; }
    public XmlDocument? GroupHeader { get; internal set; }
    public MessageHeader MessageHeader { get; internal set; } = null!;
    public Stream? Stream { get; set; }
}
