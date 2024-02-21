namespace Net.Leksi.Edifact;

internal class Location
{
    private const string s_toStringFormat = "offset: {0}, line: {1}, col: {2}";

    internal int Offset { get; set; } = 0;
    internal int Line { get; set; } = 0;
    internal int Col { get; set; } = 0;
    internal void Set(Location loc)
    {
        Offset = loc.Offset;
        Line = loc.Line;
        Col = loc.Col;
    }
    public override string ToString()
    {
        return string.Format(s_toStringFormat, Offset, Line, Col);
    }
    internal Location() { }
    internal Location(string[] parts, int off)
    {
        Offset = int.Parse(parts[off]);
        Line = int.Parse(parts[off+1]);
        Col = int.Parse(parts[off+2]);
    }
}
