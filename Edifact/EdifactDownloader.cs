using Microsoft.Extensions.Logging;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.XPath;

namespace Net.Leksi.Edifact;

public class EdifactDownloader
{
    public event DirectoryNotFoundEventHandler? DirectoryNotFound;

    #region const
    private const string s_webSite = "https://unece.org";
    private const string s_webSite1 = "https://www.unece.org";
    private const string s_directoryNotExistsFormat = "Directory not exists: {0}";
    private const string s_rmRegexTuningName = "Net.Leksi.Edifact.Properties.regex_tuning";
    private const string s_rmFixedName = "Net.Leksi.Edifact.Properties.fixed";
    private const string s_rmUnslName = "Net.Leksi.Edifact.Properties.unsl";
    private const string s_rmErrorsName = "Net.Leksi.Edifact.Properties.errors";
    private const string s_typeAlreadyDeclared = "TYPE_ALREADY_DECLARED";
    private const string s_directoryFormat = "D{0:00}{1}";
    private const string s_defaultTargetDirectory = "xsd";
    private const string s_pathDam = "/DAM/trade/untdid/{0}/{1}.zip";
    private const string s_pathNoDam = "/fileadmin/DAM/trade/untdid/{0}/{1}.zip";
    private const string s_uriFormat = "{0}{1}";
    private const string s_fileNameFormat = "{0}.{1}";
    private const string s_messagePattern = "^([A-Z]{{6}}){0}\\.{1}$";
    private const string s_fileNotFoundFormat = "FILE_FOR_DIRECTORY_NOT_FOUND";
    private const string s_slash = "\\";
    private const string s_d20b = "D20B";
    private const string s_d9 = "D9";
    private const string s_un = "UN";
    private const string s_postfix_S = "_S";
    private const string s_postfix_D = "_D";
    private const string s_edcd = "edcd";
    private const string s_d96b = "D96B";
    private const string s_trcd = "trcd";
    private const string s_edsd = "edsd";
    private const string s_trsd = "trsd";
    private const string s_eded = "eded";
    private const string s_tred = "tred";
    private const string s_uncl = "uncl";
    private const string s_unsl = "unsl";
    private const string s_trcl = "trcl-";
    private const string s_edcl = "edcl-";
    private const string s_filePatternFormat = "{0}*.{1}";
    private const string s_unslFileNameFormat = "{0}{1}.{2}";
    private const string s_unslMessageFormat = "UNSL_MESSAGE";
    private const string s_unsl_ = "unsl-";
    private const string s_sourceArchiveDir = "--source";
    private const string s_preparedFilesDir = "--prepared";
    private const string s_failedUnzip = "FAILED_UNZIP";
    private const string s_usingExternalUnzip = "USING_EXTERNAL_UNZIP";
    private const string s_cmd = "cmd";
    private const string s_args = "args";
    private const string s_zipPattern = "*.zip";
    private const string s_logMessage = "{message}";
    private const string s_receivingDirectory = "RECEIVING_DIRECTORY";
    private const string s_d16a = "D16A";
    private const string s_macosx = "__MACOSX";
    private const string s_edifact_xsd = "edifact.xsd";
    private const string s_xsdFileNameFormat = "{0}.xsd";
    private const string s_tmpXsdFileNameFormat = "_{0}.xsd";
    private const string s_duplicatedTypeXPathFormat = "//xs:complexType[@name='{0}'][2]";
    private const string s_simpleTypesFileName = "simpletypes.xsd";
    private const string s_typesFileName = "types.xsd";
    private const string s_segmentsFileName = "segments.xsd";
    private const string s_sharp = "#";
    private const string s_messageNotFound = "MESSAGE_NOT_FOUND";
    private const string s_loadFixedFile = "LOAD_FIXED_FILE";
    private const string s_commentsXPath = "/comment()[1]";
    private const string s_unMessageFormat = " UN/{0} ";
    private const string s_message = "message";
    private const string s_sequenceIdStructureXPath = "//xs:sequence[@id='structure']";
    private const string s_xsPrefix = "xs";
    private const string s_element = "element";
    private const string s_elementByNameXPathFormat = "xs:element[@name='{0}']";
    private const string s_name = "name";
    private const string s_type = "type";
    private const string s_sg_ = "SG-";
    private const string s_complexType = "complexType";
    private const string s_sequence = "sequence";
    private const string s_lastElementComplexTypeSequenceXPath = "xs:element[last()]/xs:complexType/xs:sequence";
    private const string s_ancestorSequenceXPath = "ancestor::xs:sequence[1]";
    private const string s_sequenceIdStructureElementXPath = "//xs:sequence[@id='structure']//xs:element";
    private const string s_sgNameFormat = "SG-{0}";
    private const string s_ancestorElement = "ancestor::xs:element[1]";
    private const string s_invalidStructure = "INVALID_STRUCTURE";
    private const string s_minOccurs = "minOccurs";
    private const string s_maxOccurs = "maxOccurs";
    private const string s_exceptionParsingMessage = "EXCEPTION_PARSING_MESSAGE";
    private const string s_segments = "segments";
    private const string s_schema = "schema";
    private const string s_noItemsForElement = "NO_ITEMS_FOR_ELEMENT";
    private const string s_parentXPath = "..";
    private const string s_annotation = "annotation";
    private const string s_documentation = "documentation";
    private const string s_changeIndicatorFormat = "Change indicator: {0}";
    private const string s_complexContent = "complexContent";
    private const string s_extension = "extension";
    private const string s_upComplexContentExtension = "../xs:complexContent/xs:extension";
    private const string s_base = "base";
    private const string s_genericSegment = "GENERIC-SEGMENT";
    private const string s_lastElement = "xs:element[last()]";
    private const string s_renameElementFormat = "E{0}";
    private const string s_types = "types";
    private const string s_simpleTypes = "simpletypes";
    private const string s_noItemsForComplexType = "NO_ITEMS_FOR_COMPLEX_TYPE";
    private const string s_complexTypeByNameRestrictionFormat = "xs:complexType[@name='E{0:0000}']//xs:restriction";
    private const string s_enumeration = "enumeration";
    private const string s_value = "value";
    private const string s_e = "E";
    private const string s_n = "n";
    private const string s_pattern = "pattern";
    private const string s_numberTypePatternFormat = "^-?([0-9]\\.?){{{0}{1}}}[0-9]$";
    private const string s_minOccursPatternFormat = "{0},";
    private const string s_numberTypePattern1Format = "^-?[^.]*\\.?[^.]+$";
    private const string s_length = "length";
    private const string s_minLength = "minLength";
    private const string s_maxLength = "maxLength";
    private const string s_noSimpleTypes = "NO_SIMPLE_TYPES";
    private const string s_0051 = "0051";
    private const string s_0052 = "0052";
    private const string s_0054 = "0054";
    private const string s_0065 = "0065";
    #endregion const

