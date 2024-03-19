namespace Net.Leksi.Edifact;

public class MessageHeader
{
    public string MessageReferenceNumber { get; set; } = null!;
    public MessageIdentifier Identifier { get; set; } = null!;
    public StatusOfTransfer? StatusOfTransfer { get; set; }
}