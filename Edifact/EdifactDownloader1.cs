﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.XPath;

namespace Net.Leksi.Edifact;

public partial class EdifactDownloader1 : IDownloader
{
    public event DirectoryNotFoundEventHandler? DirectoryNotFound;

    private static readonly List<string> s_directories = [];
    private static readonly Regex s_reExternalUnzip = new("^\\s*(?<cmd>(?:\\\"[^\"]+\\\")|(?:[^\\s]+))(?<args>.+)$");
    private static readonly Regex s_reRepr = new("(a?n?)((?:\\.\\.)?)(\\d+)");
    private static readonly Regex s_reXmlNs = new($"\\s(?<attr>targetNamespace|xmlns)\\s*=\\s*\"{Properties.Resources.edifact_ns}\"");
    private static readonly ResourceManager s_rmLabels;

    private readonly ILogger<EdifactDownloader>? _logger;
    private readonly EdifactDownloaderOptions _options;
    private readonly string _tmpDir;
    private readonly HttpClient _wc = new();
    private readonly XmlResolver? _xmlResolver;
    private readonly XmlNamespaceManager _man;
    private readonly NameTable _nameTable = new();

    private string? _directory;
    private string? _eded;
    private string? _edcd;
    private string? _uncl;
    private string? _ext;

    internal string Ns => !string.IsNullOrEmpty(_options.Namespace)
        ? _options.Namespace
        : Properties.Resources.edifact_ns;
    static EdifactDownloader1()
    {
        for (int i = 1998; i <= DateTime.Now.Year; i++)
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
        s_rmLabels = new ResourceManager(s_rmLabelsName, Assembly.GetExecutingAssembly());
    }
    public EdifactDownloader1(IServiceProvider services)
    {
        _options = services.GetRequiredService<EdifactDownloaderOptions>();

        _logger = services.GetService<ILogger<EdifactDownloader>>();
        _xmlResolver = services.GetService<XmlResolver>();
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
        _directory = _options.Directory?.ToUpper();
        _man = new XmlNamespaceManager(_nameTable);
        _man.AddNamespace(s_xsPrefix, Properties.Resources.schema_ns);
    }
    public async Task DownloadAsync(CancellationToken stoppingToken)
    {
        if (_directory is null)
        {
            foreach (string d in s_directories)
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

                ExtractAll(response.Content.ReadAsStream(stoppingToken));

                await BuildSchemasAsync();
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

    private async Task BuildSchemasAsync()
    {
        SaveXmlDocument(InitXmlDocument(s_edifact), Path.Combine(_tmpDir, s_edifactXsd));

        using TextReader eded = new StreamReader(
                File.OpenRead(
                    Path.Combine(
                        _tmpDir,
                        string.Format(s_fileNameFormat, _eded, _ext)
                    )
                )
            );
        using TextReader uncl = new StreamReader(
                File.OpenRead(
                    Path.Combine(
                        _tmpDir,
                        string.Format(s_fileNameFormat, _uncl, _ext)
                    )
                )
            );
        await MakeSimpleTypesAsync(eded, uncl);

        using TextReader edcd = new StreamReader(
                File.OpenRead(
                    Path.Combine(
                        _tmpDir,
                        string.Format(s_fileNameFormat, _edcd, _ext)
                    )
                )
            );
        await MakeTypesAsync(edcd);
        XmlSchemaSet schemaSet = new() {
            XmlResolver = _xmlResolver
        };
        schemaSet.ValidationEventHandler += SchemaSet_ValidationEventHandler;
        schemaSet.Add(Ns, Path.Combine(_tmpDir, s_simpleTypesXsd));

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
    private async Task MakeTypesAsync(TextReader reader)
    {
        //XmlDocument doc = InitXmlDocument(s_types);
        //CompositeParser parser = new();
        //await foreach (Composite c in parser.ParseAsync(reader))
        //{
        //    Console.WriteLine($"{c.Code}, {c.Name}, {c.Properties.Count}");
        //}
        //SaveXmlDocument(doc, Path.Combine(_tmpDir, s_typesXsd));
    }
    private async Task MakeSimpleTypesAsync(TextReader ededReader, TextReader unclReader)
    {
        XmlDocument doc = InitXmlDocument(s_simpleTypes);
        DataElementParser parser = new();
        await foreach (DataElement st in parser.ParseAsync(ededReader))
        {
            XmlElement ct = doc.CreateElement(s_xsPrefix, s_complexType, Properties.Resources.schema_ns);
            ct.SetAttribute(s_name, null, string.Format(s_renameElementFormat, st.Code));
            CreateAnnotation(ct, st);
            XmlElement simpleContent = doc.CreateElement(s_xsPrefix, s_simpleContent, Properties.Resources.schema_ns);
            if (!string.IsNullOrEmpty(st.Representation))
            {
                XmlElement restriction = doc.CreateElement(s_xsPrefix, s_restriction, Properties.Resources.schema_ns);
                restriction.SetAttribute("base", "E");
                ApplyRepresentation(restriction, st.Representation);
                simpleContent.AppendChild(restriction);
            }
            ct.AppendChild(simpleContent);
            doc.DocumentElement!.AppendChild(ct);
        }
        EnumerationParser enumerationParser = new();
        await foreach (Enumeration en in enumerationParser.ParseAsync(unclReader))
        {
            XmlElement restriction = (XmlElement)doc.CreateNavigator()!
                .SelectSingleNode(
                    string.Format("/xs:schema/xs:complexType[@name='E{0}']/xs:simpleContent/xs:restriction", en.TypeCode),
                    _man
                )?.UnderlyingObject! ?? throw new Exception($"Data element '{en.TypeCode}' not found.");
            XmlElement enumeration = doc.CreateElement(s_xsPrefix, "enumeration", Properties.Resources.schema_ns);
            enumeration.SetAttribute("value", en.Code);
            CreateAnnotation(enumeration, en);
            restriction.AppendChild(enumeration);
        }
        SaveXmlDocument(doc, Path.Combine(_tmpDir, s_simpleTypesXsd));

    }
    private static void CreateAnnotation(XmlElement element, DataElement simpleType)
    {
        if (
            !string.IsNullOrEmpty(simpleType.Name)
            || !string.IsNullOrEmpty(simpleType.Description)
            || !string.IsNullOrEmpty(simpleType.Change)
            || !string.IsNullOrEmpty(simpleType.Note)
        )
        {
            XmlElement ann = element.OwnerDocument.CreateElement(s_xsPrefix, s_annotation, Properties.Resources.schema_ns);
            if (!string.IsNullOrEmpty(simpleType.Name))
            {
                XmlElement documentation = element.OwnerDocument.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns);
                documentation.SetAttribute(s_name, Properties.Resources.annotation_ns, s_name);
                documentation.AppendChild(element.OwnerDocument.CreateTextNode(simpleType.Name));
                ann.AppendChild(documentation);
            }
            if (!string.IsNullOrEmpty(simpleType.Description))
            {
                XmlElement documentation = element.OwnerDocument.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns);
                documentation.SetAttribute(s_name, Properties.Resources.annotation_ns, s_description);
                documentation.AppendChild(element.OwnerDocument.CreateTextNode(simpleType.Description));
                ann.AppendChild(documentation);
            }
            if (!string.IsNullOrEmpty(simpleType.Note))
            {
                XmlElement documentation = element.OwnerDocument.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns);
                documentation.SetAttribute(s_name, Properties.Resources.annotation_ns, s_note);
                documentation.AppendChild(element.OwnerDocument.CreateTextNode(simpleType.Note));
                ann.AppendChild(documentation);
            }
            if (!string.IsNullOrEmpty(simpleType.Change))
            {
                XmlElement documentation = element.OwnerDocument.CreateElement(s_xsPrefix, s_documentation, Properties.Resources.schema_ns);
                documentation.SetAttribute(s_name, Properties.Resources.annotation_ns, s_change);
                documentation.AppendChild(element.OwnerDocument.CreateTextNode(simpleType.Change));
                ann.AppendChild(documentation);
            }
            element.AppendChild(ann);
        }
    }
    private void SaveXmlDocument(XmlDocument doc, string path)
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
    private void ApplyRepresentation(XmlElement restr, string repr)
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
            return s_reXmlNs.Replace(str, m => string.Format("{0}=\"{1}\"", m.Groups["attr"].Value, _options.Namespace));
        }
        else
        {
            return str;
        }
    }
    private XmlDocument InitXmlDocument(string fname)
    {
        XmlDocument result = new();
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
        _eded = "EDED";
        _edcd = "EDCD";
        _uncl = "UNCL";
        _ext = _directory![1..];
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
                        _logger?.LogError(s_logMessage, string.Format(s_rmLabels.GetString(s_failedUnzip)!, file));
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
        foreach (string file in Directory.GetFiles(_tmpDir))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
    }

    private void UseExternalUnzip(string tmpDir, string file, string cmd)
    {
        string commandLine = string.Format(cmd, tmpDir, file);
        Match match = s_reExternalUnzip.Match(commandLine);
        if (match.Success)
        {
            _logger?.LogInformation(
                s_logMessage,
                string.Format(s_rmLabels.GetString(s_usingExternalUnzip)!, commandLine)
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
    private Uri GetRequestUri()
    {
        string dir = _directory!.ToLower();
        Uri requestUri;
        if (
            string.Compare(_directory, s_d20b, StringComparison.OrdinalIgnoreCase) < 0
            || string.Compare(_directory, s_d9, StringComparison.OrdinalIgnoreCase) > 0
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
