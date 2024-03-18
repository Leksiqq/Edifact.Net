using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Net.Leksi.Streams;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

public class EdifactMessageVisualizerCLI(IServiceProvider services) : BackgroundService
{
    private const string s_schema2treeUsage = "SCHEMA2TREE_USAGE";
    private readonly EdifactMessageVisualizerOptions _optoins = services.GetRequiredService<EdifactMessageVisualizerOptions>();
    public static async Task RunAsync(string[] args, Action<IHostApplicationBuilder>? config = null)
    {
        IConfiguration bootstrapConfig = new ConfigurationBuilder()
            .AddCommandLine(args)
            .Build();
        if (args.Contains(s_helpKey))
        {
            Usage();
            return;
        }

        EdifactMessageVisualizerOptions options = new()
        {
            SchemasUri = bootstrapConfig[s_schemasRoot],
            MessageType = bootstrapConfig[s_message]?.ToUpper(),
            MessageDirectory = bootstrapConfig[s_directory]?.ToUpper(),
        };
        if (options.SchemasUri is null || options.MessageType is null || options.MessageDirectory is null || bootstrapConfig[s_output] is null)
        {
            Usage();
            return;
        }
        if (bootstrapConfig[s_controlAgency] is { })
        {
            options.ControllingAgency = bootstrapConfig[s_controlAgency]!.ToUpper();
        }
        if (bootstrapConfig[s_suffix] is { })
        {
            options.MessageSuffix = bootstrapConfig[s_suffix]!.ToUpper();
        }

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);


        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<EdifactMessageVisualizer>();
        builder.Services.AddHostedService<EdifactMessageVisualizerCLI>();
        builder.Services.AddKeyedSingleton<IStreamFactory, LocalFileStreamFactory>(s_file);
        config?.Invoke(builder);
        IHost host = builder.Build();
        Uri uri = new(bootstrapConfig[s_output]!);
        options.Output = host.Services.GetRequiredKeyedService<IStreamFactory>(uri.Scheme)?.GetOutputStream(uri, FileMode.Create);
        if (options.Output is null)
        {
            Usage();
            return;
        }
        await host.RunAsync();

    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await services.GetRequiredService<EdifactMessageVisualizer>().RenderAsync(_optoins);
        }
        finally
        {
            await services.GetRequiredService<IHost>().StopAsync(stoppingToken);
        }
    }
    private static void Usage()
    {
        Console.WriteLine(
            string.Format(
                s_rmLabels.GetString(s_schema2treeUsage)!,
                Path.GetFileName(Environment.ProcessPath),
                s_schemasRoot,
                s_message,
                s_directory,
                s_output,
                s_controlAgency,
                s_suffix,
                s_pageWidth,
                EdifactMessageVisualizer.s_deafultWidth
            )
        );
    }
}
