namespace Net.Leksi.Edifact;

public class BatchInterchangeHeader : InterchangeHeader
{
    public DateTimeOfEvent? DateAndTimeOfPreparation { get; set; }
    public string ControlReference { get; set; } = null!;
    public ReferenceOrPasswordDetails? RecipientReferencePasswordDetails { get; set; }
    public string? ApplicationReference { get; set; }
    public string? PriorityCode { get; set; }
    public bool AcknowledgementRequest { get; set; } = false;
    public string? AgreementIdentifier { get; set; }
}
 
