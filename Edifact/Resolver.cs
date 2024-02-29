using Microsoft.Extensions.DependencyInjection;
using Net.Leksi.Streams;
using System.Xml;

namespace Net.Leksi.Edifact;

public class Resolver(IServiceProvider services) : XmlResolver
{
    public override object? GetEntity(Uri uri, string? role, Type? ofObjectToReturn)
    {
        IStreamFactory? streamFactory = services.GetKeyedService<IStreamFactory>(uri.Scheme);
        return streamFactory?.GetInputStream(uri);
    }
}
