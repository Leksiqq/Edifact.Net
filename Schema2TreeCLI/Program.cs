using Microsoft.Extensions.DependencyInjection;
using Net.Leksi.Edifact;
using System.Xml;

await Schema2TreeCLI.RunAsync(args, builder =>
{
    builder.Services.AddSingleton<XmlResolver, Resolver>();

});
