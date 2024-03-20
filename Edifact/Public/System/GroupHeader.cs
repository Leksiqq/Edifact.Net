namespace Net.Leksi.Edifact;

public class GroupHeader
{
    public string GroupReferenceNumber { get; set; } = null!;
    public MessageIdentification? MessageGroupIdentification { get; set; }
    public PartyIdentification? ApplicationSender { get; set; }
    public PartyIdentification? ApplicationRecipient { get; set; }
    public DateTimeOfEvent? DateAndTimeOfPreparation {  get; set; }
    public string? ApplicationPassword {  get; set; }
}
