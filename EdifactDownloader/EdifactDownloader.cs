using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Resources;
using System.Text;
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
    private const string s_typeAlreadyDeclared = "TYPE_ALREADY_DECLARED";
    private const string s_directoryFormat = "D{0:00}{1}";
    private const string s_defaultTargetDirectory = "xsd";
    private const string s_path881 = "/DAM/trade/untdid/d88/88-1.zip";
    private const string s_path911 = "/DAM/trade/untdid/d91/91-1.zip";
    private const string s_path912 = "/DAM/trade/untdid/d91/91-2.zip";
    private const string s_path921 = "/DAM/trade/untdid/d92/92-1.zip";
    private const string s_path932 = "/DAM/trade/untdid/d93/93-2.zip";
    private const string s_path93a = "/DAM/trade/untdid/d93/d93a.zip";
    private const string s_pathS93a = "/DAM/trade/untdid/d93/s93a.zip";
    private const string s_pathDam = "/DAM/trade/untdid/{0}/{1}.zip";
    private const string s_pathNoDam = "/trade/untdid/{0}/{1}.zip";
    private const string s_uriFormat = "{0}{1}";
    private const string s_messagePattern = "^([A-Z]{{6}}){0}\\.{1}$";
    private const string s_oldFileFormat = "{0}.old";
    private readonly EdifactDownloaderOptions _options;
    private readonly Regex _reTypeAlreadyDeclared;
    private readonly HttpClient _wc = new();
    private readonly List<string> _directories = ["1881", "1911", "2912", "1921", "2932", "D93A", "S93A"];
    private readonly XmlDocument _xsd = new();
    private readonly string _targetDirectory;

    private string? _directory;
    private XmlNamespaceManager _man = null;
    private ResourceManager _rmRegexTuning = null;
    private ResourceManager _rmFixed = null;

    int num_elements = 0;
    int num_sys_elements = 0;

    public EdifactDownloader(EdifactDownloaderOptions options)
    {
        _options = options;

        _rmRegexTuning = new ResourceManager(s_rmRegexTuning, Assembly.GetExecutingAssembly());
        _rmFixed = new ResourceManager(s_rmFixed, Assembly.GetExecutingAssembly());
        _reTypeAlreadyDeclared = new Regex(_rmRegexTuning.GetString(s_typeAlreadyDeclared)!);
        for (int i = 1994; i <= DateTime.Now.Year; i++)
        {
            for (char c = 'A'; c <= 'B'; c++)
            {
                _directories.Add(string.Format(s_directoryFormat, i % 100, c).ToUpper());
            }
            if(i == 2001)
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
            _directory = _options.Directory;
        }
        //Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task Download(CancellationToken stoppingToken)
    {
        if(_directory is null)
        {
            foreach(string d in _directories)
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
        string tmpDir = _options.TmpFolder is { } ?  Path.GetFullPath(_options.TmpFolder) : Path.GetTempPath();
        if(_options.TmpFolder is null)
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
                tmpDir += "\\";
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

            string main_zip = tmpDir + "\\" + _directory.ToLower() + ".zip";
            string dir = _directory.ToLower();
            string fname = dir;
            string ext = dir[1..];
            Uri requestUri;
            HttpResponseMessage response;
            if ("1881".Equals(dir))
            {
                requestUri = new Uri(string.Format(s_uriFormat, s_webSite, s_path881));
            }
            else if ("1911".Equals(dir))
            {
                requestUri = new Uri(string.Format(s_uriFormat, s_webSite, s_path911));
            }
            else if ("2912".Equals(dir))
            {
                requestUri = new Uri(string.Format(s_uriFormat, s_webSite, s_path912));
            }
            else if ("1921".Equals(dir))
            {
                requestUri = new Uri(string.Format(s_uriFormat, s_webSite, s_path921));
            }
            else if ("2932".Equals(dir))
            {
                requestUri = new Uri(string.Format(s_uriFormat, s_webSite, s_path932));
            }
            else if ("d93a".Equals(dir))
            {
                requestUri = new Uri(string.Format(s_uriFormat, s_webSite, s_path93a));
            }
            else if ("s93a".Equals(dir))
            {
                requestUri = new Uri(string.Format(s_uriFormat, s_webSite, s_pathS93a));
            }
            else if(
                string.Compare(dir, "d20b", StringComparison.OrdinalIgnoreCase) < 0
                || string.Compare(dir, "d9", StringComparison.OrdinalIgnoreCase) > 0
            )
            {
                requestUri = new Uri(string.Format(s_uriFormat, s_webSite, string.Format(s_pathDam, dir, fname)));
            }
            else
            {
                requestUri = new Uri(string.Format(s_uriFormat, s_webSite1, string.Format(s_pathNoDam, dir, fname)));
            }
            
            response = await _wc.GetAsync(requestUri, stoppingToken);

            ExtractAll(response.Content.ReadAsStream(stoppingToken), tmpDir);

            if ("d96b".Equals(dir))
            {
                new Compiler96B().Run(tmpDir, _directory, null);
            }

            string m_postfix = string.Empty;
            if ("2912".Equals(dir) || "1911".Equals(dir) || "1921".Equals(dir) || "2932".Equals(dir))
            {
            }
            else if ("s93a".Equals(dir))
            {
                m_postfix = "_S";
            }
            else
            {
                m_postfix = "_D";
            }
            Regex reMessage = new(string.Format(s_messagePattern, m_postfix.ToUpper(), ext.ToUpper()));
            ResourceSet? resources = _rmFixed.GetResourceSet(CultureInfo.InvariantCulture, true, true);
            string prefix = Path.Combine("UN", _directory.ToUpper());
            foreach (object? res in resources!)
            {
                if(res is DictionaryEntry de && de.Key is string key && key.StartsWith(prefix))
                {
                    string target = Path.Combine(tmpDir, Path.GetFileName(key));
                    string old = string.Format(s_oldFileFormat, target);
                    File.Move(target, old);
                    File.WriteAllBytes(target, (byte[])de.Value!);
                }
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex);
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

    private void ExtractAll(Stream stream, string tmpDir)
    {
        ZipArchive zip;
        try
        {
            zip = new(stream);
        }
        catch (Exception)
        {
            DirectoryNotFound?.Invoke(this, new DirectoryNotFoundEventArgs { Directory = _directory });
            throw;
        }
        zip.ExtractToDirectory(tmpDir);

        bool found = true;
        while (found)
        {
            found = false;
            foreach (string file in Directory.GetFiles(tmpDir, "*.zip"))
            {
                FileStream? fs = null;
                try
                {
                    fs = new(file, FileMode.Open, FileAccess.Read);
                    zip = new ZipArchive(fs);
                    zip.ExtractToDirectory(tmpDir, true);
                    found = true;
                }
                catch (Exception) { }
                finally
                {
                    fs?.Close();
                    File.Delete(file);
                }
            }
            foreach (string folder in Directory.GetDirectories(tmpDir))
            {
                found = true;
                foreach (string file in Directory.GetFiles(folder))
                {
                    File.Move(file, Path.Combine(Path.GetDirectoryName(file)!, "..", Path.GetFileName(file)));
                }
                foreach (string dir in Directory.GetDirectories(folder))
                {
                    string dest = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(dir)!, "..", Path.GetFileName(dir)));
                    Directory.Move(dir, dest);
                }
                Directory.Delete(folder, true);
            }
        }
        foreach (string file in Directory.GetFiles(tmpDir))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
    }
}
