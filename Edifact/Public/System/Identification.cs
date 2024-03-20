namespace Net.Leksi.Edifact;

public class Identification
{
    public string Identifier { get; set; } = null!;
    public string? VersionNumber { get; set; } = null!;
    public string? ReleaseNumber { get; set; } = null!;
    public string? ControllingAgencyCoded { get; set; } = null!;
}
