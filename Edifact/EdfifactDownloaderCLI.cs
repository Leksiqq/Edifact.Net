using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;

namespace Net.Leksi.Edifact;

public class EdfifactDownloaderCLI : BackgroundService
{
    private const string s_logMessage = "{message}";
    private const string s_rmResource1 = "Net.Leksi.Edifact.Properties.errors";
    private const string s_directoryNotFound = "DIRECTORY_NOT_FOUND";

    private static readonly Regex s_reProxy = new("^(https?\\://)(?:([^\\s:]+)(?::(.+))?@)?(.*)$");

    private readonly IServiceProvider _services;
    private readonly string _directoryNotFoundFormat;
    private readonly EdifactDownloader _downloader;

    private readonly ILogger<EdfifactDownloaderCLI>? _logger;

    public EdfifactDownloaderCLI(IServiceProvider services)
    {
        _services = services;
        _logger = services.GetService<ILogger<EdfifactDownloaderCLI>>();
        _directoryNotFoundFormat = new ResourceManager(
            s_rmResource1,
            Assembly.GetExecutingAssembly()
        ).GetString(s_directoryNotFound)!;
        _downloader = new(
            _services.GetRequiredService<EdifactDownloaderOptions>(),
            _services.GetService<ILogger<EdifactDownloader>>()
        );
        _downloader.DirectoryNotFound += Downloader_DirectoryNotFound;
    }

    public static async Task RunAsync(string[] args)
    {
        EdifactDownloaderOptions? options = Create(args);

        if (options is null)
        {
            return;
        }

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddSingleton(options);
        builder.Services.AddHostedService<EdfifactDownloaderCLI>();

        IHost host = builder.Build();
        await host.RunAsync();

    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        try
        {
            await _downloader.DownloadAsync(stoppingToken);
        }
        catch (Exception)
        {

        }
        await _services.GetRequiredService<IHost>().StopAsync(stoppingToken);

    }

    private void Downloader_DirectoryNotFound(object sender, DirectoryNotFoundEventArgs e)
    {
        _logger?.LogError(s_logMessage, string.Format(_directoryNotFoundFormat, e.Directory));
    }


    private static EdifactDownloaderOptions? Create(string[] args)
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
                Match m = s_reProxy.Match(arg);
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
                    Usage();
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
            else if (waiting is Waiting.ExternalUnzipCommandLineFormat)
            {
                options.ExternalUnzipCommandLineFormat = arg;
                waiting = Waiting.None;
            }
            else if (arg.Equals("/m", StringComparison.OrdinalIgnoreCase) || arg.Equals("--message", StringComparison.OrdinalIgnoreCase))
            {
                if (options.Message is { })
                {
                    AlreadyUsed(arg);
                    Usage();
                    return null;
                }
                waiting = Waiting.Message;
            }
            else if (arg.Equals("/d", StringComparison.OrdinalIgnoreCase) || arg.Equals("--directory", StringComparison.OrdinalIgnoreCase))
            {
                if (options.Directory is { })
                {
                    AlreadyUsed(arg);
                    Usage();
                    return null;
                }
                waiting = Waiting.Directory;
            }
            else if (arg.Equals("/n", StringComparison.OrdinalIgnoreCase) || arg.Equals("--ns", StringComparison.OrdinalIgnoreCase))
            {
                if (options.Namespace is { })
                {
                    AlreadyUsed(arg);
                    Usage();
                    return null;
                }
                waiting = Waiting.Namespace;
            }
            else if (arg.Equals("/t", StringComparison.OrdinalIgnoreCase) || arg.Equals("--target-folder", StringComparison.OrdinalIgnoreCase))
            {
                if (options.Namespace is { })
                {
                    AlreadyUsed(arg);
                    Usage();
                    return null;
                }
                waiting = Waiting.TargetFolder;
            }
            else if (arg.Equals("--tmp-folder", StringComparison.OrdinalIgnoreCase))
            {
                if (options.Namespace is { })
                {
                    AlreadyUsed(arg);
                    Usage();
                    return null;
                }
                waiting = Waiting.TmpFolder;
            }
            else if (arg.Equals("--external-unzip", StringComparison.OrdinalIgnoreCase))
            {
                if (options.ExternalUnzipCommandLineFormat is { })
                {
                    AlreadyUsed(arg);
                    Usage();
                    return null;
                }
                waiting = Waiting.ExternalUnzipCommandLineFormat;
            }
            else if (arg.Equals("/p", StringComparison.OrdinalIgnoreCase) || arg.Equals("--proxy", StringComparison.OrdinalIgnoreCase))
            {
                if (options.Proxy is { })
                {
                    AlreadyUsed(arg);
                    Usage();
                    return null;
                }
                waiting = Waiting.Proxy;
            }
        }
        return options;
    }

    private static void AlreadyUsed(string arg)
    {
        Console.WriteLine($"The key {arg} is already used!");
    }

    private static void Usage()
    {
        Console.WriteLine(string.Format(@"usage: {0} ARGS ...
ARGS:
  /m, --message  {{MESSAGE|#}}                             - message type (# for no message)
  /d, --directory  DIRECTORY                               - message directory
  /n, --ns NS                                              - EDIFACT namespace substitution
  /t, --target-folder  PATH                                - target folder where schemas will be placed
  --tmp-folder PATH                                        - temporary folder
  /p, --proxy  http[s]://[USER[:PASSWORD]@]ADDRESS:PORT    - use proxy for download
  --external-unzip FORMAT                                  - command line for external unzip program - 
                                                             C#-style format string, where {{0}} means output directory
                                                             and {{1}} means input file path 
", Path.GetFileName(Assembly.GetExecutingAssembly().Location)));
    }


}
