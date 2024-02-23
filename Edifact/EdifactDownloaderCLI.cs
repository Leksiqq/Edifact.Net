using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;

namespace Net.Leksi.Edifact;

public class EdifactDownloaderCLI : BackgroundService
{
    private const string s_logMessage = "{message}";
    private const string s_edifactDownloaderUsage = "EDIFACT_DOWNLOADER_USAGE";
    private const string s_directoryNotFound = "DIRECTORY_NOT_FOUND";
    private static readonly Regex s_reProxy = new("^(https?\\://)(?:([^\\s:]+)(?::(.+))?@)?(.*)$");

    private readonly IServiceProvider _services;
    private readonly IDownloader _downloader;

    private readonly ILogger<EdifactDownloaderCLI>? _logger;
    public EdifactDownloaderCLI(IServiceProvider services)
    {
        _services = services;
        _logger = services.GetService<ILogger<EdifactDownloaderCLI>>();
        _downloader = _services.GetRequiredService<IDownloader>();
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

        builder.Services.AddSingleton<IDownloader, EdifactDownloader1>();
        builder.Services.AddSingleton<XmlResolver, Resolver>();
        builder.Services.AddSingleton(options);
        builder.Services.AddHostedService<EdifactDownloaderCLI>();

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
        _logger?.LogError(s_logMessage, string.Format(CommonCLI.LabelsResourceManager.GetString(s_directoryNotFound)!, e.Directory));
    }


    private static EdifactDownloaderOptions? Create(string[] args)
    {
        EdifactDownloaderOptions options = new();

        string? prevArg = null;

        foreach (string arg in args)
        {
            Waiting waiting = GetWaiting(prevArg);
            if (waiting is Waiting.Message)
            {
                options.Message = arg;
                prevArg = null;
            }
            else if (waiting is Waiting.Directory)
            {
                options.Directory = arg;
                prevArg = null;
            }
            else if (waiting is Waiting.Namespace)
            {
                options.Namespace = arg;
                prevArg = null;
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
                prevArg = null;
            }
            else if (waiting is Waiting.TargetFolder)
            {
                options.TargetUri = new Uri(arg);
                prevArg = null;
            }
            else if (waiting is Waiting.TmpFolder)
            {
                options.TmpFolder = arg;
                prevArg = null;
            }
            else if (waiting is Waiting.ExternalUnzipCommandLineFormat)
            {
                options.ExternalUnzipCommandLineFormat = arg;
                prevArg = null;
            }
            else if (waiting is not Waiting.None)
            {
                CommonCLI.MissedArgumentError(prevArg!);
                Usage();
                return null;
            }
            else
            {
                waiting = GetWaiting(arg);
                if (waiting is Waiting.Message)
                {
                    if (options.Message is { })
                    {
                        CommonCLI.AlreadyUsed(arg);
                        Usage();
                        return null;
                    }
                    prevArg = arg;
                }
                else if (waiting is Waiting.Directory)
                {
                    if (options.Directory is { })
                    {
                        CommonCLI.AlreadyUsed(arg);
                        Usage();
                        return null;
                    }
                    prevArg = arg;
                }
                else if (waiting is Waiting.Namespace)
                {
                    if (options.Namespace is { })
                    {
                        CommonCLI.AlreadyUsed(arg);
                        Usage();
                        return null;
                    }
                    prevArg = arg;
                }
                else if (waiting is Waiting.TargetFolder)
                {
                    if (options.Namespace is { })
                    {
                        CommonCLI.AlreadyUsed(arg);
                        Usage();
                        return null;
                    }
                    prevArg = arg;
                }
                else if (waiting is Waiting.TmpFolder)
                {
                    if (options.Namespace is { })
                    {
                        CommonCLI.AlreadyUsed(arg);
                        Usage();
                        return null;
                    }
                    prevArg = arg;
                }
                else if (waiting is Waiting.ExternalUnzipCommandLineFormat)
                {
                    if (options.ExternalUnzipCommandLineFormat is { })
                    {
                        CommonCLI.AlreadyUsed(arg);
                        Usage();
                        return null;
                    }
                    prevArg = arg;
                }
                else if (waiting is Waiting.Proxy)
                {
                    if (options.Proxy is { })
                    {
                        CommonCLI.AlreadyUsed(arg);
                        Usage();
                        return null;
                    }
                    prevArg = arg;
                }
                else if(waiting is Waiting.Help)
                {
                    Usage();
                    return null;
                }
                else
                {
                    CommonCLI.UnknownArgumentError(arg);
                    Usage();
                    return null;
                }
            }
        }
        if (GetWaiting(prevArg) is not Waiting.None)
        {
            CommonCLI.MissedArgumentError(prevArg!);
            Usage();
            return null;
        }
        return options;
    }
    private static Waiting GetWaiting(string? arg)
    {
        return arg switch
        {
            "/t" or "--target-folder" => Waiting.TargetFolder,
            "/m" or "--message" => Waiting.Message,
            "/d" or "--directory" => Waiting.Directory,
            "/n" or "--ns" => Waiting.Namespace,
            "--tmp-folder" => Waiting.TmpFolder,
            "--external-unzip" => Waiting.ExternalUnzipCommandLineFormat,
            "/p" or "--proxy" => Waiting.Proxy,
            "/?" or "--help" => Waiting.Help,
            null => Waiting.None,
            _ => Waiting.Unknown
        };
    }
    private static void Usage()
    {
        Console.WriteLine(
            string.Format(
                CommonCLI.LabelsResourceManager.GetString(s_edifactDownloaderUsage)!, 
                Path.GetFileName(Environment.ProcessPath)
            )
        );
    }


}
