using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Net.Leksi.Streams;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

public class EdifactDownloaderCLI : BackgroundService
{
    private static readonly Regex s_reProxy = new("^(https?\\://)(?:([^\\s:]+)(?::(.+))?@)?(.*)$");

    private readonly IServiceProvider _services;
    private readonly EdifactDownloader _downloader;
    private readonly EdifactDownloaderOptions _options;
    private readonly IStreamFactory? _outputStreamFactory;
    private readonly ILogger<EdifactDownloaderCLI>? _logger;
    private readonly Uri _schemas;

    public EdifactDownloaderCLI(IServiceProvider services)
    {
        _services = services;
        _options = services.GetRequiredService<EdifactDownloaderOptions>();
        _logger = services.GetService<ILogger<EdifactDownloaderCLI>>();
        _downloader = _services.GetRequiredService<EdifactDownloader>();
        _downloader.DirectoryNotFound += Downloader_DirectoryNotFound;
        _downloader.DirectoryDownloaded += Downloader_DirectoryDownloaded;
        _schemas = new Uri(_options.SchemasUri!);
        _outputStreamFactory = _services.GetKeyedService<IStreamFactory>(_schemas.Scheme);
        if (_outputStreamFactory is null)
        {
            throw new IOException(
                string.Format(
                    s_rmLabels.GetString(s_uriSchemeNotSupported)!, _schemas.Scheme)
            );
        }
        Uri nsUri = new(new Uri(string.Format(s_folderUriFormat, _options.SchemasUri!)), ".ns");
        if (_outputStreamFactory.FileExists(nsUri) && _outputStreamFactory.GetInputStream(nsUri) is Stream streamNs)
        {
            string ns = new StreamReader(streamNs).ReadToEnd();
            if(!string.IsNullOrEmpty(_options.Namespace) && _options.Namespace != ns)
            {
                _logger?.LogWarning(s_logMessage, string.Format(s_rmLabels.GetString(s_usingSavedNs)!, ns));
            }
            else
            {
                _logger?.LogInformation(s_logMessage, string.Format(s_rmLabels.GetString(s_usingSavedNs)!, ns));
            }

            _options.Namespace = ns;
            streamNs.Close();
        }
        else
        {
            if(_options.Namespace is null)
            {
                _options.Namespace = Properties.Resources.edifact_ns;
            }
            if (_outputStreamFactory.GetOutputStream(new Uri(new Uri(string.Format(s_folderUriFormat, _options.SchemasUri!)), ".ns")) is Stream streamNs1)
            {
                StreamWriter sw = new(streamNs1);
                sw.Write(_options.Namespace);
                sw.Close();
            }
        }
    }
    public static async Task RunAsync(string[] args, Action<IHostApplicationBuilder>? configHostBuilder = null, Action<IServiceProvider>? configApp = null)
    {
        IConfiguration bootstrapConfig = new ConfigurationBuilder()
            .AddCommandLine(args)
            .AddEnvironmentVariables()
            .Build();
        if (bootstrapConfig[s_defaultThreadCurrentCulture] is string ci)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo(ci);
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo(ci);
        }
        if (args.Contains(s_askKey) || args.Contains(s_helpKey))
        {
            Usage();
            return;
        }
        EdifactDownloaderOptions options = new()
        {
            SchemasUri = bootstrapConfig[s_schemasRoot],
            Directories = bootstrapConfig[s_directories],
            Message = bootstrapConfig[s_message],
            Namespace = bootstrapConfig[s_namespace],
            TmpFolder = bootstrapConfig[s_tempFolder],
        };
        if (string.IsNullOrEmpty(options.SchemasUri))
        {
            Usage();
            return;
        }
        if (bootstrapConfig[s_connectionTimeout] is string cts && int.TryParse(cts, out int ct))
        {
            options.ConnectionTimeout = ct;
        }
        if (bootstrapConfig[s_proxy] is string proxy)
        {
            Match m = s_reProxy.Match(proxy);
            if (m.Success)
            {
                options.Proxy = new WebProxy($"{m.Groups[1].Value}{m.Groups[4].Value.Trim()}");
                Console.WriteLine(options.Proxy.Address);
                if (!string.IsNullOrEmpty(m.Groups[2].Value))
                {
                    options.Proxy.Credentials = new NetworkCredential(
                        m.Groups[2].Value.Trim(),
                        !string.IsNullOrEmpty(m.Groups[3].Value) ? m.Groups[3].Value : null
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
                return;
            }
        }

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton<EdifactDownloader>();
        builder.Services.AddSingleton(options);
        builder.Services.AddHostedService<EdifactDownloaderCLI>();
        builder.Services.AddKeyedSingleton<IStreamFactory, LocalFileStreamFactory>(s_file);
        configHostBuilder?.Invoke(builder);
        if (configApp is { })
        {
            builder.Services.AddKeyedSingleton("applicationConfig", configApp);
        }

        IHost host = builder.Build();
        await host.RunAsync();

    }
    private void Downloader_DirectoryDownloaded(object sender, DirectoryDownloadedEventArgs e)
    {
        if (e.Files is { } && e.Files.Length > 0)
        {
            foreach (string file in e.Files)
            {
                using Stream stream = _outputStreamFactory!.GetOutputStream(
                    new Uri(
                        _schemas,
                        Path.Combine(
                            Path.GetFileName(_schemas.AbsolutePath),
                            file
                        )
                    )
                );
                using Stream fs = File.OpenRead(Path.Combine(e.BaseFolder!, file));
                fs.CopyTo(stream);
            }
        }
    }

    private static void Usage()
    {
        Console.WriteLine(
            string.Format(
                s_rmLabels.GetString(s_edifactDownloaderUsage)!,
                Path.GetFileName(Environment.ProcessPath),
                s_schemasRoot,
                s_message,
                s_directories,
                s_namespace,
                s_tempFolder,
                s_connectionTimeout,
                s_proxy
            )
        );
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _downloader.DownloadAsync(stoppingToken);
        }
        finally
        {
            await _services.GetRequiredService<IHost>().StopAsync(stoppingToken);
        }
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
}
