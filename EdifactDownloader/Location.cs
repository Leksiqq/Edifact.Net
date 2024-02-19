namespace Net.Leksi.Edifact;

public class Location
{
    public int offset = 0;
    public int line = 0;
    public int col = 0;
    public void Set(Location loc)
    {
        offset = loc.offset;
        line = loc.line;
        col = loc.col;
    }
    public override string ToString()
    {
        return string.Format("offset: {0}, line: {1}, col: {2}", offset, line, col);
    }
    public Location()
    {
    }
    public Location(string[] parts, int off)
    {
        offset = int.Parse(parts[off]);
        line = int.Parse(parts[off+1]);
        col = int.Parse(parts[off+2]);
    }
}
