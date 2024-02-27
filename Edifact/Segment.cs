namespace Net.Leksi.Edifact;

internal class Segment: Component
{
    internal List<Component>? Components { get; set; }
    internal string[]? Children { get; set; }
}
