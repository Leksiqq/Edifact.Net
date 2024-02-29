namespace Net.Leksi.Edifact;

public class Composite: DataElement
{
    public List<Element> Elements { get; private init; } = [];
}
