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

internal class Parser : Tokenizer
{
    internal delegate void EndInterchange(InterchangeEventArgs args);
    internal delegate void EndFunctionalGroup(FunctionalGroupEventArgs args);

    internal event EndInterchange? OnEndInterchange;
    internal event EndFunctionalGroup? OnEndFunctionalGroup;

    private const string s_currentDir = ".";
    private const string s_systemSegmentsCheck = "UNB|UNG|UNH|UNT|UNE|UNZ|";

    private static readonly ResourceManager s_rmRegexTuning;

    private readonly XmlNamespaceManager _man = new(new NameTable());
    private readonly XmlDocument doc = new();


    private XmlSchemaSet _schemas = null!;
    private string _schemasDir = s_currentDir;
    private XmlElement unb = null!;
    private XmlElement ung = null!;
    private XmlElement unh = null!;
    private XmlDocument slipDoc = new();
    private XmlElement slip = null!;
    private int _hasFunctionalGroups = -1;
    private int _interchangesProcessed = 0;
    private int _fgProcessed = 0;
    private int _messagesProcessed = 0;
    private int _segmentsProcessed = 0;
    private bool _running = true;
    private Regex _reDataTypeError = null!;

    private string _syntaxId = null;
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

    static Parser()
    {
        s_rmRegexTuning = new ResourceManager("Net.Leksi.EDIFACT.Properties.regex_tuning", Assembly.GetExecutingAssembly());
    }


    internal string SchemasDir
    {
        get
        {
            return _schemasDir;
        }
        set
        {
            _schemasDir = value;
            if (_schemasDir is { } && _schemasDir.EndsWith('\\'))
            {
                _schemasDir = _schemasDir[..^1];
            }
        }
    }
    internal Parser()
    {
        OnSegment += new Segment(on_segment);
        _man.AddNamespace("e", Properties.Resources.edifact_ns);
        _reDataTypeError = new Regex(s_rmRegexTuning.GetString("DATA_TYPE_ERROR"));
        OnEndInterchange += delegate (InterchangeEventArgs args) { };
        OnEndFunctionalGroup += delegate (FunctionalGroupEventArgs args) { };
    }

    internal string CalculateMD5Hash(string input)
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

    internal void Parse(string path)
    {
        FileStream fs = new FileStream(path, FileMode.Open);
        Parse(fs);
    }

    internal void Parse(Stream stream)
    {
        init();
        Tokenize(stream);
        interchange_ea.Errors = errors;
        OnEndInterchange(interchange_ea);
        delete_tmp_files(interchange_ea.TmpMessageFiles);
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
        if (!Directory.Exists(_schemasDir + "\\UN"))
        {
            Directory.CreateDirectory(_schemasDir + "\\UN");
        }
        if (
            !File.Exists(_schemasDir + "\\edifact._xsd")
            || !CalculateMD5Hash(Properties.Resources.edifact).Equals(CalculateMD5Hash(File.ReadAllText(_schemasDir + "\\edifact._xsd")))
        )
        {
            File.WriteAllText(_schemasDir + "\\edifact._xsd", Properties.Resources.edifact);
        }
        errors.Clear();
        _schemas = new XmlSchemaSet();
        _schemas.Add(Properties.Resources.edifact_ns, _schemasDir + "\\edifact._xsd");
        _schemas.Compile();
        slipDoc.Schemas = _schemas;
        _interchangesProcessed = 0;
        _fgProcessed = 0;
        _messagesProcessed = 0;
        _segmentsProcessed = 0;
        unb = null;
        ung = null;
        unh = null;
        _running = true;
    }

