using Microsoft.Extensions.DependencyInjection;
using Net.Leksi.Streams;
using System.Xml;

namespace Net.Leksi.Edifact;

public class MessageSchemaCustomizer
{
    private readonly IServiceProvider _services;

    public MessageSchemaCustomizer(IServiceProvider services)
    {
        _services = services;
    }

    public async Task Customize(MessageSchemaCustomizerOptions options)
    {
        XmlNameTable nameTable = new NameTable();
        XmlDocument doc = new(nameTable);
        Uri customUri = new(options.CustomSchemaUri!);
        IStreamFactory customStreamFactory = _services.GetRequiredKeyedService<IStreamFactory>(customUri.Scheme);
        Stream input = customStreamFactory.GetInputStream(customUri);
        doc.Load(input);
        input.Close();

        XmlWriterSettings xws = new() 
        {
            Indent = true,
        };
        XmlWriter writer = XmlWriter.Create(customStreamFactory.GetOutputStream(customUri, FileMode.Create), xws);
        doc.WriteTo(writer);
        writer.Close();
    }

}
