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

public class EdifactParser: EdifactProcessor
{
    public event InterchangeEventHandler? Interchange;
    public event GroupEventHandler? Group;
    public event MessageEventHandler? Message;

    private static readonly Regex s_reSegmentGroup = new("^SG\\d+$");
    private int _entersNum = 0;
    private readonly Dictionary<string, XmlSchema> _messageSchemaCache = [];
    private readonly List<Sequence> _sequencesStack = [];
    private EdifactParserOptions _options = null!;
    private XmlDocument _elementXml = null!;
    private EdifactTokenizer _tokenizer = null!;
    private string _messageHeader = string.Empty;
    private string _messageTrailer = string.Empty;
    private string _interchangeTrailer = string.Empty;
    private string _messageXsd = string.Empty;
    private MessageEventArgs _messageEventArgs = null!;
    private GroupEventArgs _groupEventArgs = null!;
    private InterchangeEventArgs _interchangeEventArgs = null!;
    private XmlSchema? _messageSchema = null;

    private bool _inMessage = false;
    private bool _inGroup = false;
    private int _messageControlCount = 0;
    private string _groupReference = string.Empty;
    private int _groupControlCount = 0;
    private int _interchangeControlCount = 0;
    private bool _isInteractive = false;

    public EdifactParser(IServiceProvider services): base(services)
    {
        _logger = _services.GetService<ILogger<EdifactParser>>();
    }
    public async Task Parse(EdifactParserOptions options, CancellationToken? cancellationToken)
    {
        try
        {
            _options = options;
            if (Interlocked.Increment(ref _entersNum) != 1)
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
            _isInteractive = false;
            _xws = new()
            {
                Async = true,
                Encoding = _tokenizer.Encoding!,
                Indent = true,
            };

            Uri input = new(options.InputUri!);
            IStreamFactory inputStreamFactory = _services.GetKeyedService<IStreamFactory>(input.Scheme)!;

            InitBaseStuff();

            int segmentPosition = 0;

            await foreach (SegmentToken segment in _tokenizer.TokenizeAsync(inputStreamFactory.GetInputStream(input)))
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
    protected override void SchemaSet_ValidationEventHandler(object? sender, ValidationEventArgs e)
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
                    _logger?.LogError(e.Exception, s_logMessage, message);
                    break;
            }
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
                _messageEventArgs!.Stream?.Close();
                _messageEventArgs.EventKind = EventKind.End;
                Message?.Invoke(this, _messageEventArgs);

                _ms.SetLength(0);
                _writer = XmlWriter.Create(_ms, _xws);
                await _writer.WriteStartDocumentAsync();
                await ParseSegmentAsync(segment, el);
                await _writer!.WriteEndDocumentAsync();
                _writer.Close();
                _sequencesStack.Last().Reset();
                _inMessage = false;
                _ms.Position = 0;
                _messageTrailerXml.Load(_ms);
                int expectedSegments = _messageTrailerXml.CreateNavigator()!
                        .SelectSingleNode(string.Format(s_d0074XPathFormat, _messageTrailer), _man)?.ValueAsInt ?? -1;
                if (expectedSegments != -1 && expectedSegments != _messageControlCount)
                {
                    throw new Exception($"TODO: expected number of segments: {expectedSegments}, got: {_messageControlCount}.");
                }
                string s = _isInteractive ? s_d0340 : s_d0062;
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

                _ms.SetLength(0);
                _writer = XmlWriter.Create(_ms, _xws);
                await _writer.WriteStartDocumentAsync();
                await ParseSegmentAsync(segment, el);
                await _writer!.WriteEndDocumentAsync();
                _writer.Close();
                _ms.Position = 0;
                _interchangeTrailerXml.Load(_ms);
                int exepectedCount = _interchangeTrailerXml.CreateNavigator()!
                        .SelectSingleNode(string.Format(s_secondLevelXPathFormat, _interchangeTrailer, s_d0036), _man)?.ValueAsInt ?? -1;
                if (exepectedCount != -1 && exepectedCount != _interchangeControlCount)
                {
                    throw new Exception($"TODO: expected number of groups/messages: {exepectedCount}, got: {_interchangeControlCount}.");
                }
                _interchangeEventArgs.EventKind = EventKind.End;
                Interchange?.Invoke(this, _interchangeEventArgs);
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
                _ms.SetLength(0);
                _writer = XmlWriter.Create(_ms, _xws);
                await _writer.WriteStartDocumentAsync();
                await ParseSegmentAsync(segment, el);
                await _writer!.WriteEndDocumentAsync();
                _writer.Close();
                _ms.Position = 0;
                _messageHeaderXml.Load(_ms);

                string s = _isInteractive ? s_s306 : s_s009;
                string s1 = _isInteractive ? s_d0340 : s_d0062;
                _messageEventArgs ??= new MessageEventArgs();
                _messageEventArgs.EventKind = EventKind.Start;
                if(_isInteractive)
                {
                    _messageEventArgs.Header = new InteractiveMessageHeader();
                }
                else
                {
                    _messageEventArgs.Header = new BatchMessageHeader();
                }
                _messageEventArgs.IsInteractive = _isInteractive;

                BuildMessageHeader();

                Message?.Invoke(this, _messageEventArgs);


                if (
                    _options.MessagesSuffixes is null 
                    || !_options.MessagesSuffixes.TryGetValue(
                        _messageEventArgs.Header.Identifier.Identifier, 
                        out string? suffix
                    )
                )
                {
                    suffix = string.Empty;
                }

                _messageXsd = string.Format(
                    s_fileInDirectoryXsdFormat,
                    _messageEventArgs.Header.Identifier.ControllingAgencyCoded,
                    _messageEventArgs.Header.Identifier.VersionNumber,
                    _messageEventArgs.Header.Identifier.ReleaseNumber,
                    _messageEventArgs.Header.Identifier.Identifier,
                    suffix
                );

                if (_messageSchema is { })
                {
                    if (_messageSchemaCache[_messageEventArgs.Header.Identifier.Identifier] != _messageSchema)
                    {
                        _schemaSet.Remove(_messageSchema);
                        _messageSchema = _schemaSet.Add(_messageSchemaCache[_messageEventArgs.Header.Identifier.Identifier]);
                        _schemaSet.Compile();
                    }
                }
                else
                {
                    _messageSchema = _schemaSet.Add(_targetNamespace, new Uri(_schemas, _messageXsd).ToString());
                    _messageSchemaCache.Add(_messageEventArgs.Header.Identifier.Identifier, _messageSchema!);
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
                    _ms.SetLength(0);
                    _writer = XmlWriter.Create(_ms, _xws);
                }
                else
                {
                    _writer = XmlWriter.Create(_messageEventArgs.Stream, _xws);
                }
                await _writer.WriteStartDocumentAsync();
                _path.Add(_isInteractive ? s_interactiveInterchange1 : s_batchInterchange1);
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

    private void BuildMessageHeader()
    {
        XPathNavigator nav = _messageHeaderXml.CreateNavigator()!.SelectSingleNode(string.Format("/e:{0}", _isInteractive ? s_uih : s_unh), _man)!;
        if(nav.SelectSingleNode("e:S009", _man) is XPathNavigator mi)
        {
            _messageEventArgs.Header.Identifier = new MessageIdentification
            {
                Identifier = mi.SelectSingleNode("e:D0065", _man)!.Value,
                VersionNumber = mi.SelectSingleNode("e:D0052", _man)!.Value,
                ReleaseNumber = mi.SelectSingleNode("e:D0054", _man)!.Value,
                ControllingAgencyCoded = mi.SelectSingleNode("e:D0051", _man)!.Value,
                AssociationAssignedCode = mi.SelectSingleNode("e:D0057", _man)?.Value,
                CodeListDirectoryVersionNUmber = mi.SelectSingleNode("e:D0110", _man)?.Value,
                MessageTypeSubfunctionIdentification = mi.SelectSingleNode("e:D0113", _man)?.Value,
            };
            if (_isInteractive)
            {
                InteractiveMessageHeader header = (_messageEventArgs.Header as InteractiveMessageHeader)!;
                header.MessageReferenceNumber = nav.SelectSingleNode("e:D0340", _man)?.Value;
                if (nav.SelectSingleNode("e:S009") is XPathNavigator dr)
                {
                    header.DialogueReference = new DialogueReference
                    {
                        InitiatorControlReference = dr.SelectSingleNode("e:D0300", _man)!.Value,
                        InitiatorReferenceIdentification = dr.SelectSingleNode("e:D0303", _man)?.Value,
                        ControllingAgencyCoded = dr.SelectSingleNode("e:D0051", _man)!.Value,
                        ResponderControlReference = dr.SelectSingleNode("e:D0304", _man)?.Value,
                    };
                }
                if (nav.SelectSingleNode("e:S301") is XPathNavigator sof)
                {
                    header.StatusOfTransfer = new InteractiveStatusOfTransfer
                    {
                        SenderSequenceNumber = sof.SelectSingleNode("e:D0320", _man)?.Value,
                        TransferPositionCoded = sof.SelectSingleNode("e:D0323", _man)?.Value,
                        DuplicateIndicator = sof.SelectSingleNode("e:D0325", _man)?.Value,
                    };
                }
                if (nav.SelectSingleNode("e:S300", _man) is XPathNavigator dt)
                {
                    header.DateAndTimeOfInitiation = new DateTimeOfEvent
                    {
                        Date = dt.SelectSingleNode("e:D0338", _man)?.Value,
                        Time = dt.SelectSingleNode("e:D0314", _man)?.Value,
                        UtcOffset = dt.SelectSingleNode("e:D0336", _man)?.Value,
                    };
                }
                header.TestIndicator = nav.SelectSingleNode("e:D0035", _man)?.Value;
            }
            else
            {
                BatchMessageHeader header = (_messageEventArgs.Header as BatchMessageHeader)!;
                header.MessageReferenceNumber = nav.SelectSingleNode("e:D0062", _man)!.Value;
                header.CommonAccessReference = nav.SelectSingleNode("e:D0068", _man)?.Value;
                if (nav.SelectSingleNode("e:S010", _man) is XPathNavigator sof)
                {
                    header.StatusOfTransfer = new BatchStatusOfTransfer
                    {
                        SequenceOfTransfers = sof.SelectSingleNode("e:D0070", _man)!.Value,
                        FirstAndLastTransfer = sof.SelectSingleNode("e:D0073", _man)?.Value,
                    };
                }
                if (nav.SelectSingleNode("e:S016", _man) is XPathNavigator si)
                {
                    header.SubsetIdentification = new Identification
                    {
                        Identifier = si.SelectSingleNode("e:D0115", _man)!.Value,
                        VersionNumber = si.SelectSingleNode("e:D0116", _man)?.Value,
                        ReleaseNumber = si.SelectSingleNode("e:D0118", _man)?.Value,
                        ControllingAgencyCoded = si.SelectSingleNode("e:D0051", _man)?.Value,
                    };
                }
                if (nav.SelectSingleNode("e:S017", _man) is XPathNavigator gi)
                {
                    header.SubsetIdentification = new Identification
                    {
                        Identifier = gi.SelectSingleNode("e:D0121", _man)!.Value,
                        VersionNumber = gi.SelectSingleNode("e:D0122", _man)?.Value,
                        ReleaseNumber = gi.SelectSingleNode("e:D0124", _man)?.Value,
                        ControllingAgencyCoded = gi.SelectSingleNode("e:D0051", _man)?.Value,
                    };
                }
                if (nav.SelectSingleNode("e:S018", _man) is XPathNavigator sci)
                {
                    header.SubsetIdentification = new Identification
                    {
                        Identifier = sci.SelectSingleNode("e:D0127", _man)!.Value,
                        VersionNumber = sci.SelectSingleNode("e:D0128", _man)?.Value,
                        ReleaseNumber = sci.SelectSingleNode("e:D0130", _man)?.Value,
                        ControllingAgencyCoded = sci.SelectSingleNode("e:D0051", _man)?.Value,
                    };
                }
            }
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
                    _ms.SetLength(0);
                    _writer = XmlWriter.Create(_ms, _xws);
                    await _writer.WriteStartDocumentAsync();
                    await ParseSegmentAsync(segment, el);
                    await _writer!.WriteEndDocumentAsync();
                    _writer.Close();
                    _ms.Position = 0;
                    _groupTrailerXml.Load(_ms);
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
                    _groupEventArgs.EventKind = EventKind.End;

                    Group?.Invoke(this, _groupEventArgs);
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
                    _ms.SetLength(0);
                    _writer = XmlWriter.Create(_ms, _xws);
                    await _writer.WriteStartDocumentAsync();
                    await ParseSegmentAsync(segment, el);
                    await _writer!.WriteEndDocumentAsync();
                    _writer.Close();
                    _ms.Position = 0;
                    _groupHeaderXml.Load(_ms);
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
                    _groupEventArgs ??= new GroupEventArgs()
                    {
                        Header = new GroupHeader()
                    };
                    BuildGroupHeader();
                    _groupEventArgs.EventKind = EventKind.Start;

                    Group?.Invoke(this, _groupEventArgs);
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

    private void BuildGroupHeader()
    {
        XPathNavigator nav = _groupHeaderXml.CreateNavigator()!.SelectSingleNode("/e:UNG", _man)!;
        if(nav.SelectSingleNode("e:D0038", _man)?.Value is not null)
        {
            _groupEventArgs.Header.MessageGroupIdentification = new MessageIdentification
            {
                Identifier = nav.SelectSingleNode("e:D0038", _man)!.Value,
                VersionNumber = nav.SelectSingleNode("e:S008/e:D0052", _man)!.Value,
                ReleaseNumber = nav.SelectSingleNode("e:S008/e:D0054", _man)!.Value,
                ControllingAgencyCoded = nav.SelectSingleNode("e:S008/e:D0051", _man)!.Value,
                AssociationAssignedCode = nav.SelectSingleNode("e:S008/e:D0057", _man)?.Value,
            };
        }
        if (nav.SelectSingleNode("e:S006", _man) is XPathNavigator si)
        {
            _groupEventArgs.Header.ApplicationSender = new PartyIdentification
            {
                Identification = si.SelectSingleNode("e:D0040", _man)!.Value,
                CodeQualifier = si.SelectSingleNode("e:D0007", _man)?.Value,
            };
        }
        if (nav.SelectSingleNode("e:S007", _man) is XPathNavigator ri)
        {
            _groupEventArgs.Header.ApplicationRecipient = new PartyIdentification
            {
                Identification = ri.SelectSingleNode("e:D0044", _man)!.Value,
                CodeQualifier = ri.SelectSingleNode("e:D0007", _man)?.Value,
            };
        }
        if (nav.SelectSingleNode("e:S004", _man) is XPathNavigator dt)
        {
            _groupEventArgs.Header.DateAndTimeOfPreparation = new DateTimeOfEvent
            {
                Date = dt.SelectSingleNode("e:D0017", _man)!.Value,
                Time = dt.SelectSingleNode("e:D0019", _man)!.Value,
            };
        }
        _groupEventArgs.Header.GroupReferenceNumber = nav.SelectSingleNode("e:D0048", _man)!.Value;
        _groupEventArgs.Header.ApplicationPassword = nav.SelectSingleNode("e:D0058", _man)?.Value;
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
        _isInteractive = segment.Tag == s_uib;
        _ms.SetLength(0);
        _writer = XmlWriter.Create(_ms, _xws);

        await _writer.WriteStartDocumentAsync();
        XmlSchemaElement? el0;
        _interchangeEventArgs ??= new InterchangeEventArgs();

        if(_isInteractive)
        {
            Uri interactiveUri = new(_schemas, s_interactiveInterchangeXsd);
            _schemaSet.Add(_targetNamespace, interactiveUri.ToString());
            _schemaSet.Compile();
            el0 = (XmlSchemaElement)_schemaSet.GlobalElements[new XmlQualifiedName(s_interactiveInterchange1, _targetNamespace)]!;
            _messageHeader = s_uih;
            _messageTrailer = s_uit;
            _interchangeTrailer = s_uiz;
            _interchangeEventArgs.Header = new InteractiveInterchangeHeader();
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
            _interchangeEventArgs.Header = new BatchInterchangeHeader();
        }
        _interchangeEventArgs.IsInteractive = _isInteractive;
        if (
            el0 is { }
            && el0.ElementSchemaType is XmlSchemaComplexType ct
            && ct.Particle is XmlSchemaSequence seq
        )
        {
            _sequencesStack.Add(new Sequence(el0.Name!, seq));
            _sequencesStack.Last().Move();
            await ParseSegmentAsync(segment, _sequencesStack.Last().Item);
            await _writer!.WriteEndDocumentAsync();
            _writer.Close();
            _ms.Position = 0;
            _interchangeHeaderXml.Load(_ms);
            BuildInterchangeHeader();
            _interchangeEventArgs.EventKind = EventKind.Start;

            Interchange?.Invoke(this, _interchangeEventArgs);
        }
        else
        {
            throw new Exception($"TODO: expected tag: 'UNB' or 'UIB', got: '{segment.Tag}'");
        }
    }
    private void BuildInterchangeHeader()
    {
        XPathNavigator nav = _interchangeHeaderXml.CreateNavigator()!
            .SelectSingleNode(string.Format("/e:{0}", _isInteractive ? s_uib : s_unb), _man)!;

        if (nav.SelectSingleNode("e:S001", _man) is XPathNavigator syntax)
        {
            _interchangeEventArgs.Header!.SyntaxIdentifier.Identifier = syntax.SelectSingleNode("e:D0001", _man)!.Value;
            _interchangeEventArgs.Header!.SyntaxIdentifier.VersionNumber = syntax.SelectSingleNode("e:D0002", _man)!.Value;
            _interchangeEventArgs.Header!.SyntaxIdentifier.ServiceCodeListDirectoryVersionNumber = syntax.SelectSingleNode("e:D0080", _man)?.Value;
            _interchangeEventArgs.Header!.SyntaxIdentifier.CharacterEncodingCoded = syntax.SelectSingleNode("e:D0133", _man)?.Value;
        }
        if (nav.SelectSingleNode("e:S002", _man) is XPathNavigator sender)
        {
            _interchangeEventArgs.Header!.Sender = new PartyIdentification
            {
                Identification = sender.SelectSingleNode("e:D0004", _man)!.Value,
                CodeQualifier = sender.SelectSingleNode("e:D0007", _man)?.Value,
                InternalIdentification = sender.SelectSingleNode("e:D0008", _man)?.Value,
                InternalSubIdentification = sender.SelectSingleNode("e:D0042", _man)?.Value,
            };
        }
        if (nav.SelectSingleNode("e:S003", _man) is XPathNavigator recipient)
        {
            _interchangeEventArgs.Header!.Recipient = new PartyIdentification
            {
                Identification = recipient.SelectSingleNode("e:D0010", _man)!.Value,
                CodeQualifier = recipient.SelectSingleNode("e:D0007", _man)?.Value,
                InternalIdentification = recipient.SelectSingleNode("e:D0014", _man)?.Value,
                InternalSubIdentification = recipient.SelectSingleNode("e:D0046", _man)?.Value,
            };
        }
        _interchangeEventArgs.Header!.TestIndicator = nav.SelectSingleNode("e:D0035", _man)?.Value;
        if (_isInteractive)
        {
            InteractiveInterchangeHeader header = (_interchangeEventArgs.Header as InteractiveInterchangeHeader)!;
            if(nav.SelectSingleNode("e:S302", _man) is XPathNavigator dr)
            {
                header.DialogueReference = new DialogueReference
                {
                    InitiatorControlReference = dr.SelectSingleNode("e:D0300", _man)!.Value,
                    InitiatorReferenceIdentification = dr.SelectSingleNode("e:D0303", _man)?.Value,
                    ControllingAgencyCoded = dr.SelectSingleNode("e:D0051", _man)?.Value,
                    ResponderControlReference = dr.SelectSingleNode("e:D0304", _man)?.Value,
                };
            }
            if (nav.SelectSingleNode("e:S303", _man) is XPathNavigator tr)
            {
                header.TransactionReference = new TransactionReference
                {
                    TransactionControlReference = tr.SelectSingleNode("e:D0306", _man)!.Value,
                    InitiatorReferenceIdentification = tr.SelectSingleNode("e:D0303", _man)?.Value,
                    ControllingAgencyCoded = tr.SelectSingleNode("e:D0051", _man)?.Value,
                };
            }
            if (nav.SelectSingleNode("e:S018", _man) is XPathNavigator si)
            {
                header.ScenarioIdentification = new Identification
                {
                    Identifier = si.SelectSingleNode("e:D0127", _man)!.Value,
                    VersionNumber = si.SelectSingleNode("e:D0128", _man)?.Value,
                    ReleaseNumber = si.SelectSingleNode("e:D0130", _man)?.Value,
                    ControllingAgencyCoded = si.SelectSingleNode("e:D0051", _man)?.Value,
                };
            }
            if (nav.SelectSingleNode("e:S305", _man) is XPathNavigator di)
            {
                header.DialogueIdentification = new Identification
                {
                    Identifier = di.SelectSingleNode("e:D0311", _man)!.Value,
                    VersionNumber = di.SelectSingleNode("e:D0342", _man)?.Value,
                    ReleaseNumber = di.SelectSingleNode("e:D0344", _man)?.Value,
                    ControllingAgencyCoded = di.SelectSingleNode("e:D0051", _man)?.Value,
                };
            }
            if (nav.SelectSingleNode("e:S300", _man) is XPathNavigator dt)
            {
                header.DateAndTimeOfInitiation = new DateTimeOfEvent
                {
                    Date = dt.SelectSingleNode("e:D0338", _man)?.Value,
                    Time = dt.SelectSingleNode("e:D0314", _man)?.Value,
                    UtcOffset = dt.SelectSingleNode("e:D0336", _man)?.Value,
                };
            }
            header.DuplicateIndicator = nav.SelectSingleNode("e:D0325", _man)?.Value;
        }
        else
        {
            BatchInterchangeHeader header = (_interchangeEventArgs.Header as BatchInterchangeHeader)!;
            if (nav.SelectSingleNode("e:S004", _man) is XPathNavigator dt)
            {
                header.DateAndTimeOfPreparation = new DateTimeOfEvent
                {
                    Date = dt.SelectSingleNode("e:D0017", _man)?.Value,
                    Time = dt.SelectSingleNode("e:D0019", _man)?.Value,
                };
            }
            header.ControlReference = nav.SelectSingleNode("e:D0020", _man)!.Value;
            if (nav.SelectSingleNode("e:S005", _man) is XPathNavigator rrpd)
            {
                header.RecipientReferencePasswordDetails = new ReferenceOrPasswordDetails
                {
                    ReferenceOrPassword = rrpd.SelectSingleNode("e:D0022", _man)!.Value,
                    Qualifier = rrpd.SelectSingleNode("e:D0025", _man)?.Value,
                };
            }
            header.ApplicationReference = nav.SelectSingleNode("e:D0026", _man)?.Value;
            header.PriorityCode = nav.SelectSingleNode("e:D0029", _man)?.Value;
            header.AcknowledgementRequest = nav.SelectSingleNode("e:D0031", _man)?.Value;
            header.AgreementIdentifier = nav.SelectSingleNode("e:D0032", _man)?.Value;
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
        _elementXml.Validate(ElementValidationEventHandler);
        _path.RemoveAt(_path.Count - 1);
    }

    private void ElementValidationEventHandler(object? sender, ValidationEventArgs e)
    {
        string message = string.Format("/{0}: {1}", string.Join('/', _path.Skip(1)), e.Message);
        if (_validationWarningsCache.Add(message))
        {
            _logger?.LogWarning(s_logMessage, message);
        }
    }
}
