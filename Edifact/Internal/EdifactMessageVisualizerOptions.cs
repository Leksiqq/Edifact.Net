using System.Xml;

namespace Net.Leksi.Edifact;

internal class EdifactMessageVisualizerOptions
{
    internal string? SchemasUri { get; set; }
    internal string? MessageType { get; set; }
    internal string? MessageDirectory { get; set; }
    internal string? ControllingAgency { get; set; }
    internal string? MessageSuffix { get; set; }
    internal Stream? Output { get; set; }
    internal int? PageWidth { get; set; }

}
