using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Net.Leksi.Streams;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.XPath;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

public class EdifactBuilder: EdifactProcessor
{
    private EdifactBuilderOptions _options = null!;
    private char _syntaxLevel = 'A';
    private TextWriter _output = null!;
    public EdifactBuilder(IServiceProvider services) : base(services)
    {
        _logger = _services.GetService<ILogger<EdifactBuilder>>();
    }
    public async Task BeginInterchangeAsync(EdifactBuilderOptions options, InterchangeHeader header)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        if (header is null)
        {
            throw new ArgumentNullException(nameof(header));
        }
        if (options.OutputUri is null)
        {
            throw new ArgumentNullException($"{nameof(options)}.{nameof(options.OutputUri)}");
        }
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _options = options;
        _nameTable = new NameTable();
        _path.Clear();

        Uri output = new(options.OutputUri!);
        IStreamFactory outputStreamFactory = _services.GetRequiredKeyedService<IStreamFactory>(output.Scheme)!;
        Stream outputStream = outputStreamFactory.GetOutputStream(output);

        byte[] una = new byte[]{(byte)'U', (byte)'N', (byte)'A',
            (byte)_options.ComponentPartsSeparator, (byte)_options.SegmentPartsSeparator, (byte)_options.DecimalMark,
            (byte)_options.ReleaseCharacter, (byte)'*', (byte)_options.SegmentTerminator
            }.Concat((_options.SegmentsSeparator?.ToCharArray() ?? []).Select(ch => (byte)ch)).ToArray();

        await outputStream.WriteAsync(una);

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
        if (encoding is null) 
        {
            encoding = EdifactProcessor.SyntaxLevelToEncoding(_syntaxLevel); 
        }

