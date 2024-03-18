namespace Net.Leksi.Edifact;

public class PartyIdentification
{
    public string Value { get; set; } = null!;
    public string? CodeQualifier { get; set; }
    public string? InternalIdentification { get; set; }
    public string? InternalSubIdentification { get; set; }
}
