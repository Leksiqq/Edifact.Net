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
            if (string.IsNullOrEmpty(_options.MessageIdentifier))
            {
                throw new Exception($"TODO: --{s_message} is mandatary.");
            }
            if (string.IsNullOrEmpty(_options.SchemasUri))
            {
                throw new Exception($"TODO: --{s_schemasRoot} is mandatary.");
            }
            if (string.IsNullOrEmpty(_options.ScriptUri))
            {
                throw new Exception($"TODO: --{s_script} is mandatary.");
            }
            if (string.IsNullOrEmpty(_options.Suffix))
            {
                throw new Exception($"TODO: --{s_suffix} is mandatary.");
            }
            _nameTable = new NameTable();
            _man = new(_nameTable);
            _man.AddNamespace(s_xsPrefix, Properties.Resources.schema_ns);
            _schemaSet = new()
            {
                XmlResolver = _resolver,
            };
            _schemaSet.ValidationEventHandler += _schemaSet_ValidationEventHandler;
            Match match = s_reMessageIdentifier.Match(_options.MessageIdentifier!);
            if (!match.Success)
            {
                throw new Exception($"TODO: Not a message identifier: {_options.MessageIdentifier}.");
            }
            Uri inputUri = new(
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

            IStreamFactory streamFactory = _services.GetRequiredKeyedService<IStreamFactory>(inputUri.Scheme);
            Stream input = streamFactory.GetInputStream(inputUri)
                ?? throw new Exception($"TODO: File does not exist: {inputUri}");
            XmlDocument doc = new(_nameTable);
            doc.Load(input);
            XPathNavigator nav = doc.CreateNavigator()!;
            if (nav.SelectSingleNode(s_targetNamespaceXPath1, _man) is not XPathNavigator tns)
            {
                throw new Exception("TODO: not schema.");
            }
            _schemaSet.Add(tns.Value, inputUri.ToString());
            _schemaSet.Compile();
            nav.SelectSingleNode(s_messageFirstChildXpath)?
                .ReplaceSelf(
                    string.Format(
                        s_messageTypeAndVersion, 
                        match.Groups[s_type].Value, 
                        _options.Suffix,
                        match.Groups[s_version].Value,
                        match.Groups[s_release].Value,
                        match.Groups[s_agensy].Value
                    )
                );




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
        catch(Exception ex)
        {
            _logger?.LogCritical(ex, string.Empty);
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
}
