namespace Net.Leksi.Edifact;

internal class Composite: DataElement
{
    internal List<CompositeProperty> Properties { get; private init; } = [];
}
