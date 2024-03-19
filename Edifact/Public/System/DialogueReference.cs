namespace Net.Leksi.Edifact;

public class DialogueReference
{
    public string InitiatorControlReference { get; set; } = null!;
    public string? InitiatorReferenceIdentification { get; set; }
    public string? ControllingAgencyCoded { get; set; }
    public string? ResponderControlReference { get; set; }
}
