namespace Net.Leksi.Edifact;

internal class SegmentEventArgs : EventArgs
{
    internal LocatedString tag = null;
    internal List<List<LocatedString>> elements = new List<List<LocatedString>>();
}
