using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Net.Leksi.Streams;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using System.Xml.Schema;
using System.Xml.XPath;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

public class MessageSchemaCustomizer
{
    private static readonly Regex s_reMessageIdentifier = 
        new("^(?<type>[A-Z]{6}):(?<version>[^:]{1,3}):(?<release>[^:]{1,3}):(?<agency>[^:]{1,3})$");
    private readonly IServiceProvider _services;
    private readonly ILogger<MessageSchemaCustomizer>? _logger;
    private readonly XmlResolver _resolver;
    private MessageSchemaCustomizerOptions _options;
    private int _entersNum = 0;
    private XmlNameTable _nameTable;
    private XmlNamespaceManager _man = null!;
    private XmlSchemaSet _schemaSet;

    public MessageSchemaCustomizer(IServiceProvider services)
    {
        _services = services;
        _logger = _services.GetService<ILogger<MessageSchemaCustomizer>>();
        _resolver = new Resolver(_services);
    }

    public async Task Customize(MessageSchemaCustomizerOptions options, CancellationToken cancellationToken)
    {
        try
        {
            if (Interlocked.Increment(ref _entersNum) != 1)
            {
                throw new Exception("TODO: Thread unsafety.");
            }
            _options = options;
            _nameTable = new NameTable();
            _man = new(_nameTable);
            _man.AddNamespace(s_xsPrefix, Properties.Resources.schema_ns);
            _schemaSet = new()
            {
                XmlResolver = _resolver,
            };
            _schemaSet.ValidationEventHandler += _schemaSet_ValidationEventHandler;
            await InvokeAction(cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref _entersNum);
        }
    }

    private void _schemaSet_ValidationEventHandler(object? sender, ValidationEventArgs e)
    {
        switch (e.Severity)
        {
            case XmlSeverityType.Warning:
                _logger?.LogWarning(s_logMessage, e.Message);
                break;
            case XmlSeverityType.Error:
                throw e.Exception;
        }
    }

    private async Task InvokeAction(CancellationToken cancellationToken)
    {
        switch (_options.Action)
        {
            case MessageSchemaCustomizerAction.CopySchema:
                CopySchema(cancellationToken);
                break;
            case MessageSchemaCustomizerAction.RemoveSegmentGroup:
                await RemoveSegmentGroup(cancellationToken);
                break;
            case MessageSchemaCustomizerAction.RemoveSegment:
                await RemoveSegment(cancellationToken);
                break;
            case MessageSchemaCustomizerAction.ChangeType:
                await ChangeType(cancellationToken);
                break;
            default:
                throw new InvalidOperationException("TODO: Action is missed.");
        }
    }

    private async Task ChangeType(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private async Task RemoveSegment(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private async Task RemoveSegmentGroup(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private void CopySchema(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.MessageIdentifier))
        {
            throw new Exception($"TODO: --{s_message} is mandatary.");
        }
        Match match = s_reMessageIdentifier.Match(_options.MessageIdentifier!);
        if(!match.Success)
        {
            throw new Exception($"TODO: Not a message identifier: {_options.MessageIdentifier}.");
        }
        Uri uri = new(
            new Uri(string.Format(s_folderUriFormat, _options.SchemasUri)),
            string.Format(
                s_messageXsdFormat, 
                match.Groups[s_agensy].Value,
                match.Groups[s_version].Value,
                match.Groups[s_release].Value,
                match.Groups[s_type].Value,
                string.Empty
            )
        );

        IStreamFactory streamFactory = _services.GetRequiredKeyedService<IStreamFactory>(uri.Scheme);
        Stream input = streamFactory.GetInputStream(uri)
            ?? throw new Exception($"TODO: File does not exist: {uri}");
        XmlDocument doc = new(_nameTable);
        doc.Load(input);
        XPathNavigator nav = doc.CreateNavigator()!;
        if (nav.SelectSingleNode(s_targetNamespaceXPath1, _man) is not XPathNavigator tns)
        {
            throw new Exception("TODO: not schema.");
        }
        _schemaSet.Add(tns.Value, uri.ToString());
        _schemaSet.Compile();
        if (string.IsNullOrEmpty(_options.Suffix))
        {
            throw new Exception($"TODO: --{s_suffix} is mandatary.");
        }
        Uri outputUri = new(
            new Uri(string.Format(s_folderUriFormat, _options.SchemasUri)),
            string.Format(
                s_messageXsdFormat,
                match.Groups[s_agensy].Value,
                match.Groups[s_version].Value,
                match.Groups[s_release].Value,
                match.Groups[s_type].Value,
                _options.Suffix
            )
        );
        streamFactory = _services.GetRequiredKeyedService<IStreamFactory>(outputUri.Scheme);
        Stream output = streamFactory.GetOutputStream(outputUri, FileMode.Create);
        XmlWriterSettings xws = new()
        {
            Indent = true,
        };
        XmlWriter writer = XmlWriter.Create(output, xws);
        doc.WriteTo(writer);
        writer.Close();
    }
}
