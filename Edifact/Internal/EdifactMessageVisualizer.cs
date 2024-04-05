using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.XPath;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

internal class EdifactMessageVisualizer
{
    internal const int s_deafultWidth = 80;
    private const string s_htmlBegin = @"<html>
<head>
    <meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8""/>
</head>
<body>
<h1>{0}</h1>
<pre>";
    private const string s_htmlEnd = @"</table>
</pre>
</body>
</html>";
    private const string s_x2502 = "&#x2502;";
    private const string s_nbsp = "&nbsp;";
    private const string s_x250Cx2500 = "&#x250C;&#x2500;";
    private const string s_x251Cx2500 = "&#x251C;&#x2500;";
    private const string s_x2514x2500 = "&#x2514;&#x2500;";
    private const string s_segmentGroupFormat = "<b>Segment Group {0}</b>";
    private const string s_boldFormat = "<b>{0}</b>";
    private const string s_spaceBeforeFormat = " {0}";
    private const string s_repCondFormat = "x{0} (C)";
    private const string s_repMandFormat = "x{0} (M)";
    private const string s_space = " ";
    private static readonly Regex s_reCharCode = new("^(?:[^&]*(&[^;]+;|<[^>]*>))*[^&]*$");
    private static readonly Regex s_reCollapseSpaces = new("( )+");

    private readonly StringBuilder _sb = new();
    private readonly XmlResolver _resolver;
    private int _width;
    private TextWriter? _output;

