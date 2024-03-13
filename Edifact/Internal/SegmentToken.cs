namespace Net.Leksi.Edifact;

internal class SegmentToken
{
    internal string? Tag { get; set; }
    internal List<int>? ExplcitNestingIndication { get; set; }
    internal List<ComponentToken>? Components { get; set; }
}
