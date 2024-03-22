using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Net.Leksi.Streams;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.XPath;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

public class EdifactProcessor(IServiceProvider services)
{
    protected readonly IServiceProvider _services = services;
    protected readonly XmlResolver _xmlResolver = new Resolver(services);
    protected readonly HashSet<string> _validationWarningsCache = [];
    protected readonly List<string> _path = [];
    protected readonly MemoryStream _ms = new();
    protected ILogger? _logger = null!;
    protected XmlDocument _interchangeHeaderXml = null!;
    protected XmlDocument _interchangeTrailerXml = null!;
    protected XmlDocument _groupHeaderXml = null!;
    protected XmlDocument _groupTrailerXml = null!;
    protected XmlDocument _messageHeaderXml = null!;
    protected XmlDocument _messageTrailerXml = null!;
    protected XmlSchemaSet _schemaSet = null!;
    protected XmlNameTable _nameTable = null!;
    protected XmlNamespaceManager _man = null!;
    protected XmlWriterSettings _xws = null!;
    protected Uri _schemas = null!;
    protected XmlWriter _writer = null!;
    protected string _targetNamespace = string.Empty;

    internal static HashSet<char> s_levelAChars = [
        'A','B','C','D','E','F','G','H','I','J','K','L','M','N','O','P','Q','R','S','T','U','V','W','X','Y','Z',
        '0','1','2','3','4','5','6','7','8','9',
        ' ','.',',','-','(',')','/','=','\'','+',':','?','!','"','%','&','*',';','<','>'
    ];
    internal static HashSet<char> s_levelBChars = [
        'A','B','C','D','E','F','G','H','I','J','K','L','M','N','O','P','Q','R','S','T','U','V','W','X','Y','Z',
        'a','b','c','d','e','f','g','h','i','j','k','l','m','n','o','p','q','r','s','t','u','v','w','x','y','z',
        '0','1','2','3','4','5','6','7','8','9',
        ' ','.',',','-','(',')','/','=','\'','+',':','?','!','"','%','&','*',';','<','>'
    ];
    internal static Encoding? SyntaxLevelToEncoding(char syntaxLevel)
    {
        switch (syntaxLevel)
        {
            case 'A' or 'B':
                return Encoding.ASCII;
            case 'C':
                return Encoding.Latin1;
            case 'D':
                return Encoding.GetEncoding("ISO-8859-2");
            case 'E':
                return Encoding.GetEncoding("ISO-8859-5");
            case 'F':
                return Encoding.GetEncoding("ISO-8859-7");
            case 'G':
                return Encoding.GetEncoding("ISO-8859-3");
            case 'H':
                return Encoding.GetEncoding("ISO-8859-4");
            case 'I':
                return Encoding.GetEncoding("ISO-8859-6");
            case 'J':
                return Encoding.GetEncoding("ISO-8859-8");
            case 'K':
                return Encoding.GetEncoding("ISO-8859-9");
        }
        return null;
    }
    internal static Encoding? CharacterEncodingCodedToEncoding(string value)
    {
        switch (value)
        {
            case "1":
                throw new NotSupportedException("TODO: 7-bit ASCII is not supported.");
            case "2":
                return Encoding.ASCII;
            case "3":
                return Encoding.GetEncoding(500);
            case "4":
                return Encoding.GetEncoding(850);
            case "5":
                return Encoding.GetEncoding("UCS-2");
            case "6":
                return Encoding.GetEncoding("UCS-4");
            case "7":
                return Encoding.UTF8;
            case "8":
                return Encoding.Unicode;
        }
        return null;
    }
    protected void SchemaSet_ValidationEventHandler(object? sender, ValidationEventArgs e)
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
    protected void InitBaseStuff()
    {
        IStreamFactory schemasStreamFactory = _services.GetKeyedService<IStreamFactory>(_schemas.Scheme)!;
        Uri edifactUri = new(_schemas, s_edifactXsd);
        XmlDocument doc = new(_nameTable);
        using (Stream edifact = _xmlResolver.GetEntity(edifactUri, null, typeof(Stream)) as Stream
            ?? throw new Exception("TODO: edifact.xsd not found."))
        {
            _man = new(_nameTable);
            _man.AddNamespace(s_xsPrefix, Properties.Resources.schema_ns);
            _man.AddNamespace("xsi", Properties.Resources.schema_instance_ns);

            doc.Load(edifact);
        }
        XPathNavigator nav = doc.CreateNavigator()!;
        if (nav.SelectSingleNode(s_targetNamespaceXPath1, _man) is not XPathNavigator tns)
        {
            throw new Exception("TODO: not schema.");
        }
        _targetNamespace = tns.Value;
        _man.AddNamespace("e", _targetNamespace);
    }

}
