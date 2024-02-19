using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.XPath;

namespace Net.Leksi.Edifact;

public class Parser : Tokenizer
{
    public delegate void EndInterchange(InterchangeEventArgs args);
    public delegate void EndFunctionalGroup(FunctionalGroupEventArgs args);

    public EndInterchange OnEndInterchange;
    public EndFunctionalGroup OnEndFunctionalGroup;

    XmlDocument doc = new XmlDocument();
    XmlSchemaSet schemas = null;
    string schemas_dir = ".";
    XmlElement unb = null;
    XmlElement ung = null;
    XmlElement unh = null;
    XmlDocument slipDoc = new XmlDocument();
    XmlElement slip = null;
    string system_segments_check = "UNB|UNG|UNH|UNT|UNE|UNZ|";
    int has_functional_groups = -1;
    int interchanges_processed = 0;
    int fg_processed = 0;
    int messages_processed = 0;
    int segments_processed = 0;
    bool running = true;
    XmlNamespaceManager man = new XmlNamespaceManager(new NameTable());
    ResourceManager rm_regex_tuning = null;
    Regex reDataTypeError = null;

    string syntax_id = null;
    string syntax_version = null;
    Encoding interchange_encoding = Encoding.ASCII;
    string message_type = null;
    string message_version = null;
    string message_release = null;
    string controlling_agency = null;
    string association_assigned_code = null;
    InterchangeEventArgs interchange_ea = new InterchangeEventArgs();
    FunctionalGroupEventArgs fg_ea = null;
    BaseEventArgs ea = null;

    public string SchemasDir
    {
        get
        {
            return schemas_dir;
        }
        set
        {
            schemas_dir = value;
            if (schemas_dir != null && schemas_dir.EndsWith("\\"))
            {
                schemas_dir = schemas_dir.Substring(0, schemas_dir.Length - 1);
            }
        }
    }

