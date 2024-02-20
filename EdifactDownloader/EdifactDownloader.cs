using Microsoft.Extensions.Logging;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using System.Xml;

namespace Net.Leksi.Edifact;

public class EdifactDownloader
{
    public event DirectoryNotFoundEventHandler? DirectoryNotFound;

    private const string s_webSite = "https://unece.org";
    private const string s_webSite1 = "https://service.unece.org";
    private const string s_directoryNotExistsFormat = "Directory not exists: {0}";
    private const string s_rmRegexTuning = "Net.Leksi.Edifact.Properties.regex_tuning";
    private const string s_rmFixed = "Net.Leksi.Edifact.Properties.fixed";
    private const string s_rmUnsl = "Net.Leksi.Edifact.Properties.unsl";
    private const string s_rmErrors = "Net.Leksi.Edifact.Properties.errors";
    private const string s_typeAlreadyDeclared = "TYPE_ALREADY_DECLARED";
    private const string s_directoryFormat = "D{0:00}{1}";
    private const string s_defaultTargetDirectory = "xsd";
    private const string s_path911 = "/DAM/trade/untdid/d91/91-1.zip";
    private const string s_path912 = "/DAM/trade/untdid/d91/91-2.zip";
    private const string s_path921 = "/DAM/trade/untdid/d92/92-1.zip";
    private const string s_path932 = "/DAM/trade/untdid/d93/93-2.zip";
    private const string s_path93a = "/DAM/trade/untdid/d93/d93a.zip";
    private const string s_pathS93a = "/DAM/trade/untdid/d93/s93a.zip";
    private const string s_pathDam = "/DAM/trade/untdid/{0}/{1}.zip";
    private const string s_pathNoDam = "/trade/untdid/{0}/{1}.zip";
    private const string s_uriFormat = "{0}{1}";
    private const string s_fileNameFormat = "{0}.{1}";
    private const string s_messagePattern = "^([A-Z]{{6}}){0}\\.{1}$";
    private const string s_fileNotFoundFormat = "FILE_FOR_DIRECTORY_NOT_FOUND";
    private const string s_slash = "\\";
    private const string s_1911 = "1911";
    private const string s_2912 = "2912";
    private const string s_1921 = "1921";
    private const string s_2932 = "2932";
    private const string s_d93a = "D93A";
    private const string s_s93a = "S93A";
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
    private const string s_failedUnzip = "FAILED_UNZIP";
    private const string s_usingExternalUnzip = "USING_EXTERNAL_UNZIP";
    private const string s_cmd = "cmd";
    private const string s_args = "args";
    private const string s_zipPattern = "*.zip";
    private const string s_logMessage = "{message}";
    private const string s_receivingDirectory = "RECEIVING_DIRECTORY";
    private const string s_d16a = "D16A";
    private const string s_macosx = "__MACOSX";
    private readonly EdifactDownloaderOptions _options;
    private readonly Regex _reTypeAlreadyDeclared;
    private readonly Regex _reExternalUnzip = new("^\\s*(?<cmd>(?:\\\"[^\"]+\\\")|(?:[^\\s]+))(?<args>.+)$");
    private readonly HttpClient _wc = new();
    private readonly List<string> _directories = [s_1911, s_2912, s_1921, s_2932, s_d93a, s_s93a];
    private readonly XmlDocument _xsd = new();
    private readonly string _targetDirectory;

    private string? _directory;
    private XmlNamespaceManager _man = null;
    private readonly ResourceManager _rmRegexTuning;
    private readonly ResourceManager _rmFixed;
    private readonly ResourceManager _rmUnsl;
    private readonly ResourceManager _rmErrors;
    private readonly ILogger<EdifactDownloader>? _logger;

    int num_elements = 0;
    int num_sys_elements = 0;

    public EdifactDownloader(EdifactDownloaderOptions options, ILogger<EdifactDownloader>? logger = null)
    {
        _options = options;

        _logger = logger;

        _rmRegexTuning = new ResourceManager(s_rmRegexTuning, Assembly.GetExecutingAssembly());
        _rmFixed = new ResourceManager(s_rmFixed, Assembly.GetExecutingAssembly());
        _rmUnsl = new ResourceManager(s_rmUnsl, Assembly.GetExecutingAssembly());
        _rmErrors = new ResourceManager(s_rmErrors, Assembly.GetExecutingAssembly());
        _reTypeAlreadyDeclared = new Regex(_rmRegexTuning.GetString(s_typeAlreadyDeclared)!);
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
    }

