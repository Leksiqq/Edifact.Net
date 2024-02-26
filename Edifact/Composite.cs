namespace Net.Leksi.Edifact;

internal class Composite: DataElement
{
    internal List<Element> Elements { get; private init; } = [];
}
