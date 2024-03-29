﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Net.Leksi.Streams;
using System.Text;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

public class EdifactParserCLI : BackgroundService
{
    private readonly EdifactParserOptions _options;
    private readonly EdifactParser _parser;
    private readonly IServiceProvider _services;
    private readonly ILogger<EdifactParserCLI>? _logger;

    public EdifactParserCLI(IServiceProvider services)
    {
        _services = services;
        _options = _services.GetRequiredService<EdifactParserOptions>();
        _parser = _services.GetRequiredService<EdifactParser>();
        _logger = _services.GetService<ILogger<EdifactParserCLI>>();
    }
    public static async Task RunAsync(string[] args, Action<IHostApplicationBuilder>? configHostBuilder = null, Action<IServiceProvider>? configApp = null)
    {
        IConfiguration bootstrapConfig = new ConfigurationBuilder()
            .AddCommandLine(args)
            .Build();
        if(args.Contains(s_askKey) || args.Contains(s_helpKey))
        {
            Usage();
            return;
        }
        EdifactParserOptions options = new()
        {
            SchemasUri = bootstrapConfig[s_schemasRoot],
            InputUri = bootstrapConfig[s_input],
            OutputUri = bootstrapConfig[s_output],
        };
        if(options.SchemasUri is null || options.InputUri is null)
        {
            Usage();
            return;
        }
        if ((bootstrapConfig[s_encoding]) is string encoding)
        {
            options.Encoding = Encoding.GetEncoding(encoding);
        }
        if ((bootstrapConfig[s_suffixes]) is string suffixes)
        {
            string[] items = suffixes.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if(items.Length > 0)
            {
                options.MessagesSuffixes = [];
                foreach(var it in items)
                {
                    string[] parts = it.Split('=', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    options.MessagesSuffixes.TryAdd(parts[0], parts[1]);
                }
            }
        }
        if (bootstrapConfig[s_strict] is string strict)
        {
            options.IsStrict = !strict.Equals("no", StringComparison.OrdinalIgnoreCase);
        }

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<EdifactParser>();
        builder.Services.AddHostedService<EdifactParserCLI>();
        builder.Services.AddKeyedTransient<IStreamFactory, LocalFileStreamFactory>(s_file);
        if(configApp is { })
        {
            builder.Services.AddSingleton(configApp);
        }

        configHostBuilder?.Invoke(builder);

        IHost host = builder.Build();
        await host.RunAsync();

    }

    private static void Usage()
    {
        Console.WriteLine(
            string.Format(
                s_rmLabels.GetString(s_parserCliUsage)!,
                Path.GetFileName(Environment.ProcessPath),
                s_schemasRoot,
                s_input,
                s_output,
                s_suffixes,
                s_encoding,
                s_strict,
                s_bufferSize
            )
        );
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _services.GetService<Action<IServiceProvider>>()?.Invoke(_services);
            _parser.Message += _parser_Message;
            await _parser.Parse(_options, stoppingToken);
        }
        finally
        {
            await _services.GetRequiredService<IHost>().StopAsync(stoppingToken);
        }
    }

    private void _parser_Message(object sender, MessageEventArgs e)
    {
        if(e.EventKind is EventKind.Start){
            if(_options.OutputUri is { })
            {
                Uri uri = new(
                    new Uri(
                        string.Format(
                            s_folderUriFormat,
                            _options.OutputUri
                        )
                    ),
                    string.Format(
                        s_fileNameFormat,
                        e.Header.MessageReferenceNumber,
                        s_xml
                    )
                );
                e.Stream = _services.GetRequiredKeyedService<IStreamFactory>(uri.Scheme)
                    .GetOutputStream(uri);
            }
            else
            {
                _logger?.LogWarning(
                    s_logMessage,
                    string.Format(
                        s_rmLabels.GetString(s_outputNotSetForMessage)!, 
                        e.Header.Identifier.Identifier, 
                        e.Header.Identifier.ControllingAgencyCoded, 
                        e.Header.Identifier.VersionNumber, 
                        e.Header.Identifier.ReleaseNumber
                    )
                );
            }
        }
    }
}
