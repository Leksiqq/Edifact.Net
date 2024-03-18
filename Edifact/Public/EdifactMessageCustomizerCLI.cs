using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Net.Leksi.Streams;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

public class EdifactMessageCustomizerCLI(IServiceProvider services) : BackgroundService
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
        EdifactMessageCustomizerOptions options = new()
        {
            SchemasUri = bootstrapConfig[s_schemasRoot],
            ScriptUri = bootstrapConfig[s_script],
        };
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<EdifactParser>();
        builder.Services.AddSingleton<EdifactMessageCustomizer>();
        builder.Services.AddHostedService<EdifactMessageCustomizerCLI>();
        builder.Services.AddKeyedTransient<IStreamFactory, LocalFileStreamFactory>(s_file);

        config?.Invoke(builder);

        IHost host = builder.Build();
        await host.RunAsync();

    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            services.GetRequiredService<EdifactMessageCustomizer>()
                .Customize(
                    services.GetRequiredService<EdifactMessageCustomizerOptions>(), 
                    stoppingToken
                );
        }
        finally
        {
            await services.GetRequiredService<IHost>().StopAsync(stoppingToken);
        }
    }
    private static void Usage()
    {
        throw new NotImplementedException();
    }
}