        if(
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
        _output = new StreamWriter(outputStream, encoding);
        try
        {
            InitBaseStuff();

            _interchangeHeaderXml = InterchangeHeaderToXml(header);
            await WriteSegmentAsync(_interchangeHeaderXml.DocumentElement!);
        }
        finally
        {
            _output.Flush();
            _output.Close();
            outputStream.Close();
        }

    }
    public async Task BeginGroup(GroupHeader header)
    {
        await Task.CompletedTask;
    }
    public async Task SendMessage(MessageHeader header, Stream input)
    {
        await Task.CompletedTask;
    }
    public async Task EndGroup()
    {
        await Task.CompletedTask;
    }
    public async Task EndInterchange()
    {
        await Task.CompletedTask;
    }
    private async Task WriteSegmentAsync(XmlElement element)
    {
        await _output.WriteAsync(element.LocalName);
        XmlSchemaComplexType ct = (XmlSchemaComplexType)element.SchemaInfo.SchemaType!;
        XmlSchemaSequence seq = (XmlSchemaSequence)ct.ContentTypeParticle!;
        XPathNodeIterator components = element.CreateNavigator()!.SelectChildren(XPathNodeType.Element);
        int pos = 0;
        int occurs = 0;
        while(components.MoveNext())
        {
            while(pos < seq.Items.Count && ((XmlSchemaElement)seq.Items[pos]).Name != components.Current!.LocalName)
            {
                if(occurs == 0)
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
    }

    private async Task WriteComponentAsync(XmlElement element)
    {
        XmlSchemaComplexType ct = (XmlSchemaComplexType)element.SchemaInfo.SchemaType!;
        XmlSchemaSequence? seq = ct.ContentTypeParticle as XmlSchemaSequence;
        if(seq is { })
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
                    if(pos > 0)
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
            await _output.WriteAsync(element.InnerText);
        }
    }
    private XmlDocument InterchangeHeaderToXml(InterchangeHeader header)
    {
        _ms.SetLength(0);
        _writer = XmlWriter.Create(_ms, _xws);

        _writer.WriteStartDocument();
        if (header is InteractiveInterchangeHeader)
        {
            Uri interactiveUri = new(_schemas, s_interactiveInterchangeXsd);
            _schemaSet.Add(_targetNamespace, interactiveUri.ToString());
            _schemaSet.Compile();

            _writer.WriteStartElement("UIB", _targetNamespace);



        }
        else if (header is BatchInterchangeHeader)
        {
            Uri batchUri = new(_schemas, s_batchInterchangeXsd);
            _schemaSet.Add(_targetNamespace, batchUri.ToString());
            _schemaSet.Compile();

            _writer.WriteStartElement("UNB", _targetNamespace);
        }
        _writer.WriteStartElement("S001", _targetNamespace);
        _writer.WriteElementString("D0001", _targetNamespace, header.SyntaxIdentifier.Identifier);
        _writer.WriteElementString("D0002", _targetNamespace, header.SyntaxIdentifier.VersionNumber);

        if (!string.IsNullOrEmpty(header.SyntaxIdentifier.ServiceCodeListDirectoryVersionNumber))
        {
            _writer.WriteElementString("D0080", _targetNamespace, header.SyntaxIdentifier.ServiceCodeListDirectoryVersionNumber);
        }
        if (!string.IsNullOrEmpty(header.SyntaxIdentifier.CharacterEncodingCoded))
        {
            _writer.WriteElementString("D0133", _targetNamespace, header.SyntaxIdentifier.CharacterEncodingCoded);
        }
        _writer.WriteEndElement();

        if (header is InteractiveInterchangeHeader interactive)
        {
            if (interactive.DialogueReference is { })
            {
                _writer.WriteStartElement("S302", _targetNamespace);

                _writer.WriteElementString("D0300", _targetNamespace, interactive.DialogueReference.InitiatorControlReference);
                if (!string.IsNullOrEmpty(interactive.DialogueReference.InitiatorReferenceIdentification))
                {
                    _writer.WriteElementString("D0303", _targetNamespace, interactive.DialogueReference.InitiatorReferenceIdentification);
                }
                if (!string.IsNullOrEmpty(interactive.DialogueReference.ControllingAgencyCoded))
                {
                    _writer.WriteElementString("D0051", _targetNamespace, interactive.DialogueReference.ControllingAgencyCoded);
                }
                if (!string.IsNullOrEmpty(interactive.DialogueReference.ResponderControlReference))
                {
                    _writer.WriteElementString("D0304", _targetNamespace, interactive.DialogueReference.ResponderControlReference);
                }

                _writer.WriteEndElement();
            }
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
            if(interactive.ScenarioIdentification is { })
            {
                _writer.WriteStartElement("S018", _targetNamespace);
                
                _writer.WriteElementString("D0127", _targetNamespace, interactive.ScenarioIdentification.Identifier);
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
            if(interactive.DialogueIdentification is { })
            {
                _writer.WriteStartElement("S305", _targetNamespace);

                _writer.WriteElementString("D0311", _targetNamespace, interactive.DialogueIdentification.Identifier);
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
            if(interactive.Sender is { })
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
            if(interactive.Recipient is { })
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
            if(interactive.DateAndTimeOfInitiation is { })
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
            if(!string.IsNullOrEmpty(interactive.DuplicateIndicator))
            {
                _writer.WriteElementString("D0325", _targetNamespace, interactive.DuplicateIndicator);
            }
        }
        else if (header is BatchInterchangeHeader batch)
        {
            _writer.WriteStartElement("S002", _targetNamespace);
            _writer.WriteElementString("D0004", _targetNamespace, batch.Sender!.Identification);
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
        if (!string.IsNullOrEmpty(header.TestIndicator))
        {
            _writer.WriteElementString("D0035", _targetNamespace, header.TestIndicator);
        }
        _writer.WriteEndElement();
        _writer.WriteEndDocument();
        _writer.Close();

        XmlDocument result = new()
        {
            Schemas = _schemaSet
        };
        _ms.Position = 0;
        result.Load(_ms);

        ValidateElement(result.DocumentElement!);

        return result;
    }

    private void ValidateElement(XmlElement element)
    {
        if(element.SchemaInfo.Validity is XmlSchemaValidity.NotKnown)
        {
            element.SetAttribute("xmlns:e", _targetNamespace);
            element.SetAttribute("type", Properties.Resources.schema_instance_ns, string.Format("e:{0}", element.LocalName));
            element.OwnerDocument.Validate(SchemaSet_ValidationEventHandler, element);
        }
    }
}
