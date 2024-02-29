namespace Net.Leksi.Edifact;

public class Segment: Component
{
    public List<Component>? Components { get; set; }
    public string[]? Children { get; set; }
}
