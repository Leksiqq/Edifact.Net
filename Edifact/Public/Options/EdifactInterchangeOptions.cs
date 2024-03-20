using System.Text;
using System.Xml;

namespace Net.Leksi.Edifact;

public class EdifactInterchangeOptions
{
    public string? SchemasUri { get; set; }
    public Stream? Output { get; set; }
    public InterchangeHeader? InterchangeHeader { get; set; }
    public Encoding? Encoding { get; set; } = Encoding.UTF8;
    public char SegmentPartsSeparator { get; set; } = '+';
    public char ComponentPartsSeparator { get; set; } = ':';
    public char DecimalMark { get; set; } = '.';
    public char ReleaseCharacter { get; set; } = '?';
    public char SegmentTerminator { get; set; } = '\'';
    public string? SegmentsSeparator {  get; set; } = Environment.NewLine;
}
