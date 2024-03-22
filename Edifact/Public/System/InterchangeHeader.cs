namespace Net.Leksi.Edifact;

public class InterchangeHeader
{
    public SyntaxIdentifier SyntaxIdentifier { get; private init; } = new();
    public PartyIdentification? Sender { get; set; } = null!;
    public PartyIdentification? Recipient { get; set; } = null!;
    public string? TestIndicator { get; set; }
}
