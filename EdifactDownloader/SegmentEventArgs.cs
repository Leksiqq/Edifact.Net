namespace Net.Leksi.Edifact;

public class SegmentEventArgs : EventArgs
{
    public LocatedString tag = null;
    public List<List<LocatedString>> elements = new List<List<LocatedString>>();
}
