using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Net.Leksi.Streams;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.XPath;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

public class EdifactParser
{
    public event MessageEventHandler? Message;
    private readonly IServiceProvider _services;
    private readonly ILogger<EdifactParser>? _logger;
    public EdifactParser(IServiceProvider services)
    {
        _services = services;
        _logger = _services.GetService<ILogger<EdifactParser>>();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

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
        IStreamFactory schemasStreamFactory = _services.GetKeyedService<IStreamFactory>(schemas.Scheme)!;
        IStreamFactory inputStreamFactory = _services.GetKeyedService<IStreamFactory>(input.Scheme)!;
        XmlWriter? writer = null;
        XmlResolver xmlResolver = _services.GetRequiredService<Resolver>();
        Uri edifactUri = new(schemas, s_edifactXsd);
        using Stream edifact = xmlResolver.GetEntity(edifactUri, null, typeof(Stream)) as Stream
            ?? throw new Exception("TODO: edifact.xsd not found.");
        XmlNameTable nameTable = new NameTable();
        XmlNamespaceManager man = new(nameTable);
        XmlDocument interchange = new(nameTable);
        XmlSchemaSet schemaSet = new(nameTable)
        {
            XmlResolver = xmlResolver
        };
        schemaSet.ValidationEventHandler += SchemaSet_ValidationEventHandler;
        man.AddNamespace(s_xsPrefix, Properties.Resources.schema_ns);
        XmlDocument doc = new(nameTable);
        doc.Load(edifact);
        XPathNavigator nav = doc.CreateNavigator()!;
        if (nav.SelectSingleNode("/xs:schema/@targetNamespace", man) is not XPathNavigator tns)
        {
            throw new Exception("TODO: not schema.");
        }
        string targetNamespace = tns.Value;

        int segmentPosition = 0;

        Stack<XmlSchemaElement> elementsStack = [];

        XmlWriterSettings? xws = null;

        try
        {
            await foreach (SegmentToken token in tokenizer.Tokenize(inputStreamFactory.GetInputStream(input)))
            {
                if (token.ExplcitNestingIndication is { } && token.ExplcitNestingIndication.Count > 0)
                {
                    throw new NotImplementedException("TODO: explicit indication of nesting");
                }
                if (segmentPosition == 0)
                {
                    xws = new()
                    {
                        Async = true,
                        Encoding = tokenizer.Encoding!,
                        Indent = true,
                    };
                    StringBuilder sb = new();
                    writer = XmlWriter.Create(sb, xws);

                    //writer = XmlWriter.Create(outputStreamFactory.GetOutputStream(output), xws);
                    await writer.WriteStartDocumentAsync();
                    XmlSchemaElement? el0 = null;
                    if (token.Tag == "UNB")
                    {
                        Uri batchUri = new(schemas, s_batchInterchangeXsd);
                        schemaSet.Add(targetNamespace, batchUri.ToString());
                        schemaSet.Compile();
                        el0 = (XmlSchemaElement)schemaSet.GlobalElements[new XmlQualifiedName("BATCH_INTERCHANGE", targetNamespace)]!;
                    }
                    else if (token.Tag == "UIB")
                    {
                        Uri interactiveUri = new(schemas, s_interactiveInterchangeXsd);
                        schemaSet.Add(targetNamespace, interactiveUri.ToString());
                        schemaSet.Compile();
                        el0 = (XmlSchemaElement)schemaSet.GlobalElements[new XmlQualifiedName("INTERACTIVE_INTERCHANGE", targetNamespace)]!;
                    }
                    if (
                        el0 is { }
                        && el0.ElementSchemaType is XmlSchemaComplexType ct
                        && ct.Particle is XmlSchemaSequence seq
                        && seq.Items.Count > 0
                        && seq.Items[0] is XmlSchemaElement el
                        && el.QualifiedName == new XmlQualifiedName(token.Tag, targetNamespace)
                    )
                    {
                        await writer.WriteStartElementAsync(null, el0.Name!, targetNamespace);
                        Action<string, string?>? action = (path, value) =>
                        {
                            Console.WriteLine($"{path}: {value}");
                        };
                        await ParseSegmentAsync(token, el, writer!, action);
                        await writer!.WriteEndElementAsync();
                        await writer!.WriteEndDocumentAsync();
                        writer.Close();
                        Console.WriteLine(sb);
                        interchange.LoadXml(sb.ToString());
                    }
                    else
                    {
                        throw new Exception($"TODO: expected tag: 'UNB' or 'UIB', got: {token.Tag}");
                    }
                }
                else
                {

                }

                ++segmentPosition;
                break;
            }
        }
        finally
        {
            writer?.Close();
            writer?.Dispose();
        }
    }

    private async Task ParseSegmentAsync(SegmentToken token, XmlSchemaElement el, XmlWriter writer, Action<string, string?>? action)
    {
        if (
            el.ElementSchemaType is XmlSchemaComplexType ct
            && ct.ContentTypeParticle is XmlSchemaSequence seq
        )
        {
            await writer.WriteStartElementAsync(null, el.QualifiedName.Name, el.QualifiedName.Namespace);
            action?.Invoke(el.QualifiedName.Name, null);
            if(seq.Items.Count > 0)
            {
                int pos = 0;
                int occurs = 0;
                int minOccurs = (int)((XmlSchemaElement)seq.Items[pos]).MinOccurs;
                int maxOccurs = ((XmlSchemaElement)seq.Items[pos]).MaxOccursString == "unbounded" ? -1 : (int)((XmlSchemaElement)seq.Items[pos]).MaxOccurs;
                if (token.Elements is { })
                {
                    foreach (ElementToken et in token.Elements)
                    {
                        if (et.Components is null || et.Components.Count == 0 && occurs < minOccurs)
                        {
                            throw new Exception($"TODO: mandatory element {((XmlSchemaElement)seq.Items[pos]).Name} missed at segment {token.Tag}");
                        }
                        if (maxOccurs != -1 &&  occurs > maxOccurs)
                        {
                            throw new Exception($"TODO: extra element {((XmlSchemaElement)seq.Items[pos]).Name} at segment {token.Tag}, maxOccurs: {((XmlSchemaElement)seq.Items[pos]).MaxOccursString}, occurs: {occurs}.");
                        }
                    }
                }
            }
            else if(token.Elements is { } && token.Elements.Count > 0)
            {
                throw new Exception($"TODO: unexpected elements at segment {token.Tag}");
            }
            await writer.WriteEndElementAsync();
        }
    }

    private void SchemaSet_ValidationEventHandler(object? sender, ValidationEventArgs e)
    {
        switch (e.Severity)
        {
            case XmlSeverityType.Warning:
                _logger?.LogWarning(s_logMessage, e.Message);
                break;
            case XmlSeverityType.Error:
                _logger?.LogWarning(s_logMessage, e.Message);
                break;
        }
    }
}
