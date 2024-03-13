using System.Xml;

namespace Net.Leksi.Edifact;

public class MessageEventArgs: EventArgs
{
    public MessageEventKind EventKind { get; internal set; }
    public XmlDocument? InterchangeHeader { get; internal set; }
    public XmlDocument? GroupHeader { get; internal set; }
    public XmlDocument? MessageHeader { get; internal set; }
    public string? MessageReferenceNumber { get; internal set; }
    public string? MessageType { get; internal set; }
    public string? MessageVersion { get; internal set; }
    public string? MessageRelease { get; internal set; }
    public Stream? Stream { get; set; }
    public string? Xml { get; internal set; }
    public string? ControllingAgencyCoded {  get; set; } 
}
