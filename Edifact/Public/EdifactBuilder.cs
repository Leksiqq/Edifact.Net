using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Net.Leksi.Streams;
using System.Text;
using System.Xml;
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
        await _output.WriteLineAsync(element.OuterXml);

    }
    private XmlDocument InterchangeHeaderToXml(InterchangeHeader header)
    {
        _ms.SetLength(0);
        _writer = XmlWriter.Create(_ms, _xws);

        _writer.WriteStartDocument();
        if (header is InteractiveInterchangeHeader interactive)
        {

        }
        else if(header is BatchInterchangeHeader batch)
        {
            _writer.WriteStartElement("S001", _targetNamespace);
            if (batch.SyntaxIdentifier.Identifier is { })
            {
                _writer.WriteElementString("D0001", _targetNamespace, batch.SyntaxIdentifier.Identifier);
            }
            if (batch.SyntaxIdentifier.VersionNumber is { })
            {
                _writer.WriteElementString("D0002", _targetNamespace, batch.SyntaxIdentifier.VersionNumber);
            }
            if (batch.SyntaxIdentifier.ServiceCodeListDirectoryVersionNumber is { })
            {
                _writer.WriteElementString("D0080", _targetNamespace, batch.SyntaxIdentifier.ServiceCodeListDirectoryVersionNumber);
            }
            if (batch.SyntaxIdentifier.CharacterEncodingCoded is { })
            {
                _writer.WriteElementString("D0133", _targetNamespace, batch.SyntaxIdentifier.CharacterEncodingCoded);
            }
            _writer.WriteEndElement();
        }
        _writer.WriteEndDocument();

        XmlDocument result = new();
        _ms.Position = 0;
        result.Load(_ms);

        return result;
    }
}
