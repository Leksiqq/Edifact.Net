namespace Net.Leksi.Edifact;

public class TransactionReference
{
    public string TransactionControlReference { get; set; } = null!;
    public string? InitiatorReferenceIdentification { get; set; }
    public string? ControllingAgencyCoded { get; set; }
}
