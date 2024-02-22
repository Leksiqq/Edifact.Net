using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;

namespace Net.Leksi.Edifact;

public class Schema2Tree
{
    private const string s_message = "MESSAGE";
    private const string s_notEdifactSchema = "Not EDIFACT schema!";
    private const string s_htmlBegin = @"<html>
<head>
    <meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8""/>
</head>
<body>
<pre>";
    private const string s_htmlEnd = @"</table>
</pre>
</body>
</html>";
    private const string s_messageHeader = "&#x251C;&#x2500;<b>UNH</b> Mesage header";
    private const string s_unhRep = "x1 (M)";
    private const string s_unb = "UNB";
    private const string s_ung = "UNG";
    private const string s_unh = "UNH";
    private const string s_x2502 = "&#x2502;";
    private const string s_nbsp = "&nbsp;";
    private const string s_x251Cx2500 = "&#x251C;&#x2500;";
    private const string s_x2514x2500 = "&#x2514;&#x2500;";
    private const string s_sg = "SG-";
    private const string s_segmentGroupFormat = "<b>Segment Group {0}</b>";
    private const string s_boldFormat = "<b>{0}</b>";
    private const string s_spaceBeforeFormat = " {0}";
    private const string s_repCondFormat = "x{0} (C)";
    private const string s_repMandFormat = "x{0} (M)";
    private const string s_messageTrailer = "&#x2514;&#x2500;<b>UNT</b> Mesage trailer";
    private const string s_space = " ";
    private static  readonly Regex s_re = new("^(?:[^&]*(&[^;]+;|<[^>]*>))*[^&]*$");
    private static readonly Regex s_reCollapseSpaces = new("( )+");

    private readonly StringBuilder _sb = new();
    private int _padLen = 80;
    private TextWriter? _output;

    public async Task TranslateAsync(string file, TextWriter output, int padLen, string ns)
    {
        _padLen = padLen;
        _output = output;
        XmlQualifiedName root_qname = new(s_message, ns);
        XmlNameTable xmlNameTable = new NameTable();
        XmlSchemaSet xmlSchemaSet = new(xmlNameTable);
        xmlSchemaSet.Add(ns, file);
        xmlSchemaSet.Compile();
        if (xmlSchemaSet.GlobalElements[root_qname] is not XmlSchemaElement root)
        {
            throw new Exception(s_notEdifactSchema);
        }
        await output.WriteLineAsync(s_htmlBegin);
        await WriteAsync(s_messageHeader);
        await WriteAsync(s_unhRep, true);
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
                    if (
                        positions[^1] < seq.Items.Count 
                        && seq.Items[positions[^1]] is XmlSchemaElement cur
                    )
                    {
                        ++positions[^1];
                        if (s_unb.Equals(cur.Name) || s_ung.Equals(cur.Name) || s_unh.Equals(cur.Name))
                        {
                            continue;
                        }
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
                            await WriteAsync(s_x251Cx2500);
                        }
                        else
                        {
                            await WriteAsync(s_x2514x2500);
                        }
                        bool sg = false;
                        if (cur.Name!.StartsWith(s_sg))
                        {
                            await WriteAsync(string.Format(s_segmentGroupFormat, cur.Name[3..]));
                            vlines[positions.Count - 1] = (positions[^1] < seq.Items.Count || positions.Count == 1);
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
                    }
                    if (positions[^1] == seq.Items.Count)
                    {
                        elementsStack.RemoveAt(elementsStack.Count - 1);
                        positions.RemoveAt(positions.Count - 1);
                        break;
                    }
                }
            }
        }
        await WriteAsync(s_messageTrailer);
        await WriteAsync(s_unhRep, true);
        await output.WriteLineAsync(s_htmlEnd);
        output.Close();
    }

    private async Task WriteAsync(string str, bool end = false)
    {
        if (end)
        {
            Match m = s_re.Match(_sb.ToString());
            int len = _sb.Length;
            foreach (Capture c in m.Groups[1].Captures.Cast<Capture>())
            {
                len -= c.Length;
                if (c.Value.StartsWith('&'))
                {
                    len++;
                }
            }
            int rest = _padLen - len - str.Length;
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