    public string CalculateMD5Hash(string input)
    {
        MD5 md5 = MD5.Create();
        byte[] inputBytes = Encoding.ASCII.GetBytes(input);
        byte[] hash = md5.ComputeHash(inputBytes);

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < hash.Length; i++)
        {
            sb.Append(hash[i].ToString("X2"));
        }
        return sb.ToString();
    }

    public Parser()
    {
        OnSegment += new Segment(on_segment);
        man.AddNamespace("e", Properties.Resources.edifact_ns);
        rm_regex_tuning = new ResourceManager("Net.Leksi.EDIFACT.Properties.regex_tuning", Assembly.GetExecutingAssembly());
        reDataTypeError = new Regex(rm_regex_tuning.GetString("DATA_TYPE_ERROR"));
        OnEndInterchange += delegate(InterchangeEventArgs args) { };
        OnEndFunctionalGroup += delegate(FunctionalGroupEventArgs args) { };
    }

    public void Parse(string path)
    {
        FileStream fs = new FileStream(path, FileMode.Open);
        Parse(fs);
    }

    public void Parse(Stream stream)
    {
        init();
        Tokenize(stream);
        interchange_ea.errors = errors;
        OnEndInterchange(interchange_ea);
        delete_tmp_files(interchange_ea.tmp_message_files);
    }

    void delete_tmp_files(List<string> tmp_files)
    {
        if (tmp_files != null)
        {
            foreach (string file in tmp_files)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }
    }

    void init()
    {
        if (!Directory.Exists(schemas_dir + "\\UN"))
        {
            Directory.CreateDirectory(schemas_dir + "\\UN");
        }
        if (
            !File.Exists(schemas_dir + "\\edifact._xsd")
            || !CalculateMD5Hash(Properties.Resources.edifact).Equals(CalculateMD5Hash(File.ReadAllText(schemas_dir + "\\edifact._xsd")))
        )
        {
            File.WriteAllText(schemas_dir + "\\edifact._xsd", Properties.Resources.edifact);
        }
        errors.Clear();
        schemas = new XmlSchemaSet();
        schemas.Add(Properties.Resources.edifact_ns, schemas_dir + "\\edifact._xsd");
        schemas.Compile();
        slipDoc.Schemas = schemas;
        interchanges_processed = 0;
        fg_processed = 0;
        messages_processed = 0;
        segments_processed = 0;
        unb = null;
        ung = null;
        unh = null;
        running = true;
    }

    protected new ParseError add_error(ErrorTypes type, ErrorKinds kind)
    {
        ParseError res = base.add_error(type, kind);
        if (ung != null || unh != null)
        {
            if (ung != null)
            {
                XPathNavigator nav = ung.CreateNavigator().SelectSingleNode("e:E0038", man);
                if (nav != null)
                {
                    res.AddExteraMessage("FG", new object[] { nav.Value });
                }
            }
            if (unh != null)
            {
                XPathNavigator nav = unh.CreateNavigator().SelectSingleNode("e:E0062", man);
                if (nav != null)
                {
                    res.AddExteraMessage("MESS", new object[] { nav.Value });
                }
            }
        }
        if (fg_ea != null)
        {
            if (fg_ea.errors == null)
            {
                fg_ea.errors = new List<ParseError>();
            }
            fg_ea.errors.Add(res);
        }
        return res;
    }

    protected List<ParseError> find_error_by_location(Location loc)
    {
        List<ParseError> res = new List<ParseError>();
        foreach (ParseError err in errors)
        {
            if (err.IsAt(loc))
            {
                res.Add(err);
            }
        }
        return res;
    }

    void on_segment(object sender, SegmentEventArgs e)
    {
        if (!running)
        {
            return;
        }
        if (unh != null)
        {
            segments_processed++;
        }
        XmlSchemaComplexType segment =
            schemas.GlobalTypes[new XmlQualifiedName(e.tag.data, Properties.Resources.edifact_ns)] as XmlSchemaComplexType;
        if (segment == null)
        {
            add_error(ErrorTypes.ERROR, ErrorKinds.UNKNOWN_SEGMENT).AddLocation(e.tag.begin).AddData(e.tag.data);
        }
        else
        {
            bool terminal_ok = true;
            bool system_segment = system_segments_check.Contains(e.tag.data + "|");
            if (!system_segment)
            {
                terminal_ok = false;
            }
            if (terminal_ok)
            {
                if (system_segment)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("<").Append(e.tag.data).Append(" xmlns=\"").Append(Properties.Resources.edifact_ns).Append("\"/>");
                    slipDoc.LoadXml(sb.ToString());
                    slip = slipDoc.DocumentElement;
                }
                parse_segment(e, segment);
                slipDoc.DocumentElement.SetAttribute("xmlns:xsi", Properties.Resources.schema_instance_ns);
                slipDoc.DocumentElement.SetAttribute("xmlns:e", Properties.Resources.edifact_ns);
                slipDoc.DocumentElement.SetAttribute("type", Properties.Resources.schema_instance_ns, "e:" + e.tag.data);
                CultureInfo ci = Thread.CurrentThread.CurrentUICulture;
                Thread.CurrentThread.CurrentUICulture = CultureInfo.CreateSpecificCulture("en-US");
                slipDoc.Validate(delegate(object obj, ValidationEventArgs args)
                {
                    Match m = reDataTypeError.Match(args.Message);
                    if (m.Success)
                    {
                        //{
                        //    XmlWriterSettings ws = new XmlWriterSettings();
                        //    ws.Indent = true;
                        //    ws.Encoding = Encoding.UTF8;
                        //    StringBuilder sb = new StringBuilder();
                        //    XmlWriter wr = XmlWriter.Create(sb, ws);
                        //    slipDoc.WriteTo(wr);
                        //    wr.Close();
                        //    Console.WriteLine(sb.ToString());
                        //}
                        string xpath = "//e:" + m.Groups[1].Captures[0].Value + "";
                        //Console.WriteLine(xpath1);
                        List<string> uniq = new List<string>();
                        XPathNodeIterator ni = slipDoc.CreateNavigator().Select(xpath, man);
                        while (ni.MoveNext())
                        {
                            if (m.Groups[2].Captures[0].Value.Equals(ni.Current.Value))
                            {
                                if (!uniq.Contains(ni.Current.GetAttribute("loc", "")))
                                {
                                    uniq.Add(ni.Current.GetAttribute("loc", ""));
                                    xpath = "ancestor-or-self::*";
                                    XPathNodeIterator ni1 = ni.Current.Select(xpath, man);
                                    List<XPathNavigator> nodes = new List<XPathNavigator>();
                                    while (ni1.MoveNext())
                                    {
                                        nodes.Add(ni1.Current.CreateNavigator());
                                    }
                                    string[] parts = ni1.Current.GetAttribute("loc", "").Split(new char[] { ':' });
                                    ParseError err = add_error(
                                        ErrorTypes.ERROR, nodes.Count == 2 ?
                                        ErrorKinds.INVALID_ELEMENT_VALUE :
                                        ErrorKinds.INVALID_SUB_ELEMENT_VALUE
                                    ).AddLocation(new Location(parts, 0)).AddData(m.Groups[2].Captures[0].Value);
                                    foreach (XPathNavigator node in nodes)
                                    {
                                        err.AddData(node.Name);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("=" + args.Message);
                    }
                });
                Thread.CurrentThread.CurrentUICulture = ci;
                if (system_segment)
                {
                    if ("UNB".Equals(e.tag.data))
                    {
                        if (interchanges_processed > 0)
                        {
                            add_error(ErrorTypes.ERROR, ErrorKinds.UNEXPECTED_SEGMENT).AddLocation(e.tag.begin).AddData(e.tag.data);
                            running = false;
                        }
                        else if (unb != null)
                        {
                            add_error(ErrorTypes.ERROR, ErrorKinds.UNEXPECTED_SEGMENT).AddLocation(e.tag.begin).AddData(e.tag.data);
                        }
                        else
                        {
                            unb = slip;
                            ea = interchange_ea;

                            LocatedString ls;
                            ls = get_located_string(unb, "e:S001/e:E0001");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                syntax_id = ls.data;
                            }
                            ls = get_located_string(unb, "e:S001/e:E0002");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                syntax_version = ls.data;
                            }
                            ls = get_located_string(unb, "e:S002/e:E0004");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                interchange_ea.sender_identification = ls.data;
                            }
                            ls = get_located_string(unb, "e:S002/e:E0007");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                interchange_ea.sender_identification_code_qualifier = ls.data;
                            }
                            ls = get_located_string(unb, "e:S003/e:E0010");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                interchange_ea.recipient_identification = ls.data;
                            }
                            ls = get_located_string(unb, "e:S003/e:E0007");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                interchange_ea.recipient_identification_code_qualifier = ls.data;
                            }
                            ls = get_located_string(unb, "e:E0031");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                interchange_ea.acknowledgement_request = "1".Equals(ls.data);
                            }
                            ls = get_located_string(unb, "e:E0032");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                interchange_ea.communication_agreement_id = ls.data;
                            }
                            ls = get_located_string(unb, "e:E0035");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                interchange_ea.test_indicator = "1".Equals(ls.data);
                            }
                            if ("UNOA".Equals(syntax_id) || "UNOB".Equals(syntax_id))
                            {
                                interchange_encoding = Encoding.ASCII;
                            }
                            else if ("UNOC".Equals(syntax_id))
                            {
                                interchange_encoding = Encoding.GetEncoding("iso-8859-1");
                            }
                            else if ("UNOD".Equals(syntax_id))
                            {
                                interchange_encoding = Encoding.GetEncoding("iso-8859-2");
                            }
                            else if ("UNOE".Equals(syntax_id))
                            {
                                interchange_encoding = Encoding.GetEncoding("iso-8859-5");
                            }
                            else if ("UNOF".Equals(syntax_id))
                            {
                                interchange_encoding = Encoding.GetEncoding("iso-8859-7");
                            }
                            ls = get_located_string(unb, "e:S004/e:E0017");
                            LocatedString ls1 = get_located_string(unb, "e:S004/e:E0019");
                            if (ls != null && ls1 != null && find_error_by_location(ls.begin).Count == 0 && find_error_by_location(ls1.begin).Count == 0)
                            {
                                interchange_ea.preparation_datetime = Formats.ParseDateTime(ls.data + ls1.data, "201");
                            }
                        }
                    }
                    else if ("UNG".Equals(e.tag.data))
                    {
                        if (has_functional_groups == -1)
                        {
                            has_functional_groups = 1;
                        }
                        else if (has_functional_groups == 0)
                        {
                            add_error(ErrorTypes.ERROR, ErrorKinds.UNEXPECTED_SEGMENT).AddLocation(e.tag.begin).AddData(e.tag.data);
                        }
                        if (ung != null)
                        {
                            add_error(ErrorTypes.ERROR, ErrorKinds.UNEXPECTED_SEGMENT).AddLocation(e.tag.begin).AddData(e.tag.data);
                        }
                        else
                        {
                            ung = slip;
                            fg_ea = new FunctionalGroupEventArgs();
                            fg_ea.interchange = interchange_ea;
                            ea = fg_ea;
                            LocatedString ls;
                            ls = get_located_string(ung, "e:E0038");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                fg_ea.identification = ls.data;
                            }
                            ls = get_located_string(ung, "e:S006/e:E0040");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                fg_ea.sender_identification = ls.data;
                            }
                            ls = get_located_string(ung, "e:S006/e:E0007");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                fg_ea.sender_identification_code_qualifier = ls.data;
                            }
                            ls = get_located_string(ung, "e:S007/e:E0044");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                fg_ea.recipient_identification = ls.data;
                            }
                            ls = get_located_string(ung, "e:S007/e:E0007");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                fg_ea.recipient_identification_code_qualifier = ls.data;
                            }
                            ls = get_located_string(ung, "e:E0048");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                fg_ea.control_reference = ls.data;
                            }
                            ls = get_located_string(ung, "e:E0051");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                fg_ea.controlling_agency = ls.data;
                            }
                            ls = get_located_string(ung, "e:S008/e:E0052");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                fg_ea.message_version = ls.data;
                            }
                            ls = get_located_string(ung, "e:S008/e:E0054");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                fg_ea.message_release = ls.data;
                            }
                            ls = get_located_string(ung, "e:S008/e:E0057");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                fg_ea.association_assigned_code = ls.data;
                            }
                            ls = get_located_string(ung, "e:E0058");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                fg_ea.application_password = ls.data;
                            }
                            ls = get_located_string(ung, "e:S004/e:E0017");
                            LocatedString ls1 = get_located_string(unb, "e:S004/e:E0019");
                            if (ls != null && ls1 != null && find_error_by_location(ls.begin).Count == 0 && find_error_by_location(ls1.begin).Count == 0)
                            {
                                fg_ea.preparation_datetime = Formats.ParseDateTime(ls.data + ls1.data, "201");
                            }
                        }
                    }
                    else if ("UNH".Equals(e.tag.data))
                    {
                        if (has_functional_groups == -1)
                        {
                            has_functional_groups = 0;
                        }
                        else if (has_functional_groups == 1 && ung == null)
                        {
                            add_error(ErrorTypes.ERROR, ErrorKinds.UNEXPECTED_SEGMENT).AddLocation(e.tag.begin).AddData(e.tag.data);
                        }
                        if (unh != null)
                        {
                            add_error(ErrorTypes.ERROR, ErrorKinds.UNEXPECTED_SEGMENT).AddLocation(e.tag.begin).AddData(e.tag.data);
                        }
                        else
                        {
                            unh = slip;
                            segments_processed = 1;
                            LocatedString ls, ls1;
                            ls = get_located_string(unb, "e:S009/e:E0065");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                message_type = ls.data;
                            }
                            if (has_functional_groups == 1)
                            {

                            }
                            ls = get_located_string(unb, "e:S009/e:E0052");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                message_version = ls.data;
                            }
                            ls = get_located_string(unb, "e:S009/e:E0054");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                message_release = ls.data;
                            }
                            ls = get_located_string(unb, "e:S009/e:E0051");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                controlling_agency = ls.data;
                            }
                            ls = get_located_string(unb, "e:S009/e:E0057");
                            if (ls != null && find_error_by_location(ls.begin).Count == 0)
                            {
                                association_assigned_code = ls.data;
                            }
                            doc.LoadXml("<MESSAGE xmlns=\"" + Properties.Resources.edifact_ns + "\">");
                        }
                    }
                    else if ("UNT".Equals(e.tag.data))
                    {
                        if (unh == null)
                        {
                            add_error(ErrorTypes.ERROR, ErrorKinds.UNEXPECTED_SEGMENT).AddLocation(e.tag.begin)
                                .AddData(e.tag.data);
                        }
                        else
                        {
                            LocatedString h_ls = get_located_string(unh, "e:E0062");
                            LocatedString t_ls = get_located_string(slip, "e:E0062");
                            if (
                                h_ls != null && h_ls.data != null && !h_ls.data.Equals(t_ls.data)
                                || t_ls != null && t_ls.data != null && !t_ls.data.Equals(h_ls.data)
                            )
                            {
                                add_error(ErrorTypes.ERROR, ErrorKinds.MESSAGE_ID_MISMATCH)
                                    .AddLocation(t_ls.begin).AddLocation(h_ls.begin).AddData(t_ls.data).AddData(h_ls.data);
                            }
                            t_ls = get_located_string(slip, "e:E0074");
                            if (t_ls != null && t_ls.data != null && !t_ls.data.Equals(segments_processed.ToString()))
                            {
                                add_error(ErrorTypes.ERROR, ErrorKinds.NUMBER_OF_SEGMENTS_MISMATCH).AddLocation(t_ls.begin).AddData(t_ls.data).AddData(segments_processed.ToString());
                            }
                            unh = null;
                            ea.control_count++;
                        }
                    }
                    else if ("UNE".Equals(e.tag.data))
                    {
                        if (ung == null)
                        {
                            add_error(ErrorTypes.ERROR, ErrorKinds.UNEXPECTED_SEGMENT).AddLocation(e.tag.begin)
                                .AddData(e.tag.data);
                        }
                        else
                        {
                            if (has_functional_groups == 1)
                            {
                                LocatedString g_ls = get_located_string(unh, "e:E0048");
                                LocatedString e_ls = get_located_string(slip, "e:E0048");
                                if (
                                    g_ls != null && g_ls.data != null && !g_ls.data.Equals(e_ls.data)
                                    || e_ls != null && e_ls.data != null && !e_ls.data.Equals(g_ls.data)
                                )
                                {
                                    add_error(ErrorTypes.ERROR, ErrorKinds.FG_ID_MISMATCH).AddLocation(e_ls.begin)
                                        .AddLocation(g_ls.begin).AddData(e_ls.data).AddData(g_ls.data);
                                }
                                e_ls = get_located_string(slip, "e:E0060");
                                if (e_ls != null && e_ls.data != null && !e_ls.data.Equals(segments_processed.ToString()))
                                {
                                    add_error(ErrorTypes.ERROR, ErrorKinds.NUMBER_OF_MESSAGES_MISMATCH)
                                        .AddLocation(e_ls.begin).AddData(e_ls.data).AddData(messages_processed.ToString());
                                }
                                messages_processed = 0;
                            }
                            interchange_ea.control_count++;
                            ung = null;
                        }
                    }
                    else if ("UNZ".Equals(e.tag.data))
                    {
                        LocatedString b_ls = get_located_string(unb, "e:E0020");
                        LocatedString z_ls = get_located_string(slip, "e:E0020");
                        if (
                            b_ls != null && b_ls.data != null && !b_ls.data.Equals(z_ls.data)
                            || z_ls != null && z_ls.data != null && !z_ls.data.Equals(b_ls.data)
                        )
                        {
                            add_error(ErrorTypes.ERROR, ErrorKinds.INTERCHANGE_ID_MISMATCH).AddLocation(z_ls.begin)
                                .AddLocation(b_ls.begin).AddData(z_ls.data).AddData(b_ls.data);
                        }
                        z_ls = get_located_string(slip, "e:E0036");
                        int control_count = (has_functional_groups == 0 ? messages_processed : fg_processed);
                        if (z_ls != null && z_ls.data != null && !z_ls.data.Equals(control_count.ToString()))
                        {
                            add_error(ErrorTypes.ERROR, ErrorKinds.NUMBER_OF_SEGMENTS_MISMATCH).AddLocation(z_ls.begin).AddData(z_ls.data).AddData(control_count.ToString());
                        }
                        unb = null;
                        interchanges_processed++;
                    }
                }
                //if (system_segment && "UNT".Equals(e.tag.data))
                //{
                //    XmlWriterSettings ws = new XmlWriterSettings();
                //    ws.Indent = true;
                //    ws.Encoding = Encoding.UTF8;
                //    StringBuilder sb = new StringBuilder();
                //    XmlWriter wr = XmlWriter.Create(sb, ws);
                //    slipDoc.WriteTo(wr);
                //    wr.Close();
                //    Console.WriteLine(sb.ToString());
                //}
            }
        }
        write_out(e);

    }

    LocatedString get_located_string(XmlElement seg, string xpath)
    {
        LocatedString res = null;
        XPathNavigator nav = seg.CreateNavigator().SelectSingleNode(xpath, man);
        if (nav != null)
        {
            string[] parts = nav.GetAttribute("loc", "").Split(new char[] { ':' });
            if (parts.Length == 6)
            {
                Location l = new Location(parts, 0);
                res = new LocatedString(l, Encoding.UTF8.GetString(interchange_encoding.GetBytes(nav.Value)));
                res.end.Set(new Location(parts, 3));
            }
        }
        return res;
    }

    string get_value(XmlElement seg, string xpath)
    {
        string res = null;
        XPathNavigator nav = seg.CreateNavigator().SelectSingleNode(xpath, man);
        if (nav != null)
        {
            res = Encoding.UTF8.GetString(interchange_encoding.GetBytes(nav.Value));
        }
        return res;
    }

    void on_begin_composite_element(string name)
    {
        slip = (XmlElement)slip.AppendChild(slipDoc.CreateElement("", name, Properties.Resources.edifact_ns));
    }

    void on_end_composite_element()
    {
        slip = (XmlElement)slip.ParentNode;
    }

    void on_element(string name, LocatedString ls)
    {
        XmlElement el = (XmlElement)slip.AppendChild(slipDoc.CreateElement("", name, Properties.Resources.edifact_ns));
        ((XmlElement)el.AppendChild(slipDoc.CreateTextNode(Encoding.UTF8.GetString(interchange_encoding.GetBytes(ls.data)))).ParentNode).SetAttribute("loc", ls.begin.offset + ":" + ls.begin.line + ":" + ls.begin.col + ":" + ls.end.offset + ":" + ls.end.line + ":" + ls.end.col);
        //XmlDocument d = new XmlDocument();
        //XmlElement el0 = d.CreateElement("", name, Properties.Resources.edifact_ns);
        //el0.SetAttribute("xmlns:xsi", Properties.Resources.schema_instance_ns);
        //el0.SetAttribute("xmlns:e", Properties.Resources.edifact_ns);
        //el0.SetAttribute("type", Properties.Resources.schema_instance_ns, "e:" + name);
        ////el0.SetAttribute("schemaLocation", Properties.Resources.schema_instance_ns, (schemas_dir + "\\UN\\edifact.xsd").Replace("\\", "/"));
        ////XmlElement el1 = (XmlElement)d.ImportNode(el, true);
        //d.AppendChild(el0)/*.AppendChild(el1)*/;
        //d.Schemas = schemas;
        //Console.WriteLine(d.OuterXml);
        //d.Validate(delegate(object obj, ValidationEventArgs args)
        //{
        //    Console.WriteLine("=" + args.Message);
        //});
    }

    bool parse_segment(SegmentEventArgs e, XmlSchemaComplexType segment)
    {
        int i = 0;
        bool elements_ended = false;
        bool absent_mandatory_element_found = false;
        List<string> expected_elements = new List<string>();
        List<string> expected_sub_elements = new List<string>();
        foreach (XmlSchemaElement element in (segment.ContentTypeParticle as XmlSchemaSequence).Items)
        {
            if (!elements_ended)
            {
                bool stop_element = false;
                for (int j = 0; j < element.MaxOccurs; j++)
                {
                    if (i < e.elements.Count)
                    {
                        if (e.elements[i].Count == 1 && "".Equals(e.elements[i][0].data))
                        {
                            if (element.MinOccurs == 0)
                            {
                                i++;
                                break;
                            }
                        }
                        if (((XmlSchemaComplexType)element.ElementSchemaType).ContentType == XmlSchemaContentType.ElementOnly)
                        {
                            on_begin_composite_element(element.Name);
                            int k = 0;
                            bool stop_sub_element = false;
                            expected_sub_elements.Clear();
                            bool absent_mandatory_sub_element_found = false;
                            foreach (XmlSchemaElement sub_element in ((element.ElementSchemaType as XmlSchemaComplexType).ContentTypeParticle as XmlSchemaSequence).Items)
                            {
                                if (!stop_sub_element)
                                {
                                    for (int l = 0; l < sub_element.MaxOccurs; l++)
                                    {
                                        if (k < e.elements[i].Count)
                                        {
                                            if ("".Equals(e.elements[i][k].data))
                                            {
                                                if (sub_element.MinOccurs > l)
                                                {
                                                    add_error(ErrorTypes.ERROR, ErrorKinds.EXPECTED_SUB_ELEMENT_NOT_FOUND)
                                                        .AddLocation(e.elements[i][k].begin).AddData(e.tag.data)
                                                        .AddData(element.Name).AddData((sub_element.Name.StartsWith("E") ? sub_element.Name.Substring(1) : sub_element.Name));
                                                }
                                                k++;
                                                break;
                                            }
                                            on_element(sub_element.Name, e.elements[i][k]);
                                        }
                                        else if (sub_element.MinOccurs > l)
                                        {
                                            add_error(ErrorTypes.ERROR, ErrorKinds.EXPECTED_SUB_ELEMENT_NOT_FOUND)
                                                .AddLocation(e.elements[i][e.elements[i].Count - 1].end).AddData(e.tag.data)
                                                .AddData(element.Name).AddData((sub_element.Name.StartsWith("E") ? sub_element.Name.Substring(1) : sub_element.Name));
                                        }
                                        else
                                        {
                                            stop_sub_element = true;
                                            break;
                                        }
                                        k++;
                                    }
                                }
                                if (stop_sub_element)
                                {
                                    expected_sub_elements.Add(sub_element.Name.Substring(1) + "(" + (sub_element.MinOccurs > 0 ? "M" : "C") + ")");
                                    if (sub_element.MinOccurs > 0)
                                    {
                                        absent_mandatory_sub_element_found = true;
                                        break;
                                    }
                                }
                            }
                            if (expected_sub_elements.Count > 0 && absent_mandatory_sub_element_found)
                            {
                                string ss = "";
                                foreach (string s in expected_sub_elements)
                                {
                                    ss += s + ", ";
                                }
                                Console.WriteLine(ss);
                                add_error(ErrorTypes.ERROR, ErrorKinds.INCOMPLETE_ELEMENT)
                                    .AddLocation(e.tag.end)
                                    .AddData(e.tag.data).AddData(element.Name).AddData(ss.Trim().Trim(new char[] { ',' }));
                            }
                            if (k < e.elements[i].Count)
                            {
                                add_error(ErrorTypes.ERROR, ErrorKinds.EXTRA_SUB_ELEMENT_FOUND)
                                    .AddLocation(e.elements[i][k - 1].end).AddData(e.tag.data).AddData(element.Name);
                            }
                            on_end_composite_element();
                        }
                        else
                        {
                            if (e.elements[i].Count > 1)
                            {
                                add_error(ErrorTypes.ERROR, ErrorKinds.EXTRA_SUB_ELEMENT_FOUND)
                                    .AddLocation(e.elements[i][0].end).AddData(e.tag.data).AddData(element.Name);
                            }
                            on_element(element.Name, e.elements[i][0]);
                        }
                    }
                    else if (element.MinOccurs > j)
                    {
                        Location loc = new Location();
                        if (i > 0)
                        {
                            loc.Set(e.elements[i - 1][e.elements[i - 1].Count - 1].end);
                        }
                        else
                        {
                            loc.Set(e.tag.end);
                        }
                        add_error(ErrorTypes.ERROR, ErrorKinds.EXPECTED_ELEMENT_NOT_FOUND).AddLocation(loc)
                            .AddData(e.tag.data)
                            .AddData((element.Name.StartsWith("E") ? element.Name.Substring(1) : element.Name));
                    }
                    else
                    {
                        stop_element = true;
                        break;
                    }
                    i++;
                }
                if (stop_element)
                {
                    elements_ended = true;
                }
            }
            if (elements_ended)
            {
                expected_elements.Add((element.Name.StartsWith("E") ? element.Name.Substring(1) : element.Name) + "(" + (element.MinOccurs > 0 ? "M" : "C") + ")");
                if (element.MinOccurs > 0)
                {
                    absent_mandatory_element_found = true;
                    break;
                }
            }
        }
        if (expected_elements.Count > 0 && absent_mandatory_element_found)
        {
            string ss = "";
            foreach (string s in expected_elements)
            {
                ss += s + ", ";
            }
            Console.WriteLine(ss);
            add_error(ErrorTypes.ERROR, ErrorKinds.INCOMPLETE_SEGMENT).AddLocation(e.tag.end).AddData(e.tag.data).AddData(ss.Trim().Trim(new char[] { ',' }));
        }
        if (i < e.elements.Count)
        {
            Location loc = new Location();
            if (i > 0)
            {
                loc.Set(e.elements[i - 1][e.elements[i - 1].Count - 1].end);
            }
            else
            {
                loc.Set(e.tag.end);
            }
            add_error(ErrorTypes.ERROR, ErrorKinds.EXTRA_ELEMENT_FOUND).AddLocation(loc).AddData(e.tag.data);
        }
        return true;
    }

    void write_out(SegmentEventArgs e)
    {
        Console.Write(e.tag.data + "{" + e.tag.begin.line + "," + e.tag.begin.col + "-" + e.tag.end.line + "," + e.tag.end.col + "}");
        foreach (List<LocatedString> el in e.elements)
        {
            Console.Write("+");
            int i = 0;
            foreach (LocatedString sub in el)
            {
                if (i > 0)
                {
                    Console.Write(":");
                }
                Console.Write(sub.data + "{" + sub.begin.line + "," + sub.begin.col + "-" + sub.end.line + "," + sub.end.col + "}");
                i++;
            }
        }
        Console.WriteLine("'");
    }
}
