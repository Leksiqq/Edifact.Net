using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Net.Leksi.Streams;
using System.Web;
using System.Xml;
using System.Xml.Schema;
using System.Xml.XPath;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

public class MessageSchemaCustomizer
{
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

    public async Task Customize(MessageSchemaCustomizerOptions options)
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
            await InvokeAction();
        }
        finally
        {
            Interlocked.Decrement(ref _entersNum);
        }
        //XmlDocument doc = new(nameTable);
        //Uri customUri = new(options.CustomSchemaUri!);
        //IStreamFactory customStreamFactory = _services.GetRequiredKeyedService<IStreamFactory>(customUri.Scheme);
        //Stream input = customStreamFactory.GetInputStream(customUri);
        //doc.Load(input);
        //input.Close();

        //XmlWriterSettings xws = new() 
        //{
        //    Indent = true,
        //};
        //XmlWriter writer = XmlWriter.Create(customStreamFactory.GetOutputStream(customUri, FileMode.Create), xws);
        //doc.WriteTo(writer);
        //writer.Close();
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

    private async Task InvokeAction()
    {
        switch (_options.Action)
        {
            case MessageSchemaCustomizerAction.CopySchema:
                await CopySchema();
                break;
            case MessageSchemaCustomizerAction.RemoveSegmentGroup:
                await RemoveSegmentGroup();
                break;
            case MessageSchemaCustomizerAction.RemoveSegment:
                await RemoveSegment();
                break;
            case MessageSchemaCustomizerAction.ChangeType:
                await ChangeType();
                break;
            default:
                throw new InvalidOperationException("TODO: Action is missed.");
        }
    }

    private async Task ChangeType()
    {
        throw new NotImplementedException();
    }

    private async Task RemoveSegment()
    {
        throw new NotImplementedException();
    }

    private async Task RemoveSegmentGroup()
    {
        throw new NotImplementedException();
    }

    private async Task CopySchema()
    {
        Uri uri = new(_options.OriginalSchemaUri!);
        string path = HttpUtility.UrlDecode(uri.AbsolutePath);
        if (!s_reOriginalMessageXsd.IsMatch(Path.GetFileName(path)))
        {
            throw new Exception($"TODO: Seems to be not an original schema: {Path.GetFileName(path)}.");
        }

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
        await Task.CompletedTask;
    }
}
