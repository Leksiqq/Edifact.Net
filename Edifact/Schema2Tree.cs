using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;

namespace Net.Leksi.Edifact;

public class Schema2Tree
{
    int pad_len = 80;
    StringBuilder sb = new StringBuilder();
    TextWriter output = null;
    Regex re = new Regex("^(?:[^&]*(&[^;]+;|<[^>]*>))*[^&]*$");

    public void Translate(string file, TextWriter output, int PadLen, string ns)
    {
        pad_len = PadLen;
        this.output = output;
        XmlQualifiedName root_qname = new XmlQualifiedName("MESSAGE", ns);
        XmlNameTable xmlNameTable = new NameTable();
        XmlSchemaSet xmlSchemaSet = new XmlSchemaSet(xmlNameTable);
        xmlSchemaSet.Add(ns, file);
        xmlSchemaSet.Compile();
        XmlSchemaElement root = xmlSchemaSet.GlobalElements[root_qname] as XmlSchemaElement;
        if (root == null)
        {
            throw new Exception("Not EDIFACT schema!");
        }
        output.WriteLine("<html>");
        output.WriteLine("<head>");
        output.WriteLine("  <meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\"/>");
        output.WriteLine("</head>");
        output.WriteLine("<body>");
        output.WriteLine("<pre>");
        write("&#x251C;&#x2500;<b>UNH</b> Mesage header");
        write("x1 (M)", true);
        List<int> positions = new List<int>();
        List<bool> vlines = new List<bool>();
        List<XmlSchemaElement> elements_stack = new List<XmlSchemaElement>();
        positions.Add(0);
        vlines.Add(true);
        elements_stack.Add(root);
        while (elements_stack.Count > 0)
        {
            XmlSchemaSequence seq = (elements_stack[elements_stack.Count - 1].ElementSchemaType as XmlSchemaComplexType).ContentTypeParticle as XmlSchemaSequence;
            while (true)
            {
                if (positions[positions.Count - 1] < seq.Items.Count)
                {
                    XmlSchemaElement cur = seq.Items[positions[positions.Count - 1]++] as XmlSchemaElement;
                    if ("UNB".Equals(cur.Name) || "UNG".Equals(cur.Name) || "UNH".Equals(cur.Name))
                    {
                        continue;
                    }
                    //if (positions.Count > 1)
                    //{
                    //    write("&#x2502;");
                    //}
                    for (int i = 0; i < positions.Count - 1; i++)
                    {
                        if (vlines[i])
                        {
                            write("&#x2502;");
                        }
                        else
                        {
                            write("&nbsp;");
                        }
                        write("&nbsp;");
                    }
                    if (positions[positions.Count - 1] < seq.Items.Count || positions.Count == 1)
                    {
                        write("&#x251C;&#x2500;");
                    }
                    else
                    {
                        write("&#x2514;&#x2500;");
                    }
                    bool sg = false;
                    if (cur.Name.StartsWith("SG-"))
                    {
                        write("<b>Segment Group " + cur.Name.Substring(3) + "</b>");
                        vlines[positions.Count - 1] = (positions[positions.Count - 1] < seq.Items.Count || positions.Count == 1);
                        positions.Add(0);
                        elements_stack.Add(cur);
                        if (vlines.Count < positions.Count)
                        {
                            vlines.Add(false);
                        }
                        sg = true;
                    }
                    else
                    {
                        write("<b>" + cur.Name + "</b>");
                    }
                    if (cur.ElementSchemaType.Annotation != null)
                    {
                        write(" " + Regex.Replace((cur.ElementSchemaType.Annotation.Items[0] as XmlSchemaDocumentation).Markup[0].Value, "( )+", " "));
                    }
                    if (cur.MinOccurs == 0)
                    {
                        write("x" + cur.MaxOccurs + " (C)", true);
                    }
                    else
                    {
                        write("x" + cur.MaxOccurs + " (M)", true);
                    }
                    if (sg)
                    {
                        break;
                    }
                }
                if (positions[positions.Count - 1] == seq.Items.Count)
                {
                    elements_stack.RemoveAt(elements_stack.Count - 1);
                    positions.RemoveAt(positions.Count - 1);
                    break;
                }
            }
        }
        write("&#x2514;&#x2500;<b>UNT</b> Mesage trailer");
        write("x1 (M)", true);
        output.WriteLine("</table>");
        output.WriteLine("</pre>");
        output.WriteLine("</body>");
        output.WriteLine("</html>");
        output.Close();
    }

    private void write(string str, bool end = false)
    {
        if (end)
        {
            Match m = re.Match(sb.ToString());
            int len = sb.Length;
            foreach (Capture c in m.Groups[1].Captures)
            {
                len -= c.Length;
                if (c.Value.StartsWith("&"))
                {
                    len++;
                }
            }
            int rest = pad_len - len - str.Length;
            if (rest <= 0)
            {
                rest = 1;
            }
            while (rest-- > 0)
            {
                sb.Append(".");
            }
            sb.Append(str);
            output.WriteLine(sb.ToString());
            sb.Clear();
        }
        else
        {
            sb.Append(str);
        }
    }
}
