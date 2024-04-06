using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO.Pipes;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.XPath;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

public class EdifactBuilder : EdifactProcessor, IDisposable
{
    private EdifactBuilderOptions _options = null!;
    private char _syntaxLevel = 'A';
    private TextWriter _output = null!;
    private GroupHeader? _groupHeader;
    private InterchangeHeader? _interchangeHeader;
    private MessageHeader? _messageHeader;
    private bool _disposed = false;
    private bool? _isGroupBased = null;
    private XmlReaderSettings _xmlReaderSettings = null!;

    public EdifactBuilder(IServiceProvider services) : base(services)
    {
        _logger = _services.GetService<ILogger<EdifactBuilder>>();
    }
    ~EdifactBuilder()
    {
        InternalDispose();
    }
    public async Task BeginInterchangeAsync(EdifactBuilderOptions options, InterchangeHeader header)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(header);
            if (options.Output is null)
            {
                throw new ArgumentNullException($"{nameof(options)}.{nameof(options.Output)}");
            }
            if (_interchangeHeader is { })
            {
                throw new Exception("TODO: an interchange is already began.");
            }
            _interchangeHeader = header;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _options = options;
            _nameTable = new NameTable();
            _path.Clear();

            byte[] una = [
                (byte)'U', 
                (byte)'N', 
                (byte)'A',
                (byte)_options.ComponentPartsSeparator, 
                (byte)_options.SegmentPartsSeparator, 
                (byte)_options.DecimalMark,
                (byte)_options.ReleaseCharacter, 
                (byte)'*', 
                (byte)_options.SegmentTerminator,
                .. 
                (_options.SegmentsSeparator?.ToCharArray() ?? []).Select(ch => (byte)ch),
            ];

            await options.Output.WriteAsync(una);

            if (
                header.SyntaxIdentifier.Identifier is null
                || !header.SyntaxIdentifier.Identifier.StartsWith("UNO")
                || header.SyntaxIdentifier.Identifier.Length != 4
            )
            {
                throw new Exception($"TODO: Invalid interchange syntax identifier: {header.SyntaxIdentifier.Identifier}");
            }
            _syntaxLevel = header.SyntaxIdentifier.Identifier[3];
            Encoding? encoding = _options.Encoding;
            encoding ??= EdifactProcessor.SyntaxLevelToEncoding(_syntaxLevel);

            if (
                encoding is null
                && header.SyntaxIdentifier.CharacterEncodingCoded is { }
                && EdifactProcessor.CharacterEncodingCodedToEncoding(header.SyntaxIdentifier.CharacterEncodingCoded) is Encoding enc
            )
            {
                encoding = enc;
            }
            if (encoding is null)
            {
                throw new Exception("TODO: Encoding is not defined.");
            }
            _xws = new()
            {
                Async = true,
                Encoding = encoding,
                Indent = true,
            };
            _schemaSet = new(_nameTable)
            {
                XmlResolver = _xmlResolver
            };
            _schemaSet.ValidationEventHandler += SchemaSet_ValidationEventHandler;
            _schemas = new(string.Format(s_folderUriFormat, options.SchemasUri!));
            _output = new StreamWriter(options.Output, encoding);
            _isGroupBased = null;
            _groupHeader = null;
            _messageHeader = null;

            InitBaseStuff();

            _xmlReaderSettings = new XmlReaderSettings
            {
                NameTable = _nameTable,
                Schemas = _schemaSet,
                ValidationType = ValidationType.Schema,
                XmlResolver = _xmlResolver,
                Async = true,
            };
            if(_options.IsStrict is not bool strict || !strict)
            {
                _xmlReaderSettings.ValidationEventHandler += XmlReaderSettings_ValidationEventHandler;
            }

