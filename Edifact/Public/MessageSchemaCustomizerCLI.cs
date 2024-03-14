﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Net.Leksi.Streams;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

public class MessageSchemaCustomizerCLI(IServiceProvider services) : BackgroundService
{
    public static async Task RunAsync(string[] args, Action<IHostApplicationBuilder>? config = null)
    {
        IConfiguration bootstrapConfig = new ConfigurationBuilder()
            .AddCommandLine(args)
            .Build();
        if (args.Contains(s_askKey) || args.Contains(s_helpKey))
        {
            Usage();
            return;
        }
        MessageSchemaCustomizerOptions options = new()
        {
            SchemasUri = bootstrapConfig[s_schemasRoot],
            MessageIdentifier = bootstrapConfig[s_message],
            Action = ParseAction(bootstrapConfig[s_action]),
            SegmentGroup = bootstrapConfig[s_segmentGroup],
            Segment = bootstrapConfig[s_segment],
            Suffix = bootstrapConfig[s_suffix],
            Type = bootstrapConfig[s_type],
        };
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<EdifactParser>();
        builder.Services.AddSingleton<MessageSchemaCustomizer>();
        builder.Services.AddHostedService<MessageSchemaCustomizerCLI>();
        builder.Services.AddKeyedTransient<IStreamFactory, LocalFileStreamFactory>(s_file);

        config?.Invoke(builder);

        IHost host = builder.Build();
        await host.RunAsync();

    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            MessageSchemaCustomizer msc = services.GetRequiredService<MessageSchemaCustomizer>();
            await msc.Customize(
                services.GetRequiredService<MessageSchemaCustomizerOptions>(), 
                stoppingToken
            );
        }
        finally
        {
            await services.GetRequiredService<IHost>().StopAsync(stoppingToken);
        }
    }
    private static MessageSchemaCustomizerAction? ParseAction(string? arg)
    {
        return arg switch 
        {
            "cs" => MessageSchemaCustomizerAction.CopySchema,
            "ct" => MessageSchemaCustomizerAction.ChangeType,
            _ => null,
        };
    }
    private static void Usage()
    {
        throw new NotImplementedException();
    }
}