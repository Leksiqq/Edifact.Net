using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Net.Leksi.Edifact;
using Net.Leksi.Streams;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
MessageSchemaCustomizerOptions options = new()
{
    CustomSchemaUri = args[0],
};
builder.Services.AddSingleton(options);
builder.Services.AddSingleton<EdifactParser>();
builder.Services.AddSingleton<MessageSchemaCustomizer>();
builder.Services.AddHostedService<CustomizerService>();
builder.Services.AddKeyedTransient<IStreamFactory, LocalFileStreamFactory>("file");

IHost host = builder.Build();
await host.RunAsync();


internal class CustomizerService(IServiceProvider services) : BackgroundService
{
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
}