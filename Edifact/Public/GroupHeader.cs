namespace Net.Leksi.Edifact;

public class GroupHeader
{
    public MessageIdentifier? MessageIdentification { get; set; }
    public PartyIdentification? ApplicationSender { get; set; }
    public PartyIdentification? ApplicationRecipient { get; set; }
    public DateTime? DateAndTimeOfPreparation {  get; set; }
    public string? ApplicationPassword {  get; set; }
}
