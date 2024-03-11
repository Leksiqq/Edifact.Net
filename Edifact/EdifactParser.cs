using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Net.Leksi.Streams;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.XPath;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

public class EdifactParser
{
    public event MessageEventHandler? Message;

    private static readonly Regex s_reSegmentGroup = new("^SG\\d+$");
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
        XmlDocument interchangeHeaderXml = new(nameTable);
        XmlDocument interchangeTrailerXml = new(nameTable);
        XmlDocument groupHeaderXml = new(nameTable);
        XmlDocument groupTrailerXml = new(nameTable);
        XmlDocument messageHeaderXml = new(nameTable);
        XmlDocument messageTrailerXml = new(nameTable);
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
        List<Sequence> sequencesStack = [];
        List<string> path = [];

        Stack<XmlSchemaElement> elementsStack = [];

        XmlWriterSettings? xws = null;

        string messageHeader = string.Empty;
        string messageTrailer = string.Empty;
        string messageXsd = string.Empty;
        MessageEventArgs? messageEventArgs = null;
        Dictionary<string, XmlSchema> messageSchemaCache = [];
        XmlSchema? messageSchema = null;
        int messageControlCount = 0;
        bool inMessage = false;
        bool inGroup = false;

        string groupReference = string.Empty;
        int groupControlCount = 0;

        int interchangeControlCount = 0;

        StringBuilder sb = new();

