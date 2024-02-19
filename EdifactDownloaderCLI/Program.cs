using Net.Leksi.Edifact;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;

EdifactDownloaderOptions? options = Create(args);

if(options is null)
{
    return;
}

EdifactDownloader downloader = new(options);
downloader.DirectoryNotFound += Downloader_DirectoryNotFound;

try
{
    await downloader.Download(CancellationToken.None);
}
catch (Exception)
{

}

void Downloader_DirectoryNotFound(object sender, DirectoryNotFoundEventArgs e)
{
    Console.WriteLine(String.Format("Directory {0} not found.", e.Directory));
}


EdifactDownloaderOptions? Create(string[] args)
{
    EdifactDownloaderOptions options = new();

    Waiting waiting = Waiting.None;

    foreach (string arg in args)
    {
        if (waiting is Waiting.Message)
        {
            options.Message = arg;
            waiting = Waiting.None;
        }
        else if (waiting is Waiting.Directory)
        {
            options.Directory = arg;
            waiting = Waiting.None;
        }
        else if (waiting is Waiting.Namespace)
        {
            options.Namespace = arg;
            waiting = Waiting.None;
        }
        else if (waiting is Waiting.Proxy)
        {
            Match m = Regex.Match(arg, "^(https?\\://)(?:([^\\s:]+)(?::(.+))?@)?(.*)$");
            if (m.Success)
            {
                options.Proxy = new WebProxy($"{m.Groups[1].Captures[0].Value}{m.Groups[4].Captures[0].Value.Trim()}");
                if (m.Groups[1].Captures.Count > 0)
                {
                    options.Proxy.Credentials = new NetworkCredential(
                        m.Groups[2].Captures[0].Value.Trim(),
                        m.Groups[3].Captures.Count > 0 ? m.Groups[3].Captures[0].Value : null
                    );
                }
                else
                {
                    options.Proxy.Credentials = CredentialCache.DefaultCredentials;
                }
            }
            else
            {
                usage();
                return null;
            }
            waiting = Waiting.None;
        }
        else if (waiting is Waiting.TargetFolder)
        {
            options.TargetFolder = arg;
            waiting = Waiting.None;
        }
        else if (waiting is Waiting.TmpFolder)
        {
            options.TmpFolder = arg;
            waiting = Waiting.None;
        }
        else if (arg.Equals("/m", StringComparison.OrdinalIgnoreCase) || arg.Equals("--message", StringComparison.OrdinalIgnoreCase))
        {
            if (options.Message is { })
            {
                AlreadyUsed(arg);
                usage();
                return null;
            }
            waiting = Waiting.Message;
        }
        else if (arg.Equals("/d", StringComparison.OrdinalIgnoreCase) || arg.Equals("--directory", StringComparison.OrdinalIgnoreCase))
        {
            if (options.Directory is { })
            {
                AlreadyUsed(arg);
                usage();
                return null;
            }
            waiting = Waiting.Directory;
        }
        else if (arg.Equals("/n", StringComparison.OrdinalIgnoreCase) || arg.Equals("--ns", StringComparison.OrdinalIgnoreCase))
        {
            if (options.Namespace is { })
            {
                AlreadyUsed(arg);
                usage();
                return null;
            }
            waiting = Waiting.Namespace;
        }
        else if (arg.Equals("/t", StringComparison.OrdinalIgnoreCase) || arg.Equals("--target-folder", StringComparison.OrdinalIgnoreCase))
        {
            if (options.Namespace is { })
            {
                AlreadyUsed(arg);
                usage();
                return null;
            }
            waiting = Waiting.TargetFolder;
        }
        else if (arg.Equals("--tmp-folder", StringComparison.OrdinalIgnoreCase))
        {
            if (options.Namespace is { })
            {
                AlreadyUsed(arg);
                usage();
                return null;
            }
            waiting = Waiting.TmpFolder;
        }
        else if (arg.Equals("/p", StringComparison.OrdinalIgnoreCase) || arg.Equals("--proxy", StringComparison.OrdinalIgnoreCase))
        {
            if (options.Proxy is { })
            {
                AlreadyUsed(arg);
                usage();
                return null;
            }
            waiting = Waiting.Proxy;
        }
    }
    return options;
}

void AlreadyUsed(string arg)
{
    Console.WriteLine($"The key {arg} is already used!");
}

void usage()
{
    Console.WriteLine(string.Format(@"usage: {0} ARGS ...
ARGS:
  /m, --message  {{MESSAGE|#}}                             - message type (# for no message)
  /d, --directory  DIRECTORY                               - message directory
  /n, --ns NS                                              - EDIFACT namespace substitution
  /t, --target-folder  PATH                                - target folder where schemas will be placed
  --tmp-folder PATH                                        - temporary folder
  /p, --proxy  http[s]://[USER[:PASSWORD]@]ADDRESS:PORT    - use proxy for download
", Path.GetFileName(Assembly.GetExecutingAssembly().Location)));
}


