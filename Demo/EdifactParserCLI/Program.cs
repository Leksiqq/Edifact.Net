using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Net.Leksi.Edifact;
using System.Text.Json;

ILogger<EdifactParserCLI>? logger;
JsonSerializerOptions serializerOptions = new() 
{ 
    WriteIndented = true,
};

await EdifactParserCLI.RunAsync(
    args, 
    configApp: services =>
    {
        logger = services.GetService<ILogger<EdifactParserCLI>>();
        services.GetRequiredService<EdifactParser>().Interchange += P_Interchange;
    }
);

void P_Interchange(object? sender, InterchangeEventArgs args)
{
    if(args.EventKind is EventKind.Begin)
    {
        //logger?.LogInformation("interchangeHeader: {interchangeHeader}", JsonSerializer.Serialize(args.Header, serializerOptions));
    }
}
