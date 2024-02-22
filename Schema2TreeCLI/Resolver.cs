using System.Web;
using System.Xml;

namespace Net.Leksi.Edifact;

public class Resolver : XmlResolver
{
    public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
    {
        Console.WriteLine($"Resolver: {absoluteUri}, {absoluteUri.Scheme}, {role}, {ofObjectToReturn}");
        if (absoluteUri.Scheme == "file")
        {
            return File.OpenRead(HttpUtility.UrlDecode(absoluteUri.AbsolutePath));
        }
        return null;
    }
}
