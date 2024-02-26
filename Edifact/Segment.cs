namespace Net.Leksi.Edifact;

internal class Segment: DataElement
{
    internal List<Component> Components { get; private init; } = [];
}
