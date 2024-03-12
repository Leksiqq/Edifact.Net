using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Net.Leksi.Edifact;
using Net.Leksi.Streams;
using System.Text;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
EdifactParserOptions options = new()
{
    SchemasUri = args[0],
    InputUri = args[1],
    OutputUri = args[2],
    Encoding = Encoding.Latin1,
    IsStrict = false,
};
builder.Services.AddSingleton(options);
builder.Services.AddSingleton<Resolver>();
builder.Services.AddSingleton<EdifactParser>();
builder.Services.AddHostedService<EdifactParserAll>();
builder.Services.AddKeyedTransient<IStreamFactory, LocalFileStreamFactory>("file");

IHost host = builder.Build();
await host.RunAsync();
