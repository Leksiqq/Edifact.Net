using Microsoft.Extensions.DependencyInjection;
using Net.Leksi.Streams;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.XPath;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

public class EdifactParser(IServiceProvider services)
{
    public async Task Parse(EdifactParserOptions options)
    {
        EdifactTokenizer tokenizer = new();
        if (options.IsStrict is bool strict)
        {
            tokenizer.IsStrict = strict;
        }
        if (options.Encoding is Encoding encoding)
        {
            tokenizer.Encoding = encoding;
        }
        if (options.BufferLength is int bufferSize)
        {
            tokenizer.BufferLength = bufferSize;
        }
        Uri schemas = new($"{options.SchemasUri!}/_");
        Uri input = new(options.InputUri!);
        Uri output = new(options.OutputUri!);
        IStreamFactory schemasStreamFactory = services.GetKeyedService<IStreamFactory>(schemas.Scheme)!;
        IStreamFactory inputStreamFactory = services.GetKeyedService<IStreamFactory>(input.Scheme)!;
        IStreamFactory outputStreamFactory = services.GetKeyedService<IStreamFactory>(output.Scheme)!;
        using TextWriter writer = new StreamWriter(outputStreamFactory.GetOutputStream(output), tokenizer.Encoding!);
        XmlResolver xmlResolver = services.GetRequiredService<Resolver>();
        XmlSchemaSet schemaSet = new()
        {
            XmlResolver = xmlResolver
        };
        using Stream edifact = xmlResolver.GetEntity(new Uri(schemas, s_edifactXsd), null, typeof(Stream)) as Stream
            ?? throw new Exception("TODO: edifact.xsd not found.");
        XmlNameTable nameTable = new NameTable();
        XmlNamespaceManager man = new(nameTable);
        man.AddNamespace(s_xsPrefix, Properties.Resources.schema_ns);
        XmlDocument doc = new(nameTable);
        doc.Load(edifact);
        XPathNavigator nav = doc.CreateNavigator()!;
        if (nav.SelectSingleNode("/xs:schema/@targetNamespace", man) is not XPathNavigator tns)
        {
            throw new Exception("TODO: not schema.");
        }
        string targetNamespace = tns.Value;

        Console.WriteLine(targetNamespace);

        await foreach (SegmentToken token in tokenizer.Tokenize(inputStreamFactory.GetInputStream(input)))
        {
            if (token.ExplcitNestingIndication is { } && token.ExplcitNestingIndication.Count > 0)
            {
                throw new NotImplementedException("TODO: explicit indication of nesting");
            }
            else
            {

            }
        }
    }
}
