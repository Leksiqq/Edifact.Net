using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Net.Leksi.Streams;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

public class EdifactMessageCustomizerCLI(IServiceProvider services) : BackgroundService
{
    public static async Task RunAsync(string[] args, Action<IHostApplicationBuilder>? configHostBuilder = null, Action<IServiceProvider>? configApp = null)
    {
        IConfiguration bootstrapConfig = new ConfigurationBuilder()
            .AddCommandLine(args)
            .Build();
        if (args.Contains(s_askKey) || args.Contains(s_helpKey))
        {
            Usage();
            return;
        }
        EdifactMessageCustomizerOptions options = new()
        {
            SchemasUri = bootstrapConfig[s_schemasRoot],
            ScriptUri = bootstrapConfig[s_script],
        };
        if (string.IsNullOrEmpty(options.SchemasUri) || string.IsNullOrEmpty(options.ScriptUri))
        {
            Usage();
            return;
        }
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<EdifactParser>();
        builder.Services.AddSingleton<EdifactMessageCustomizer>();
        builder.Services.AddHostedService<EdifactMessageCustomizerCLI>();
        builder.Services.AddKeyedTransient<IStreamFactory, LocalFileStreamFactory>(s_file);

        configHostBuilder?.Invoke(builder);
        if (configApp is { })
        {
            builder.Services.AddKeyedSingleton(s_applicationConfig, configApp);
        }

        IHost host = builder.Build();
        await host.RunAsync();

    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            services.GetRequiredService<EdifactMessageCustomizer>()
                .Customize(
                    services.GetRequiredService<EdifactMessageCustomizerOptions>()
                );
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
                s_rmLabels.GetString(s_messageCustomizerUsage)!,
                Path.GetFileName(Environment.ProcessPath),
                s_schemasRoot,
                s_script
            )
        );
    }
}
