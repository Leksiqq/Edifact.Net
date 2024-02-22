using System.Xml;

namespace Net.Leksi.Edifact;

public class Schema2TreeOptions
{
    public Uri? SchemaDocument { get; set; }
    public TextWriter? Output { get; set; }
    public int? PaddingLength { get; set; }

}
