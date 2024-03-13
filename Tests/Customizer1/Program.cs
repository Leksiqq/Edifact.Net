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
builder.Services.AddHostedService<MessageSchemaCustomizerCLI>();
builder.Services.AddKeyedTransient<IStreamFactory, LocalFileStreamFactory>("file");

IHost host = builder.Build();
await host.RunAsync();


