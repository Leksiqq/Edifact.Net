namespace Net.Leksi.Edifact;

public class Identification: IEquatable<Identification>
{
    public string Type { get; set; } = null!;
    public string? VersionNumber { get; set; }
    public string? ReleaseNumber { get; set; }
    public string? ControllingAgencyCoded { get; set; }

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

    public override bool Equals(object? obj)
    {
        return Equals(obj as Identification);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Type, VersionNumber, ReleaseNumber, ControllingAgencyCoded);
    }
}
