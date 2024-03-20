namespace Net.Leksi.Edifact;

public class MessageIdentification: Identification
{
    public string? AssociationAssignedCode { get; set; }
    public string? CodeListDirectoryVersionNUmber { get; set; }
    public string? MessageTypeSubfunctionIdentification { get; set; }
}
