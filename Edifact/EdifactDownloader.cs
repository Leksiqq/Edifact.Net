using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.XPath;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

public class EdifactDownloader : IDownloader
{
    public event DirectoryNotFoundEventHandler? DirectoryNotFound;
    public event DirectoryDownloadedEventHandler? DirectoryDownloaded;

    private static readonly List<string> s_directories = [];
    private static readonly Regex s_reRepr = new("(a?n?)((?:\\.\\.)?)(\\d+)");
    private static readonly Regex s_reMessageName = new("^(?<name>[A-Z]{6})_D$");
    private static readonly Regex s_reXmlNs = new($"\\s(?<attr>targetNamespace|xmlns)\\s*=\\s*\"{Properties.Resources.edifact_ns}\"");
    private static readonly Regex s_reOccursNote = new("The\\s+component\\s+(?<code>\\d{4})\\s+-\\s+[^-]+\\s+-\\s+occurs\\s+(?<maxOccurs>\\d+)\\s+times\\s+in\\s+the\\s+composite");
    private static readonly Regex s_reDirectoriesInterval = new("^(?<start>D\\d{2}[A-Z])(?<interval>\\s*-\\s*(?<finish>D\\d{2}[A-Z])?)?$");
    private static readonly DirectoryComparer s_directoryComparer = new();
    private static readonly ResourceManager s_rmFixed;

    private readonly ILogger<EdifactDownloader>? _logger;
    private readonly EdifactDownloaderOptions _options;
    private readonly string _tmpDir;
    private readonly HttpClient _wc = new();
    private readonly XmlResolver? _xmlResolver;
    private readonly XmlNamespaceManager _man;
    private readonly NameTable _nameTable = new();
    private readonly List<string> _generatedFiles = [];
    private readonly List<string> _requestedDirectories = [];

