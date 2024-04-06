namespace Net.Leksi.Edifact;

public class BatchMessageHeader : MessageHeader
{
    public new string MessageReferenceNumber { get => base.MessageReferenceNumber!; set => base.MessageReferenceNumber = value; }
    public string? CommonAccessReference { get; set; }
    public Identification? SubsetIdentification { get; set; }
    public Identification? ImplementationGuidelineIdentification { get; set; }
    public BatchStatusOfTransfer? StatusOfTransfer { get; set; }
    public Identification? ScenarioIdentification { get; set; }
}
