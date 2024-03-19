namespace Net.Leksi.Edifact;

public class InteractiveMessageHeader: MessageHeader
{
    public DialogueReference? DialogueReference { get; set; }
    public DateTime? DateAndTimeOfInitiation { get; set; }
}