    protected new ParseError add_error(ErrorTypes type, ErrorKinds kind)
    {
        ParseError res = base.add_error(type, kind);
        if (ung != null || unh != null)
        {
            if (ung != null)
            {
                XPathNavigator nav = ung.CreateNavigator().SelectSingleNode("e:E0038", _man);
                if (nav != null)
                {
                    res.AddExteraMessage("FG", new object[] { nav.Value });
                }
            }
            if (unh != null)
            {
                XPathNavigator nav = unh.CreateNavigator().SelectSingleNode("e:E0062", _man);
                if (nav != null)
                {
                    res.AddExteraMessage("MESS", new object[] { nav.Value });
                }
            }
        }
        if (fg_ea != null)
        {
            if (fg_ea.Errors == null)
            {
                fg_ea.Errors = new List<ParseError>();
            }
            fg_ea.Errors.Add(res);
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
        if (!_running)
        {
            return;
        }
        if (unh != null)
        {
            _segmentsProcessed++;
        }
        XmlSchemaComplexType segment =
            _schemas.GlobalTypes[new XmlQualifiedName(e.tag.Data, Properties.Resources.edifact_ns)] as XmlSchemaComplexType;
        if (segment == null)
        {
            add_error(ErrorTypes.ERROR, ErrorKinds.UNKNOWN_SEGMENT).AddLocation(e.tag.Begin).AddData(e.tag.Data);
        }
        else
        {
            bool terminal_ok = true;
            bool system_segment = s_systemSegmentsCheck.Contains(e.tag.Data + "|");
            if (!system_segment)
            {
                terminal_ok = false;
            }
            if (terminal_ok)
            {
                if (system_segment)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("<").Append(e.tag.Data).Append(" xmlns=\"").Append(Properties.Resources.edifact_ns).Append("\"/>");
                    slipDoc.LoadXml(sb.ToString());
                    slip = slipDoc.DocumentElement;
                }
                parse_segment(e, segment);
                slipDoc.DocumentElement.SetAttribute("xmlns:xsi", Properties.Resources.schema_instance_ns);
                slipDoc.DocumentElement.SetAttribute("xmlns:e", Properties.Resources.edifact_ns);
                slipDoc.DocumentElement.SetAttribute("type", Properties.Resources.schema_instance_ns, "e:" + e.tag.Data);
                CultureInfo ci = Thread.CurrentThread.CurrentUICulture;
                Thread.CurrentThread.CurrentUICulture = CultureInfo.CreateSpecificCulture("en-US");
                slipDoc.Validate(delegate(object obj, ValidationEventArgs args)
                {
                    Match m = _reDataTypeError.Match(args.Message);
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
                        XPathNodeIterator ni = slipDoc.CreateNavigator().Select(xpath, _man);
                        while (ni.MoveNext())
                        {
                            if (m.Groups[2].Captures[0].Value.Equals(ni.Current.Value))
                            {
                                if (!uniq.Contains(ni.Current.GetAttribute("loc", "")))
                                {
                                    uniq.Add(ni.Current.GetAttribute("loc", ""));
                                    xpath = "ancestor-or-self::*";
                                    XPathNodeIterator ni1 = ni.Current.Select(xpath, _man);
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
                    if ("UNB".Equals(e.tag.Data))
                    {
                        if (_interchangesProcessed > 0)
                        {
                            add_error(ErrorTypes.ERROR, ErrorKinds.UNEXPECTED_SEGMENT).AddLocation(e.tag.Begin).AddData(e.tag.Data);
                            _running = false;
                        }
                        else if (unb != null)
                        {
                            add_error(ErrorTypes.ERROR, ErrorKinds.UNEXPECTED_SEGMENT).AddLocation(e.tag.Begin).AddData(e.tag.Data);
                        }
                        else
                        {
                            unb = slip;
                            ea = interchange_ea;

                            LocatedString ls;
                            ls = get_located_string(unb, "e:S001/e:E0001");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                _syntaxId = ls.Data;
                            }
                            ls = get_located_string(unb, "e:S001/e:E0002");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                syntax_version = ls.Data;
                            }
                            ls = get_located_string(unb, "e:S002/e:E0004");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                interchange_ea.SenderIdentification = ls.Data;
                            }
                            ls = get_located_string(unb, "e:S002/e:E0007");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                interchange_ea.SenderIdentificationCodeQualifier = ls.Data;
                            }
                            ls = get_located_string(unb, "e:S003/e:E0010");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                interchange_ea.RecipientIdentification = ls.Data;
                            }
                            ls = get_located_string(unb, "e:S003/e:E0007");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                interchange_ea.RecipientIdentificationCodeQualifier = ls.Data;
                            }
                            ls = get_located_string(unb, "e:E0031");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                interchange_ea.AcknowledgementRequest = "1".Equals(ls.Data);
                            }
                            ls = get_located_string(unb, "e:E0032");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                interchange_ea.CommunicationAgreementId = ls.Data;
                            }
                            ls = get_located_string(unb, "e:E0035");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                interchange_ea.TestIndicator = "1".Equals(ls.Data);
                            }
                            if ("UNOA".Equals(_syntaxId) || "UNOB".Equals(_syntaxId))
                            {
                                interchange_encoding = Encoding.ASCII;
                            }
                            else if ("UNOC".Equals(_syntaxId))
                            {
                                interchange_encoding = Encoding.GetEncoding("iso-8859-1");
                            }
                            else if ("UNOD".Equals(_syntaxId))
                            {
                                interchange_encoding = Encoding.GetEncoding("iso-8859-2");
                            }
                            else if ("UNOE".Equals(_syntaxId))
                            {
                                interchange_encoding = Encoding.GetEncoding("iso-8859-5");
                            }
                            else if ("UNOF".Equals(_syntaxId))
                            {
                                interchange_encoding = Encoding.GetEncoding("iso-8859-7");
                            }
                            ls = get_located_string(unb, "e:S004/e:E0017");
                            LocatedString ls1 = get_located_string(unb, "e:S004/e:E0019");
                            if (ls != null && ls1 != null && find_error_by_location(ls.Begin).Count == 0 && find_error_by_location(ls1.Begin).Count == 0)
                            {
                                interchange_ea.PreparationDateTime = Formats.ParseDateTime(ls.Data + ls1.Data, "201");
                            }
                        }
                    }
                    else if ("UNG".Equals(e.tag.Data))
                    {
                        if (_hasFunctionalGroups == -1)
                        {
                            _hasFunctionalGroups = 1;
                        }
                        else if (_hasFunctionalGroups == 0)
                        {
                            add_error(ErrorTypes.ERROR, ErrorKinds.UNEXPECTED_SEGMENT).AddLocation(e.tag.Begin).AddData(e.tag.Data);
                        }
                        if (ung != null)
                        {
                            add_error(ErrorTypes.ERROR, ErrorKinds.UNEXPECTED_SEGMENT).AddLocation(e.tag.Begin).AddData(e.tag.Data);
                        }
                        else
                        {
                            ung = slip;
                            fg_ea = new FunctionalGroupEventArgs();
                            fg_ea.Interchange = interchange_ea;
                            ea = fg_ea;
                            LocatedString ls;
                            ls = get_located_string(ung, "e:E0038");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                fg_ea.Identification = ls.Data;
                            }
                            ls = get_located_string(ung, "e:S006/e:E0040");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                fg_ea.SenderIdentification = ls.Data;
                            }
                            ls = get_located_string(ung, "e:S006/e:E0007");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                fg_ea.SenderIdentificationCodeQualifier = ls.Data;
                            }
                            ls = get_located_string(ung, "e:S007/e:E0044");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                fg_ea.RecipientIdentification = ls.Data;
                            }
                            ls = get_located_string(ung, "e:S007/e:E0007");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                fg_ea.RecipientIdentificationCodeQualifier = ls.Data;
                            }
                            ls = get_located_string(ung, "e:E0048");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                fg_ea.ControlReference = ls.Data;
                            }
                            ls = get_located_string(ung, "e:E0051");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                fg_ea.ControllingAgency = ls.Data;
                            }
                            ls = get_located_string(ung, "e:S008/e:E0052");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                fg_ea.MessageVersion = ls.Data;
                            }
                            ls = get_located_string(ung, "e:S008/e:E0054");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                fg_ea.MessageRelease = ls.Data;
                            }
                            ls = get_located_string(ung, "e:S008/e:E0057");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                fg_ea.AssociationAssignedCode = ls.Data;
                            }
                            ls = get_located_string(ung, "e:E0058");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                fg_ea.ApplicationPassword = ls.Data;
                            }
                            ls = get_located_string(ung, "e:S004/e:E0017");
                            LocatedString ls1 = get_located_string(unb, "e:S004/e:E0019");
                            if (ls != null && ls1 != null && find_error_by_location(ls.Begin).Count == 0 && find_error_by_location(ls1.Begin).Count == 0)
                            {
                                fg_ea.PreparationDateTime = Formats.ParseDateTime(ls.Data + ls1.Data, "201");
                            }
                        }
                    }
                    else if ("UNH".Equals(e.tag.Data))
                    {
                        if (_hasFunctionalGroups == -1)
                        {
                            _hasFunctionalGroups = 0;
                        }
                        else if (_hasFunctionalGroups == 1 && ung == null)
                        {
                            add_error(ErrorTypes.ERROR, ErrorKinds.UNEXPECTED_SEGMENT).AddLocation(e.tag.Begin).AddData(e.tag.Data);
                        }
                        if (unh != null)
                        {
                            add_error(ErrorTypes.ERROR, ErrorKinds.UNEXPECTED_SEGMENT).AddLocation(e.tag.Begin).AddData(e.tag.Data);
                        }
                        else
                        {
                            unh = slip;
                            _segmentsProcessed = 1;
                            LocatedString ls, ls1;
                            ls = get_located_string(unb, "e:S009/e:E0065");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                message_type = ls.Data;
                            }
                            if (_hasFunctionalGroups == 1)
                            {

                            }
                            ls = get_located_string(unb, "e:S009/e:E0052");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                message_version = ls.Data;
                            }
                            ls = get_located_string(unb, "e:S009/e:E0054");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                message_release = ls.Data;
                            }
                            ls = get_located_string(unb, "e:S009/e:E0051");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                controlling_agency = ls.Data;
                            }
                            ls = get_located_string(unb, "e:S009/e:E0057");
                            if (ls != null && find_error_by_location(ls.Begin).Count == 0)
                            {
                                association_assigned_code = ls.Data;
                            }
                            doc.LoadXml("<MESSAGE xmlns=\"" + Properties.Resources.edifact_ns + "\">");
                        }
                    }
                    else if ("UNT".Equals(e.tag.Data))
                    {
                        if (unh == null)
                        {
                            add_error(ErrorTypes.ERROR, ErrorKinds.UNEXPECTED_SEGMENT).AddLocation(e.tag.Begin)
                                .AddData(e.tag.Data);
                        }
                        else
                        {
                            LocatedString h_ls = get_located_string(unh, "e:E0062");
                            LocatedString t_ls = get_located_string(slip, "e:E0062");
                            if (
                                h_ls != null && h_ls.Data != null && !h_ls.Data.Equals(t_ls.Data)
                                || t_ls != null && t_ls.Data != null && !t_ls.Data.Equals(h_ls.Data)
                            )
                            {
                                add_error(ErrorTypes.ERROR, ErrorKinds.MESSAGE_ID_MISMATCH)
                                    .AddLocation(t_ls.Begin).AddLocation(h_ls.Begin).AddData(t_ls.Data).AddData(h_ls.Data);
                            }
                            t_ls = get_located_string(slip, "e:E0074");
                            if (t_ls != null && t_ls.Data != null && !t_ls.Data.Equals(_segmentsProcessed.ToString()))
                            {
                                add_error(ErrorTypes.ERROR, ErrorKinds.NUMBER_OF_SEGMENTS_MISMATCH).AddLocation(t_ls.Begin).AddData(t_ls.Data).AddData(_segmentsProcessed.ToString());
                            }
                            unh = null;
                            ea.ControlCount++;
                        }
                    }
                    else if ("UNE".Equals(e.tag.Data))
                    {
                        if (ung == null)
                        {
                            add_error(ErrorTypes.ERROR, ErrorKinds.UNEXPECTED_SEGMENT).AddLocation(e.tag.Begin)
                                .AddData(e.tag.Data);
                        }
                        else
                        {
                            if (_hasFunctionalGroups == 1)
                            {
                                LocatedString g_ls = get_located_string(unh, "e:E0048");
                                LocatedString e_ls = get_located_string(slip, "e:E0048");
                                if (
                                    g_ls != null && g_ls.Data != null && !g_ls.Data.Equals(e_ls.Data)
                                    || e_ls != null && e_ls.Data != null && !e_ls.Data.Equals(g_ls.Data)
                                )
                                {
                                    add_error(ErrorTypes.ERROR, ErrorKinds.FG_ID_MISMATCH).AddLocation(e_ls.Begin)
                                        .AddLocation(g_ls.Begin).AddData(e_ls.Data).AddData(g_ls.Data);
                                }
                                e_ls = get_located_string(slip, "e:E0060");
                                if (e_ls != null && e_ls.Data != null && !e_ls.Data.Equals(_segmentsProcessed.ToString()))
                                {
                                    add_error(ErrorTypes.ERROR, ErrorKinds.NUMBER_OF_MESSAGES_MISMATCH)
                                        .AddLocation(e_ls.Begin).AddData(e_ls.Data).AddData(_messagesProcessed.ToString());
                                }
                                _messagesProcessed = 0;
                            }
                            interchange_ea.ControlCount++;
                            ung = null;
                        }
                    }
                    else if ("UNZ".Equals(e.tag.Data))
                    {
                        LocatedString b_ls = get_located_string(unb, "e:E0020");
                        LocatedString z_ls = get_located_string(slip, "e:E0020");
                        if (
                            b_ls != null && b_ls.Data != null && !b_ls.Data.Equals(z_ls.Data)
                            || z_ls != null && z_ls.Data != null && !z_ls.Data.Equals(b_ls.Data)
                        )
                        {
                            add_error(ErrorTypes.ERROR, ErrorKinds.INTERCHANGE_ID_MISMATCH).AddLocation(z_ls.Begin)
                                .AddLocation(b_ls.Begin).AddData(z_ls.Data).AddData(b_ls.Data);
                        }
                        z_ls = get_located_string(slip, "e:E0036");
                        int control_count = (_hasFunctionalGroups == 0 ? _messagesProcessed : _fgProcessed);
                        if (z_ls != null && z_ls.Data != null && !z_ls.Data.Equals(control_count.ToString()))
                        {
                            add_error(ErrorTypes.ERROR, ErrorKinds.NUMBER_OF_SEGMENTS_MISMATCH).AddLocation(z_ls.Begin).AddData(z_ls.Data).AddData(control_count.ToString());
                        }
                        unb = null;
                        _interchangesProcessed++;
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
        XPathNavigator nav = seg.CreateNavigator().SelectSingleNode(xpath, _man);
        if (nav != null)
        {
            string[] parts = nav.GetAttribute("loc", "").Split(new char[] { ':' });
            if (parts.Length == 6)
            {
                Location l = new Location(parts, 0);
                res = new LocatedString(l, Encoding.UTF8.GetString(interchange_encoding.GetBytes(nav.Value)));
                res.End.Set(new Location(parts, 3));
            }
        }
        return res;
    }

    string get_value(XmlElement seg, string xpath)
    {
        string res = null;
        XPathNavigator nav = seg.CreateNavigator().SelectSingleNode(xpath, _man);
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
        ((XmlElement)el.AppendChild(slipDoc.CreateTextNode(Encoding.UTF8.GetString(interchange_encoding.GetBytes(ls.Data)))).ParentNode).SetAttribute("loc", ls.Begin.Offset + ":" + ls.Begin.Line + ":" + ls.Begin.Col + ":" + ls.End.Offset + ":" + ls.End.Line + ":" + ls.End.Col);
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
                        if (e.elements[i].Count == 1 && "".Equals(e.elements[i][0].Data))
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
                                            if ("".Equals(e.elements[i][k].Data))
                                            {
                                                if (sub_element.MinOccurs > l)
                                                {
                                                    add_error(ErrorTypes.ERROR, ErrorKinds.EXPECTED_SUB_ELEMENT_NOT_FOUND)
                                                        .AddLocation(e.elements[i][k].Begin).AddData(e.tag.Data)
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
                                                .AddLocation(e.elements[i][e.elements[i].Count - 1].End).AddData(e.tag.Data)
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
                                    .AddLocation(e.tag.End)
                                    .AddData(e.tag.Data).AddData(element.Name).AddData(ss.Trim().Trim(new char[] { ',' }));
                            }
                            if (k < e.elements[i].Count)
                            {
                                add_error(ErrorTypes.ERROR, ErrorKinds.EXTRA_SUB_ELEMENT_FOUND)
                                    .AddLocation(e.elements[i][k - 1].End).AddData(e.tag.Data).AddData(element.Name);
                            }
                            on_end_composite_element();
                        }
                        else
                        {
                            if (e.elements[i].Count > 1)
                            {
                                add_error(ErrorTypes.ERROR, ErrorKinds.EXTRA_SUB_ELEMENT_FOUND)
                                    .AddLocation(e.elements[i][0].End).AddData(e.tag.Data).AddData(element.Name);
                            }
                            on_element(element.Name, e.elements[i][0]);
                        }
                    }
                    else if (element.MinOccurs > j)
                    {
                        Location loc = new Location();
                        if (i > 0)
                        {
                            loc.Set(e.elements[i - 1][e.elements[i - 1].Count - 1].End);
                        }
                        else
                        {
                            loc.Set(e.tag.End);
                        }
                        add_error(ErrorTypes.ERROR, ErrorKinds.EXPECTED_ELEMENT_NOT_FOUND).AddLocation(loc)
                            .AddData(e.tag.Data)
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
            add_error(ErrorTypes.ERROR, ErrorKinds.INCOMPLETE_SEGMENT).AddLocation(e.tag.End).AddData(e.tag.Data).AddData(ss.Trim().Trim(new char[] { ',' }));
        }
        if (i < e.elements.Count)
        {
            Location loc = new Location();
            if (i > 0)
            {
                loc.Set(e.elements[i - 1][e.elements[i - 1].Count - 1].End);
            }
            else
            {
                loc.Set(e.tag.End);
            }
            add_error(ErrorTypes.ERROR, ErrorKinds.EXTRA_ELEMENT_FOUND).AddLocation(loc).AddData(e.tag.Data);
        }
        return true;
    }

    void write_out(SegmentEventArgs e)
    {
        Console.Write(e.tag.Data + "{" + e.tag.Begin.Line + "," + e.tag.Begin.Col + "-" + e.tag.End.Line + "," + e.tag.End.Col + "}");
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
                Console.Write(sub.Data + "{" + sub.Begin.Line + "," + sub.Begin.Col + "-" + sub.End.Line + "," + sub.End.Col + "}");
                i++;
            }
        }
        Console.WriteLine("'");
    }
}
