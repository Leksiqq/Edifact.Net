using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Net.Leksi.Streams;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

public class EdifactParserCLI : BackgroundService
{
    private readonly EdifactParserOptions _options;
    private readonly EdifactParser _parser;
    private readonly IServiceProvider _services;

    public EdifactParserCLI(IServiceProvider services)
    {
        _services = services;
        _options = _services.GetRequiredService<EdifactParserOptions>();
        _parser = _services.GetRequiredService<EdifactParser>();
    }
    public static async Task RunAsync(string[] args, Action<IHostApplicationBuilder>? config = null)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        EdifactParserOptions options = new()
        {
            SchemasUri = args[0],
            InputUri = args[1],
        };
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<Resolver>();
        builder.Services.AddSingleton<EdifactParser>();
        builder.Services.AddHostedService<EdifactParserCLI>();
        builder.Services.AddKeyedTransient<IStreamFactory, LocalFileStreamFactory>(s_file);

        config?.Invoke(builder);

        IHost host = builder.Build();
        await host.RunAsync();

    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _parser.Parse(_options);
        }
        finally
        {
            await _services.GetRequiredService<IHost>().StopAsync(stoppingToken);
        }
    }
}
