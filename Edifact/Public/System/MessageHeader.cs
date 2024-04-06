namespace Net.Leksi.Edifact;

public class MessageHeader
{
    public string? MessageReferenceNumber { get; set; } = null!;
    public MessageIdentification Identifier { get; private init; } = new();
}