            await WriteSegmentAsync(InterchangeHeaderToXml());
        }
        catch (Exception)
        {
            InternalDispose();
            throw;
        }

    }
    public async Task BeginGroupAsync(GroupHeader header)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(header);
            if (_interchangeHeader is null)
            {
                throw new Exception("TODO: Any interchange is not began.");
            }
            if (_interchangeHeader is InteractiveInterchangeHeader)
            {
                throw new Exception("TODO: The interchange is interactive.");
            }
            if (_groupHeader is { })
            {
                throw new Exception("TODO: A group is already began.");
            }
            if (_isGroupBased is bool b && !b)
            {
                throw new Exception("TODO: The interchange is message-based.");
            }
            _isGroupBased = true;
            _groupHeader = header;
            await WriteSegmentAsync(GroupHeaderToXml());
        }
        catch (Exception)
        {
            InternalDispose();
            throw;
        }
    }
    public async Task DeliverMessageAsync(MessageHeader header, Stream input)
    {
        try
        {
            if(_interchangeHeader is null)
            {
                throw new Exception("TODO: Any interchange is not began.");
            }
            if (_interchangeHeader is InteractiveInterchangeHeader && header is not InteractiveMessageHeader)
            {
                throw new Exception("TODO: An interactive message expected.");
            }
            if (_interchangeHeader is BatchInterchangeHeader && header is not BatchMessageHeader)
            {
                throw new Exception("TODO: A batch message expected.");
            }
            _isGroupBased ??= false;
            if (_isGroupBased is bool b && b)
            {
                if (_groupHeader is null)
                {
                    throw new Exception("TODO: The interchange is group-based but no group is began.");
                }
                if (_groupHeader.MessageGroupIdentification is null)
                {
                    _groupHeader.MessageGroupIdentification = header.Identifier;
                }
                else if (!_groupHeader.MessageGroupIdentification.Equals(header.Identifier))
                {
                    throw new Exception("TODO: The group is respect to another message type.");
                }
            }

            UpdateSchemaSet(_options, header);

            _messageHeader = header;
            _messageControlCount = 0;

            await WriteSegmentAsync(MessageHeaderToXml());

            
            await ProcessMessageAsync(input);


            if (_isGroupBased is bool b1 && !b1)
            {
                ++_interchangeControlCount;
            }
            else
            {
                ++_groupControlCount;
            }

            ++_messageControlCount;
            await WriteSegmentAsync(MessageTrailerToXml());

            _messageHeader = null;
        }
        catch (Exception)
        {
            InternalDispose();
            throw;
        }
    }
    public async Task EndGroupAsync()
    {
        try
        {
            if (_interchangeHeader is null)
            {
                throw new Exception("TODO: Any interchange is not began.");
            }
            if (_groupHeader is null)
            {
                throw new Exception("TODO: Any group is not began.");
            }

            await WriteSegmentAsync(GroupTrailerToXml());

            _groupHeader = null;
            await Task.CompletedTask;
        }
        catch (Exception)
        {
            InternalDispose();
            throw;
        }
    }
    public async Task EndInterchangeAsync()
    {
        try
        {
            if (_interchangeHeader is null)
            {
                throw new Exception("TODO: Any interchange is not began.");
            }
            if (_groupHeader is { })
            {
                throw new Exception("TODO: A group is not ended.");
            }

            await WriteSegmentAsync(InterchangeTrailerToXml());

            _interchangeHeader = null;
            await Task.CompletedTask;
        }
        catch (Exception)
        {
            InternalDispose();
            throw;
        }
    }
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        InternalDispose();
        GC.SuppressFinalize(this);
    }
    private void XmlReaderSettings_ValidationEventHandler(object? sender, ValidationEventArgs e)
    {
        if (_messageHeader is { })
        {
            string message = string.Format("At message {0}: {1}", _messageHeader.MessageReferenceNumber, e.Message);
            _logger?.LogWarning(e.Exception, s_logMessage, message);
        }
        else
        {
            _logger?.LogWarning(e.Exception, s_logMessage, e.Message);
        }
    }
    private async Task ProcessMessageAsync(Stream input)
    {
        XmlReader reader = XmlReader.Create(input, _xmlReaderSettings);
        XmlDocument doc = new(_nameTable);

        await reader.MoveToContentAsync();
        reader.ReadStartElement();
        await reader.MoveToContentAsync();
        while (reader.Name == s_unb || reader.Name == s_uib || reader.Name == s_ung || reader.Name == s_unh || reader.Name == s_uih)
        {
            await reader.ReadInnerXmlAsync();
            await reader.MoveToContentAsync();
        }
        while (!reader.EOF)
        {
            while (reader.NodeType is XmlNodeType.Element && !s_reSegmentGroup.IsMatch(reader.Name))
            {
                using XmlReader r = reader.ReadSubtree();
                doc.Load(r);
                await WriteSegmentAsync(doc.DocumentElement!);
                await reader.MoveToContentAsync();
            }
            if (reader.NodeType is not XmlNodeType.None)
            {
                if (reader.NodeType is XmlNodeType.EndElement)
                {
                    reader.ReadEndElement();
                }
                else
                {
                    reader.ReadStartElement();
                }
                await reader.MoveToContentAsync();
            }
        }
    }
    private void InternalDispose()
    {
        _isGroupBased = null;
        _groupHeader = null;
        _messageHeader = null;
        _interchangeHeader = null;
        if (_output is { })
        {
            _output.Close();
        }
    }
    private async Task WriteSegmentAsync(XmlElement element)
    {
        await _output.WriteAsync(element.LocalName);
        XmlSchemaComplexType ct = (XmlSchemaComplexType)element.SchemaInfo.SchemaType!;
        XmlSchemaSequence seq = (XmlSchemaSequence)ct.ContentTypeParticle!;
        XPathNodeIterator components = element.CreateNavigator()!.SelectChildren(XPathNodeType.Element);
        int pos = 0;
        int occurs = 0;
        while (components.MoveNext())
        {
            while (pos < seq.Items.Count && ((XmlSchemaElement)seq.Items[pos]).Name != components.Current!.LocalName)
            {
                if (occurs == 0)
                {
                    await _output.WriteAsync(_options.SegmentPartsSeparator);
                }
                occurs = 0;
                ++pos;
            }
            if (pos < seq.Items.Count)
            {
                await _output.WriteAsync(_options.SegmentPartsSeparator);
                await WriteComponentAsync((XmlElement)components.Current!.UnderlyingObject!);
                ++occurs;
            }
        }
        await _output.WriteAsync(_options.SegmentTerminator);
        if (!string.IsNullOrEmpty(_options.SegmentsSeparator))
        {
            await _output.WriteAsync(_options.SegmentsSeparator);
        }
        if (_messageHeader is { })
        {
            ++_messageControlCount;
        }
    }

    private async Task WriteComponentAsync(XmlElement element)
    {
        XmlSchemaComplexType ct = (XmlSchemaComplexType)element.SchemaInfo.SchemaType!;
        XmlSchemaSequence? seq = ct.ContentTypeParticle as XmlSchemaSequence;
        if (seq is { })
        {
            XPathNodeIterator components = element.CreateNavigator()!.SelectChildren(XPathNodeType.Element);
            int pos = 0;
            int occurs = 0;
            while (components.MoveNext())
            {
                while (pos < seq.Items.Count && ((XmlSchemaElement)seq.Items[pos]).Name != components.Current!.LocalName)
                {
                    if (occurs == 0 && pos > 0)
                    {
                        await _output.WriteAsync(_options.ComponentPartsSeparator);
                    }
                    occurs = 0;
                    ++pos;
                }
                if (pos < seq.Items.Count)
                {
                    if (pos > 0)
                    {
                        await _output.WriteAsync(_options.ComponentPartsSeparator);
                    }
                    await WriteComponentAsync((XmlElement)components.Current!.UnderlyingObject!);
                    ++occurs;
                }
            }
        }
        else
        {
            foreach(char ch in element.InnerText)
            {
                if(
                    ch == _options.ReleaseCharacter 
                    || ch == _options.ComponentPartsSeparator 
                    || ch == _options.SegmentPartsSeparator 
                    || ch == _options.DecimalMark 
                    || ch == _options.SegmentTerminator
                )
                {
                    await _output.WriteAsync(_options.ReleaseCharacter);
                }
                await _output.WriteAsync(ch);
            }
        }
    }
    private XmlElement MessageTrailerToXml()
    {
        StartDocument();

        BatchMessageHeader? batch = _messageHeader as BatchMessageHeader;

        _writer.WriteStartElement(batch is { } ? s_unt : s_uit, _targetNamespace);

        if(batch is { })
        {
            _writer.WriteElementString("D0074", _targetNamespace, _messageControlCount.ToString());
            _writer.WriteElementString("D0062", _targetNamespace, _messageHeader!.MessageReferenceNumber);
        }
        else
        {
            _writer.WriteElementString("D0340", _targetNamespace, _messageHeader!.MessageReferenceNumber);
            _writer.WriteElementString("D0074", _targetNamespace, _messageControlCount.ToString());
        }

        _writer.WriteEndElement();

        return EndDocument();
    }
    private XmlElement MessageHeaderToXml()
    {
        StartDocument();

        BatchMessageHeader? batch = _messageHeader as BatchMessageHeader;
        InteractiveMessageHeader? interactive = _messageHeader as InteractiveMessageHeader;

        _writer.WriteStartElement(batch is { } ? s_unh : s_uih, _targetNamespace);

        if(interactive is { })
        {
            _writer.WriteStartElement("S306", _targetNamespace);
            _writer.WriteElementString("D0065", _targetNamespace, interactive.Identifier.Type);
            _writer.WriteElementString("D0052", _targetNamespace, interactive.Identifier.VersionNumber);
            _writer.WriteElementString("D0054", _targetNamespace, interactive.Identifier.ReleaseNumber);
            if (!string.IsNullOrEmpty(interactive.Identifier.MessageTypeSubfunctionIdentification))
            {
                _writer.WriteElementString("D0113", _targetNamespace, interactive.Identifier.MessageTypeSubfunctionIdentification);
            }
            if (!string.IsNullOrEmpty(interactive.Identifier.ControllingAgencyCoded))
            {
                _writer.WriteElementString("D0051", _targetNamespace, interactive.Identifier.ControllingAgencyCoded);
            }
            if (!string.IsNullOrEmpty(interactive.Identifier.AssociationAssignedCode))
            {
                _writer.WriteElementString("D0057", _targetNamespace, interactive.Identifier.AssociationAssignedCode);
            }
            _writer.WriteEndElement();
            if (!string.IsNullOrEmpty(interactive.MessageReferenceNumber))
            {
                _writer.WriteElementString("D0340", _targetNamespace, interactive.MessageReferenceNumber);
            }
            DialogueReferenceToXml(interactive.DialogueReference);
            if(interactive.StatusOfTransfer is { })
            {
                _writer.WriteStartElement("S301", _targetNamespace);
                if (!string.IsNullOrEmpty(interactive.StatusOfTransfer.SenderSequenceNumber))
                {
                    _writer.WriteElementString("D0320", _targetNamespace, interactive.StatusOfTransfer.SenderSequenceNumber);
                }
                if (!string.IsNullOrEmpty(interactive.StatusOfTransfer.TransferPositionCoded))
                {
                    _writer.WriteElementString("D0323", _targetNamespace, interactive.StatusOfTransfer.TransferPositionCoded);
                }
                if (!string.IsNullOrEmpty(interactive.StatusOfTransfer.DuplicateIndicator))
                {
                    _writer.WriteElementString("D0325", _targetNamespace, interactive.StatusOfTransfer.DuplicateIndicator);
                }
                _writer.WriteEndElement();
            }
            if (interactive.DateAndTimeOfInitiation is { })
            {
                _writer.WriteStartElement("S300", _targetNamespace);
                if (!string.IsNullOrEmpty(interactive.DateAndTimeOfInitiation.Date))
                {
                    _writer.WriteElementString("D0338", _targetNamespace, interactive.DateAndTimeOfInitiation.Date);
                }
                if (!string.IsNullOrEmpty(interactive.DateAndTimeOfInitiation.Time))
                {
                    _writer.WriteElementString("D0314", _targetNamespace, interactive.DateAndTimeOfInitiation.Time);
                }
                if (!string.IsNullOrEmpty(interactive.DateAndTimeOfInitiation.UtcOffset))
                {
                    _writer.WriteElementString("D0336", _targetNamespace, interactive.DateAndTimeOfInitiation.UtcOffset);
                }
                _writer.WriteEndElement();
            }
            if (!string.IsNullOrEmpty(interactive.TestIndicator))
            {
                _writer.WriteElementString("D0035", _targetNamespace, interactive.TestIndicator);
            }
        }
        else
        {
            _writer.WriteElementString("D0062", _targetNamespace, batch!.MessageReferenceNumber);
            _writer.WriteStartElement("S009", _targetNamespace);
            _writer.WriteElementString("D0065", _targetNamespace, batch.Identifier.Type);
            _writer.WriteElementString("D0052", _targetNamespace, batch.Identifier.VersionNumber);
            _writer.WriteElementString("D0054", _targetNamespace, batch.Identifier.ReleaseNumber);
            _writer.WriteElementString("D0051", _targetNamespace, batch.Identifier.ControllingAgencyCoded);
            if (!string.IsNullOrEmpty(batch.Identifier.AssociationAssignedCode))
            {
                _writer.WriteElementString("D0057", _targetNamespace, batch.Identifier.AssociationAssignedCode);
            }
            if (!string.IsNullOrEmpty(batch.Identifier.CodeListDirectoryVersionNUmber))
            {
                _writer.WriteElementString("D0110", _targetNamespace, batch.Identifier.CodeListDirectoryVersionNUmber);
            }
            if (!string.IsNullOrEmpty(batch.Identifier.MessageTypeSubfunctionIdentification))
            {
                _writer.WriteElementString("D0113", _targetNamespace, batch.Identifier.MessageTypeSubfunctionIdentification);
            }
            _writer.WriteEndElement();
            if (!string.IsNullOrEmpty(batch.CommonAccessReference))
            {
                _writer.WriteElementString("D0068", _targetNamespace, batch.CommonAccessReference);
            }
            if(batch.StatusOfTransfer is { })
            {
                _writer.WriteStartElement("S010", _targetNamespace);
                _writer.WriteElementString("D0070", _targetNamespace, batch.StatusOfTransfer.SequenceOfTransfers);
                if (!string.IsNullOrEmpty(batch.StatusOfTransfer.FirstAndLastTransfer))
                {
                    _writer.WriteElementString("D0073", _targetNamespace, batch.StatusOfTransfer.FirstAndLastTransfer);
                }
                _writer.WriteEndElement();
            }
            if (batch.SubsetIdentification is { })
            {
                _writer.WriteStartElement("S016", _targetNamespace);
                _writer.WriteElementString("D0115", _targetNamespace, batch.SubsetIdentification.Type);
                if (!string.IsNullOrEmpty(batch.SubsetIdentification.VersionNumber))
                {
                    _writer.WriteElementString("D0116", _targetNamespace, batch.SubsetIdentification.VersionNumber);
                }
                if (!string.IsNullOrEmpty(batch.SubsetIdentification.ReleaseNumber))
                {
                    _writer.WriteElementString("D0118", _targetNamespace, batch.SubsetIdentification.ReleaseNumber);
                }
                if (!string.IsNullOrEmpty(batch.SubsetIdentification.ControllingAgencyCoded))
                {
                    _writer.WriteElementString("D0051", _targetNamespace, batch.SubsetIdentification.ControllingAgencyCoded);
                }
                _writer.WriteEndElement();
            }
            if (batch.ImplementationGuidelineIdentification is { })
            {
                _writer.WriteStartElement("S017", _targetNamespace);
                _writer.WriteElementString("D0121", _targetNamespace, batch.ImplementationGuidelineIdentification.Type);
                if (!string.IsNullOrEmpty(batch.ImplementationGuidelineIdentification.VersionNumber))
                {
                    _writer.WriteElementString("D0122", _targetNamespace, batch.ImplementationGuidelineIdentification.VersionNumber);
                }
                if (!string.IsNullOrEmpty(batch.ImplementationGuidelineIdentification.ReleaseNumber))
                {
                    _writer.WriteElementString("D0124", _targetNamespace, batch.ImplementationGuidelineIdentification.ReleaseNumber);
                }
                if (!string.IsNullOrEmpty(batch.ImplementationGuidelineIdentification.ControllingAgencyCoded))
                {
                    _writer.WriteElementString("D0051", _targetNamespace, batch.ImplementationGuidelineIdentification.ControllingAgencyCoded);
                }
                _writer.WriteEndElement();
            }
            if (batch.ScenarioIdentification is { })
            {
                _writer.WriteStartElement("S018", _targetNamespace);
                _writer.WriteElementString("D0127", _targetNamespace, batch.ScenarioIdentification.Type);
                if (!string.IsNullOrEmpty(batch.ScenarioIdentification.VersionNumber))
                {
                    _writer.WriteElementString("D0128", _targetNamespace, batch.ScenarioIdentification.VersionNumber);
                }
                if (!string.IsNullOrEmpty(batch.ScenarioIdentification.ReleaseNumber))
                {
                    _writer.WriteElementString("D0130", _targetNamespace, batch.ScenarioIdentification.ReleaseNumber);
                }
                if (!string.IsNullOrEmpty(batch.ScenarioIdentification.ControllingAgencyCoded))
                {
                    _writer.WriteElementString("D0051", _targetNamespace, batch.ScenarioIdentification.ControllingAgencyCoded);
                }
                _writer.WriteEndElement();
            }
        }

        _writer.WriteEndElement();

        return EndDocument();
    }
    private XmlElement EndDocument()
    {
        _writer.WriteEndDocument();
        _writer.Close();

        XmlDocument doc = new()
        {
            Schemas = _schemaSet
        };
        _ms.Position = 0;
        doc.Load(_ms);


        ValidateElement(doc.DocumentElement!);

        return doc.DocumentElement!;
    }
    private void StartDocument()
    {
        _ms.SetLength(0);
        _writer = XmlWriter.Create(_ms, _xws);

        _writer.WriteStartDocument();
    }
    private XmlElement InterchangeTrailerToXml()
    {
        StartDocument();

        InteractiveInterchangeHeader? interactive = _interchangeHeader as InteractiveInterchangeHeader;
        BatchInterchangeHeader? batch = _interchangeHeader as BatchInterchangeHeader;

        if (interactive is { })
        {
            _writer.WriteStartElement(s_une, _targetNamespace);
            DialogueReferenceToXml(interactive.DialogueReference);
        }
        else
        {
            _writer.WriteStartElement(s_unz, _targetNamespace);
        }
        _writer.WriteElementString("D0036", _targetNamespace, _interchangeControlCount.ToString());
        if (interactive is { } && !string.IsNullOrEmpty(interactive.DuplicateIndicator))
        {
            _writer.WriteElementString("D0325", _targetNamespace, interactive.DuplicateIndicator);
        }
        else
        {
            _writer.WriteElementString("D0020", _targetNamespace, batch!.ControlReference);
        }

        _writer.WriteEndElement();

        return EndDocument();
    }
    private XmlElement GroupTrailerToXml()
    {
        StartDocument();

        _writer.WriteStartElement(s_une, _targetNamespace);

        _writer.WriteElementString("D0060", _targetNamespace, _groupControlCount.ToString());
        _writer.WriteElementString("D0048", _targetNamespace, _groupHeader!.GroupReferenceNumber);

        _writer.WriteEndElement();

        return EndDocument();
    }
    private XmlElement GroupHeaderToXml()
    {
        StartDocument();

        _writer.WriteStartElement(s_ung, _targetNamespace);
        if (_groupHeader!.MessageGroupIdentification is { } && !string.IsNullOrEmpty(_groupHeader.MessageGroupIdentification.Type))
        {
            _writer.WriteElementString("D0038", _targetNamespace, _groupHeader.MessageGroupIdentification.Type);
        }
        if (_groupHeader.ApplicationSender is { })
        {
            _writer.WriteElementString("D0040", _targetNamespace, _groupHeader.ApplicationSender.Identification);
            if (!string.IsNullOrEmpty(_groupHeader.ApplicationSender.CodeQualifier))
            {
                _writer.WriteElementString("D0007", _targetNamespace, _groupHeader.ApplicationSender.CodeQualifier);
            }
        }
        if (_groupHeader.ApplicationRecipient is { })
        {
            _writer.WriteElementString("D0044", _targetNamespace, _groupHeader.ApplicationRecipient.Identification);
            if (!string.IsNullOrEmpty(_groupHeader.ApplicationRecipient.CodeQualifier))
            {
                _writer.WriteElementString("D0007", _targetNamespace, _groupHeader.ApplicationRecipient.CodeQualifier);
            }
        }
        if (_groupHeader.DateAndTimeOfPreparation is { })
        {
            _writer.WriteElementString("D0017", _targetNamespace, _groupHeader.DateAndTimeOfPreparation.Date);
            _writer.WriteElementString("D0019", _targetNamespace, _groupHeader.DateAndTimeOfPreparation.Time);
        }
        _writer.WriteElementString("D0048", _targetNamespace, _groupHeader.GroupReferenceNumber);
        if (_groupHeader.MessageGroupIdentification is { })
        {
            if (!string.IsNullOrEmpty(_groupHeader.MessageGroupIdentification.ControllingAgencyCoded))
            {
                _writer.WriteElementString("D0051", _targetNamespace, _groupHeader.MessageGroupIdentification.ControllingAgencyCoded);
            }
            _writer.WriteElementString("D0052", _targetNamespace, _groupHeader.MessageGroupIdentification.VersionNumber);
            _writer.WriteElementString("D0054", _targetNamespace, _groupHeader.MessageGroupIdentification.ReleaseNumber);
            if (!string.IsNullOrEmpty(_groupHeader.MessageGroupIdentification.AssociationAssignedCode))
            {
                _writer.WriteElementString("D0057", _targetNamespace, _groupHeader.MessageGroupIdentification.AssociationAssignedCode);
            }
        }
        if (!string.IsNullOrEmpty(_groupHeader.ApplicationPassword))
        {
            _writer.WriteElementString("D0058", _targetNamespace, _groupHeader.ApplicationPassword);
        }
        _writer.WriteEndElement();

        return EndDocument();
    }
    private XmlElement InterchangeHeaderToXml()
    {
        StartDocument();

        InteractiveInterchangeHeader? interactive = _interchangeHeader as InteractiveInterchangeHeader;
        BatchInterchangeHeader? batch = _interchangeHeader as BatchInterchangeHeader;

        if (interactive is { })
        {
            Uri interactiveUri = new(_schemas, s_interactiveInterchangeXsd);
            _schemaSet.Add(_targetNamespace, interactiveUri.ToString());
            _schemaSet.Compile();

            _writer.WriteStartElement("UIB", _targetNamespace);



        }
        else if (batch is { })
        {
            Uri batchUri = new(_schemas, s_batchInterchangeXsd);
            _schemaSet.Add(_targetNamespace, batchUri.ToString());
            _schemaSet.Compile();

            _writer.WriteStartElement("UNB", _targetNamespace);
        }
        _writer.WriteStartElement("S001", _targetNamespace);
        _writer.WriteElementString("D0001", _targetNamespace, _interchangeHeader!.SyntaxIdentifier.Identifier);
        _writer.WriteElementString("D0002", _targetNamespace, _interchangeHeader.SyntaxIdentifier.VersionNumber);

        if (!string.IsNullOrEmpty(_interchangeHeader.SyntaxIdentifier.ServiceCodeListDirectoryVersionNumber))
        {
            _writer.WriteElementString("D0080", _targetNamespace, _interchangeHeader.SyntaxIdentifier.ServiceCodeListDirectoryVersionNumber);
        }
        if (!string.IsNullOrEmpty(_interchangeHeader.SyntaxIdentifier.CharacterEncodingCoded))
        {
            _writer.WriteElementString("D0133", _targetNamespace, _interchangeHeader.SyntaxIdentifier.CharacterEncodingCoded);
        }
        _writer.WriteEndElement();

        if (interactive is { })
        {
            DialogueReferenceToXml(interactive.DialogueReference);
            if (interactive.TransactionReference is { })
            {
                _writer.WriteStartElement("S303", _targetNamespace);

                _writer.WriteElementString("D0306", _targetNamespace, interactive.TransactionReference.TransactionControlReference);

                if (!string.IsNullOrEmpty(interactive.TransactionReference.InitiatorReferenceIdentification))
                {
                    _writer.WriteElementString("D0303", _targetNamespace, interactive.TransactionReference.InitiatorReferenceIdentification);
                }
                if (!string.IsNullOrEmpty(interactive.TransactionReference.ControllingAgencyCoded))
                {
                    _writer.WriteElementString("D0051", _targetNamespace, interactive.TransactionReference.ControllingAgencyCoded);
                }
                _writer.WriteEndElement();
            }
            if (interactive.ScenarioIdentification is { })
            {
                _writer.WriteStartElement("S018", _targetNamespace);

                _writer.WriteElementString("D0127", _targetNamespace, interactive.ScenarioIdentification.Type);
                if (!string.IsNullOrEmpty(interactive.ScenarioIdentification.VersionNumber))
                {
                    _writer.WriteElementString("D0128", _targetNamespace, interactive.ScenarioIdentification.VersionNumber);
                }
                if (!string.IsNullOrEmpty(interactive.ScenarioIdentification.ReleaseNumber))
                {
                    _writer.WriteElementString("D0130", _targetNamespace, interactive.ScenarioIdentification.ReleaseNumber);
                }
                if (!string.IsNullOrEmpty(interactive.ScenarioIdentification.ControllingAgencyCoded))
                {
                    _writer.WriteElementString("D0051", _targetNamespace, interactive.ScenarioIdentification.ControllingAgencyCoded);
                }

                _writer.WriteEndElement();
            }
            if (interactive.DialogueIdentification is { })
            {
                _writer.WriteStartElement("S305", _targetNamespace);

                _writer.WriteElementString("D0311", _targetNamespace, interactive.DialogueIdentification.Type);
                if (!string.IsNullOrEmpty(interactive.DialogueIdentification.VersionNumber))
                {
                    _writer.WriteElementString("D0342", _targetNamespace, interactive.DialogueIdentification.VersionNumber);
                }
                if (!string.IsNullOrEmpty(interactive.DialogueIdentification.ReleaseNumber))
                {
                    _writer.WriteElementString("D0344", _targetNamespace, interactive.DialogueIdentification.ReleaseNumber);
                }
                if (!string.IsNullOrEmpty(interactive.DialogueIdentification.ControllingAgencyCoded))
                {
                    _writer.WriteElementString("D0051", _targetNamespace, interactive.DialogueIdentification.ControllingAgencyCoded);
                }

                _writer.WriteEndElement();
            }
            if (interactive.Sender is { })
            {
                _writer.WriteStartElement("S002", _targetNamespace);
                _writer.WriteElementString("D0004", _targetNamespace, interactive.Sender.Identification);
                if (!string.IsNullOrEmpty(interactive.Sender.CodeQualifier))
                {
                    _writer.WriteElementString("D0007", _targetNamespace, interactive.Sender.CodeQualifier);
                }
                if (!string.IsNullOrEmpty(interactive.Sender.InternalIdentification))
                {
                    _writer.WriteElementString("D0014", _targetNamespace, interactive.Sender.InternalIdentification);
                }
                if (!string.IsNullOrEmpty(interactive.Sender.InternalSubIdentification))
                {
                    _writer.WriteElementString("D0046", _targetNamespace, interactive.Sender.InternalSubIdentification);
                }
                _writer.WriteEndElement();
            }
            if (interactive.Recipient is { })
            {
                _writer.WriteStartElement("S003", _targetNamespace);
                _writer.WriteElementString("D0010", _targetNamespace, interactive.Recipient.Identification);
                if (!string.IsNullOrEmpty(interactive.Recipient.CodeQualifier))
                {
                    _writer.WriteElementString("D0007", _targetNamespace, interactive.Recipient.CodeQualifier);
                }
                if (!string.IsNullOrEmpty(interactive.Recipient.InternalIdentification))
                {
                    _writer.WriteElementString("D0014", _targetNamespace, interactive.Recipient.InternalIdentification);
                }
                if (!string.IsNullOrEmpty(interactive.Recipient.InternalSubIdentification))
                {
                    _writer.WriteElementString("D0046", _targetNamespace, interactive.Recipient.InternalSubIdentification);
                }
                _writer.WriteEndElement();
            }
            if (interactive.DateAndTimeOfInitiation is { })
            {
                if (!string.IsNullOrEmpty(interactive.DateAndTimeOfInitiation.Date))
                {
                    _writer.WriteElementString("D0338", _targetNamespace, interactive.DateAndTimeOfInitiation.Date);
                }
                if (!string.IsNullOrEmpty(interactive.DateAndTimeOfInitiation.Time))
                {
                    _writer.WriteElementString("D0314", _targetNamespace, interactive.DateAndTimeOfInitiation.Time);
                }
                if (!string.IsNullOrEmpty(interactive.DateAndTimeOfInitiation.UtcOffset))
                {
                    _writer.WriteElementString("D0336", _targetNamespace, interactive.DateAndTimeOfInitiation.UtcOffset);
                }
            }
            if (!string.IsNullOrEmpty(interactive.DuplicateIndicator))
            {
                _writer.WriteElementString("D0325", _targetNamespace, interactive.DuplicateIndicator);
            }
        }
        else 
        {
            _writer.WriteStartElement("S002", _targetNamespace);
            _writer.WriteElementString("D0004", _targetNamespace, batch!.Sender!.Identification);
            if (!string.IsNullOrEmpty(batch.Sender!.CodeQualifier))
            {
                _writer.WriteElementString("D0007", _targetNamespace, batch.Sender!.CodeQualifier);
            }
            if (!string.IsNullOrEmpty(batch.Sender!.InternalIdentification))
            {
                _writer.WriteElementString("D0014", _targetNamespace, batch.Sender!.InternalIdentification);
            }
            if (!string.IsNullOrEmpty(batch.Sender!.InternalSubIdentification))
            {
                _writer.WriteElementString("D0046", _targetNamespace, batch.Sender!.InternalSubIdentification);
            }
            _writer.WriteEndElement();

            _writer.WriteStartElement("S003", _targetNamespace);
            _writer.WriteElementString("D0010", _targetNamespace, batch.Recipient!.Identification);
            if (!string.IsNullOrEmpty(batch.Recipient!.CodeQualifier))
            {
                _writer.WriteElementString("D0007", _targetNamespace, batch.Recipient!.CodeQualifier);
            }
            if (!string.IsNullOrEmpty(batch.Recipient!.InternalIdentification))
            {
                _writer.WriteElementString("D0014", _targetNamespace, batch.Recipient!.InternalIdentification);
            }
            if (!string.IsNullOrEmpty(batch.Recipient!.InternalSubIdentification))
            {
                _writer.WriteElementString("D0046", _targetNamespace, batch.Recipient!.InternalSubIdentification);
            }
            _writer.WriteEndElement();

            _writer.WriteStartElement("S004", _targetNamespace);
            _writer.WriteElementString("D0017", _targetNamespace, batch.DateAndTimeOfPreparation!.Date);
            _writer.WriteElementString("D0019", _targetNamespace, batch.DateAndTimeOfPreparation!.Time);
            _writer.WriteEndElement();

            _writer.WriteElementString("D0020", _targetNamespace, batch.ControlReference);

            if (batch.RecipientReferencePasswordDetails is { })
            {
                _writer.WriteStartElement("S005", _targetNamespace);
                _writer.WriteElementString("D0022", _targetNamespace, batch.RecipientReferencePasswordDetails.ReferenceOrPassword);
                if (!string.IsNullOrEmpty(batch.RecipientReferencePasswordDetails.Qualifier))
                {
                    _writer.WriteElementString("D0025", _targetNamespace, batch.RecipientReferencePasswordDetails.Qualifier);
                }
                _writer.WriteEndElement();
            }
            if (!string.IsNullOrEmpty(batch.ApplicationReference))
            {
                _writer.WriteElementString("D0026", _targetNamespace, batch.ApplicationReference);
            }
            if (!string.IsNullOrEmpty(batch.PriorityCode))
            {
                _writer.WriteElementString("D0029", _targetNamespace, batch.PriorityCode);

            }
            if (!string.IsNullOrEmpty(batch.AcknowledgementRequest))
            {
                _writer.WriteElementString("D0031", _targetNamespace, batch.AcknowledgementRequest);

            }
            if (!string.IsNullOrEmpty(batch.AgreementIdentifier))
            {
                _writer.WriteElementString("D0032", _targetNamespace, batch.AgreementIdentifier);
            }
        }
        if (!string.IsNullOrEmpty(_interchangeHeader.TestIndicator))
        {
            _writer.WriteElementString("D0035", _targetNamespace, _interchangeHeader.TestIndicator);
        }
        _writer.WriteEndElement();

        return EndDocument();
    }
    private void DialogueReferenceToXml(DialogueReference? dialogue)
    {
        if (dialogue is { })
        {
            _writer.WriteStartElement("S302", _targetNamespace);

            _writer.WriteElementString("D0300", _targetNamespace, dialogue.InitiatorControlReference);
            if (!string.IsNullOrEmpty(dialogue.InitiatorReferenceIdentification))
            {
                _writer.WriteElementString("D0303", _targetNamespace, dialogue.InitiatorReferenceIdentification);
            }
            if (!string.IsNullOrEmpty(dialogue.ControllingAgencyCoded))
            {
                _writer.WriteElementString("D0051", _targetNamespace, dialogue.ControllingAgencyCoded);
            }
            if (!string.IsNullOrEmpty(dialogue.ResponderControlReference))
            {
                _writer.WriteElementString("D0304", _targetNamespace, dialogue.ResponderControlReference);
            }

            _writer.WriteEndElement();
        }
    }
    private void ValidateElement(XmlElement element)
    {
        if (element.SchemaInfo.Validity is XmlSchemaValidity.NotKnown)
        {
            element.SetAttribute("type", Properties.Resources.schema_instance_ns, element.LocalName);
            element.OwnerDocument.Validate(SchemaSet_ValidationEventHandler, element);
        }
    }
}
