﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Net.Leksi.Edifact;
using Net.Leksi.Streams;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
EdifactBuilderOptions options = new()
{
    OutputUri = @"W:\C#\Edifact\var\out\1.edi",
    SchemasUri = @"W:\C#\Edifact\var\xsd",
    MessagesSuffixes = new Dictionary<string, string>() { { "IFCSUM", ".2" } },
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

            BatchInterchangeHeader header = new();

            header.SyntaxIdentifier.Identifier = "UNOZ";
            header.SyntaxIdentifier.VersionNumber = "1";
            header.SyntaxIdentifier.CharacterEncodingCoded = "5";
            header.DateAndTimeOfPreparation = new DateTimeOfEvent(DateTime.Now);
            header.Sender = new PartyIdentification { Identification = "BTS", CodeQualifier = "30" };
            header.Recipient = new PartyIdentification { Identification = "003702011539", CodeQualifier = "30" };
            //header.TestIndicator = "1";
            header.ControlReference = "10122823639495";

            string[] messages = Directory.GetFiles(@"W:\C#\Edifact\var\out\manifest.poll", "*.xml");

            await edifactBuilder.BeginInterchangeAsync(services.GetRequiredService<EdifactBuilderOptions>(), header);

            for(int i = 0; i < messages.Length; ++i)
            {
                BatchMessageHeader mh = new()
                {
                    Identifier = new MessageIdentification
                    {
                        Type = "IFCSUM",
                        VersionNumber = "D",
                        ReleaseNumber = "97B",
                        ControllingAgencyCoded = "UN",
                    },
                    MessageReferenceNumber = i.ToString()
                };
                await edifactBuilder.DeliverMessageAsync(mh, File.OpenRead(messages[i]));
            }

            await edifactBuilder.EndInterchangeAsync();
        }
        finally
        {
            await services.GetRequiredService<IHost>().StopAsync(stoppingToken);
        }
    }
}