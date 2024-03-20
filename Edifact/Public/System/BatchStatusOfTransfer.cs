namespace Net.Leksi.Edifact;

public class BatchStatusOfTransfer
{
    public string SequenceOfTransfers { get; set; } = null!;
    public string? FirstAndLastTransfer { get; set; }
}
