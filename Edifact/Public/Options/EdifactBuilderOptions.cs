using System.Text;

namespace Net.Leksi.Edifact;

public class EdifactBuilderOptions: EdifactProcessorOptions
{
    public Stream? Output { get; set; }
    public char SegmentPartsSeparator { get; set; } = '+';
    public char ComponentPartsSeparator { get; set; } = ':';
    public char DecimalMark { get; set; } = '.';
    public char ReleaseCharacter { get; set; } = '?';
    public char SegmentTerminator { get; set; } = '\'';
    public string? SegmentsSeparator {  get; set; } = Environment.NewLine;
}