    private static readonly ResourceManager s_rmRegexTuning;
    private static readonly ResourceManager s_rmFixed;
    private static readonly ResourceManager s_rmUnsl;
    private static readonly ResourceManager s_rmErrors;
    private static readonly Regex _reExternalUnzip = new("^\\s*(?<cmd>(?:\\\"[^\"]+\\\")|(?:[^\\s]+))(?<args>.+)$");
    private static readonly Regex _reRepr = new("(a?n?)((?:\\.\\.)?)(\\d+)");
    private static readonly List<string> _directories = [];

    private readonly EdifactDownloaderOptions _options;
    private readonly Regex _reTypeAlreadyDeclared;
    private readonly HttpClient _wc = new();
    private readonly XmlDocument _xsd = new();
    private readonly string _targetDirectory;
    private readonly string _preparedDir;
    private readonly ILogger<EdifactDownloader>? _logger;
    private readonly string _tmpDir;
    private readonly XmlNamespaceManager _man;

    private string? _directory;
    private string _dir = null!;
    private string _fname = null!;
    private string _ext = null!;
    private string _uncl = null!;
    private string _unsl = null!;
    private string[] _uncls = null!;
    private string[] _unsls = null!;
    private string _unsl_message = null!;
    private string _edcd = null!;
    private string _edsd = null!;
    private string _eded = null!;
    private string _mPostfix = string.Empty;
    private int _num_elements = 0;
    private int _num_sys_elements = 0;

    static EdifactDownloader()
    {
        s_rmRegexTuning = new ResourceManager(s_rmRegexTuningName, Assembly.GetExecutingAssembly());
        s_rmFixed = new ResourceManager(s_rmFixedName, Assembly.GetExecutingAssembly());
        s_rmUnsl = new ResourceManager(s_rmUnslName, Assembly.GetExecutingAssembly());
        s_rmErrors = new ResourceManager(s_rmErrorsName, Assembly.GetExecutingAssembly());
    }
    public EdifactDownloader(EdifactDownloaderOptions options, ILogger<EdifactDownloader>? logger = null)
    {
        _options = options;

        _logger = logger;

        _reTypeAlreadyDeclared = new Regex(s_rmRegexTuning.GetString(s_typeAlreadyDeclared)!);
        for (int i = 1994; i <= DateTime.Now.Year; i++)
        {
            for (char c = 'A'; c <= 'B'; c++)
            {
                _directories.Add(string.Format(s_directoryFormat, i % 100, c).ToUpper());
            }
            if (i == 2001)
            {
                _directories.Add(string.Format(s_directoryFormat, i % 100, 'C').ToUpper());
            }
        }
        if (_options.TargetFolder is null)
        {
            _targetDirectory = s_defaultTargetDirectory;
        }
        else
        {
            _targetDirectory = _options.TargetFolder;
        }
        if (_options.TmpFolder is { } && !Directory.Exists(Path.GetFullPath(_options.TmpFolder)))
        {
            throw new Exception(string.Format(s_directoryNotExistsFormat, _options.TmpFolder));
        }
        if (_options.Directory is { })
        {
            _directory = _options.Directory.ToUpper();
        }
        _tmpDir = _options.TmpFolder is { } ? Path.GetFullPath(_options.TmpFolder) : Path.GetTempPath();
        if (_options.TmpFolder is null)
        {
            string tempDirectory;
            for (
                tempDirectory = Path.Combine(_tmpDir, Path.GetRandomFileName());
                Directory.Exists(tempDirectory);
                tempDirectory = Path.Combine(_tmpDir, Path.GetRandomFileName())
            ) { }

            _tmpDir = tempDirectory;
        }
        if (!_tmpDir.EndsWith('\\'))
        {
            _tmpDir += s_slash;
        }
        if (!Directory.Exists(_targetDirectory))
        {
            Directory.CreateDirectory(_targetDirectory);
        }
        _preparedDir = Path.Combine(_tmpDir, s_preparedFilesDir);

        _man = new XmlNamespaceManager(_xsd.NameTable);
        _man.AddNamespace(s_xsPrefix, Properties.Resources.schema_ns);
    }

    public async Task DownloadAsync(CancellationToken stoppingToken)
    {
        if (_directory is null)
        {
            foreach (string d in _directories)
            {
                _directory = d;
                try
                {
                    await DownloadAsync(stoppingToken);
                }
                catch (Exception)
                {
                }
            }
            return;
        }
        _logger?.LogInformation(s_logMessage, string.Format(s_rmErrors.GetString(s_receivingDirectory)!, _directory));

        try
        {
            InitContext();

            Uri requestUri = GetRequestUri();

            HttpResponseMessage response = await _wc.GetAsync(requestUri, stoppingToken);

            ExtractAll(response.Content.ReadAsStream(stoppingToken));

            LoadFixedFilesFromResources();

            if (s_d96b.Equals(_directory))
            {
                new Compiler96B().Run(_tmpDir, _directory, null);
            }

            PrepareFilesLists();

            CopyNeededFilesToPreparedDirectory();

            BuildSchemas();
        }
        catch (Exception ex)
        {
            if(ex is not InvalidDataException)
            {
                _logger?.LogError(ex, s_logMessage, ex.Message);
            }
            throw;
        }
        finally
        {
            if (_options.TmpFolder is null)
            {
                Directory.Delete(_tmpDir, true);
            }
        }
    }

