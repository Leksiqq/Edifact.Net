namespace Net.Leksi.Edifact;

public class InteractiveInterchangeHeader: InterchangeHeader
{
    public DialogueReference? DialogueReference { get; set; }
    public TransactionReference? TransactionReference { get; set; }
    public Identification? ScenarioIdentification { get; set; }
    public Identification? DialogueIdentification { get; set; }
    public DateTimeOfEvent? DateAndTimeOfInitiation { get; set; }
    public string? DuplicateIndicator { get; set; }
}