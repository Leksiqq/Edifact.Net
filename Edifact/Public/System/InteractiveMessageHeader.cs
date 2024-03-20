namespace Net.Leksi.Edifact;

public class InteractiveMessageHeader: MessageHeader
{
    public DialogueReference? DialogueReference { get; set; }
    public DateTimeOfEvent? DateAndTimeOfInitiation { get; set; }
    public InteractiveStatusOfTransfer? StatusOfTransfer { get; set; }
    public string? TestIndicator { get; set; }
}
