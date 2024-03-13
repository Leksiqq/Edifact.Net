using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

internal class DirectoryComparer : IComparer<string>
{
    public int Compare(string? x, string? y)
    {
        if(x == y)
        {
            return 0;
        }
        if(x!.CompareTo(s_d8) > 0 && x.CompareTo(s_d99z) < 0 && y!.CompareTo(s_d8) < 0)
        {
            return -1;
        }
        if (y!.CompareTo(s_d8) > 0 && y.CompareTo(s_d99z) < 0 && x!.CompareTo(s_d8) < 0)
        {
            return 1;
        }
        return x.CompareTo(y);
    }
}
