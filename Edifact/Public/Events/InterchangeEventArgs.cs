using System.Xml;

namespace Net.Leksi.Edifact;

public class InterchangeEventArgs: EdifactEventArgs
{
    public bool IsInteractive { get; set; }
    public InterchangeHeader? Header { get; internal set; }
}
