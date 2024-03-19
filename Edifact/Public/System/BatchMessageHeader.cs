namespace Net.Leksi.Edifact;

public class BatchMessageHeader : MessageHeader
{
    public string? CommonAccessReference { get; set; }
    public MessageIdentifier? SubsetIdentification { get; set; }
    public MessageIdentifier? ImplementationGuidelineIdentification { get; set; }
    public MessageIdentifier? ScenarioIdentification { get; set; }
}