    private void BuildSchemas()
    {
        File.WriteAllText(Path.Combine(_targetDirectory, s_edifact_xsd), ReplaceNs(Properties.Resources.edifact));
        XmlSchemaSet? xmlSchemaSet = new(_xsd.NameTable);

        xmlSchemaSet.ValidationEventHandler += ValidationEventHandler;

        DeleteTmpFile(s_simpleTypes);
        DeleteTmpFile(s_types);
        DeleteTmpFile(s_segments);

        CultureInfo ci = Thread.CurrentThread.CurrentUICulture;
        Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

        string targetDirectory = Path.Combine(_targetDirectory, s_un, _directory!);
        if (Directory.Exists(targetDirectory))
        {
            Directory.Delete(targetDirectory, true);
        }
        Directory.CreateDirectory(targetDirectory);

        _num_elements = 0;
        _num_sys_elements = 0;

        MakeSimpleTypes(
            File.ReadAllLines(
                Path.Combine(
                    _preparedDir, 
                    string.Format(s_fileNameFormat, _eded, _ext)
                )
            )
        );
        foreach (string unc in _uncls)
        {
            MakeCodelist(
                File.ReadAllLines(
                    Path.Combine(_preparedDir, Path.GetFileName(unc))
                )
            );
        }
        if(_num_sys_elements == 0)
        {
            if (_unsl_message != null)
            {
                _logger?.LogInformation(s_logMessage, _unsl_message);
            }
            foreach (string uns in _unsls)
            {
                MakeCodelist(
                    File.ReadAllLines(
                        Path.Combine(_preparedDir, Path.GetFileName(uns))
                    )
                );
            }
        }
        if (_num_sys_elements == 0)
        {
            _logger?.LogWarning(s_logMessage, "todo: _num_sys_elements == 0");
        }
        AjustSysElementsList();
        if (_num_elements == 0)
        {
            _logger?.LogWarning(s_logMessage, "todo: _num_elements == 0");
        }
        xmlSchemaSet.Add(
            ReplaceNs(Properties.Resources.edifact_ns),
            Path.Combine(targetDirectory, s_simpleTypesFileName)
        );

        MakeTypes(
            File.ReadAllLines(
                Path.Combine(
                    _preparedDir, 
                    string.Format(s_fileNameFormat, _edcd, _ext)
                )
            )
        );
        xmlSchemaSet.Add(
            ReplaceNs(Properties.Resources.edifact_ns),
            Path.Combine(targetDirectory, s_typesFileName)
        );

        MakeSegments(
            File.ReadAllLines(
                Path.Combine(
                    _preparedDir,
                    string.Format(s_fileNameFormat, _edsd, _ext)
                )
            )
        );
        xmlSchemaSet.Add(
            ReplaceNs(Properties.Resources.edifact_ns),
            Path.Combine(targetDirectory, s_segmentsFileName)
        );

        string edmd = null!;
        List<string> messages = [];
        if (_options.Message is { })
        {
            if (!s_sharp.Equals(_options.Message))
            {
                messages.Add(_options.Message);
            }
        }
        else if ("d96b".Equals(_dir))
        {
            ListMessages(messages, "HTM");
        }
        else
        {
            ListMessages(messages, _ext);
        }

        foreach (string message in messages)
        {
            if ("d96b".Equals(_dir))
            {
                new Compiler96B().Run(_tmpDir, _directory!, message);
            }

            edmd = message + _mPostfix;
            if (
                !File.Exists(
                    Path.Combine(
                        _tmpDir, 
                        string.Format(s_fileNameFormat, edmd, _ext)
                    )
                )
            )
            {
                _logger?.LogWarning(s_logMessage, string.Format(s_rmErrors.GetString(s_messageNotFound)!, edmd, _directory));
                continue;
            }

            string src = Path.Combine(_tmpDir, string.Format(s_fileNameFormat, edmd, _ext));
            string dst = Path.Combine(_preparedDir, Path.GetFileName(src));

            if (File.Exists(dst))
            {
                File.Delete(dst);
            }
            CopyFile(src, dst);

            MakeMessage(message, dst);

            if (
                File.Exists(
                    Path.Combine(
                        targetDirectory, 
                        string.Format(s_xsdFileNameFormat, message)
                    )
                )
            )
            {

                IEnumerator ie = xmlSchemaSet.Schemas().GetEnumerator();
                if (ie.MoveNext())
                {
                    xmlSchemaSet.Remove((XmlSchema)ie.Current);
                }

                xmlSchemaSet.Add(
                    ReplaceNs(Properties.Resources.edifact_ns),
                    Path.Combine(targetDirectory, s_simpleTypesFileName)
                );
                xmlSchemaSet.Add(
                    ReplaceNs(Properties.Resources.edifact_ns),
                    Path.Combine(targetDirectory, s_typesFileName)
                );
                xmlSchemaSet.Add(
                    ReplaceNs(Properties.Resources.edifact_ns),
                    Path.Combine(targetDirectory, s_segmentsFileName)
                );
                xmlSchemaSet.Add(
                    ReplaceNs(Properties.Resources.edifact_ns),
                    Path.Combine(
                        targetDirectory,
                        string.Format(s_xsdFileNameFormat, message)
                    )
                );
            }
        }
        xmlSchemaSet = null;
        MoveTmpFile(s_simpleTypes);
        MoveTmpFile(s_types);
        MoveTmpFile(s_segments);
        Thread.CurrentThread.CurrentUICulture = ci;

    }
    private void MoveTmpFile(string selector)
    {
        string file1 = Path.Combine(_targetDirectory, s_un, _directory!, string.Format(s_tmpXsdFileNameFormat, selector));
        string file2 = Path.Combine(_targetDirectory, s_un, _directory!, string.Format(s_xsdFileNameFormat, selector));
        if (File.Exists(file1))
        {
            if (File.Exists(file2))
            {
                File.Delete(file2);
            }
            File.Move(file1, file2);
        }
    }
    private void InitXmlDocument(string fname)
    {
        _xsd.LoadXml(ReplaceNs(Properties.Resources.ResourceManager.GetString(fname, Properties.Resources.Culture)!));
        XPathNavigator nav = _xsd.CreateNavigator()!;
        XPathNodeIterator ni1 = nav.Select(s_commentsXPath);
        if (ni1.MoveNext())
        {
            XPathNavigator nav1 = ni1.Current!.CreateNavigator();
            nav1.SetValue(string.Format(s_unMessageFormat, _directory));
            ni1.Current.InsertBefore(nav1);
            ni1.Current.DeleteSelf();
        }
    }
    private void MakeMessage(string message, string file)
    {
        string[] data = File.ReadAllLines(file);
        InitXmlDocument(s_message);
        XPathNavigator nav = _xsd.CreateNavigator()!.SelectSingleNode(s_sequenceIdStructureXPath, _man)!;
        MParser mp = new();
        XPathNodeIterator? ni = null;
        mp.OnSegment += (string name, string info, string desc, int sg) =>
        {
            XmlElement el = _xsd.CreateElement(s_xsPrefix, s_element, Properties.Resources.schema_ns);
            string ename = name;
            for (int i = 1; ; i++)
            {
                if (nav.SelectSingleNode(string.Format(s_elementByNameXPathFormat, ename), _man) == null)
                {
                    break;
                }
                ename = string.Format(s_fileNameFormat, name, i);
            }
            el.SetAttribute(s_name, ename);
            el.SetAttribute(s_type, name);
            nav.AppendChild(el.CreateNavigator()!);
        };
        mp.OnBeginSG += delegate (int num, string desc)
        {
            XmlElement el = _xsd.CreateElement(s_xsPrefix, s_element, Properties.Resources.schema_ns);
            el.SetAttribute(s_name, s_sg_ + num.ToString());
            el.AppendChild(_xsd.CreateElement(s_xsPrefix, s_complexType, Properties.Resources.schema_ns))!
                .AppendChild(_xsd.CreateElement(s_xsPrefix, s_sequence, Properties.Resources.schema_ns));
            nav.AppendChild(el.CreateNavigator()!);
            nav = nav.SelectSingleNode(s_lastElementComplexTypeSequenceXPath, _man)!;
        };
        mp.OnEndSG += delegate (int num)
        {
            nav = nav.SelectSingleNode(s_ancestorSequenceXPath, _man)!;
        };
        mp.OnEndMessage += delegate ()
        {
            nav = _xsd.CreateNavigator()!.SelectSingleNode(s_sequenceIdStructureXPath, _man)!;
        };
        mp.OnOccurs += delegate (int sgnum, string segment, int min_occurs, int max_occurs)
        {
            ni ??= _xsd.CreateNavigator()!.Select(s_sequenceIdStructureElementXPath, _man);
            bool move = ni.MoveNext();
            if (
                !move
                || (
                    sgnum == 0
                    && (
                        segment is null
                        || !ni.Current!.GetAttribute(s_name, string.Empty).StartsWith(segment)
                    )
                )
                || (
                    sgnum > 0
                    && (
                        (
                            segment is null
                            && !ni.Current!.GetAttribute(s_name, string.Empty).Equals(string.Format(s_sgNameFormat, sgnum))
                        )
                        || (
                            segment is not null
                            && (
                                !ni.Current!.GetAttribute(s_name, string.Empty).StartsWith(segment)
                                || !ni.Current.SelectSingleNode(s_ancestorElement, _man)!.GetAttribute(s_name, "").Equals(string.Format(s_sgNameFormat, sgnum))
                            )
                        )
                    )
                )
            )
            {
                throw new Exception(s_rmErrors.GetString(s_invalidStructure));
            }
            if (min_occurs != 1 || max_occurs != 1)
            {
                if (min_occurs != 1)
                {
                    ni.Current!.CreateAttribute(string.Empty, s_minOccurs, string.Empty, min_occurs.ToString());
                }
                if (max_occurs != 1)
                {
                    ni.Current!.CreateAttribute(string.Empty, s_maxOccurs, string.Empty, max_occurs.ToString());
                }
            }
        };
        try
        {
            mp.Run(data);
            SaveXmlDocument(message);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, s_logMessage, string.Format(s_rmErrors.GetString(s_exceptionParsingMessage)!, message, _directory, Path.GetFileName(file), mp.LineNumber));
        }
    }
    private void ListMessages(List<string> list, string ext)
    {
        string[] files = Directory.GetFiles(_tmpDir);
        Regex re = new(string.Format(s_messagePattern, _mPostfix, ext));
        foreach (string file in files)
        {
            Match m = re.Match(Path.GetFileName(file).ToUpper());
            if (m.Success)
            {
                list.Add(m.Groups[1].Captures[0].Value);
            }
        }
    }
    private void MakeSegments(string[] data)
    {
        InitXmlDocument(s_segments);
        XPathNavigator nav = _xsd.CreateNavigator()!;
        nav.MoveToChild(s_schema, Properties.Resources.schema_ns);
        SCParser scp = new();
        XmlElement seq = null!;
        string elName = string.Empty;
        int num_items = 0;
        _num_elements = 0;
        scp.OnSegmentOrType += delegate (string name, string change_indicator, string info, string description, string note)
        {
            if (!string.IsNullOrEmpty(elName))
            {
                if (num_items == 0)
                {
                    _logger?.LogWarning(s_logMessage, string.Format(s_rmErrors.GetString(s_noItemsForElement)!, _directory, elName));
                }
            }
            elName = name;
            num_items = 0;
            XmlElement ann = (XmlElement)((XmlElement)nav.UnderlyingObject!)
                .AppendChild(_xsd.CreateElement(s_xsPrefix, s_complexType, Properties.Resources.schema_ns))!
                .AppendChild(_xsd.CreateElement(s_xsPrefix, s_annotation, Properties.Resources.schema_ns))!
            ;

            ann.CreateNavigator()!.SelectSingleNode(s_parentXPath, _man)!
                .CreateAttribute(string.Empty, s_name, string.Empty, name)
            ;

            ann.AppendChild(_xsd.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns))!
                .AppendChild(_xsd.CreateTextNode(info))
            ;
            ann.AppendChild(_xsd.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns))!
                .AppendChild(_xsd.CreateTextNode(description))
            ;
            if (!string.IsNullOrEmpty(note))
            {
                ann.AppendChild(_xsd.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns))!
                    .AppendChild(_xsd.CreateTextNode(note))
                ;
            }
            if (!string.IsNullOrEmpty(change_indicator))
            {
                ann.AppendChild(_xsd.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns))!
                    .AppendChild(_xsd.CreateTextNode(string.Format(s_changeIndicatorFormat, change_indicator)))
                ;
            }
            seq = (XmlElement)ann.ParentNode!
                .AppendChild(_xsd.CreateElement(s_xsPrefix, s_complexContent, Properties.Resources.schema_ns))!
                .AppendChild(_xsd.CreateElement(s_xsPrefix, s_extension, Properties.Resources.schema_ns))!
                .AppendChild(_xsd.CreateElement(s_xsPrefix, s_sequence, Properties.Resources.schema_ns))!
            ;
            ann.CreateNavigator()!
                .SelectSingleNode(s_upComplexContentExtension, _man)!
                .CreateAttribute(string.Empty, s_base, string.Empty, s_genericSegment)
            ;
            ++_num_elements;
        };
        string prev_name = string.Empty;
        int prev_max_occurs = 0;
        int prev_min_occurs = 0;
        scp.OnItem += delegate (string name, string info, int min_occurs, int max_occurs, string repr)
        {
            if (!name.StartsWith('C'))
            {
                name = string.Format(s_renameElementFormat, name);
            }
            string ename = name;
            OnItem(seq, name, ename, info, min_occurs, max_occurs, ref prev_name, ref prev_min_occurs, ref prev_max_occurs);
            ++num_items;
        };
        scp.Run(data);
        if (!string.IsNullOrEmpty(elName))
        {
            if (num_items == 0)
            {
                _logger?.LogWarning(s_logMessage, string.Format(s_rmErrors.GetString(s_noItemsForElement)!, _directory, elName));
            }
        }
        SaveXmlDocument(s_segments);
    }
    private void OnItem(
        XmlElement seq,
        string name, string ename, string info, int min_occurs, int max_occurs,
        ref string prev_name, ref int prev_min_occurs, ref int prev_max_occurs
    )
    {
        XmlElement el;
        if (prev_name.Equals(ename))
        {
            prev_max_occurs += max_occurs;
            prev_min_occurs += min_occurs;
            el = (XmlElement)seq.CreateNavigator()!
                .SelectSingleNode(s_lastElement, _man)!
                .UnderlyingObject!
            ;
        }
        else
        {
            for (int i = 1; ; i++)
            {
                if (
                    seq.CreateNavigator()!
                        .SelectSingleNode(string.Format(s_elementByNameXPathFormat, ename), _man) is null
                )
                {
                    break;
                }
                ename = string.Format(s_fileNameFormat, name, i);
            }
            prev_name = ename;
            prev_max_occurs = max_occurs;
            prev_min_occurs = min_occurs;
            el = (XmlElement)seq.AppendChild(_xsd.CreateElement(s_xsPrefix, s_element, Properties.Resources.schema_ns))!;
            el.SetAttribute(s_name, ename);
            el.SetAttribute(s_type, name);
            el.AppendChild(_xsd.CreateElement(s_xsPrefix, s_annotation, Properties.Resources.schema_ns))!
                .AppendChild(_xsd.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns))!
                .AppendChild(_xsd.CreateTextNode(info));
        }
        if (prev_min_occurs != 1)
        {
            el.SetAttribute(s_minOccurs, prev_min_occurs.ToString());
        }
        else
        {
            el.RemoveAttribute(s_minOccurs);
        }
        if (prev_max_occurs != 1)
        {
            el.SetAttribute(s_maxOccurs, prev_max_occurs.ToString());
        }
        else
        {
            el.RemoveAttribute(s_maxOccurs);
        }
    }
    private void MakeTypes(string[] data)
    {
        InitXmlDocument(s_types);
        XPathNavigator nav = _xsd.CreateNavigator()!;
        nav.MoveToChild(s_schema, Properties.Resources.schema_ns);
        SCParser scp = new()
        {
            WaitEmptyStringForNextItem = false
        };
        XmlElement seq = null!;
        _num_elements = 0;
        int num_items = 0;
        string elName = string.Empty;
        scp.OnSegmentOrType += delegate (string name, string change_indicator, string info, string description, string note)
        {
            if (!string.IsNullOrEmpty(elName))
            {
                if (num_items == 0)
                {
                    _logger?.LogWarning(s_logMessage, string.Format(s_rmErrors.GetString(s_noItemsForComplexType)!, _directory, elName));
                }
            }
            elName = name;
            num_items = 0;
            XmlElement ann = (XmlElement)((XmlElement)nav.UnderlyingObject!)
                .AppendChild(_xsd.CreateElement(s_xsPrefix, s_complexType, Properties.Resources.schema_ns))!
                .AppendChild(_xsd.CreateElement(s_xsPrefix, s_annotation, Properties.Resources.schema_ns))!
            ;
            ann.CreateNavigator()!
                .SelectSingleNode(s_parentXPath, _man)!
                .CreateAttribute(string.Empty, s_name, string.Empty, name)
            ;
            ann.AppendChild(_xsd.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns))!
                .AppendChild(_xsd.CreateTextNode(info))
            ;
            ann.AppendChild(_xsd.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns))!
                .AppendChild(_xsd.CreateTextNode(description))
            ;
            if (!string.IsNullOrEmpty(change_indicator))
            {
                ann.AppendChild(_xsd.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns))!
                    .AppendChild(_xsd.CreateTextNode(string.Format(s_changeIndicatorFormat, change_indicator)))
                ;
            }
            seq = (XmlElement)ann.ParentNode!.AppendChild(_xsd.CreateElement(s_xsPrefix, s_sequence, Properties.Resources.schema_ns))!;
            ++_num_elements;
        };
        string prev_name = string.Empty;
        int prev_max_occurs = 0;
        int prev_min_occurs = 0;
        scp.OnItem += (string name, string info, int min_occurs, int max_occurs, string repr) =>
        {
            name = string.Format(s_renameElementFormat, name);
            string ename = name;
            OnItem(seq, name, ename, info, min_occurs, max_occurs, ref prev_name, ref prev_min_occurs, ref prev_max_occurs);
            ++num_items;
        };
        scp.Run(data);
        if (!string.IsNullOrEmpty(elName))
        {
            if (num_items == 0)
            {
                _logger?.LogWarning(s_logMessage, string.Format(s_rmErrors.GetString(s_noItemsForComplexType)!, _directory, elName));
            }
        }
        SaveXmlDocument(s_types);
    }
    private void AjustSysElementsList()
    {
        _xsd.Load(Path.Combine(_targetDirectory, s_un, _directory!, s_simpleTypesFileName));
        XPathNavigator nav = _xsd.CreateNavigator()!;
        nav.MoveToChild(s_schema, Properties.Resources.schema_ns);
        foreach (int num in new int[] { 51, 52, 54, 65 })
        {
            string value = null!;
            switch (num)
            {
                case 51:
                    value = s_un;
                    break;
                case 52:
                    value = _directory![..1];
                    break;
                case 54:
                    value = _directory![1..];
                    break;
            }
            XPathNavigator navRestr = nav.SelectSingleNode(string.Format(s_complexTypeByNameRestrictionFormat, num), _man)!;
            if (num != 65)
            {
                AddEnumeration(navRestr, value);
            }
            else
            {
                List<string> list = [];
                ListMessages(list, _ext);
                foreach (string file in list)
                {
                    AddEnumeration(navRestr, file);
                }
            }
        }
        SaveXmlDocument(s_simpleTypes);
    }
    private void AddEnumeration(XPathNavigator nav, string value)
    {
        XmlElement enumeration = (XmlElement)((XmlElement)nav.UnderlyingObject!)
            .AppendChild(_xsd.CreateElement(s_xsPrefix, s_enumeration, Properties.Resources.schema_ns))!
        ;
        enumeration.SetAttribute(s_value, value);
    }
    private void MakeCodelist(string[] data)
    {
        _xsd.Load(Path.Combine(_targetDirectory, s_un, _directory!, s_simpleTypesFileName));
        XPathNavigator nav = _xsd.CreateNavigator()!;
        nav.MoveToChild(s_schema, Properties.Resources.schema_ns);
        CLParser clp = new();
        XPathNavigator navRestr = null!;
        int num_items = 0;
        bool skip = false;
        clp.OnSimpleType += (string name) =>
        {
            skip = false;
            navRestr = nav.SelectSingleNode(string.Format(s_complexTypeByNameRestrictionFormat, name), _man)!;
            if (int.Parse(name) < 1000)
            {
                if (
                    s_0051.Equals(name)
                    || s_0052.Equals(name)
                    || s_0054.Equals(name)
                    || s_0065.Equals(name)
                )
                {
                    skip = true;
                }
                if (!skip)
                {
                    ++_num_sys_elements;
                }
            }
            else
            {
                ++_num_elements;
            }
        };
        clp.OnItem += (string value, string change_indicator, string info, string description) =>
        {
            if (skip)
            {
                return;
            }
            if (navRestr != null)
            {
                XmlElement enumeration = (XmlElement)((XmlElement)navRestr.UnderlyingObject!)
                    .AppendChild(_xsd.CreateElement(s_xsPrefix, s_enumeration, Properties.Resources.schema_ns))!
                ;
                enumeration.SetAttribute(s_value, value);
                XmlElement ann = (XmlElement)enumeration
                    .AppendChild(_xsd.CreateElement(s_xsPrefix, s_annotation, Properties.Resources.schema_ns))!
                ;
                ann.AppendChild(_xsd.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns))!
                    .AppendChild(_xsd.CreateTextNode(info))
                ;
                ann.AppendChild(_xsd.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns))!
                    .AppendChild(_xsd.CreateTextNode(description))
                ;
                if (!string.IsNullOrEmpty(change_indicator))
                {
                    ann.AppendChild(_xsd.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns))!
                        .AppendChild(_xsd.CreateTextNode(string.Format(s_changeIndicatorFormat, change_indicator)))
                    ;
                }
                ++num_items;
            }
        };
        clp.Run(data);
        SaveXmlDocument(s_simpleTypes);
    }
    private void MakeSimpleTypes(string[] data)
    {
        InitXmlDocument(s_simpleTypes);
        XPathNavigator nav = _xsd.CreateNavigator()!;
        nav.MoveToChild(s_schema, Properties.Resources.schema_ns);
        EParser ep = new();
        _num_elements = 0;
        ep.OnSimpleType += (string name, string change_indicator, string info, string description, string repr, string note) =>
        {
            XmlElement ct = (XmlElement)((XmlElement)nav.UnderlyingObject!)
                .AppendChild(_xsd.CreateElement(s_xsPrefix, s_complexType, Properties.Resources.schema_ns))!
            ;
            XmlElement ann = (XmlElement)ct
                .AppendChild(_xsd.CreateElement(s_xsPrefix, s_annotation, Properties.Resources.schema_ns))!
            ;
            XmlElement restr = (XmlElement)ct
                .AppendChild(_xsd.CreateElement(s_xsPrefix, "simpleContent", Properties.Resources.schema_ns))!
                .AppendChild(_xsd.CreateElement(s_xsPrefix, "restriction", Properties.Resources.schema_ns))!
            ;
            ct.SetAttribute(s_name, string.Format(s_renameElementFormat, name));
            restr.SetAttribute(s_base, s_e);
            ann.AppendChild(_xsd.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns))!
                .AppendChild(_xsd.CreateTextNode(info))
            ;
            ann.AppendChild(_xsd.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns))!
                .AppendChild(_xsd.CreateTextNode(description))
            ;
            if (!string.IsNullOrEmpty(note))
            {
                ann.AppendChild(_xsd.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns))!
                    .AppendChild(_xsd.CreateTextNode(note))
                ;
            }
            if (!string.IsNullOrEmpty(change_indicator))
            {
                ann.AppendChild(_xsd.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns))!
                    .AppendChild(_xsd.CreateTextNode(string.Format(s_changeIndicatorFormat, change_indicator)))
                ;
            }
            int min_oocurs = 0;
            int max_oocurs = 1;
            bool number = false;
            Match m = _reRepr.Match(repr);
            if (m.Success)
            {
                if (s_n.Equals(m.Groups[1].Captures[0].Value))
                {
                    number = true;
                    min_oocurs = 1;
                }
                max_oocurs = int.Parse(m.Groups[3].Captures[0].Value);
                if (string.IsNullOrEmpty(m.Groups[2].Captures[0].Value))
                {
                    min_oocurs = max_oocurs;
                }
            }
            if (number)
            {
                ((XmlElement)restr.AppendChild(_xsd.CreateElement(s_xsPrefix, s_pattern, Properties.Resources.schema_ns))!)
                    .SetAttribute(s_value, string.Format(s_numberTypePatternFormat, (min_oocurs == max_oocurs ? string.Empty : string.Format(s_minOccursPatternFormat, min_oocurs)), max_oocurs))
                ;
                ((XmlElement)restr.AppendChild(_xsd.CreateElement(s_xsPrefix, s_pattern, Properties.Resources.schema_ns))!)
                    .SetAttribute(s_value, s_numberTypePattern1Format)
                ;
            }
            else
            {
                if (min_oocurs == max_oocurs)
                {
                    ((XmlElement)restr.AppendChild(_xsd.CreateElement(s_xsPrefix, s_length, Properties.Resources.schema_ns))!)
                        .SetAttribute(s_value, min_oocurs.ToString())
                    ;
                }
                else
                {
                    if (min_oocurs > 0)
                    {
                        ((XmlElement)restr.AppendChild(_xsd.CreateElement(s_xsPrefix, s_minLength, Properties.Resources.schema_ns))!)
                            .SetAttribute(s_value, min_oocurs.ToString())
                        ;
                    }
                    ((XmlElement)restr.AppendChild(_xsd.CreateElement(s_xsPrefix, s_maxLength, Properties.Resources.schema_ns))!)
                        .SetAttribute(s_value, max_oocurs.ToString())
                    ;
                }
            }
            _num_elements++;
        };
        ep.Run(data);
        if (_num_elements == 0)
        {
            _logger?.LogWarning(s_logMessage, string.Format(s_rmErrors.GetString(s_noSimpleTypes)!, _directory));
        }
        SaveXmlDocument(s_simpleTypes);
    }
    private void DeleteTmpFile(string selector)
    {
        string file1 = Path.Combine(_targetDirectory, s_un, _directory!, string.Format(s_tmpXsdFileNameFormat, selector));
        if (File.Exists(file1))
        {
            File.Delete(file1);
        }
    }
    private void ValidationEventHandler (object? obj, ValidationEventArgs args)
    {
        Match m = _reTypeAlreadyDeclared.Match(args.Message);
        if (m.Success)
        {
            string type_name = m.Groups[1].Captures[0].Value;
            if (type_name.StartsWith(s_e))
            {
                RemoveDuplicatedType(s_simpleTypes, type_name);
            }
            else if (type_name.StartsWith('S') || type_name.StartsWith('C'))
            {
                RemoveDuplicatedType(s_types, type_name);
            }
            else
            {
                RemoveDuplicatedType(s_segments, type_name);
            }
        }
        else
        {
            Console.WriteLine(args.Message);
        }
    }
    private void RemoveDuplicatedType(string selector, string type_name)
    {
        string file = string.Format(s_tmpXsdFileNameFormat, selector);
        string file1 = Path.Combine(_targetDirectory, s_un, _directory!, file);
        if (File.Exists(file1))
        {
            _xsd.Load(file1);
        }
        else
        {
            _xsd.Load(Path.Combine(_targetDirectory, s_un, _directory!, string.Format(s_xsdFileNameFormat, selector)));
        }
        XPathNavigator nav = _xsd.CreateNavigator()!.SelectSingleNode(string.Format(s_duplicatedTypeXPathFormat, type_name), _man)!;
        if (nav != null)
        {
            nav.DeleteSelf();
            SaveXmlDocument(file);
        }
    }
    private void SaveXmlDocument(string fname)
    {
        XmlWriterSettings ws = new()
        {
            Indent = true,
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = true
        };
        XmlWriter wr = XmlWriter.Create(Path.Combine(_targetDirectory, s_un, _directory!, string.Format(s_xsdFileNameFormat, fname)), ws);
        _xsd.WriteTo(wr);
        wr.Close();
    }

    private string ReplaceNs(string str)
    {
        if (_options.Namespace is { })
        {
            return str.Replace(Properties.Resources.edifact_ns, _options.Namespace);
        }
        else
        {
            return str;
        }
    }
    private void CopyNeededFilesToPreparedDirectory()
    {
        string targetFile = Path.Combine(_preparedDir, string.Format(s_fileNameFormat, _edsd, _ext));

        if (!Directory.Exists(_preparedDir))
        {
            Directory.CreateDirectory(_preparedDir);
        }

        if (File.Exists(targetFile))
        {
            File.Delete(targetFile);
        }
        CopyFile(Path.Combine(_tmpDir, string.Format(s_fileNameFormat, _edsd, _ext)), targetFile);

        targetFile = Path.Combine(_preparedDir, string.Format(s_fileNameFormat, _edcd, _ext));
        if (File.Exists(targetFile))
        {
            File.Delete(targetFile);
        }
        CopyFile(Path.Combine(_tmpDir, string.Format(s_fileNameFormat, _edcd, _ext)), targetFile);

        targetFile = Path.Combine(_preparedDir, string.Format(s_fileNameFormat, _eded, _ext));
        if (File.Exists(targetFile))
        {
            File.Delete(targetFile);
        }
        CopyFile(Path.Combine(_tmpDir, string.Format(s_fileNameFormat, _eded, _ext)), targetFile);
        foreach (string unc in _uncls)
        {
            CopyFile(
                unc,
                Path.Combine(_preparedDir, Path.GetFileName(unc))
            );
        }
        foreach (string uns in _unsls)
        {
            CopyFile(
                uns,
                Path.Combine(_preparedDir, Path.GetFileName(uns))
            );
        }
    }

    private void PrepareFilesLists()
    {
        _edcd = s_edcd;
        if (!File.Exists(Path.Combine(_tmpDir, string.Format(s_fileNameFormat, _edcd, _ext))))
        {
            _edcd = s_trcd;
            if (!File.Exists(Path.Combine(_tmpDir, string.Format(s_fileNameFormat, _edcd, _ext))))
            {
                _logger?.LogCritical(
                    s_logMessage,
                    string.Format(s_rmErrors.GetString(s_fileNotFoundFormat)!, 'C', _directory)
                );
                return;
            }
        }

        _edsd = s_edsd;
        if (!File.Exists(Path.Combine(_tmpDir, string.Format(s_fileNameFormat, _edsd, _ext))))
        {
            _edsd = s_trsd;
            if (!File.Exists(Path.Combine(_tmpDir, string.Format(s_fileNameFormat, _edsd, _ext))))
            {
                _logger?.LogCritical(
                    s_logMessage,
                    string.Format(s_rmErrors.GetString(s_fileNotFoundFormat)!, 'S', _directory)
                );
                return;
            }
        }

        _eded = s_eded;
        if (!File.Exists(Path.Combine(_tmpDir, string.Format(s_fileNameFormat, _eded, _ext))))
        {
            _eded = s_tred;
            if (!File.Exists(Path.Combine(_tmpDir, string.Format(s_fileNameFormat, _eded, _ext))))
            {
                _logger?.LogCritical(
                    s_logMessage,
                    string.Format(s_rmErrors.GetString(s_fileNotFoundFormat)!, 'E', _directory)
                );
                return;
            }
        }

        _uncls = Directory.GetFiles(_tmpDir, string.Format(s_filePatternFormat, _uncl, _ext));
        List<string> unl = new(Directory.GetFiles(_tmpDir, string.Format(s_filePatternFormat, _unsl, _ext)));
        if (unl.Count == 0)
        {
            int di = 0;
            for (; di < _directories.Count; di++)
            {
                if (_directory!.Equals(_directories[di]))
                {
                    break;
                }
            }
            if (di < _directories.Count)
            {
                for (; di >= 0; di--)
                {
                    ResourceSet? resources = s_rmUnsl.GetResourceSet(CultureInfo.InvariantCulture, true, true);
                    string prefix = Path.Combine(s_un, _directories[di].ToUpper());
                    int n = 0;
                    foreach (object? res in resources!)
                    {
                        if (
                            res is DictionaryEntry de
                            && de.Key is string key
                            && key.StartsWith(prefix)
                            && de.Value is byte[] bytes
                        )
                        {
                            ++n;
                            File.WriteAllBytes(
                                Path.Combine(
                                    _tmpDir,
                                    string.Format(s_unslFileNameFormat, s_unsl_, n, _ext)
                                ),
                                bytes
                            );
                        }
                    }
                    unl.AddRange(
                        Directory.GetFiles(
                            _tmpDir,
                            string.Format(s_filePatternFormat, s_unsl, _ext)
                        )
                    );
                    if (unl.Count > 0)
                    {
                        _unsl_message = string.Format(s_rmErrors.GetString(s_unslMessageFormat)!, _directory, _directories[di]);
                        _unsl = s_unsl_;
                        break;
                    }


                }
            }
        }
        _unsls = new string[unl.Count];
        unl.CopyTo(_unsls);


    }

    private void LoadFixedFilesFromResources()
    {
        ResourceSet? resources = s_rmFixed.GetResourceSet(CultureInfo.InvariantCulture, true, true);
        string prefix = Path.Combine(s_un, _directory!);
        foreach (object? res in resources!)
        {
            if (
                res is DictionaryEntry de
                && de.Key is string key
                && key.StartsWith(prefix)
                && de.Value is byte[] bytes
            )
            {
                _logger?.LogInformation(s_logMessage, string.Format(s_rmErrors.GetString(s_loadFixedFile)!, key));
                string target = Path.Combine(_tmpDir, Path.GetFileName(key));
                File.WriteAllBytes(target, bytes);
            }
        }

    }

    private void InitContext()
    {
        if (!Directory.Exists(_tmpDir))
        {
            Directory.CreateDirectory(_tmpDir);
        }
        else
        {
            foreach (string f in Directory.GetFiles(_tmpDir))
            {
                File.Delete(f);
            }
            foreach (string d in Directory.GetDirectories(_tmpDir))
            {
                Directory.Delete(d, true);
            }
        }

        _dir = _directory!.ToLower();
        _fname = _dir;
        _ext = _directory[1..];
        _uncl = s_uncl;
        _unsl = s_unsl;

        _mPostfix = s_postfix_D;
    }

    private Uri GetRequestUri()
    {
        Uri requestUri;
        if (
            string.Compare(_dir, s_d20b, StringComparison.OrdinalIgnoreCase) < 0
            || string.Compare(_dir, s_d9, StringComparison.OrdinalIgnoreCase) > 0
        )
        {
            requestUri = new Uri(string.Format(s_uriFormat, s_webSite, string.Format(s_pathDam, _dir, _fname)));
        }
        else
        {
            requestUri = new Uri(string.Format(s_uriFormat, s_webSite1, string.Format(s_pathNoDam, _dir, _fname)));
        }
        _logger?.LogInformation(s_logMessage, requestUri);
        return requestUri;
    }

    private void ExtractAll(Stream stream)
    {
        ZipArchive zip;
        try
        {
            zip = new(stream);
        }
        catch (Exception)
        {
            DirectoryNotFound?.Invoke(this, new DirectoryNotFoundEventArgs { Directory = _directory! });
            throw;
        }
        string sourceArchve = Path.Combine(_tmpDir, s_sourceArchiveDir);
        if (Directory.Exists(sourceArchve))
        {
            Directory.Delete(sourceArchve, true);
        }
        Directory.CreateDirectory(sourceArchve);
        zip.ExtractToDirectory(sourceArchve);
        zip.ExtractToDirectory(_tmpDir);

        List<string> list = [];
        List<string> list1 = [];
        bool found = true;
        while (found)
        {
            found = false;
            list.Clear();
            list.AddRange(Directory.GetFiles(_tmpDir, s_zipPattern));
            foreach (string file in list)
            {
                found = true;
                FileStream? fs = null;
                try
                {
                    fs = new(file, FileMode.Open, FileAccess.Read);
                    zip = new ZipArchive(fs);
                    zip.ExtractToDirectory(_tmpDir, true);
                }
                catch (Exception) 
                {
                    if (_options.ExternalUnzipCommandLineFormat is string cmd)
                    {
                        UseExternalUnzip(_tmpDir, file, cmd);
                    }
                    else
                    {
                        _logger?.LogError(s_logMessage, string.Format(s_rmErrors.GetString(s_failedUnzip)!, file));
                        throw;
                    }
                }
                finally
                {
                    fs?.Close();
                    File.Delete(file);
                }
            }
            list.Clear();
            list.AddRange(Directory.GetDirectories(_tmpDir).Where(v => v != sourceArchve));
            foreach (string folder in list)
            {
                if(_directory == s_d16a && Path.GetFileName(folder) == s_macosx)
                {
                    continue;
                }
                found = true;
                list1.Clear();
                list1.AddRange(Directory.GetFiles(folder));
                foreach (string file in list1)
                {
                    string dest = Path.Combine(Path.GetDirectoryName(file)!, s_parentXPath, Path.GetFileName(file));
                    if (!File.Exists(dest))
                    {
                        File.Move(file, dest);
                    }
                }
                list1.Clear();
                list1.AddRange(Directory.GetDirectories(folder));
                foreach (string dir in list1)
                {
                    string dest = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(dir)!, s_parentXPath, Path.GetFileName(dir)));
                    Directory.Move(dir, dest);
                }
                Directory.Delete(folder, true);
            }
        }
        foreach(string file in Directory.GetFiles(_tmpDir))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
    }

    private void UseExternalUnzip(string tmpDir, string file, string cmd)
    {
        string commandLine = string.Format(cmd, tmpDir, file);
        Match match = _reExternalUnzip.Match(commandLine);
        if (match.Success)
        {
            _logger?.LogInformation(
                s_logMessage,
                string.Format(s_rmErrors.GetString(s_usingExternalUnzip)!, commandLine)
            );
            Process unzip = new()
            {
                StartInfo = new()
                {
                    FileName = match.Groups[s_cmd].Value,
                    Arguments = match.Groups[s_args].Value,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = tmpDir,
                }
            };
            unzip.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger?.LogError(s_logMessage, e.Data);
                }
            };
            unzip.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger?.LogInformation(s_logMessage, e.Data);
                }
            };
            unzip.Start();
            unzip.BeginErrorReadLine();
            unzip.BeginOutputReadLine();

            unzip.WaitForExit();
            unzip.CancelErrorRead();
            unzip.CancelOutputRead();
        }
    }

    private static void CopyFile(string src, string dst)
    {
        byte[] bb = File.ReadAllBytes(src);
        List<byte> l = new(bb);
        for (int i = 0; i < l.Count; i++)
        {
            if (l[i] == 0x1A)
            {
                l.RemoveRange(i, l.Count - i);
            }
        }
        byte[] b = new byte[l.Count];
        l.CopyTo(b);
        File.WriteAllBytes(dst, b);
    }
}
