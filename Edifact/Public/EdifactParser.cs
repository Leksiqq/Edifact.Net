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
    private int _entersNum = 0;
    private readonly StringBuilder _sb = new();
    private readonly XmlResolver _xmlResolver;
    private readonly Dictionary<string, XmlSchema> _messageSchemaCache = [];
    private readonly HashSet<string> _validationWarningsCache = [];
    private readonly List<Sequence> _sequencesStack = [];
    private readonly List<string> _path = [];
    private EdifactParserOptions _options = null!;
    private XmlSchemaSet _schemaSet = null!;
    private XmlNameTable _nameTable = null!;
    private XmlDocument _interchangeHeaderXml = null!;
    private XmlDocument _interchangeTrailerXml = null!;
    private XmlDocument _groupHeaderXml = null!;
    private XmlDocument _groupTrailerXml = null!;
    private XmlDocument _messageHeaderXml = null!;
    private XmlDocument _messageTrailerXml = null!;
    private XmlDocument _elementXml = null!;
    private XmlNamespaceManager _man = null!;
    private XmlWriterSettings _xws = null!;
    private EdifactTokenizer _tokenizer = null!;
    private XmlWriter _writer = null!;
    private Uri _schemas = null!;
    private string _targetNamespace = string.Empty;
    private string _messageHeader = string.Empty;
    private string _messageTrailer = string.Empty;
    private string _interchangeTrailer = string.Empty;
    private string _messageXsd = string.Empty;
    private MessageEventArgs _messageEventArgs = null!;
    private XmlSchema? _messageSchema = null;

    private bool _inMessage = false;
    private bool _inGroup = false;
    private int _messageControlCount = 0;
    private string _groupReference = string.Empty;
    private int _groupControlCount = 0;
    private int _interchangeControlCount = 0;

    public EdifactParser(IServiceProvider services)
    {
        _services = services;
        _logger = _services.GetService<ILogger<EdifactParser>>();
        _xmlResolver = new Resolver(_services);
        
}
    public async Task Parse(EdifactParserOptions options, CancellationToken? cancellationToken)
    {
        try
        {
            _options = options;
            if(Interlocked.Increment(ref _entersNum) != 1)
            {
                throw new Exception("TODO: Thread unsafety.");
            }
            _tokenizer = new();
            if (options.IsStrict is bool strict)
            {
                _tokenizer.IsStrict = strict;
            }
            if (options.Encoding is Encoding encoding)
            {
                _tokenizer.Encoding = encoding;
            }
            if (options.BufferLength is int bufferSize)
            {
                _tokenizer.BufferLength = bufferSize;
            }
            _nameTable = new NameTable();
            _interchangeHeaderXml = new XmlDocument(_nameTable);
            _interchangeTrailerXml = new XmlDocument(_nameTable);
            _groupHeaderXml = new XmlDocument(_nameTable);
            _groupTrailerXml = new XmlDocument(_nameTable);
            _messageHeaderXml = new XmlDocument(_nameTable);
            _messageTrailerXml = new XmlDocument(_nameTable);
            _elementXml = new XmlDocument(_nameTable);
            _elementXml.LoadXml(s_placeholderElement);
            _schemaSet = new(_nameTable)
            {
                XmlResolver = _xmlResolver
            };
            _elementXml.Schemas = _schemaSet;
            _schemaSet.ValidationEventHandler += SchemaSet_ValidationEventHandler;
            _schemas = new(string.Format(s_folderUriFormat, options.SchemasUri!));
            _messageSchemaCache.Clear();
            _sequencesStack.Clear();
            _path.Clear();
            _inMessage = false;
            _inGroup = false;
            _messageControlCount = 0;
            _groupReference = string.Empty;
            _groupControlCount = 0;
            _interchangeControlCount = 0;
            _messageSchema = null;

            Uri input = new(options.InputUri!);
            IStreamFactory schemasStreamFactory = _services.GetKeyedService<IStreamFactory>(_schemas.Scheme)!;
            IStreamFactory inputStreamFactory = _services.GetKeyedService<IStreamFactory>(input.Scheme)!;
            Uri edifactUri = new(_schemas, s_edifactXsd);
            using Stream edifact = _xmlResolver.GetEntity(edifactUri, null, typeof(Stream)) as Stream
                ?? throw new Exception("TODO: edifact.xsd not found.");
            _man = new(_nameTable);
            _man.AddNamespace(s_xsPrefix, Properties.Resources.schema_ns);
            _man.AddNamespace("xsi", Properties.Resources.schema_instance_ns);

            XmlDocument doc = new(_nameTable);
            doc.Load(edifact);
            XPathNavigator nav = doc.CreateNavigator()!;
            if (nav.SelectSingleNode(s_targetNamespaceXPath1, _man) is not XPathNavigator tns)
            {
                throw new Exception("TODO: not schema.");
            }
            _targetNamespace = tns.Value;
            _man.AddNamespace("e", _targetNamespace);

            int segmentPosition = 0;

            await foreach (SegmentToken segment in _tokenizer.Tokenize(inputStreamFactory.GetInputStream(input)))
            {
                cancellationToken?.ThrowIfCancellationRequested();

                if (segment.ExplcitNestingIndication is { } && segment.ExplcitNestingIndication.Count > 0)
                {
                    throw new NotImplementedException("TODO: explicit indication of nesting");
                }
                if (segmentPosition == 0)
                {
                    await ProcessInterchangeHeader(segment);
                }
                else
                {
                    if (segmentPosition == 1)
                    {
                        SelectProperSequence(segment);
                    }
                    if (!_inMessage)
                    {
                        if (segment.Tag == s_ung)
                        {
                            await ProcessGroupHeader(segment);
                        }
                        else if (segment.Tag == s_une)
                        {
                            await ProcessGroupTraier(segment);
                        }
                        else if (segment.Tag == _messageHeader)
                        {
                            await ProcessMessageHeader(segment);
                        }
                        else if (segment.Tag == _interchangeTrailer)
                        {
                            await ProcessInterchangeTrailer(segment);
                        }
                        else
                        {
                            throw new Exception($"TODO: unexpected tag: '{segment.Tag}'");
                        }
                    }
                    else
                    {
                        if (segment.Tag == _messageTrailer)
                        {
                            await ProcessMessageTrailer(segment);
                        }
                        else if (segment.Tag == s_ung || segment.Tag == s_une || segment.Tag == s_unh || segment.Tag == s_uih)
                        {
                            throw new Exception($"TODO: unexpected tag: '{segment.Tag}'");
                        }
                        else
                        {
                            await ProcessSegment(segment);
                        }
                    }
                }

                ++_messageControlCount;
                ++segmentPosition;
            }
        }
        finally
        {
            Interlocked.Decrement(ref _entersNum);
        }
    }

    private async Task ProcessSegment(SegmentToken segment)
    {
        while (true)
        {
            if (_sequencesStack.Last().MoveIfNeed())
            {
                if (_sequencesStack.Last().Item is XmlSchemaElement el)
                {
                    if (s_reSegmentGroup.IsMatch(((XmlSchemaElement)_sequencesStack.Last().Item!).Name!))
                    {
                        _sequencesStack.Add(
                            new Sequence(
                                ((XmlSchemaElement)_sequencesStack.Last().Item!).Name!,
                                (XmlSchemaSequence)(
                                    (XmlSchemaComplexType)(
                                        (XmlSchemaElement)_sequencesStack.Last().Item!
                                    ).ElementSchemaType!
                                ).ContentTypeParticle
                            )
                        );
                    }
                    else if (el.QualifiedName == new XmlQualifiedName(segment.Tag, _targetNamespace))
                    {
                        int i = _sequencesStack.Count - 1;
                        for (; i >= 0 && _path.Last() != ((XmlSchemaElement)_sequencesStack[i].Item!).Name; --i) { }

                        for (++i; i < _sequencesStack.Count - 1; ++i)
                        {
                            _sequencesStack[i].IncrementOccurs();
                            _path.Add(((XmlSchemaElement)_sequencesStack[i].Item!).Name!);
                            await _writer!.WriteStartElementAsync(null, _path.Last(), _targetNamespace);
                        }
                        _sequencesStack[i].IncrementOccurs();
                        await ParseSegmentAsync(segment, el);
                        break;
                    }
                    else
                    {
                        if (_sequencesStack.Last().State is SequenceState.ShouldOccur)
                        {
                            if (!_sequencesStack.Last().IsFirst)
                            {
                                throw new Exception($"TODO: expected tag: '{((XmlSchemaElement)_sequencesStack.Last().Item!).Name}', got: '{segment.Tag}'");
                            }
                            if (_path.Last() == _sequencesStack.Last().Name)
                            {
                                _path.RemoveAt(_path.Count - 1);
                                await _writer!.WriteEndElementAsync();
                            }
                            _sequencesStack.RemoveAt(_sequencesStack.Count - 1);
                        }
                        if (!_sequencesStack.Last().Move())
                        {
                            if (_sequencesStack.Last().IsLast)
                            {
                                if (_path.Last() == _sequencesStack.Last().Name)
                                {
                                    _path.RemoveAt(_path.Count - 1);
                                    await _writer!.WriteEndElementAsync();
                                }
                                _sequencesStack.RemoveAt(_sequencesStack.Count - 1);
                            }
                        }
                    }
                }
                else
                {
                    throw new Exception($"TODO: expected: {nameof(XmlSchemaElement)}, got: {_sequencesStack.Last().Item}");
                }
            }
            else
            {
                if (_path.Last() == _sequencesStack.Last().Name)
                {
                    _path.RemoveAt(_path.Count - 1);
                    await _writer!.WriteEndElementAsync();
                }
                _sequencesStack.RemoveAt(_sequencesStack.Count - 1);
            }
        }
    }

    private async Task ProcessMessageTrailer(SegmentToken segment)
    {
        while (_sequencesStack.Last().Name != s_message1)
        {
            _path.RemoveAt(_path.Count - 1);
            await _writer!.WriteEndElementAsync();
            _sequencesStack.RemoveAt(_sequencesStack.Count - 1);
        }
        _sequencesStack.RemoveAt(_sequencesStack.Count - 1);
        if (
            _sequencesStack.Last().Move()
            && _sequencesStack.Last().Item is XmlSchemaElement el
        )
        {
            if (el.QualifiedName == new XmlQualifiedName(segment.Tag, _targetNamespace))
            {
                _path.RemoveAt(_path.Count - 1);
                await _writer!.WriteEndElementAsync();
                await ParseSegmentAsync(segment, el);
                _path.RemoveAt(_path.Count - 1);
                await _writer.WriteEndElementAsync();
                await _writer.WriteEndDocumentAsync();
                _writer.Close();
                if (_messageEventArgs!.Stream is { })
                {
                    _messageEventArgs!.Stream.Close();
                }
                else
                {
                    _messageEventArgs!.Xml = _sb.ToString();
                }
                _messageEventArgs.EventKind = MessageEventKind.End;
                Message?.Invoke(this, _messageEventArgs);

                _sb.Clear();
                _writer = XmlWriter.Create(_sb, _xws);
                await _writer.WriteStartDocumentAsync();
                await ParseSegmentAsync(segment, el);
                await _writer!.WriteEndDocumentAsync();
                _writer.Close();
                _sequencesStack.Last().Reset();
                _inMessage = false;
                _messageTrailerXml.LoadXml(_sb.ToString());
                int expectedSegments = _messageTrailerXml.CreateNavigator()!
                        .SelectSingleNode(string.Format(s_d0074XPathFormat, _messageTrailer), _man)?.ValueAsInt ?? -1;
                if (expectedSegments != -1 && expectedSegments != _messageControlCount)
                {
                    throw new Exception($"TODO: expected number of segments: {expectedSegments}, got: {_messageControlCount}.");
                }
                string s = _tokenizer.IsInteractive ? s_d0340 : s_d0062;
                string messageReference1 = _messageHeaderXml.CreateNavigator()!
                    .SelectSingleNode(string.Format(s_secondLevelXPathFormat, _messageHeader, s), _man)?.Value ?? string.Empty;
                string messageReference2 = _messageTrailerXml.CreateNavigator()!
                    .SelectSingleNode(string.Format(s_secondLevelXPathFormat, _messageTrailer, s), _man)?.Value ?? string.Empty;
                if (messageReference1 != messageReference2)
                {
                    throw new Exception($"TODO: message references differ; expected: {messageReference1}, got: {messageReference2}.");
                }
            }
            else
            {
                throw new Exception($"TODO: expected tag: '{((XmlSchemaElement)_sequencesStack.Last().Item!).Name}', got: '{segment.Tag}'");
            }
        }
        else
        {
            throw new Exception($"TODO: extra tag: {segment.Tag}");
        }
    }

    private async Task ProcessInterchangeTrailer(SegmentToken segment)
    {
        if (!_inGroup)
        {
            _sequencesStack.RemoveAt(_sequencesStack.Count - 1);
            if (
                _sequencesStack.Last().Move()
                && _sequencesStack.Last().Item is XmlSchemaElement el
            )
            {

                _sb.Clear();
                _writer = XmlWriter.Create(_sb, _xws);
                await _writer.WriteStartDocumentAsync();
                await ParseSegmentAsync(segment, el);
                await _writer!.WriteEndDocumentAsync();
                _writer.Close();
                _interchangeTrailerXml.LoadXml(_sb.ToString());
                int exepectedCount = _interchangeTrailerXml.CreateNavigator()!
                        .SelectSingleNode(string.Format(s_secondLevelXPathFormat, _interchangeTrailer, s_d0036), _man)?.ValueAsInt ?? -1;
                if (exepectedCount != -1 && exepectedCount != _interchangeControlCount)
                {
                    throw new Exception($"TODO: expected number of groups/messages: {exepectedCount}, got: {_interchangeControlCount}.");
                }
            }
        }
        else
        {
            throw new Exception($"TODO: unexpected tag: '{segment.Tag}'.");
        }
    }

    private async Task ProcessMessageHeader(SegmentToken segment)
    {
        if (
            _sequencesStack.Last().Move()
            && _sequencesStack.Last().Item is XmlSchemaElement el
        )
        {
            if (el.QualifiedName == new XmlQualifiedName(segment.Tag, _targetNamespace))
            {
                _sb.Clear();
                _writer = XmlWriter.Create(_sb, _xws);
                await _writer.WriteStartDocumentAsync();
                await ParseSegmentAsync(segment, el);
                await _writer!.WriteEndDocumentAsync();
                _writer.Close();
                _messageHeaderXml.LoadXml(_sb.ToString());

                string s = _tokenizer.IsInteractive ? s_s306 : s_s009;
                string s1 = _tokenizer.IsInteractive ? s_d0340 : s_d0062;
                _messageEventArgs ??= new MessageEventArgs();
                _messageEventArgs.EventKind = MessageEventKind.Start;
                _messageEventArgs.MessageReferenceNumber = _messageHeaderXml.CreateNavigator()!.SelectSingleNode(string.Format(s_secondLevelXPathFormat, _messageHeader, s1), _man)!.Value;
                XPathNavigator nav1 = _messageHeaderXml.CreateNavigator()!.SelectSingleNode(string.Format(s_secondLevelXPathFormat, _messageHeader, s), _man)!;
                _messageEventArgs.MessageType = nav1.SelectSingleNode(s_d0065XPath, _man)!.Value;
                _messageEventArgs.MessageVersion = nav1.SelectSingleNode(s_d0052XPath, _man)!.Value;
                _messageEventArgs.MessageRelease = nav1.SelectSingleNode(s_d0054XPath, _man)!.Value;
                _messageEventArgs.ControllingAgencyCoded = nav1.SelectSingleNode(s_d0051XPath, _man)?.Value ?? s_un;
                _messageEventArgs.InterchangeHeader = new XmlDocument(_nameTable);
                _messageEventArgs.InterchangeHeader.LoadXml(_interchangeHeaderXml.OuterXml);
                if (_inGroup)
                {
                    _messageEventArgs.GroupHeader = new XmlDocument(_nameTable);
                    _messageEventArgs.GroupHeader.LoadXml(_groupHeaderXml.OuterXml);
                }
                _messageEventArgs.MessageHeader = new XmlDocument(_nameTable);
                _messageEventArgs.MessageHeader.LoadXml(_messageHeaderXml.OuterXml);

                Message?.Invoke(this, _messageEventArgs);


                if(_options.MessagesSuffixes is null ||  !_options.MessagesSuffixes.TryGetValue(_messageEventArgs.MessageType, out string? suffix))
                {
                    suffix = string.Empty;
                }

                _messageXsd = string.Format(
                    s_messageXsdFormat,
                    _messageEventArgs.ControllingAgencyCoded,
                    _messageEventArgs.MessageVersion,
                    _messageEventArgs.MessageRelease,
                    _messageEventArgs.MessageType,
                    suffix
                );

                if (_messageSchema is { })
                {
                    if (_messageSchemaCache[_messageEventArgs.MessageType] != _messageSchema)
                    {
                        _schemaSet.Remove(_messageSchema);
                        _messageSchema = _schemaSet.Add(_messageSchemaCache[_messageEventArgs.MessageType]);
                        _schemaSet.Compile();
                    }
                }
                else
                {
                    _messageSchema = _schemaSet.Add(_targetNamespace, new Uri(_schemas, _messageXsd).ToString());
                    _messageSchemaCache.Add(_messageEventArgs.MessageType, _messageSchema!);
                    _schemaSet.Compile();
                }

                if (_schemaSet.GlobalTypes[new XmlQualifiedName(s_message1, _targetNamespace)] is XmlSchemaComplexType messageType)
                {
                    _sequencesStack.Last().Move();
                    _sequencesStack.Last().IncrementOccurs();
                    _sequencesStack.Add(new Sequence(messageType.Name!, (XmlSchemaSequence)messageType.ContentTypeParticle));
                }
                if (_messageEventArgs.Stream is null)
                {
                    _sb.Clear();
                    _writer = XmlWriter.Create(_sb, _xws);
                }
                else
                {
                    _writer = XmlWriter.Create(_messageEventArgs.Stream, _xws);
                }
                await _writer.WriteStartDocumentAsync();
                _path.Add(_tokenizer.IsInteractive ? s_interactiveInterchange1 : s_batchInterchange1);
                await _writer.WriteStartElementAsync(
                null,
                    _path.Last(),
                    _targetNamespace
                );
                List<XmlDocument> headers = [_interchangeHeaderXml];
                if (_inGroup)
                {
                    headers.Add(_groupHeaderXml);
                }
                headers.Add(_messageHeaderXml);
                foreach (XmlDocument h in headers)
                {
                    XPathNodeIterator ni = h.CreateNavigator()!.Select(s_allElementsXPath, _man);
                    Stack<XPathNavigator> tree = [];
                    while (ni.MoveNext())
                    {
                        while (tree.TryPeek(out XPathNavigator? nav2))
                        {
                            XPathNavigator nav3 = ni.Current!.CreateNavigator();
                            if (!nav3.MoveToParent() || nav3.LocalName != nav2.LocalName || nav3.NamespaceURI != nav2.NamespaceURI)
                            {
                                tree.Pop();
                                _path.RemoveAt(_path.Count - 1);
                                await _writer.WriteEndElementAsync();
                            }
                            else
                            {
                                break;
                            }
                        }
                        if (ni.Current!.Evaluate(s_allElementsCountXPath, _man).ToString() == s_0)
                        {
                            await _writer.WriteElementStringAsync(null, ni.Current!.LocalName, ni.Current.NamespaceURI, ni.Current!.Value);
                        }
                        else
                        {
                            _path.Add(ni.Current!.LocalName);
                            await _writer.WriteStartElementAsync(null, ni.Current!.LocalName, ni.Current.NamespaceURI);
                            tree.Push(ni.Current!.CreateNavigator());
                        }
                    }
                    while (tree.Count > 0)
                    {
                        _path.RemoveAt(_path.Count - 1);
                        await _writer.WriteEndElementAsync();
                        tree.Pop();
                    }
                }
                _path.Add(s_message1);
                await _writer.WriteStartElementAsync(null, _path.Last(), _targetNamespace);
                _inMessage = true;
                _messageControlCount = 1;
                if (_inGroup)
                {
                    ++_groupControlCount;
                }
                else
                {
                    ++_interchangeControlCount;
                }
            }
            else
            {
                throw new Exception($"TODO: expected tag: '{((XmlSchemaElement)_sequencesStack.Last().Item!).Name}', got: '{segment.Tag}'");
            }
        }
        else
        {
            throw new Exception($"TODO: extra tag: {segment.Tag}");
        }
    }

    private async Task ProcessGroupTraier(SegmentToken segment)
    {
        if (_inGroup)
        {
            _sequencesStack.RemoveAt(_sequencesStack.Count - 1);
            if (
                _sequencesStack.Last().Move()
                && _sequencesStack.Last().Item is XmlSchemaElement el
            )
            {
                if (el.QualifiedName == new XmlQualifiedName(segment.Tag, _targetNamespace))
                {
                    _sb.Clear();
                    _writer = XmlWriter.Create(_sb, _xws);
                    await _writer.WriteStartDocumentAsync();
                    await ParseSegmentAsync(segment, el);
                    await _writer!.WriteEndDocumentAsync();
                    _writer.Close();
                    _groupTrailerXml.LoadXml(_sb.ToString());
                    _sequencesStack.Last().Reset();
                    _inGroup = false;
                    int expectedMessages = _groupTrailerXml.CreateNavigator()!
                        .SelectSingleNode(s_uneD0060XPath, _man)?.ValueAsInt ?? -1;
                    if (expectedMessages != -1 && expectedMessages != _groupControlCount)
                    {
                        throw new Exception($"TODO: expected number of messages: {expectedMessages}, got: {_groupControlCount}.");
                    }
                    string groupReference1 = _groupHeaderXml.CreateNavigator()!
                        .SelectSingleNode(s_ungD0048XPath, _man)?.Value ?? string.Empty;
                    string groupReference2 = _groupTrailerXml.CreateNavigator()!
                        .SelectSingleNode(s_uneD0048XPath, _man)?.Value ?? string.Empty;
                    if (groupReference1 != groupReference2)
                    {
                        throw new Exception($"TODO: group references differ; expected: {groupReference1}, got: {groupReference2}.");
                    }
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

    private async Task ProcessGroupHeader(SegmentToken segment)
    {
        if (!_inGroup)
        {
            if (
                _sequencesStack.Last().MoveIfNeed()
                && _sequencesStack.Last().Item is XmlSchemaElement el
            )
            {
                if (el.QualifiedName == new XmlQualifiedName(segment.Tag, _targetNamespace))
                {
                    _sb.Clear();
                    _writer = XmlWriter.Create(_sb, _xws);
                    await _writer.WriteStartDocumentAsync();
                    await ParseSegmentAsync(segment, el);
                    await _writer!.WriteEndDocumentAsync();
                    _writer.Close();
                    _groupHeaderXml.LoadXml(_sb.ToString());
                    _groupReference = _groupHeaderXml.CreateNavigator()!.SelectSingleNode(s_ungD0048XPath, _man)!.Value;
                    _groupControlCount = 0;
                    _inGroup = true;
                    ++_interchangeControlCount;
                    if (_sequencesStack.Last().Move() && _sequencesStack.Last().Item is XmlSchemaSequence seq)
                    {
                        _sequencesStack.Add(new Sequence(el.Name!, seq));
                    }
                    else
                    {
                        throw new Exception($"TODO: expected: {nameof(XmlSchemaSequence)}, got: {_sequencesStack.Last().Item}");
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
        else
        {
            throw new Exception($"TODO: unexpected tag: '{segment.Tag}'.");
        }
    }

    private void SelectProperSequence(SegmentToken segment)
    {
        if (
            _sequencesStack.Last().Move()
            && _sequencesStack.Last().Item is XmlSchemaChoice choice
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
                        && el.QualifiedName == new XmlQualifiedName(segment.Tag, _targetNamespace)
                    )
                    {
                        seq.Reset();
                        _sequencesStack.Add(seq);
                        break;
                    }
                }
            }
        }
        else
        {
            throw new NotImplementedException("Never occurs.");
        }
    }

    private async Task ProcessInterchangeHeader(SegmentToken segment)
    {
        _xws = new()
        {
            Async = true,
            Encoding = _tokenizer.Encoding!,
            Indent = true,
        };
        _sb.Clear();
        _writer = XmlWriter.Create(_sb, _xws);

        await _writer.WriteStartDocumentAsync();
        XmlSchemaElement? el0;
        if (_tokenizer.IsInteractive)
        {
            Uri interactiveUri = new(_schemas, s_interactiveInterchangeXsd);
            _schemaSet.Add(_targetNamespace, interactiveUri.ToString());
            _schemaSet.Compile();
            el0 = (XmlSchemaElement)_schemaSet.GlobalElements[new XmlQualifiedName(s_interactiveInterchange1, _targetNamespace)]!;
            _messageHeader = s_uih;
            _messageTrailer = s_uit;
            _interchangeTrailer = s_uiz;
        }
        else
        {
            Uri batchUri = new(_schemas, s_batchInterchangeXsd);
            _schemaSet.Add(_targetNamespace, batchUri.ToString());
            _schemaSet.Compile();
            el0 = (XmlSchemaElement)_schemaSet.GlobalElements[new XmlQualifiedName(s_batchInterchange1, _targetNamespace)]!;
            _messageHeader = s_unh;
            _messageTrailer = s_unt;
            _interchangeTrailer = s_unz;
        }
        if (
            el0 is { }
            && el0.ElementSchemaType is XmlSchemaComplexType ct
            && ct.Particle is XmlSchemaSequence seq
        )
        {
            _sequencesStack.Add(new Sequence(el0.Name!, seq));
            if (
                _sequencesStack.Last().Move()
            )
            {
                await ParseSegmentAsync(segment, _sequencesStack.Last().Item);
                await _writer!.WriteEndDocumentAsync();
                _writer.Close();
                _interchangeHeaderXml.LoadXml(_sb.ToString());
            }
        }
        else
        {
            throw new Exception($"TODO: expected tag: 'UNB' or 'UIB', got: '{segment.Tag}'");
        }
    }

    private async Task ParseSegmentAsync(SegmentToken segment, XmlSchemaObject? obj)
    {
        if (obj is XmlSchemaElement el)
        {
            if (
                el.QualifiedName == new XmlQualifiedName(segment.Tag, _targetNamespace)
                && el.ElementSchemaType is XmlSchemaComplexType ct
                && ct.ContentTypeParticle is XmlSchemaSequence seq
            )
            {
                _path.Add(segment.Tag!);
                await _writer.WriteStartElementAsync(null, el.QualifiedName.Name, el.QualifiedName.Namespace);
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
                                    ValidateElement((XmlSchemaElement)sequence.Item!, et.Elements[0]);
                                    await _writer.WriteElementStringAsync(null, ((XmlSchemaElement)sequence.Item).Name!, _targetNamespace, et.Elements[0]);
                                }
                                else
                                {
                                    await ParseCompositeAsync(segment.Tag!, et, (XmlSchemaElement)sequence.Item);
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
                await _writer.WriteEndElementAsync();
                _path.RemoveAt(_path.Count - 1);
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

    private async Task ParseCompositeAsync(string tag, ComponentToken token, XmlSchemaElement el)
    {
        if (
            el.ElementSchemaType is XmlSchemaComplexType ct
            && ct.ContentTypeParticle is XmlSchemaSequence seq
        )
        {
            _path.Add(el.QualifiedName.Name);
            await _writer.WriteStartElementAsync(null, el.QualifiedName.Name, el.QualifiedName.Namespace);
            if (seq.Items.Count > 0)
            {
                Sequence sequence = new(el.Name!, seq);
                if (token.Elements is { })
                {
                    foreach (string value in token.Elements)
                    {
                        sequence.Move();

                        if (string.IsNullOrEmpty(value) && sequence.Occurs < sequence.MinOccurs)
                        {
                            throw new Exception($"TODO: mandatory element {((XmlSchemaElement)sequence.Item!).Name} missed at composite {el.Name} at segment {tag}");
                        }
                        if (!string.IsNullOrEmpty(value))
                        {
                            ValidateElement((XmlSchemaElement)sequence.Item!, value);
                            await _writer.WriteElementStringAsync(null, ((XmlSchemaElement)sequence.Item!).Name!, _targetNamespace, value);
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
            await _writer.WriteEndElementAsync();
            _path.RemoveAt(_path.Count - 1);
        }
        else
        {
            throw new Exception($"TODO: unexpected composite");
        }
    }

    private void ValidateElement(XmlSchemaElement xmlSchemaElement, string value)
    {
        _path.Add(xmlSchemaElement.Name!);
        _elementXml.RemoveAll();
        XmlElement elem = (XmlElement)_elementXml.AppendChild(_elementXml.CreateElement(null, xmlSchemaElement.Name!, _targetNamespace))!;
        elem.AppendChild(_elementXml.CreateTextNode(value));
        elem.SetAttribute("xmlns:e", _targetNamespace);
        elem.SetAttribute("type", Properties.Resources.schema_instance_ns, string.Format("e:{0}", xmlSchemaElement.ElementSchemaType!.Name!));
        _elementXml.Validate(SchemaSet_ValidationEventHandler);
        _path.RemoveAt(_path.Count - 1);
    }

    private void SchemaSet_ValidationEventHandler(object? sender, ValidationEventArgs e)
    {
        string message = string.Format("/{0}: {1}", string.Join('/', _path.Skip(1)), e.Message);
        if (_validationWarningsCache.Add(message))
        {
            
            switch (e.Severity)
            {
                case XmlSeverityType.Warning:
                    _logger?.LogWarning(s_logMessage, message);
                    break;
                case XmlSeverityType.Error:
                    _logger?.LogError(s_logMessage, message);
                    break;
            }
        }
    }
}
