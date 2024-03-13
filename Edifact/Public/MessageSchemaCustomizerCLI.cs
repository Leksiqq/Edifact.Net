using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Net.Leksi.Streams;
using System.Text;
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
            OriginalSchemaUri = bootstrapConfig[s_originalSchema] ?? bootstrapConfig[s_o],
            CustomSchemaUri = bootstrapConfig[s_customSchema] ?? bootstrapConfig[s_c],
            Action = ParseAction(bootstrapConfig[s_action] ?? bootstrapConfig[s_a]),
            SegmentGroup = ParseSegmentGroup(bootstrapConfig[s_segmentGroup] ?? bootstrapConfig[s_g]),
            Segment = bootstrapConfig[s_segment] ?? bootstrapConfig[s_s],
            Suffix = bootstrapConfig[s_suffix] ?? bootstrapConfig[s_x],
            Type = bootstrapConfig[s_type1] ?? bootstrapConfig[s_t],
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

    private static string ParseSegmentGroup(string? v)
    {
        throw new NotImplementedException();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            MessageSchemaCustomizer msc = services.GetRequiredService<MessageSchemaCustomizer>();
            await msc.Customize(services.GetRequiredService<MessageSchemaCustomizerOptions>());
        }
        finally
        {
            await services.GetRequiredService<IHost>().StopAsync(stoppingToken);
        }
    }
    private static MessageSchemaCustomizerAction? ParseAction(string? v)
    {
        throw new NotImplementedException();
    }
    private static void Usage()
    {
        throw new NotImplementedException();
    }
}
