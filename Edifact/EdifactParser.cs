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
        XmlDocument? functionalGroup;
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
        man.AddNamespace("e", targetNamespace);

        int segmentPosition = 0;
        Stack<Sequence> sequencesStack = [];

        Stack<XmlSchemaElement> elementsStack = [];

        XmlWriterSettings? xws = null;

        string messageHeader = string.Empty;
        string messageTrailer = string.Empty;
        int messageControlCount = 0;
        bool inMessage = false;

        string functionalGroupReference = string.Empty;
        int functionalGroupControlCount = 0;


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
                    if (tokenizer.IsInteractive)
                    {
                        Uri interactiveUri = new(schemas, s_interactiveInterchangeXsd);
                        schemaSet.Add(targetNamespace, interactiveUri.ToString());
                        schemaSet.Compile();
                        el0 = (XmlSchemaElement)schemaSet.GlobalElements[new XmlQualifiedName("INTERACTIVE_INTERCHANGE", targetNamespace)]!;
                        messageHeader = "UIH";
                        messageTrailer = "UIT";
                    }
                    else
                    {
                        Uri batchUri = new(schemas, s_batchInterchangeXsd);
                        schemaSet.Add(targetNamespace, batchUri.ToString());
                        schemaSet.Compile();
                        el0 = (XmlSchemaElement)schemaSet.GlobalElements[new XmlQualifiedName("BATCH_INTERCHANGE", targetNamespace)]!;
                        messageHeader = "UNH";
                        messageTrailer = "UNT";
                    }
                    if (
                        el0 is { }
                        && el0.ElementSchemaType is XmlSchemaComplexType ct
                        && ct.Particle is XmlSchemaSequence seq
                    )
                    {
                        sequencesStack.Push(new Sequence(seq));
                        if (
                            sequencesStack.Peek().Move()
                        )
                        {
                            await ParseSegmentAsync(token, sequencesStack.Peek().Item, writer!, targetNamespace);
                            await writer!.WriteEndDocumentAsync();
                            writer.Close();
                            Console.WriteLine(sb);
                            interchange.LoadXml(sb.ToString());
                        }
                    }
                    else
                    {
                        throw new Exception($"TODO: expected tag: 'UNB' or 'UIB', got: '{token.Tag}'");
                    }
                }
                else
                {
                    while(!sequencesStack.Peek().Move())
                    {
                        sequencesStack.Pop();
                        await writer!.WriteEndElementAsync();
                    }
                    bool processed = false;
                    if(segmentPosition == 1)
                    {
                        if(sequencesStack.Peek().Item is XmlSchemaChoice choice)
                        {
                            foreach(XmlSchemaObject item in choice.Items)
                            {
                                if(item is XmlSchemaSequence sequence)
                                {
                                    Sequence seq = new(sequence);
                                    if(
                                        seq.Move()
                                        && seq.Item is XmlSchemaElement el 
                                        && el.QualifiedName == new XmlQualifiedName(token.Tag, targetNamespace)
                                    )
                                    {
                                        seq.Reset();
                                        sequencesStack.Push(seq);
                                        break;
                                    }
                                }
                            }
                            if (token.Tag == "UNG")
                            {
                                if (sequencesStack.Peek().Item is XmlSchemaElement el)
                                {
                                    if (el.QualifiedName == new XmlQualifiedName(token.Tag, targetNamespace))
                                    {
                                        StringBuilder sb = new();
                                        writer = XmlWriter.Create(sb, xws);
                                        await writer.WriteStartDocumentAsync();
                                        await ParseSegmentAsync(token, el, writer!, targetNamespace);
                                        await writer!.WriteEndDocumentAsync();
                                        writer.Close();
                                        Console.WriteLine(sb);
                                        functionalGroup = new(nameTable);
                                        functionalGroup.LoadXml(sb.ToString());
                                        functionalGroupReference = functionalGroup.CreateNavigator()!.SelectSingleNode("/e:UNG/e:D0048", man)!.Value;
                                        functionalGroupControlCount = 0;
                                        if(sequencesStack.Peek().Move() && sequencesStack.Peek().Item is XmlSchemaSequence seq)
                                        {
                                            sequencesStack.Push(new Sequence(seq));
                                            processed = true;
                                        }
                                        else
                                        {
                                            throw new Exception($"TODO: expected: {nameof(XmlSchemaSequence)}, got: {sequencesStack.Peek().Item}");
                                        }
                                    }
                                    else {
                                        throw new Exception($"TODO: expected tag: 'UNG', got: '{token.Tag}'");
                                    }
                                }
                                else
                                {
                                    throw new Exception($"TODO: extra tag: {token.Tag}");
                                }
                            }
                        }
                        else
                        {
                            throw new Exception($"TODO: expected: {nameof(XmlSchemaChoice)}, got: {sequencesStack.Peek().Item}");
                        }
                    }
                    if(!processed)
                    {
                        if(!inMessage)
                        {

                        }
                        else
                        {

                        }
                    }
                }

                ++segmentPosition;
                if(segmentPosition > 1)
                {
                    break;
                }
            }
        }
        finally
        {
            writer?.Close();
            writer?.Dispose();
        }
    }

    private async Task ParseSegmentAsync(SegmentToken segment, XmlSchemaObject? obj, XmlWriter writer, string? ns)
    {
        if(obj is XmlSchemaElement el)
        {
            if (
                el.QualifiedName == new XmlQualifiedName(segment.Tag, ns)
                && el.ElementSchemaType is XmlSchemaComplexType ct
                && ct.ContentTypeParticle is XmlSchemaSequence seq
            )
            {
                await writer.WriteStartElementAsync(null, el.QualifiedName.Name, el.QualifiedName.Namespace);
                if (seq.Items.Count > 0)
                {
                    Sequence sequence = new(seq);
                    if (segment.Components is { })
                    {
                        foreach (ComponentToken et in segment.Components)
                        {
                            bool notLast = sequence.Move();
                            if (notLast && sequence.MaxOccurs != 1)
                            {
                                throw new Exception($"TODO: unexpected maxOccurs {sequence.MaxOccurs} at not last component at segment {segment.Tag}");
                            }
                            if ((et.Elements is null || et.Elements.Count == 0) && sequence.Occurs < sequence.MinOccurs)
                            {
                                throw new Exception($"TODO: mandatory element {((XmlSchemaElement)sequence.Item!).Name} missed at segment {segment.Tag}");
                            }
                            if (sequence.MaxOccurs != 1 && sequence.Occurs > sequence.MaxOccurs)
                            {
                                throw new Exception($"TODO: extra element {((XmlSchemaElement)sequence.Item!).Name} at segment {segment.Tag}, maxOccurs: {sequence.MaxOccursString}, occurs: {sequence.Occurs}.");
                            }
                            if (et.Elements is { } && et.Elements.Count > 0)
                            {
                                if (
                                    ((XmlSchemaElement)sequence.Item!).ElementSchemaType is XmlSchemaComplexType ct1
                                    && ct1.ContentModel is XmlSchemaSimpleContent
                                )
                                {
                                    await writer.WriteElementStringAsync(null, ((XmlSchemaElement)sequence.Item).Name!, ns, et.Elements[0]);
                                }
                                else
                                {
                                    await ParseCompositeAsync(segment.Tag!, et, (XmlSchemaElement)sequence.Item, writer, ns);
                                }
                                if (!sequence.IncrementOccurs())
                                {
                                    throw new Exception($"TODO: extra component {((XmlSchemaElement)sequence.Item).Name} at segment {segment.Tag}, maxOccurs: {((XmlSchemaElement)sequence.Item).MaxOccursString}, occurs: {sequence.Occurs}.");
                                }
                            }
                        }
                    }
                }
                else if (segment.Components is { } && segment.Components.Count > 0)
                {
                    throw new Exception($"TODO: unexpected components at segment {segment.Tag}");
                }
                await writer.WriteEndElementAsync();
            }
            else
            {
                throw new Exception($"TODO: unexpected segment {segment.Tag}");
            }
        }
        else
        {
            throw new Exception($"TODO: expected: {nameof(XmlSchemaElement)}, got: {obj}");
        }
    }

    private async Task ParseCompositeAsync(string tag, ComponentToken token, XmlSchemaElement el, XmlWriter writer, string? ns)
    {
        if (
            el.ElementSchemaType is XmlSchemaComplexType ct
            && ct.ContentTypeParticle is XmlSchemaSequence seq
        )
        {
            await writer.WriteStartElementAsync(null, el.QualifiedName.Name, el.QualifiedName.Namespace);
            if (seq.Items.Count > 0)
            {
                Sequence sequence = new(seq);
                if (token.Elements is { })
                {
                    foreach (string value in token.Elements)
                    {
                        bool notLast = sequence.Move();
                        if (notLast && sequence.MaxOccurs != 1)
                        {
                            throw new Exception($"TODO: unexpected maxOccurs {sequence.MaxOccurs} at not last element at composite {el.Name} at segment {tag}");
                        }
                        
                        if (string.IsNullOrEmpty(value) && sequence.Occurs < sequence.MinOccurs)
                        {
                            throw new Exception($"TODO: mandatory element {((XmlSchemaElement)sequence.Item!).Name} missed at composite {el.Name} at segment {tag}");
                        }
                        if (!string.IsNullOrEmpty(value))
                        {
                            await writer.WriteElementStringAsync(null, ((XmlSchemaElement)sequence.Item!).Name!, ns, value);
                            if (!sequence.IncrementOccurs())
                            {
                                throw new Exception($"TODO: extra element {((XmlSchemaElement)sequence.Item).Name} at composite {el.Name} at segment {tag}, maxOccurs: {((XmlSchemaElement)sequence.Item).MaxOccursString}, occurs: {sequence.Occurs}.");
                            }
                        }
                    }
                }
            }
            else if (token.Elements is { } && token.Elements.Count > 0)
            {
                throw new Exception($"TODO: unexpected elements at composite {el.Name} at segment {tag}");
            }
            await writer.WriteEndElementAsync();
        }
        else
        {
            throw new Exception($"TODO: unexpected composite");
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
