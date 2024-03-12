﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Net.Leksi.Edifact;
using Net.Leksi.Streams;
using System.Web;

internal class EdifactParserAll : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly EdifactParserOptions _options;
    private readonly EdifactParser _parser;
    public EdifactParserAll(IServiceProvider services)
    {
        _services = services;
        _options = _services.GetRequiredService<EdifactParserOptions>();
        _parser = _services.GetRequiredService<EdifactParser>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _parser.Message += _parser_Message;
            Uri uri = new(_options.InputUri!);
            string path = HttpUtility.UrlDecode(uri.AbsolutePath);
            Console.WriteLine(path);
            if (File.Exists(path))
            {
                Console.WriteLine($"Parsing {path}");
                await _parser.Parse(_options);
            }
            else if(Directory.Exists(path))
            {
                foreach (var item in Directory.GetFiles(path))
                {
                    Console.WriteLine($"Parsing {item}");
                    _options.InputUri = item;
                    await _parser.Parse(_options);
                }
            }
        }
        finally
        {
            await _services.GetRequiredService<IHost>().StopAsync(stoppingToken);
        }
    }
    private void _parser_Message(object sender, MessageEventArgs e)
    {
        if (e.EventKind is MessageEventKind.Start)
        {
            Uri uri = new(new Uri($"{_options.OutputUri}/_"), $"{e.MessageReferenceNumber}.xml");
            e.Stream = _services.GetRequiredKeyedService<IStreamFactory>(uri.Scheme).GetOutputStream(uri);
        }
    }
}