using System.Xml;

namespace Net.Leksi.Edifact;

public class EdifactMessageVisualizerOptions
{
    public string? SchemasUri { get; set; }
    public string? MessageType { get; set; }
    public string? MessageDirectory { get; set; }
    public string? ControllingAgency { get; set; }
    public string? MessageSuffix { get; set; }
    public Stream? Output { get; set; }
    public int? PageWidth { get; set; }

}
