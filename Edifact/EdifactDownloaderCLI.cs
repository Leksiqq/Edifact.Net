using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Net.Leksi.Streams;
using System.Net;
using System.Text.RegularExpressions;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

public class EdifactDownloaderCLI : BackgroundService
{
    private static readonly Regex s_reProxy = new("^(https?\\://)(?:([^\\s:]+)(?::(.+))?@)?(.*)$");

    private readonly IServiceProvider _services;
    private readonly IDownloader _downloader;
    private readonly EdifactDownloaderOptions _options;
    private readonly IStreamFactory? _outputStreamFactory;

    private readonly ILogger<EdifactDownloaderCLI>? _logger;
    public EdifactDownloaderCLI(IServiceProvider services)
    {
        _services = services;
        _options = services.GetRequiredService<EdifactDownloaderOptions>();
        _logger = services.GetService<ILogger<EdifactDownloaderCLI>>();
        _downloader = _services.GetRequiredService<IDownloader>();
        _downloader.DirectoryNotFound += Downloader_DirectoryNotFound;
        _downloader.DirectoryDownloaded += _downloader_DirectoryDownloaded;
        _outputStreamFactory = _services.GetKeyedService<IStreamFactory>(_options.TargetUri!.Scheme);
        if (_outputStreamFactory is null)
        {
            throw new IOException(
                string.Format(
                    s_rmLabels.GetString(s_uriSchemeNotSupported)!, _options.TargetUri!.Scheme)
            );
        }
    }

    private void _downloader_DirectoryDownloaded(object sender, DirectoryDownloadedEventArgs e)
    {
        if(e.Files is { } && e.Files.Length > 0)
        {
            foreach (string file in e.Files)
            {
                using Stream stream = _outputStreamFactory!.GetOutputStream(
                    new Uri(
                        _options.TargetUri!, 
                        Path.Combine(
                            Path.GetFileName(_options.TargetUri!.AbsolutePath), 
                            file
                        )
                    )
                );
                using Stream fs = File.OpenRead(Path.Combine(e.BaseFolder!, file));
                fs.CopyTo( stream );
            }
        }
    }

    public static async Task RunAsync(string[] args, Action<IHostApplicationBuilder>? config = null)
    {
        EdifactDownloaderOptions? options = Create(args);

        if (options is null)
        {
            return;
        }

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton<IDownloader, EdifactDownloader>();
        builder.Services.AddSingleton(options);
        builder.Services.AddHostedService<EdifactDownloaderCLI>();
        builder.Services.AddKeyedSingleton<IStreamFactory, LocalFileStreamFactory>(s_file);
        config?.Invoke(builder);

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
        _logger?.LogError(
            s_logMessage, 
            string.Format(
                s_rmLabels.GetString(s_directoryNotFound)!,
                e.Directory, e.Url
            )
        );
    }


    private static EdifactDownloaderOptions? Create(string[] args)
    {
        EdifactDownloaderOptions options = new();

        string? prevArg = null;
        string? unknownArg = null;

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
                else
                {
                    unknownArg ??= arg;
                }
            }
        }
        if(unknownArg is { })
        {
            CommonCLI.UnknownArgumentError(unknownArg);
            Usage();
        }
        if (GetWaiting(prevArg) is not Waiting.None)
        {
            CommonCLI.MissedArgumentError(prevArg!);
            Usage();
            return null;
        }
        if(options.TargetUri is null)
        {
            CommonCLI.MissedMandatoryKeyError("--target-folder");
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
                s_rmLabels.GetString(s_edifactDownloaderUsage)!, 
                Path.GetFileName(Environment.ProcessPath)
            )
        );
    }


}