        await foreach (SegmentToken segment in tokenizer.Tokenize(inputStreamFactory.GetInputStream(input)))
        {
            if (segment.ExplcitNestingIndication is { } && segment.ExplcitNestingIndication.Count > 0)
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
                sb.Clear();
                writer = XmlWriter.Create(sb, xws);

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
                    sequencesStack.Add(new Sequence(el0.Name!, seq));
                    if (
                        sequencesStack.Last().Move()
                    )
                    {
                        await ParseSegmentAsync(segment, sequencesStack.Last().Item, writer!, targetNamespace);
                        await writer!.WriteEndDocumentAsync();
                        writer.Close();
                        interchangeHeaderXml.LoadXml(sb.ToString());
                    }
                }
                else
                {
                    throw new Exception($"TODO: expected tag: 'UNB' or 'UIB', got: '{segment.Tag}'");
                }
            }
            else
            {
                if (segmentPosition == 1)
                {
                    if (
                        sequencesStack.Last().Move()
                        && sequencesStack.Last().Item is XmlSchemaChoice choice
                    )
                    {
                        foreach (XmlSchemaObject item in choice.Items)
                        {
                            if (item is XmlSchemaSequence sequence)
                            {
                                Sequence seq = new(string.Empty, sequence);
                                if (
                                    seq.Move()
                                    && seq.Item is XmlSchemaElement el
                                    && el.QualifiedName == new XmlQualifiedName(segment.Tag, targetNamespace)
                                )
                                {
                                    seq.Reset();
                                    sequencesStack.Add(seq);
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        throw new Exception($"TODO: expected: {nameof(XmlSchemaChoice)}, got: {sequencesStack.Last().Item}");
                    }
                }
                if (!inMessage)
                {
                    if (segment.Tag == "UNG")
                    {
                        if (
                            sequencesStack.Last().MoveIfNeed()
                            && sequencesStack.Last().Item is XmlSchemaElement el
                        )
                        {
                            if (el.QualifiedName == new XmlQualifiedName(segment.Tag, targetNamespace))
                            {
                                sb.Clear();
                                writer = XmlWriter.Create(sb, xws);
                                await writer.WriteStartDocumentAsync();
                                await ParseSegmentAsync(segment, el, writer!, targetNamespace);
                                await writer!.WriteEndDocumentAsync();
                                writer.Close();
                                groupHeaderXml.LoadXml(sb.ToString());
                                groupReference = groupHeaderXml.CreateNavigator()!.SelectSingleNode("/e:UNG/e:D0048", man)!.Value;
                                groupControlCount = 0;
                                inGroup = true;
                                ++interchangeControlCount;
                                if (sequencesStack.Last().Move() && sequencesStack.Last().Item is XmlSchemaSequence seq)
                                {
                                    sequencesStack.Add(new Sequence(el.Name!, seq));
                                }
                                else
                                {
                                    throw new Exception($"TODO: expected: {nameof(XmlSchemaSequence)}, got: {sequencesStack.Last().Item}");
                                }
                            }
                            else
                            {
                                throw new Exception($"TODO: expected tag: 'UNG', got: '{segment.Tag}'");
                            }
                        }
                        else
                        {
                            throw new Exception($"TODO: extra tag: {segment.Tag}");
                        }
                    }
                    else if (segment.Tag == "UNE")
                    {
                        if (inGroup)
                        {
                            sequencesStack.RemoveAt(sequencesStack.Count - 1);
                            if (
                                sequencesStack.Last().Move()
                                && sequencesStack.Last().Item is XmlSchemaElement el
                            )
                            {
                                if (el.QualifiedName == new XmlQualifiedName(segment.Tag, targetNamespace))
                                {
                                    sb.Clear();
                                    writer = XmlWriter.Create(sb, xws);
                                    await writer.WriteStartDocumentAsync();
                                    await ParseSegmentAsync(segment, el, writer!, targetNamespace);
                                    await writer!.WriteEndDocumentAsync();
                                    writer.Close();
                                    groupTrailerXml.LoadXml(sb.ToString());
                                    sequencesStack.Last().Reset();
                                    inGroup = false;
                                    int expectedMessages = groupTrailerXml.CreateNavigator()
                                }
                                else
                                {
                                    throw new Exception($"TODO: expected tag: 'UNE', got: '{segment.Tag}'");
                                }
                            }
                            else
                            {
                                throw new Exception($"TODO: extra tag: {segment.Tag}");
                            }
                        }
                        else
                        {
                            throw new Exception($"TODO: unexpected tag: '{segment.Tag}'.");
                        }
                    }
                    else if (segment.Tag == messageHeader)
                    {
                        if (
                            sequencesStack.Last().Move()
                            && sequencesStack.Last().Item is XmlSchemaElement el
                        )
                        {
                            if (el.QualifiedName == new XmlQualifiedName(segment.Tag, targetNamespace))
                            {
                                sb.Clear();
                                writer = XmlWriter.Create(sb, xws);
                                await writer.WriteStartDocumentAsync();
                                await ParseSegmentAsync(segment, el, writer!, targetNamespace);
                                await writer!.WriteEndDocumentAsync();
                                writer.Close();
                                messageHeaderXml.LoadXml(sb.ToString());

                                string s = tokenizer.IsInteractive ? "S306" : "S009";
                                string s1 = tokenizer.IsInteractive ? "D0340" : "D0062";
                                messageEventArgs ??= new MessageEventArgs();
                                messageEventArgs.EventKind = MessageEventKind.Start;
                                messageEventArgs.MessageReferenceNumber = messageHeaderXml.CreateNavigator()!.SelectSingleNode(string.Format("/e:{0}/e:{1}", messageHeader, s1), man)!.Value;
                                XPathNavigator nav1 = messageHeaderXml.CreateNavigator()!.SelectSingleNode(string.Format("/e:{0}/e:{1}", messageHeader, s), man)!;
                                messageEventArgs.MessageType = nav1.SelectSingleNode("e:D0065", man)!.Value;
                                messageEventArgs.MessageVersion = nav1.SelectSingleNode("e:D0052", man)!.Value;
                                messageEventArgs.MessageRelease = nav1.SelectSingleNode("e:D0054", man)!.Value;
                                messageEventArgs.ControllingAgencyCoded = nav1.SelectSingleNode("e:D0051", man)?.Value ?? "UN";
                                messageEventArgs.InterchangeHeader = new XmlDocument(nameTable);
                                messageEventArgs.InterchangeHeader.LoadXml(interchangeHeaderXml.OuterXml);
                                if (inGroup)
                                {
                                    messageEventArgs.GroupHeader = new XmlDocument(nameTable);
                                    messageEventArgs.GroupHeader.LoadXml(groupHeaderXml.OuterXml);
                                }
                                messageEventArgs.MessageHeader = new XmlDocument(nameTable);
                                messageEventArgs.MessageHeader.LoadXml(messageHeaderXml.OuterXml);

                                Message?.Invoke(this, messageEventArgs);

                                messageXsd = string.Format(
                                    "{0}/{1}{2}/{3}.xsd",
                                    messageEventArgs.ControllingAgencyCoded,
                                    messageEventArgs.MessageVersion,
                                    messageEventArgs.MessageRelease,
                                    messageEventArgs.MessageType
                                );

                                if (messageSchema is { })
                                {
                                    if (messageSchemaCache[messageEventArgs.MessageType] != messageSchema)
                                    {
                                        schemaSet.Remove(messageSchema);
                                        messageSchema = schemaSet.Add(messageSchemaCache[messageEventArgs.MessageType]);
                                        schemaSet.Compile();
                                    }
                                }
                                else
                                {
                                    messageSchema = schemaSet.Add(targetNamespace, new Uri(schemas, messageXsd).ToString());
                                    messageSchemaCache.Add(messageEventArgs.MessageType, messageSchema!);
                                    schemaSet.Compile();
                                }

                                if (schemaSet.GlobalTypes[new XmlQualifiedName("MESSAGE", targetNamespace)] is XmlSchemaComplexType messageType)
                                {
                                    sequencesStack.Last().Move();
                                    sequencesStack.Last().IncrementOccurs();
                                    sequencesStack.Add(new Sequence(messageType.Name!, (XmlSchemaSequence)messageType.ContentTypeParticle));
                                }
                                if (messageEventArgs.Stream is null)
                                {
                                    sb.Clear();
                                    writer = XmlWriter.Create(sb, xws);
                                }
                                else
                                {
                                    writer = XmlWriter.Create(messageEventArgs.Stream, xws);
                                }
                                await writer.WriteStartDocumentAsync();
                                path.Add(tokenizer.IsInteractive ? "INTERACTIVE_INTERCHANGE" : "BATCH_INTERCHANGE");
                                await writer.WriteStartElementAsync(
                                    null,
                                    path.Last(),
                                    targetNamespace
                                );
                                List<XmlDocument> headers = [interchangeHeaderXml];
                                if (inGroup)
                                {
                                    headers.Add(groupHeaderXml);
                                }
                                headers.Add(messageHeaderXml);
                                foreach (XmlDocument h in headers)
                                {
                                    XPathNodeIterator ni = h.CreateNavigator()!.Select("//e:*", man);
                                    Stack<XPathNavigator> tree = [];
                                    while (ni.MoveNext())
                                    {
                                        while (tree.TryPeek(out XPathNavigator? nav2))
                                        {
                                            XPathNavigator nav3 = ni.Current!.CreateNavigator();
                                            if (!nav3.MoveToParent() || nav3.LocalName != nav2.LocalName || nav3.NamespaceURI != nav2.NamespaceURI)
                                            {
                                                tree.Pop();
                                                path.RemoveAt(path.Count - 1);
                                                await writer.WriteEndElementAsync();
                                            }
                                            else
                                            {
                                                break;
                                            }
                                        }
                                        if (ni.Current!.Evaluate("count(e:*)", man).ToString() == "0")
                                        {
                                            await writer.WriteElementStringAsync(null, ni.Current!.LocalName, ni.Current.NamespaceURI, ni.Current!.Value);
                                        }
                                        else
                                        {
                                            path.Add(ni.Current!.LocalName);
                                            await writer.WriteStartElementAsync(null, ni.Current!.LocalName, ni.Current.NamespaceURI);
                                            tree.Push(ni.Current!.CreateNavigator());
                                        }
                                    }
                                    while (tree.Count > 0)
                                    {
                                        path.RemoveAt(path.Count - 1);
                                        await writer.WriteEndElementAsync();
                                        tree.Pop();
                                    }
                                }
                                path.Add("MESSAGE");
                                await writer.WriteStartElementAsync(null, path.Last(), targetNamespace);
                                inMessage = true;
                                messageControlCount = 1;
                                if (inGroup)
                                {
                                    ++groupControlCount;
                                }
                                else
                                {
                                    ++interchangeControlCount;
                                }
                            }
                            else
                            {
                                throw new Exception($"TODO: expected tag: '{((XmlSchemaElement)sequencesStack.Last().Item!).Name}', got: '{segment.Tag}'");
                            }
                        }
                        else
                        {
                            throw new Exception($"TODO: extra tag: {segment.Tag}");
                        }
                    }
                    else
                    {
                        throw new Exception($"TODO: unexpected tag: '{segment.Tag}'");
                    }
                }
                else
                {
                    if (segment.Tag == messageTrailer)
                    {
                        while(sequencesStack.Last().Name != "MESSAGE")
                        {
                            path.RemoveAt(path.Count - 1);
                            await writer!.WriteEndElementAsync();
                            sequencesStack.RemoveAt(sequencesStack.Count - 1);
                        }
                        sequencesStack.RemoveAt(sequencesStack.Count - 1);
                        if (
                            sequencesStack.Last().Move()
                            && sequencesStack.Last().Item is XmlSchemaElement el
                        )
                        {
                            if (el.QualifiedName == new XmlQualifiedName(segment.Tag, targetNamespace))
                            {
                                path.RemoveAt(path.Count - 1);
                                await writer!.WriteEndElementAsync();
                                await ParseSegmentAsync(segment, el, writer, targetNamespace);
                                path.RemoveAt(path.Count - 1);
                                await writer.WriteEndElementAsync();
                                await writer.WriteEndDocumentAsync();
                                writer.Close();
                                if (messageEventArgs!.Stream is { })
                                {
                                    messageEventArgs!.Stream.Close();
                                }
                                else
                                {
                                    messageEventArgs!.Xml = sb.ToString();
                                }
                                messageEventArgs.EventKind = MessageEventKind.End;
                                Message?.Invoke(this, messageEventArgs);
                                Console.WriteLine(sb);

                                sb.Clear();
                                writer = XmlWriter.Create(sb, xws);
                                await writer.WriteStartDocumentAsync();
                                await ParseSegmentAsync(segment, el, writer!, targetNamespace);
                                await writer!.WriteEndDocumentAsync();
                                writer.Close();
                                sequencesStack.Last().Reset();
                                inMessage = false;
                                messageTrailerXml.LoadXml(sb.ToString());
                                int expectedSegments = messageTrailerXml.CreateNavigator()!
                                        .SelectSingleNode(string.Format("/e:{0}/e:D0074", messageTrailer), man)!.ValueAsInt;
                                if (expectedSegments != messageControlCount)
                                {
                                    throw new Exception($"TODO: expected number of segments: {expectedSegments}, got: {messageControlCount}.");
                                }
                            }
                            else
                            {
                                throw new Exception($"TODO: expected tag: '{((XmlSchemaElement)sequencesStack.Last().Item!).Name}', got: '{segment.Tag}'");
                            }
                        }
                        else
                        {
                            throw new Exception($"TODO: extra tag: {segment.Tag}");
                        }
                    }
                    else if (segment.Tag == "UNG" || segment.Tag == "UNE" || segment.Tag == "UNH" || segment.Tag == "UIH")
                    {
                        throw new Exception($"TODO: unexpected tag: '{segment.Tag}'");
                    }
                    else
                    {
                        while (true)
                        {
                            if (sequencesStack.Last().MoveIfNeed())
                            {
                                if (sequencesStack.Last().Item is XmlSchemaElement el)
                                {
                                    if (s_reSegmentGroup.IsMatch(((XmlSchemaElement)sequencesStack.Last().Item!).Name!))
                                    {
                                        sequencesStack.Add(
                                            new Sequence(
                                                ((XmlSchemaElement)sequencesStack.Last().Item!).Name!,
                                                (XmlSchemaSequence)(
                                                    (XmlSchemaComplexType)(
                                                        (XmlSchemaElement)sequencesStack.Last().Item!
                                                    ).ElementSchemaType!
                                                ).ContentTypeParticle
                                            )
                                        );
                                    }
                                    else if (el.QualifiedName == new XmlQualifiedName(segment.Tag, targetNamespace))
                                    {
                                        int i = sequencesStack.Count - 1;
                                        for (; i >= 0 && path.Last() != ((XmlSchemaElement)sequencesStack[i].Item!).Name; --i) { }

                                        for (++i; i < sequencesStack.Count - 1; ++i)
                                        {
                                            sequencesStack[i].IncrementOccurs();
                                            path.Add(((XmlSchemaElement)sequencesStack[i].Item!).Name!);
                                            await writer!.WriteStartElementAsync(null, path.Last(), targetNamespace);
                                        }
                                        sequencesStack[i].IncrementOccurs();
                                        await ParseSegmentAsync(segment, el, writer!, targetNamespace);
                                        await writer!.FlushAsync();
                                        break;
                                    }
                                    else
                                    {
                                        if (sequencesStack.Last().State is SequenceState.ShouldOccur)
                                        {
                                            if (!sequencesStack.Last().IsFirst)
                                            {
                                                throw new Exception($"TODO: expected tag: '{((XmlSchemaElement)sequencesStack.Last().Item!).Name}', got: '{segment.Tag}'");
                                            }
                                            if(path.Last() == sequencesStack.Last().Name)
                                            {
                                                path.RemoveAt(path.Count - 1);
                                                await writer!.WriteEndElementAsync();
                                            }
                                            sequencesStack.RemoveAt(sequencesStack.Count - 1);
                                        }
                                        if (!sequencesStack.Last().Move())
                                        {
                                            if (sequencesStack.Last().IsLast)
                                            {
                                                if (path.Last() == sequencesStack.Last().Name)
                                                {
                                                    path.RemoveAt(path.Count - 1);
                                                    await writer!.WriteEndElementAsync();
                                                }
                                                sequencesStack.RemoveAt(sequencesStack.Count - 1);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    throw new Exception($"TODO: expected: {nameof(XmlSchemaElement)}, got: {sequencesStack.Last().Item}");
                                }
                            }
                            else
                            {
                                if (path.Last() == sequencesStack.Last().Name)
                                {
                                    path.RemoveAt(path.Count - 1);
                                    await writer!.WriteEndElementAsync();
                                }
                                sequencesStack.RemoveAt(sequencesStack.Count - 1);
                            }
                        }
                    }
                }
            }

            ++messageControlCount;
            ++segmentPosition;
        }
    }

    private async Task ParseSegmentAsync(SegmentToken segment, XmlSchemaObject? obj, XmlWriter writer, string? ns)
    {
        if (obj is XmlSchemaElement el)
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
                    Sequence sequence = new(el.Name!, seq);
                    if (segment.Components is { })
                    {
                        foreach (ComponentToken et in segment.Components)
                        {
                            sequence.Move();
                            if (!sequence.IsLast && sequence.MaxOccurs != 1)
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
                Sequence sequence = new(el.Name!, seq);
                if (token.Elements is { })
                {
                    foreach (string value in token.Elements)
                    {
                        sequence.Move();
                        //if (!sequence.IsLast && sequence.MaxOccurs != 1)
                        //{
                        //    throw new Exception($"TODO: unexpected maxOccurs {sequence.MaxOccurs} at not last element at composite {el.Name} at segment {tag}");
                        //}

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
