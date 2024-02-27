using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

public class EdifactDownloaderCLI : BackgroundService
{
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
        _downloader.DirectoryDownloaded += _downloader_DirectoryDownloaded;
    }

    private void _downloader_DirectoryDownloaded(object sender, DirectoryDownloadedEventArgs e)
    {
        if(e.Files is { })
        {
            foreach (string file in e.Files)
            {
                //Console.WriteLine(file);
            }
        }
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
        _logger?.LogError(s_logMessage, string.Format(CommonCLI.LabelsResourceManager.GetString(s_directoryNotFound)!, e.Directory, e.Url));
    }


    private static EdifactDownloaderOptions? Create(string[] args)
    {
        EdifactDownloaderOptions options = new();

        string? prevArg = null;
        string? unkownArg = null;

        foreach (string arg in args)
        {
            Waiting waiting = GetWaiting(prevArg);
            if (waiting is Waiting.Message)
            {
                options.Message = arg;
                prevArg = null;
            }
            else if (waiting is Waiting.Directories)
            {
                options.Directories = arg;
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
            else if(waiting is Waiting.ConnectionTimeout)
            {
                if(int.TryParse(arg, out int timeout))
                {
                    options.ConnectionTimeout = timeout;
                }
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
                else if (waiting is Waiting.Directories)
                {
                    if (options.Directories is { })
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
                else if (waiting is Waiting.ConnectionTimeout)
                {
                    if (options.ConnectionTimeout is { })
                    {
                        CommonCLI.AlreadyUsed(arg);
                        Usage();
                        return null;
                    }
                    prevArg = arg;
                }
                else if (waiting is Waiting.Help)
                {
                    Usage();
                    return null;
                }
                else if(waiting is Waiting.None)
                {

                }
                else
                {
                    if (unkownArg is null)
                    {
                        unkownArg = arg;
                    }
                }
            }
        }
        if(unkownArg is { })
        {
            CommonCLI.UnknownArgumentError(unkownArg);
            Usage();
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
            "/d" or "--directories" => Waiting.Directories,
            "/n" or "--ns" => Waiting.Namespace,
            "--tmp-folder" => Waiting.TmpFolder,
            "--connection-timeout" => Waiting.ConnectionTimeout,
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