    private string? _directory;
    private string? _directoryFolder;
    private string? _eded;
    private string? _edcd;
    private string? _idcd;
    private string? _edsd;
    private string? _idsd;
    private string? _uncl;
    private string? _unsl;
    private string? _ext;
    internal string Ns => !string.IsNullOrEmpty(_options.Namespace)
        ? _options.Namespace
        : Properties.Resources.edifact_ns;
    static EdifactDownloader()
    {
        s_rmFixed = new ResourceManager($"{typeof(Properties.Resources).Namespace}.{s_rmFixedName}", Assembly.GetExecutingAssembly());
        for (int i = 1997; i <= DateTime.Now.Year; i++)
        {
            for (char c = 'A'; c <= 'B'; c++)
            {
                s_directories.Add(string.Format(s_directoryFormat, i % 100, c).ToUpper());
            }
            if (i == 2001)
            {
                s_directories.Add(string.Format(s_directoryFormat, i % 100, 'C').ToUpper());
            }
        }
    }
    public EdifactDownloader(IServiceProvider services)
    {
        _options = services.GetRequiredService<EdifactDownloaderOptions>();

        _logger = services.GetService<ILogger<EdifactDownloader>>();
        _xmlResolver = new Resolver(services);
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
        _man = new XmlNamespaceManager(_nameTable);
        _man.AddNamespace(s_xsPrefix, Properties.Resources.schema_ns);
        if(_options.ConnectionTimeout is int timeout)
        {
            _wc.Timeout = TimeSpan.FromSeconds(timeout);
        }
        if(_options.Directories is { })
        {
            string[] parts = [.. 
                _options.Directories.Split(
                    ',', 
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                )
                .Select(v => v.ToUpper())
                .OrderBy(v => v, s_directoryComparer)
            ];
            foreach( string part in parts )
            {
                Match m = s_reDirectoriesInterval.Match(part);
                if (
                    !m.Success 
                    || (
                        !string.IsNullOrEmpty(m.Groups[s_finish].Value) 
                        && s_directoryComparer.Compare(m.Groups[s_finish].Value, m.Groups[s_start].Value) < 0
                    )
                )
                {
                    throw new Exception(string.Format(s_rmLabels.GetString(s_invalidDirectoryNameOrInterval)!, part));
                }
                string start = m.Groups[s_start].Value;
                _requestedDirectories.Add(start);
                if (!string.IsNullOrEmpty(m.Groups[s_interval].Value))
                {
                    string finish = !string.IsNullOrEmpty(m.Groups[s_finish].Value) ? m.Groups[s_finish].Value : s_d79;
                    int pos = 0;
                    for(
                        ; 
                        (
                            pos < s_directories.Count 
                            && s_directoryComparer.Compare(s_directories[pos], start) < 0
                        ); 
                        ++pos
                    ) { }
                    for (
                        ;
                        (
                            pos < s_directories.Count
                            && s_directoryComparer.Compare(s_directories[pos], start) >= 0
                            && s_directoryComparer.Compare(s_directories[pos], finish) <= 0
                        );
                        ++pos
                    ) 
                    {
                        if(
                            s_directoryComparer.Compare(s_directories[pos], start) > 0
                            && s_directoryComparer.Compare(s_directories[pos], finish) < 0
                        )
                        {
                            _requestedDirectories.Add(s_directories[pos]);
                        }
                    }
                    if (!string.IsNullOrEmpty(m.Groups[s_finish].Value))
                    {
                        _requestedDirectories.Add(finish);
                    }
                }
            }
        }
        else
        {
            _requestedDirectories.AddRange(s_directories);
        }
    }
    public async Task DownloadAsync(CancellationToken stoppingToken)
    {
        if (_directory is null)
        {
            foreach (string d in _requestedDirectories)
            {
                _directory = d;
                await DownloadAsync(stoppingToken);
            }
            return;
        }
        _logger?.LogInformation(s_logMessage, string.Format(s_rmLabels.GetString(s_receivingDirectory)!, _directory));
        try
        {
            if (s_directories.Contains(_directory))
            {
                InitContext();

                Uri requestUri = GetRequestUri();

                HttpResponseMessage response = await _wc.GetAsync(requestUri, stoppingToken);

                if(
                    response.StatusCode != System.Net.HttpStatusCode.OK
                    || !ExtractAll(response.Content.ReadAsStream(stoppingToken))
                )
                {
                    DirectoryNotFound?.Invoke(this, new DirectoryNotFoundEventArgs
                    {
                        Directory = _directory!,
                        Url = requestUri.OriginalString
                    }); ;
                }
                else
                {
                    LoadFixedFilesFromResources();

                    await BuildSchemasAsync(stoppingToken);
                    DirectoryDownloaded?.Invoke(this, new DirectoryDownloadedEventArgs
                    {
                        Directory = _directory,
                        BaseFolder = _tmpDir,
                        Files = [.. _generatedFiles.Select(f => Path.GetRelativePath(_tmpDir, f))],
                    });
                }
            }
            else
            {
                _logger?.LogWarning(s_logMessage, string.Format(s_rmLabels.GetString(s_directoryNotFound)!, _directory));
            }
        }
        catch (Exception ex)
        {
            if (ex is not InvalidDataException)
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

    private async Task BuildSchemasAsync(CancellationToken stoppingToken)
    {
        if (Directory.Exists(_directoryFolder))
        {
            Directory.Delete(_directoryFolder, true);
        }
        Directory.CreateDirectory(_directoryFolder!);

        string targetFile = Path.Combine(_tmpDir, s_edifactXsd);
        SaveXmlDocument(InitXmlDocument(s_edifact), targetFile);
        _generatedFiles.Add(targetFile);
        targetFile = Path.Combine(_tmpDir, s_batchInterchangeXsd);
        SaveXmlDocument(InitXmlDocument(s_edifact), targetFile);
        _generatedFiles.Add(targetFile);
        targetFile = Path.Combine(_tmpDir, s_interactiveInterchangeXsd);
        SaveXmlDocument(InitXmlDocument(s_edifact), targetFile);
        _generatedFiles.Add(targetFile);

        await MakeElementsAsync(stoppingToken);

        await MakeCompositesAsync(stoppingToken);

        await MakeSegmentsAsync(stoppingToken);

        XmlSchemaSet schemaSet = new(_nameTable) {
            XmlResolver = _xmlResolver
        };
        schemaSet.ValidationEventHandler += SchemaSet_ValidationEventHandler;
        schemaSet.Add(Ns, Path.Combine(_directoryFolder!, s_segmentsXsd));
        schemaSet.Compile();

        await MakeMessagesAsync(schemaSet, stoppingToken);
    }

    private async Task MakeMessagesAsync(XmlSchemaSet schemaSet, CancellationToken stoppingToken)
    {
        if(_options.Message != s_sharp)
        {
            string[] messages = Directory.GetFiles(
                _tmpDir, 
                string.Format(
                    s_messagesPatternFormat,
                    _options.Message ?? s_asterisk, 
                    _ext
                )
            )
                .Select(f => s_reMessageName.Match(Path.GetFileNameWithoutExtension(f)))
                .Where(m => m.Success)
                .Select(m => m.Groups[s_name].Value)
                .ToArray()
            ;
            if (_options.Message is { } && messages.Length == 0)
            {
                throw new Exception(string.Format(s_rmLabels.GetString(s_messageNotFound)!, _directory, _options.Message));
            }
            foreach (string mess in messages)
            {
                await MakeMessageAsync(schemaSet, mess, stoppingToken);
            }
        }
    }

    private async Task MakeMessageAsync(XmlSchemaSet schemaSet, string mess, CancellationToken stoppingToken)
    {
        XmlDocument doc = InitXmlDocument(s_message);
        string targetFile = Path.Combine(_directoryFolder!, string.Format(s_fileNameFormat, mess, s_xsd));
        SaveXmlDocument(
            doc,
            string.Format(
                s_fileNameFormat,
                targetFile,
                s_src
            )
        );
        using TextReader reader = new StreamReader(
                File.OpenRead(
                    Path.Combine(
                        _tmpDir,
                        string.Format(s_messagesPatternFormat, mess, _ext)
                    )
                ),
                Encoding.Latin1
            );
        MessageParser parser = new();
        Stack<Segment> segmentGroupsStack = [];
        Stack<int> positionStack = [];
        Stack<XmlElement> sequenceStack = [];
        Dictionary<string, XmlElement> elementsByPosition = [];
        sequenceStack.Push(
            (XmlElement)doc.CreateNavigator()!
                .SelectSingleNode(s_selectStructureSequenceXPath, _man)!
                .UnderlyingObject!
        );
        try
        {
            await foreach (Segment? segment in parser.ParseAsync(reader, stoppingToken))
            {
                if (elementsByPosition.TryGetValue(segment.Position!, out XmlElement? el))
                {
                    if(segment.MinOccurs != s_m)
                    {
                        el.SetAttribute(s_minOccurs, s_0);
                    }
                    if(segment.MaxOccurs != s_1)
                    {
                        el.SetAttribute(s_maxOccurs, segment.MaxOccurs);
                    }
                }
                else
                {
                    while (
                        segmentGroupsStack.Count > 0
                        && positionStack.Peek() == segmentGroupsStack.Peek().Children!.Length
                    )
                    {
                        segmentGroupsStack.Pop();
                        positionStack.Pop();
                        sequenceStack.Pop();
                    }
                    if (segmentGroupsStack.Count == 0 || segmentGroupsStack.Peek().Children![positionStack.Peek()] == segment.Code)
                    {
                        XmlElement element = doc.CreateElement(s_xsPrefix, s_element, Properties.Resources.schema_ns);
                        elementsByPosition.Add(segment.Position!, element);
                        sequenceStack.Peek().AppendChild(element);
                        element.SetAttribute(s_name, segment.Code);
                        CreateAnnotation(element, segment);
                        if (segmentGroupsStack.Count > 0)
                        {
                            positionStack.Push(positionStack.Pop() + 1);
                        }
                        if (s_reSegmentGroup.IsMatch(segment.Code!))
                        {
                            segmentGroupsStack.Push(segment);
                            positionStack.Push(0);
                            XmlElement ct = doc.CreateElement(s_xsPrefix, s_complexType, Properties.Resources.schema_ns);
                            element.AppendChild(ct);
                            XmlElement seq = doc.CreateElement(s_xsPrefix, s_sequence, Properties.Resources.schema_ns);
                            ct.AppendChild(seq);
                            sequenceStack.Push(seq);
                        }
                        else
                        {
                            element.SetAttribute(s_type, segment.Code);
                        }
                    }
                    else if(segmentGroupsStack.Count > 0)
                    {
                        throw new Exception(
                            string.Format(s_rmLabels.GetString("INVALID_SEQUENCE")!, 
                                mess,
                                segment.Position, 
                                segmentGroupsStack.Peek().Children![positionStack.Peek()], 
                                segment.Code
                            )
                        );
                    }
                }
            }
            if (segmentGroupsStack.Count > 0)
            {
                throw new Exception();
            }
            SaveXmlDocument(doc, targetFile);
            if (new FileInfo(targetFile).Length == new FileInfo(string.Format(s_fileNameFormat, targetFile, s_src)).Length)
            {
                throw new Exception(string.Format(s_rmLabels.GetString(s_noSegmentsFound)!, _directory));
            }
            XmlSchemaSet mesageSchemaSet = new()
            {
                XmlResolver = _xmlResolver
            };
            mesageSchemaSet.ValidationEventHandler += SchemaSet_ValidationEventHandler;
            mesageSchemaSet.Add(schemaSet);
            mesageSchemaSet.Add(Ns, targetFile);
            mesageSchemaSet.Compile();

            _generatedFiles.Add(targetFile);
        }
        catch (Exception ex)
        {
            throw new AggregateException(mess, ex);
        }
    }

    private async Task MakeSegmentsAsync(CancellationToken stoppingToken)
    {
        XmlDocument doc = InitXmlDocument(s_segments);
        SaveXmlDocument(doc, Path.Combine(_directoryFolder!, string.Format(s_fileNameFormat, s_segmentsXsd, s_src)));

        using TextReader edsd = new StreamReader(
                File.OpenRead(
                    Path.Combine(
                        _tmpDir,
                        string.Format(s_fileNameFormat, _edsd, _ext)
                    )
                ),
                Encoding.Latin1
            );
        await MakeSegmentsOfUsageMeanAsync(doc, edsd, 'C', stoppingToken);
        using TextReader idsd = new StreamReader(
                File.OpenRead(
                    Path.Combine(
                        _tmpDir,
                        string.Format(s_fileNameFormat, _idsd, _ext)
                    )
                ),
                Encoding.Latin1
            );
        await MakeSegmentsOfUsageMeanAsync(doc, idsd, 'E', stoppingToken);
        string targetFile = Path.Combine(_directoryFolder!, s_segmentsXsd);
        SaveXmlDocument(doc, targetFile);
        if (new FileInfo(targetFile).Length == new FileInfo(string.Format(s_fileNameFormat, targetFile, s_src)).Length)
        {
            throw new Exception(string.Format(s_rmLabels.GetString(s_noSegmentsFound)!, _directory));
        }
        _generatedFiles.Add(targetFile);
    }

    private async Task MakeSegmentsOfUsageMeanAsync(XmlDocument doc, TextReader reader, char nameFirstChar, CancellationToken stoppingToken)
    {
        Dictionary<string, int[]> occurs = [];
        Dictionary<string, Element> elements = [];
        List<string> codes = [];

        SegmentParser parser = new(nameFirstChar);
        await foreach (Segment segment in parser.ParseAsync(reader, stoppingToken))
        {
            XmlElement complexType = doc.CreateElement(s_xsPrefix, s_complexType, Properties.Resources.schema_ns);
            doc.DocumentElement!.AppendChild(complexType);
            complexType.SetAttribute(s_name, null, segment.Code);
            CreateAnnotation(complexType, segment);

            XmlElement complexContent = doc.CreateElement(s_xsPrefix, s_complexContent, Properties.Resources.schema_ns);
            complexType.AppendChild(complexContent);

            XmlElement extension = doc.CreateElement(s_xsPrefix, s_extension, Properties.Resources.schema_ns);
            complexContent.AppendChild(extension);
            extension.SetAttribute(s_base, s_baseSegment);

            XmlElement sequence = doc.CreateElement(s_xsPrefix, s_sequence, Properties.Resources.schema_ns);
            extension.AppendChild(sequence);

            elements.Clear();
            occurs.Clear();
            codes.Clear();

            foreach (Component component in segment.Components!)
            {
                if (!int.TryParse(component.MaxOccurs!, out int maxOccurs))
                {
                    maxOccurs = 1;
                }
                if (occurs.TryGetValue(component.Code!, out int[]? occur))
                {
                    occur[1] += maxOccurs;
                    if (component.MinOccurs == s_m)
                    {
                        ++occur[0];
                    }
                }
                else
                {
                    codes.Add(component.Code!);
                    occurs.Add(component.Code!, [component.MinOccurs == s_m ? 1 : 0, maxOccurs]);
                    elements.Add(component.Code!, component);
                }
            }
            foreach (string code in codes)
            {
                XmlElement element = doc.CreateElement(s_xsPrefix, s_element, Properties.Resources.schema_ns);
                string name = code.StartsWith(nameFirstChar) ? code : string.Format(s_renameElementFormat, code);
                element.SetAttribute(s_name, null, name);
                element.SetAttribute(s_type, null, name);
                if (occurs[code][0] != 1)
                {
                    element.SetAttribute(s_minOccurs, null, occurs[code][0].ToString());
                }
                if (occurs[code][1] != 1)
                {
                    element.SetAttribute(s_maxOccurs, null, occurs[code][1].ToString());
                }
                CreateAnnotation(element, elements[code]);
                sequence.AppendChild(element);
            }
        }
    }
    private async Task MakeCompositesAsync(CancellationToken stoppingToken)
    {
        XmlDocument doc = InitXmlDocument(s_composites);
        SaveXmlDocument(doc, Path.Combine(_directoryFolder!, string.Format(s_fileNameFormat, s_compositesXsd, s_src)));

        using TextReader edcd = new StreamReader(
                File.OpenRead(
                    Path.Combine(
                        _tmpDir,
                        string.Format(s_fileNameFormat, _edcd, _ext)
                    )
                ),
                Encoding.Latin1
            );
        await MakeCompositesOfUsageMeanAsync(doc, edcd, 'C', stoppingToken);
        using TextReader idcd = new StreamReader(
                File.OpenRead(
                    Path.Combine(
                        _tmpDir,
                        string.Format(s_fileNameFormat, _idcd, _ext)
                    )
                ),
                Encoding.Latin1
            );
        await MakeCompositesOfUsageMeanAsync(doc, idcd, 'E', stoppingToken);
        string targetFile = Path.Combine(_directoryFolder!, s_compositesXsd);
        SaveXmlDocument(doc, targetFile);
        if (new FileInfo(targetFile).Length == new FileInfo(string.Format(s_fileNameFormat, targetFile, s_src)).Length)
        {
            throw new Exception(string.Format(s_rmLabels.GetString(s_noTypesFound)!, _directory));
        }
        _generatedFiles.Add(targetFile);
    }
    private void SchemaSet_ValidationEventHandler(object? sender, ValidationEventArgs e)
    {
        switch (e.Severity)
        {
            case XmlSeverityType.Warning:
                _logger?.LogWarning(s_logMessage, e.Message);
                break;
            case XmlSeverityType.Error:
                _logger?.LogWarning(s_logMessage, e.Message);
                break;
        }
    }
    private async Task MakeCompositesOfUsageMeanAsync(XmlDocument doc, TextReader reader, char nameFirstChar, CancellationToken stoppingToken)
    {
        Dictionary<string, int[]> occurs = [];
        Dictionary<string, Element> elements = [];
        List<string> codes = [];

        CompositeParser parser = new(nameFirstChar);
        await foreach (Composite composite in parser.ParseAsync(reader, stoppingToken))
        {
            XmlElement complexType = doc.CreateElement(s_xsPrefix, s_complexType, Properties.Resources.schema_ns);
            complexType.SetAttribute(s_name, null, composite.Code);
            CreateAnnotation(complexType, composite);
            XmlElement sequence = doc.CreateElement(s_xsPrefix, s_sequence, Properties.Resources.schema_ns);

            elements.Clear();
            occurs.Clear();
            codes.Clear();

            foreach(Element element in composite.Elements)
            {
                if(occurs.TryGetValue(element.Code!, out int[]? occur))
                {
                    ++occur[1];
                    if(element.MinOccurs == s_m)
                    {
                        ++occur[0];
                    }
                }
                else
                {
                    codes.Add(element.Code!);
                    occurs.Add(element.Code!, [element.MinOccurs == s_m ? 1 : 0, 1]);
                    elements.Add(element.Code!, element);
                }
            }
            if (composite.Note is { })
            {
                int pos = 0;
                Match m;
                while((m = s_reOccursNote.Match(composite.Note[pos..])).Success)
                {
                    if (occurs.TryGetValue(m.Groups[s_code].Value, out int[]? occur))
                    {
                        occur[1] = int.Parse(m.Groups[s_maxOccurs].Value);
                    }
                    pos += m.Groups[0].Index + m.Groups[0].Length;
                }
                if(pos == 0)
                {
                    _logger?.LogWarning(s_logMessage, string.Format(s_noteAtComposite, composite.Code, composite.Note));
                }
            }
            foreach (string code in codes)
            {
                XmlElement element = doc.CreateElement(s_xsPrefix, s_element, Properties.Resources.schema_ns);
                element.SetAttribute(s_name, null, string.Format(s_renameElementFormat, code));
                element.SetAttribute(s_type, null, string.Format(s_renameElementFormat, code));
                if (occurs[code][0] != 1)
                {
                    element.SetAttribute(s_minOccurs, null, occurs[code][0].ToString());
                }
                if (occurs[code][1] != 1)
                {
                    element.SetAttribute(s_maxOccurs, null, occurs[code][1].ToString());
                }
                CreateAnnotation(element, elements[code]);
                sequence.AppendChild(element);
            }
            complexType.AppendChild(sequence);
            doc.DocumentElement!.AppendChild(complexType);
        }
    }
    private async Task MakeElementsAsync(CancellationToken stoppingToken)
    {
        using TextReader eded = new StreamReader(
                File.OpenRead(
                    Path.Combine(
                        _tmpDir,
                        string.Format(s_fileNameFormat, _eded, _ext)
                    )
                ),
                Encoding.Latin1
            );
        XmlDocument doc = InitXmlDocument(s_elements);
        SaveXmlDocument(doc, Path.Combine(_directoryFolder!, string.Format(s_fileNameFormat, s_elementsXsd, s_src)));
        DataElementParser parser = new();
        await foreach (DataElement dataElement in parser.ParseAsync(eded, stoppingToken))
        {
            XmlElement complexType = doc.CreateElement(s_xsPrefix, s_complexType, Properties.Resources.schema_ns);
            complexType.SetAttribute(s_name, null, string.Format(s_renameElementFormat, dataElement.Code));
            CreateAnnotation(complexType, dataElement);
            XmlElement simpleContent = doc.CreateElement(s_xsPrefix, s_simpleContent, Properties.Resources.schema_ns);
            if (!string.IsNullOrEmpty(dataElement.Representation))
            {
                XmlElement restriction = doc.CreateElement(s_xsPrefix, s_restriction, Properties.Resources.schema_ns);
                restriction.SetAttribute(s_base, s_d);
                ApplyRepresentation(restriction, dataElement.Representation);
                simpleContent.AppendChild(restriction);
            }
            complexType.AppendChild(simpleContent);
            doc.DocumentElement!.AppendChild(complexType);
        }
        using TextReader uncl = new StreamReader(
                File.OpenRead(
                    Path.Combine(
                        _tmpDir,
                        string.Format(s_fileNameFormat, _uncl, _ext)
                    )
                ),
                Encoding.Latin1
            );
        await MakeEnumerationsAsync(uncl, doc, stoppingToken);
        using TextReader unsl = new StreamReader(
                File.OpenRead(
                    Path.Combine(
                        _tmpDir,
                        string.Format(s_fileNameFormat, _unsl, _ext)
                    )
                ),
                Encoding.Latin1
            );
        await MakeEnumerationsAsync(unsl, doc, stoppingToken);
        string targetFile = Path.Combine(_directoryFolder!, s_elementsXsd);
        SaveXmlDocument(doc, targetFile);
        if (new FileInfo(targetFile).Length == new FileInfo(string.Format(s_fileNameFormat, targetFile, s_src)).Length)
        {
            throw new Exception(string.Format(s_rmLabels.GetString(s_noSimpleTypesFound)!, _directory));
        }
        _generatedFiles.Add(targetFile);
    }

    private async Task MakeEnumerationsAsync(TextReader uncl, XmlDocument doc, CancellationToken stoppingToken)
    {
        EnumerationParser enumerationParser = new();
        await foreach (Enumeration en in enumerationParser.ParseAsync(uncl, stoppingToken))
        {
            //Console.WriteLine(JsonSerializer.Serialize(en));
            XmlElement restriction = (XmlElement)doc.CreateNavigator()!
                .SelectSingleNode(
                    string.Format(s_typeForEnumXPathFormat, en.TypeCode),
                    _man
                )?.UnderlyingObject! ?? throw new Exception(string.Format(s_rmLabels.GetString(s_dataElementNotFound)!, en.TypeCode));
            XmlElement enumeration = doc.CreateElement(s_xsPrefix, s_enumeration, Properties.Resources.schema_ns);
            enumeration.SetAttribute(s_value, en.Code);
            CreateAnnotation(enumeration, en);
            restriction.AppendChild(enumeration);
        }
    }

    private static void CreateAnnotation(XmlElement element, DataElement source)
    {
        if (
            !string.IsNullOrEmpty(source.Name)
            || !string.IsNullOrEmpty(source.Description)
            || !string.IsNullOrEmpty(source.Change)
            || !string.IsNullOrEmpty(source.Note)
            || !string.IsNullOrEmpty(source.Function)
            || !string.IsNullOrEmpty(source.Position)
        )
        {
            XmlElement ann = element.OwnerDocument.CreateElement(s_xsPrefix, s_annotation, Properties.Resources.schema_ns);
            if (!string.IsNullOrEmpty(source.Name))
            {
                XmlElement documentation = element.OwnerDocument.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns);
                documentation.SetAttribute(s_name, Properties.Resources.annotation_ns, s_name);
                documentation.AppendChild(element.OwnerDocument.CreateTextNode(source.Name));
                ann.AppendChild(documentation);
            }
            if (!string.IsNullOrEmpty(source.Description))
            {
                XmlElement documentation = element.OwnerDocument.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns);
                documentation.SetAttribute(s_name, Properties.Resources.annotation_ns, s_description);
                documentation.AppendChild(element.OwnerDocument.CreateTextNode(source.Description));
                ann.AppendChild(documentation);
            }
            if (!string.IsNullOrEmpty(source.Note))
            {
                XmlElement documentation = element.OwnerDocument.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns);
                documentation.SetAttribute(s_name, Properties.Resources.annotation_ns, s_note);
                documentation.AppendChild(element.OwnerDocument.CreateTextNode(source.Note));
                ann.AppendChild(documentation);
            }
            if (!string.IsNullOrEmpty(source.Change))
            {
                XmlElement documentation = element.OwnerDocument.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns);
                documentation.SetAttribute(s_name, Properties.Resources.annotation_ns, s_change);
                documentation.AppendChild(element.OwnerDocument.CreateTextNode(source.Change));
                ann.AppendChild(documentation);
            }
            if (!string.IsNullOrEmpty(source.Function))
            {
                XmlElement documentation = element.OwnerDocument.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns);
                documentation.SetAttribute(s_name, Properties.Resources.annotation_ns, s_function);
                documentation.AppendChild(element.OwnerDocument.CreateTextNode(source.Function));
                ann.AppendChild(documentation);
            }
            if (!string.IsNullOrEmpty(source.Position))
            {
                XmlElement documentation = element.OwnerDocument.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns);
                documentation.SetAttribute(s_name, Properties.Resources.annotation_ns, s_position);
                documentation.AppendChild(element.OwnerDocument.CreateTextNode(source.Position));
                ann.AppendChild(documentation);
            }
            element.AppendChild(ann);
        }
    }
    private static void SaveXmlDocument(XmlDocument doc, string path)
    {
        XmlWriterSettings ws = new()
        {
            Indent = true,
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = true
        };
        XmlWriter wr = XmlWriter.Create(path, ws);
        doc.WriteTo(wr);
        wr.Close();
    }
    private static void ApplyRepresentation(XmlElement restr, string repr)
    {
        int min_occurs = 0;
        int max_occurs = 1;
        bool number = false;
        Match m = s_reRepr.Match(repr);
        if (m.Success)
        {
            if (s_n.Equals(m.Groups[1].Captures[0].Value))
            {
                number = true;
                min_occurs = 1;
            }
            max_occurs = int.Parse(m.Groups[3].Captures[0].Value);
            if (string.IsNullOrEmpty(m.Groups[2].Captures[0].Value))
            {
                min_occurs = max_occurs;
            }
        }
        if (number)
        {
            (
                (XmlElement)restr.AppendChild(
                    restr.OwnerDocument.CreateElement(s_xsPrefix, s_pattern, Properties.Resources.schema_ns)
                )!
            )
            .SetAttribute(
                s_value, 
                string.Format(
                    s_numberTypePatternFormat, 
                    min_occurs == max_occurs 
                        ? string.Empty 
                        : string.Format(s_minOccursPatternFormat, min_occurs),
                    max_occurs
                )
            );
            (
                (XmlElement)restr.AppendChild(
                    restr.OwnerDocument.CreateElement(s_xsPrefix, s_pattern, Properties.Resources.schema_ns)
                )!
            ).SetAttribute(s_value, s_numberTypePattern);
        }
        else
        {
            if (min_occurs == max_occurs)
            {
                (
                    (XmlElement)restr.AppendChild(
                        restr.OwnerDocument.CreateElement(s_xsPrefix, s_length, Properties.Resources.schema_ns)
                    )!
                ).SetAttribute(s_value, min_occurs.ToString());
            }
            else
            {
                if (min_occurs > 0)
                {
                    (
                        (XmlElement)restr.AppendChild(
                            restr.OwnerDocument.CreateElement(s_xsPrefix, s_minLength, Properties.Resources.schema_ns)
                        )!
                    ).SetAttribute(s_value, min_occurs.ToString());
                }
                (
                    (XmlElement)restr.AppendChild(
                        restr.OwnerDocument.CreateElement(s_xsPrefix, s_maxLength, Properties.Resources.schema_ns)
                    )!
                ).SetAttribute(s_value, max_occurs.ToString());
            }
        }
    }

    private string ReplaceNs(string str)
    {
        if (_options.Namespace is { })
        {
            return s_reXmlNs.Replace(str, m => string.Format(s_replaceNsFormat, m.Groups[s_attr].Value, _options.Namespace));
        }
        else
        {
            return str;
        }
    }
    private XmlDocument InitXmlDocument(string fname)
    {
        XmlDocument result = new(_nameTable);
        result.LoadXml(ReplaceNs(Properties.Resources.ResourceManager.GetString(fname)!));
        XPathNavigator nav = result.CreateNavigator()!;
        XPathNodeIterator ni1 = nav.Select(s_commentsXPath);
        if (ni1.MoveNext())
        {
            XPathNavigator nav1 = ni1.Current!.CreateNavigator();
            nav1.SetValue(string.Format(s_unMessageFormat, _directory));
            ni1.Current.InsertBefore(nav1);
            ni1.Current.DeleteSelf();
        }

        result.DocumentElement!.SetAttribute(s_annotationPrefixDeclaration, Properties.Resources.annotation_ns);
        return result;
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
                _logger?.LogInformation(s_logMessage, string.Format(s_rmLabels.GetString(s_loadFixedFile)!, key));
                string target = Path.Combine(_tmpDir, Path.GetFileName(key));
                File.WriteAllBytes(target, bytes);
            }
        }
        string unsl = Path.Combine(_tmpDir, string.Format(s_fileNameFormat, s_unsl, _ext));
        if (!File.Exists(unsl))
        {
            _logger?.LogInformation(s_logMessage, string.Format(s_rmLabels.GetString(s_loadFixedFile)!, unsl));
            File.WriteAllBytes(unsl, (s_rmFixed.GetObject(s_unsl99a)! as byte[])!);
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
        _eded = s_eded;
        _edcd = s_edcd;
        _idcd = s_idcd;
        _edsd = s_edsd;
        _idsd = s_idsd;
        _uncl = s_uncl;
        _unsl = s_unsl;
        _ext = _directory![1..];
        _directoryFolder = Path.Combine(_tmpDir, s_un, _directory!);
        _generatedFiles.Clear();
    }
    private bool ExtractAll(Stream stream)
    {
        string sourceArchve = Path.Combine(_tmpDir, s_sourceArchiveDir);
        if (Directory.Exists(sourceArchve))
        {
            Directory.Delete(sourceArchve, true);
        }
        Directory.CreateDirectory(sourceArchve);
        string srcFile = string.Format(s_fileNameFormat, _directory, s_zip);
        string src = Path.Combine(sourceArchve, srcFile);
        using FileStream fileStream = File.OpenWrite(src);
        stream.CopyTo(fileStream);
        fileStream.Close();
        File.Copy(src, Path.Combine(_tmpDir, srcFile));
        if(File.ReadLines(src).First().Contains(s_doctype))
        {
            return false;
        }

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
                    ZipArchive zip = new(fs);
                    zip.ExtractToDirectory(_tmpDir, true);
                }
                catch (Exception) { }
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
                if (_directory == s_d16a && Path.GetFileName(folder) == s_macosx)
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
        found = false;
        foreach (string file in Directory.GetFiles(_tmpDir))
        {
            found = true;   
            File.SetAttributes(file, FileAttributes.Normal);
        }
        return found;
    }

    private Uri GetRequestUri()
    {
        string dir = _directory!.ToLower();
        Uri requestUri;
        if (
            string.Compare(_directory, s_d20b, StringComparison.OrdinalIgnoreCase) < 0
            || string.Compare(_directory, s_d8, StringComparison.OrdinalIgnoreCase) > 0
        )
        {
            requestUri = new Uri(
                string.Format(
                    s_uriFormat, 
                    s_webSite, 
                    string.Format(s_path1, dir, dir)
                )
            );
        }
        else
        {
            requestUri = new Uri(
                string.Format(
                    s_uriFormat, 
                    s_webSite1, 
                    string.Format(s_path2, dir, dir)
                )
            );
        }
        _logger?.LogInformation(s_logMessage, requestUri);
        return requestUri;
    }

}
