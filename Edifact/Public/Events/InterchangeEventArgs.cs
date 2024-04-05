using System.Xml;

namespace Net.Leksi.Edifact;

public class InterchangeEventArgs: EdifactEventArgs
{
    public InterchangeHeader? Header { get; internal set; }
}
