using System.Xml;

namespace Net.Leksi.Edifact;

public class MessageEventArgs: EventArgs
{
    MessageEventKind EventKind { get; set; }
    XmlWriter Writer { get; set; } = null!;
}
