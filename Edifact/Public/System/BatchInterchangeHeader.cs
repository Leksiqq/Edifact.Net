namespace Net.Leksi.Edifact;

public class BatchInterchangeHeader : InterchangeHeader
{
    public new PartyIdentification Sender { get => base.Sender!; }
    public new PartyIdentification Recipient { get => base.Recipient!; }
    public DateTimeOfEvent DateAndTimeOfPreparation { get; private init; } = new();
    public string ControlReference { get; set; } = null!;
    public ReferenceOrPasswordDetails? RecipientReferencePasswordDetails { get; set; }
    public string? ApplicationReference { get; set; }
    public string? PriorityCode { get; set; }
    public string? AcknowledgementRequest { get; set; }
    public string? AgreementIdentifier { get; set; }

    public BatchInterchangeHeader(): base() 
    {
        base.Sender = new();
        base.Recipient = new();
    }
}
 
