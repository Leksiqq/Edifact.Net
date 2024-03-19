namespace Net.Leksi.Edifact;

public class InteractiveInterchangeHeader: InterchangeHeader
{
    public DialogueReference? DialogueReference { get; set; }
    public TransactionReference? TransactionReference { get; set; }
    public MessageIdentifier? ScenarioIdentification { get; set; }
    public MessageIdentifier? DialogueIdentification { get; set; }
    public PartyIdentification? Sender { get; set; } = null!;
    public PartyIdentification? Recipient { get; set; } = null!;
    public DateTime? DateAndTimeOfInitiation { get; set; }
    public bool DuplicateIndicator { get; set; } = false;
}