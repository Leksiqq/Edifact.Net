namespace Net.Leksi.Edifact;

public class LocatedString
{
    public Location begin = new Location();
    public Location end = new Location();
    public string data;

    public LocatedString(Location begin, string data)
    {
        this.begin.Set(begin);
        this.data = data;
    }
}
