namespace Net.Leksi.Edifact;

public class MessageHeader
{
    public string MessageReferenceNumber { get; set; } = null!;
    public MessageIdentifier Identifier { get; set; } = null!;
    public string? CommonAccessReference { get; set; }
    public StatusOfTransfer? StatusOfTransfer { get; set; }
    public MessageIdentifier? SubsetIdentification { get; set; }
    public MessageIdentifier? ImplementationGuidelineIdentification { get; set; }
    public MessageIdentifier? ScenarioIdentification { get; set; }
}
