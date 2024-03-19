namespace Net.Leksi.Edifact;

public class MessageIdentifier
{
    public string MessageType { get; set; } = null!;
    public string MessageVersionNumber { get; set; } = null!;
    public string MessageReleaseNumber { get; set; } = null!;
    public string ControllingAgencyCoded { get; set; } = null!;
    public string? AssociationAssignedCode { get; set; }
    public string? CodeListDirectoryVersionNUmber { get; set; }
    public string? MessageTypeSubfunctionIdentification { get; set; }
}