    public EdifactMessageVisualizer(IServiceProvider services)
    {
        _resolver = new Resolver(services);
    }
    public async Task RenderAsync(EdifactMessageVisualizerOptions options)
    {
        _width = options.PageWidth ?? s_deafultWidth;
        if (options.SchemasUri is null)
        {
            throw new Exception($"TODO: {nameof(options)}.{nameof(options.SchemasUri)} is mandatory.");
        }
        if (options.MessageType is null)
        {
            throw new Exception($"TODO: {nameof(options)}.{nameof(options.MessageType)} is mandatory.");
        }
        if (options.MessageDirectory is null)
        {
            throw new Exception($"TODO: {nameof(options)}.{nameof(options.MessageDirectory)} is mandatory.");
        }
        if (options.Output is null)
        {
            throw new Exception($"TODO: {nameof(options)}.{nameof(options.Output)} is mandatory.");
        }

        string schemaPath = string.Format(
            s_fileInDirectoryXsdFormat, 
            options.ControllingAgency ?? s_un,
            string.Empty,
            options.MessageDirectory,
            options.MessageType,
            options.MessageSuffix ?? string.Empty
        );

        _output = new StreamWriter(options.Output!);
        XmlNameTable xmlNameTable = new NameTable();
        XmlDocument xml = new(xmlNameTable);
        Uri inputUri = new Uri(new Uri(string.Format(s_folderUriFormat, options.SchemasUri)), schemaPath);
        if (_resolver.GetEntity(inputUri, null, typeof(Stream) ) is Stream input)
        {
            xml.Load(XmlReader.Create(input));
        }
        else
        {
            throw new Exception($"TODO: {inputUri} not found.");
        }

        if (xml.DocumentElement!.NamespaceURI != Properties.Resources.schema_ns)
        {
            throw new Exception(string.Format(s_rmLabels.GetString(s_notSchema)!, inputUri));
        }
        XmlNamespaceManager man = new(xmlNameTable);
        man.AddNamespace(xml.DocumentElement.Prefix, xml.DocumentElement!.NamespaceURI);
        man.AddNamespace(s_euPrefix, Properties.Resources.edifact_utility_ns);
        XPathNavigator nav = xml.CreateNavigator()!;
        XPathNavigator? schema = nav.SelectSingleNode(string.Format(s_schemaXPathFormat, xml.DocumentElement.Prefix), man) 
            ?? throw new Exception(string.Format(s_rmLabels.GetString(s_notSchema)!, inputUri));
        string ns = schema.SelectSingleNode(s_targetNamespaceXPath)?.Value ?? string.Empty;
        string suffix = nav.SelectSingleNode(string.Format(s_appinfoXPathFormat, s_suffix), man)?.Value ?? string.Empty;
        string messageIdentifier = nav.SelectSingleNode(string.Format(s_appinfoXPathFormat, s_messageIdentifier), man)?.Value ?? string.Empty;
        if(!string.IsNullOrEmpty(suffix) && !string.IsNullOrEmpty(messageIdentifier))
        {
            string[] parts = messageIdentifier.Split(':');
            messageIdentifier = string.Format(
                s_messageIdentifierFormat,
                string.Format(s_uriFormat, parts[0], suffix),
                parts[1], parts[2], parts[3]
            );
        }

        XmlQualifiedName root_qname = new(s_message1, ns);
        XmlSchemaSet xmlSchemaSet = new(xmlNameTable)
        {
            XmlResolver = _resolver,
        };

        xmlSchemaSet.Add(ns, inputUri.ToString());
        xmlSchemaSet.Compile();
        if (xmlSchemaSet.GlobalElements[root_qname] is not XmlSchemaElement root)
        {
            throw new Exception(string.Format(s_rmLabels.GetString(s_notEdifactSchema)!, inputUri));
        }

        await _output.WriteLineAsync(string.Format(s_htmlBegin, messageIdentifier, suffix));

        List<int> positions = [];
        List<bool> vlines = [];
        List<XmlSchemaElement> elementsStack = [];
        positions.Add(0);
        vlines.Add(true);
        elementsStack.Add(root);
        while (elementsStack.Count > 0)
        {
            if(
                elementsStack[^1].ElementSchemaType is XmlSchemaComplexType ct 
                && ct.ContentTypeParticle is XmlSchemaSequence seq
            )
            {
                while (true)
                {
                    bool changed = false;
                    int startPosition = 1;
                    while(positions[^1] < seq.Items.Count
                        && seq.Items[positions[^1]] is not XmlSchemaElement)
                    {
                        ++positions[^1];
                        ++startPosition;
                    }
                    if (
                        positions[^1] < seq.Items.Count 
                        && seq.Items[positions[^1]] is XmlSchemaElement cur
                    )
                    {
                        ++positions[^1];
                        for (int i = 0; i < positions.Count - 1; i++)
                        {
                            if (vlines[i])
                            {
                                await WriteAsync(s_x2502);
                            }
                            else
                            {
                                await WriteAsync(s_nbsp);
                            }
                            await WriteAsync(s_nbsp);
                        }
                        if (positions[^1] < seq.Items.Count || positions.Count == 1)
                        {
                            if (positions.Count == 1 && positions[^1] == startPosition)
                            {
                                await WriteAsync(s_x250Cx2500);
                            }
                            else if (positions.Count == 1 && positions[^1] == seq.Items.Count)
                            {
                                await WriteAsync(s_x2514x2500);
                            }
                            else
                            {
                                await WriteAsync(s_x251Cx2500);
                            }
                        }
                        else
                        {
                            await WriteAsync(s_x2514x2500);
                        }
                        bool sg = false;
                        Match m = s_reSegmentGroup.Match(cur.Name!);
                        if (m.Success)
                        {
                            await WriteAsync(string.Format(s_segmentGroupFormat, m.Groups[s_code].Value));
                            vlines[positions.Count - 1] = (positions[^1] < seq.Items.Count || positions.Count == 1) 
                                && !(positions.Count == 1 && positions[^1] == seq.Items.Count);
                            positions.Add(0);
                            elementsStack.Add(cur);
                            if (vlines.Count < positions.Count)
                            {
                                vlines.Add(false);
                            }
                            sg = true;
                        }
                        else
                        {
                            await WriteAsync(string.Format(s_boldFormat, cur.Name));
                        }
                        if (
                            cur.ElementSchemaType!.Annotation is { } 
                            && cur.ElementSchemaType.Annotation.Items[0] is XmlSchemaDocumentation doc)
                        {
                            await WriteAsync(string.Format(s_spaceBeforeFormat, s_reCollapseSpaces.Replace(doc.Markup![0]!.Value!, s_space)));
                        }
                        if (cur.MinOccurs == 0)
                        {
                            await WriteAsync(string.Format(s_repCondFormat, cur.MaxOccurs), true);
                        }
                        else
                        {
                            await WriteAsync(string.Format(s_repMandFormat, cur.MaxOccurs), true);
                        }
                        if (sg)
                        {
                            break;
                        }
                        changed = true;
                    }
                    if (positions[^1] == seq.Items.Count)
                    {
                        elementsStack.RemoveAt(elementsStack.Count - 1);
                        positions.RemoveAt(positions.Count - 1);
                        break;
                    }
                    if (!changed)
                    {
                        throw new Exception();
                    }
                }
            }
        }
        await _output.WriteLineAsync(s_htmlEnd);
        await _output.FlushAsync();
        await options.Output!.FlushAsync();
        options.Output!.Close();
    }

    private async Task WriteAsync(string str, bool end = false)
    {
        if (end)
        {
            Match m = s_reCharCode.Match(_sb.ToString());
            int len = _sb.Length;
            foreach (Capture c in m.Groups[1].Captures.Cast<Capture>())
            {
                len -= c.Length;
                if (c.Value.StartsWith('&'))
                {
                    len++;
                }
            }
            int rest = _width - len - str.Length;
            if (rest <= 0)
            {
                rest = 1;
            }
            while (rest > 0)
            {
                --rest;
                _sb.Append('.');
            }
            _sb.Append(str);
            await _output!.WriteLineAsync(_sb.ToString());
            _sb.Clear();
        }
        else
        {
            _sb.Append(str);
        }
    }
}
