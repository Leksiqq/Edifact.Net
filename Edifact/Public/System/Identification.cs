namespace Net.Leksi.Edifact;

public class Identification: IEquatable<Identification>
{
    public string Type { get; set; } = null!;
    public string? VersionNumber { get; set; } = null!;
    public string? ReleaseNumber { get; set; } = null!;
    public string? ControllingAgencyCoded { get; set; } = null!;

    public bool Equals(Identification? other)
    {
        if (other is { })
        {
            return Type == other.Type 
                && VersionNumber == other.VersionNumber 
                && ReleaseNumber == other.ReleaseNumber 
                && ControllingAgencyCoded == other.ControllingAgencyCoded;
        }
        return false;
    }
}
