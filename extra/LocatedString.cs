namespace Net.Leksi.Edifact;

internal class LocatedString
{
    internal Location Begin { get; private init; } = new();
    internal Location End { get; private init; } = new();
    internal string Data { get; set; }

    internal LocatedString(Location begin, string data)
    {
        this.Begin.Set(begin);
        this.Data = data;
    }
}
