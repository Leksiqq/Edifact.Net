namespace Net.Leksi.Edifact;

public class EdifactParserOptions: EdifactProcessorOptions
{
    public Stream? Input { get; set; }
    public int? BufferSize { get; set; }
}