    public async Task Download(CancellationToken stoppingToken)
    {
        if (_directory is null)
        {
            foreach (string d in _directories)
            {
                _directory = d;
                try
                {
                    await Download(stoppingToken);
                }
                catch (Exception)
                {
                }
            }
            return;
        }
        _logger?.LogInformation(s_logMessage, string.Format(_rmErrors.GetString(s_receivingDirectory)!, _directory));
        string tmpDir = _options.TmpFolder is { } ? Path.GetFullPath(_options.TmpFolder) : Path.GetTempPath();
        if (_options.TmpFolder is null)
        {
            string tempDirectory;
            for (
                tempDirectory = Path.Combine(tmpDir, Path.GetRandomFileName());
                Directory.Exists(tempDirectory);
                tempDirectory = Path.Combine(tmpDir, Path.GetRandomFileName())
            ) { }

            tmpDir = tempDirectory;
        }

        try
        {
            if (!tmpDir.EndsWith('\\'))
            {
                tmpDir += s_slash;
            }
            if (!Directory.Exists(tmpDir))
            {
                Directory.CreateDirectory(tmpDir);
            }
            else
            {
                foreach (string f in Directory.GetFiles(tmpDir))
                {
                    File.Delete(f);
                }
                foreach (string d in Directory.GetDirectories(tmpDir))
                {
                    Directory.Delete(d, true);
                }
            }
            if (!Directory.Exists(_targetDirectory))
            {
                Directory.CreateDirectory(_targetDirectory);
            }

            string dir = _directory.ToLower();
            string fname = dir;
            string ext = dir[1..];
            string uncl = s_uncl;
            string unsl = s_unsl;


            if (s_1911.Equals(_directory))
            {
                uncl = s_trcl;
            }
            else if (s_2912.Equals(_directory))
            {
                uncl = s_edcl;
            }
            else if (s_1921.Equals(_directory))
            {
                uncl = s_trcl;
            }
            else if (s_2932.Equals(_directory))
            {
                uncl = s_edcl;
            }

            Uri requestUri = GetRequestUri(dir, fname);

            HttpResponseMessage response = await _wc.GetAsync(requestUri, stoppingToken);

            ExtractAll(response.Content.ReadAsStream(stoppingToken), tmpDir);

            if (s_d96b.Equals(_directory))
            {
                new Compiler96B().Run(tmpDir, _directory, null);
            }

            string m_postfix = string.Empty;
            if (
                s_2912.Equals(_directory) 
                || s_1911.Equals(_directory) 
                || s_1921.Equals(_directory) 
                || s_2932.Equals(_directory)
            )
            {
            }
            else if (s_s93a.Equals(_directory))
            {
                m_postfix = s_postfix_S;
            }
            else
            {
                m_postfix = s_postfix_D;
            }
            Regex reMessage = new(string.Format(s_messagePattern, m_postfix.ToUpper(), ext.ToUpper()));

            ResourceSet? resources = _rmFixed.GetResourceSet(CultureInfo.InvariantCulture, true, true);
            string prefix = Path.Combine(s_un, _directory);
            foreach (object? res in resources!)
            {
                if (
                    res is DictionaryEntry de
                    && de.Key is string key
                    && key.StartsWith(prefix)
                    && de.Value is byte[] bytes
                )
                {
                    string target = Path.Combine(tmpDir, Path.GetFileName(key));
                    File.WriteAllBytes(target, bytes);
                }
            }

            string edcd = s_edcd;
            if (!File.Exists(Path.Combine(tmpDir, string.Format(s_fileNameFormat, edcd, ext))))
            {
                edcd = s_trcd;
                if (!File.Exists(Path.Combine(_targetDirectory, s_un, _directory, string.Format(s_fileNameFormat, edcd, ext))))
                {
                    if (!File.Exists(Path.Combine(tmpDir, string.Format(s_fileNameFormat, edcd, ext))))
                    {
                        _logger?.LogCritical(
                            s_logMessage,
                            string.Format(_rmErrors.GetString(s_fileNotFoundFormat)!, 'C', _directory)
                        );
                        return;
                    }
                }
            }

            string edsd = s_edsd;
            if (!File.Exists(Path.Combine(tmpDir, string.Format(s_fileNameFormat, edsd, ext))))
            {
                edsd = s_trsd;
                if (!File.Exists(Path.Combine(_targetDirectory, s_un, _directory, string.Format(s_fileNameFormat, edsd, ext))))
                {
                    if (!File.Exists(Path.Combine(tmpDir, string.Format(s_fileNameFormat, edsd, ext))))
                    {
                        _logger?.LogCritical(
                            s_logMessage,
                            string.Format(_rmErrors.GetString(s_fileNotFoundFormat)!, 'S', _directory)
                        );
                        return;
                    }
                }
            }

            string eded = s_eded;
            if (!File.Exists(Path.Combine(tmpDir, string.Format(s_fileNameFormat, eded, ext))))
            {
                eded = s_tred;
                if (!File.Exists(Path.Combine(_targetDirectory, s_un, _directory, string.Format(s_fileNameFormat, eded, ext))))
                {
                    if (!File.Exists(Path.Combine(tmpDir, string.Format(s_fileNameFormat, eded, ext))))
                    {
                        _logger?.LogCritical(
                            s_logMessage,
                            string.Format(_rmErrors.GetString(s_fileNotFoundFormat)!, 'E', _directory)
                        );
                        return;
                    }
                }
            }

            string unsl_message = null!;
            string[] uncls = Directory.GetFiles(tmpDir, string.Format(s_filePatternFormat, uncl, ext));
            List<string> unl = new(Directory.GetFiles(tmpDir, string.Format(s_filePatternFormat, unsl, ext)));
            if (unl.Count == 0)
            {
                int di = 0;
                for (; di < _directories.Count; di++)
                {
                    if (_directory.Equals(_directories[di]))
                    {
                        break;
                    }
                }
                if (di < _directories.Count)
                {
                    for (; di >= 0; di--)
                    {
                        resources = _rmUnsl.GetResourceSet(CultureInfo.InvariantCulture, true, true);
                        prefix = Path.Combine(s_un, _directories[di].ToUpper());
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
                                        tmpDir,
                                        string.Format(s_unslFileNameFormat, s_unsl_, n, ext)
                                    ),
                                    bytes
                                );
                            }
                        }
                        unl.AddRange(
                            Directory.GetFiles(
                                tmpDir,
                                string.Format(s_filePatternFormat, s_unsl, ext)
                            )
                        );
                        if (unl.Count > 0)
                        {
                            unsl_message = string.Format(_rmErrors.GetString(s_unslMessageFormat)!, _directory, _directories[di]);
                            unsl = s_unsl_;
                            break;
                        }


                    }
                }
            }
            string[] unsls = new string[unl.Count];
            unl.CopyTo(unsls);
            string targetDirectory = Path.Combine(_targetDirectory, s_un, _directory);
            string targetFile = Path.Combine(targetDirectory, string.Format(s_fileNameFormat, edsd, ext));

            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            if (File.Exists(targetFile))
            {
                File.Delete(targetFile);
            }
            CopyFile(Path.Combine(tmpDir, string.Format(s_fileNameFormat, edsd, ext)), targetFile);

            targetFile = Path.Combine(targetDirectory, string.Format(s_fileNameFormat, edcd, ext));
            if (File.Exists(targetFile))
            {
                File.Delete(targetFile);
            }
            if (s_2912.Equals(dir))
            {
                Preparser1 pp = new(targetFile);
                pp.Run(File.ReadAllLines(Path.Combine(tmpDir, string.Format(s_fileNameFormat, edcd, ext))));
            }
            else
            {
                CopyFile(Path.Combine(tmpDir, string.Format(s_fileNameFormat, edcd, ext)), targetFile);
            }

            targetFile = Path.Combine(targetDirectory, string.Format(s_fileNameFormat, eded, ext));
            if (File.Exists(targetFile))
            {
                File.Delete(targetFile);
            }
            if (s_2912.Equals(dir))
            {
                Preparser1 pp = new(targetFile);
                pp.Run(File.ReadAllLines(Path.Combine(tmpDir, string.Format(s_fileNameFormat, eded, ext))));
            }
            else
            {
                CopyFile(Path.Combine(tmpDir, string.Format(s_fileNameFormat, eded, ext)), targetFile);
            }
            foreach (string unc in uncls)
            {
                CopyFile(
                    unc,
                    Path.Combine(targetDirectory, Path.GetFileName(unc))
                );
            }
            foreach (string uns in unsls)
            {
                CopyFile(
                    uns,
                    Path.Combine(targetDirectory, Path.GetFileName(uns))
                );
            }

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
                Directory.Delete(tmpDir, true);
            }
        }
    }

    private static Uri GetRequestUri(string dir, string fname)
    {
        Uri requestUri;
        if (s_1911.Equals(dir))
        {
            requestUri = new Uri(string.Format(s_uriFormat, s_webSite, s_path911));
        }
        else if (s_2912.Equals(dir))
        {
            requestUri = new Uri(string.Format(s_uriFormat, s_webSite, s_path912));
        }
        else if (s_1921.Equals(dir))
        {
            requestUri = new Uri(string.Format(s_uriFormat, s_webSite, s_path921));
        }
        else if (s_2932.Equals(dir))
        {
            requestUri = new Uri(string.Format(s_uriFormat, s_webSite, s_path932));
        }
        else if (s_d93a.Equals(dir, StringComparison.OrdinalIgnoreCase))
        {
            requestUri = new Uri(string.Format(s_uriFormat, s_webSite, s_path93a));
        }
        else if (s_s93a.Equals(dir, StringComparison.OrdinalIgnoreCase))
        {
            requestUri = new Uri(string.Format(s_uriFormat, s_webSite, s_pathS93a));
        }
        else if (
            string.Compare(dir, s_d20b, StringComparison.OrdinalIgnoreCase) < 0
            || string.Compare(dir, s_d9, StringComparison.OrdinalIgnoreCase) > 0
        )
        {
            requestUri = new Uri(string.Format(s_uriFormat, s_webSite, string.Format(s_pathDam, dir, fname)));
        }
        else
        {
            requestUri = new Uri(string.Format(s_uriFormat, s_webSite1, string.Format(s_pathNoDam, dir, fname)));
        }

        return requestUri;
    }

    private void ExtractAll(Stream stream, string tmpDir)
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
        string sourceArchve = Path.Combine(tmpDir, s_sourceArchiveDir);
        if (Directory.Exists(sourceArchve))
        {
            Directory.Delete(sourceArchve, true);
        }
        Directory.CreateDirectory(sourceArchve);
        zip.ExtractToDirectory(sourceArchve);
        zip.ExtractToDirectory(tmpDir);

        List<string> list = [];
        List<string> list1 = [];
        bool found = true;
        while (found)
        {
            found = false;
            list.Clear();
            list.AddRange(Directory.GetFiles(tmpDir, s_zipPattern));
            foreach (string file in list)
            {
                found = true;
                FileStream? fs = null;
                try
                {
                    fs = new(file, FileMode.Open, FileAccess.Read);
                    zip = new ZipArchive(fs);
                    zip.ExtractToDirectory(tmpDir, true);
                }
                catch (Exception) 
                {
                    if (_options.ExternalUnzipCommandLineFormat is string cmd)
                    {
                        UseExternalUnzip(tmpDir, file, cmd);
                    }
                    else
                    {
                        _logger?.LogError(s_logMessage, string.Format(_rmErrors.GetString(s_failedUnzip)!, file));
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
            list.AddRange(Directory.GetDirectories(tmpDir).Where(v => v != sourceArchve));
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
                    string dest = Path.Combine(Path.GetDirectoryName(file)!, "..", Path.GetFileName(file));
                    if (!File.Exists(dest))
                    {
                        File.Move(file, dest);
                    }
                }
                list1.Clear();
                list1.AddRange(Directory.GetDirectories(folder));
                foreach (string dir in list1)
                {
                    string dest = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(dir)!, "..", Path.GetFileName(dir)));
                    Directory.Move(dir, dest);
                }
                Directory.Delete(folder, true);
            }
        }
        foreach(string file in Directory.GetFiles(tmpDir))
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
                string.Format(_rmErrors.GetString(s_usingExternalUnzip)!, commandLine)
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
