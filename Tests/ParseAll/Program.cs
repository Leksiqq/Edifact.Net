using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Net.Leksi.Edifact;
using Net.Leksi.Streams;
using System.Text;

IConfiguration bootstrapConfig = new ConfigurationBuilder()
    .AddCommandLine(args)
    .Build();
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
EdifactParserOptions options = new()
{
    SchemasUri = bootstrapConfig["schemas-root"],
    InputUri = bootstrapConfig["input"],
    OutputUri = bootstrapConfig["output"],
    Encoding = Encoding.GetEncoding(bootstrapConfig["encoding"] ?? "ISO-8859-1"),
};
if ((bootstrapConfig["suffixes"]) is string suffixes)
{
    string[] items = suffixes.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (items.Length > 0)
    {
        options.MessagesSuffixes = [];
        foreach (var it in items)
        {
            string[] parts = it.Split('=', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            options.MessagesSuffixes.TryAdd(parts[0], parts[1]);
        }
    }
}
if (options.SchemasUri is null || options.InputUri is null || options.OutputUri is null)
{
    Usage();
    return;
}

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<EdifactParser>();
builder.Services.AddHostedService<EdifactParserAll>();
builder.Services.AddKeyedTransient<IStreamFactory, LocalFileStreamFactory>("file");

IHost host = builder.Build();
await host.RunAsync();

void Usage()
{
    throw new NotImplementedException();
}

