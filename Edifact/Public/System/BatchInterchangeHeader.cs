namespace Net.Leksi.Edifact;

public class BatchInterchangeHeader : InterchangeHeader
{
    public PartyIdentification Sender { get; set; } = null!;
    public PartyIdentification Recipient { get; set; } = null!;
    public DateTime DateAndTimeOfPreparation { get; set; }
    public string ControlReference { get; set; } = null!;
    public PartyIdentification? RecipientReferencePasswordDetails { get; set; }
    public string? ApplicationReference { get; set; }
    public char? PriorityCode { get; set; }
    public bool AcknowledgementRequest { get; set; } = false;
    public string? AgreementIdentifier { get; set; }
}
 
