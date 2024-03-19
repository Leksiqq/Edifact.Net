namespace Net.Leksi.Edifact;

public class InterchangeHeader
{
    public SyntaxIdentifier SyntaxIdentifier { get; set; } = null!;
    public bool TestIndicator { get; set; } = false;
}
