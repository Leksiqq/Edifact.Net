using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Net.Leksi.Edifact;
using Net.Leksi.Streams;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
EdifactBuilderOptions options = new()
{
    OutputUri = @"W:\C#\Edifact\var\out\1.edi",
    SchemasUri = @"W:\C#\Edifact\var\xsd",
};

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<EdifactBuilder>();
builder.Services.AddHostedService<Runner>();
builder.Services.AddKeyedTransient<IStreamFactory, LocalFileStreamFactory>("file");
IHost host = builder.Build();
await host.RunAsync();

class Runner(IServiceProvider services) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            EdifactBuilder edifactBuilder = services.GetRequiredService<EdifactBuilder>();

            BatchInterchangeHeader header = new()
            {

            };

            header.SyntaxIdentifier.Identifier = "UNOZ";
            header.SyntaxIdentifier.CharacterEncodingCoded = "5";

            await edifactBuilder.BeginInterchangeAsync(services.GetRequiredService<EdifactBuilderOptions>(), header);
        }
        finally
        {
            await services.GetRequiredService<IHost>().StopAsync(stoppingToken);
        }
    }
}