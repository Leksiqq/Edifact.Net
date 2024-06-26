﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Net.Leksi.Edifact;
using Net.Leksi.Streams;
using System.Web;

internal class EdifactParserAll : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly EdifactParserCLIOptions _options;
    private readonly EdifactParser _parser;
    public EdifactParserAll(IServiceProvider services)
    {
        _services = services;
        _options = _services.GetRequiredService<EdifactParserCLIOptions>();
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
                _options.Input = File.OpenRead(path);
                await _parser.ParseAsync(_options, stoppingToken);
            }
            else if(Directory.Exists(path))
            {
                foreach (var item in Directory.GetFiles(path))
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    _options.InputUri = item;
                    Console.WriteLine(item);
                    _options.Input = File.OpenRead(item);
                    try
                    {
                        await _parser.ParseAsync(_options, stoppingToken);
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
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
        if (e.EventKind is EventKind.Begin)
        {
            Uri uri = new(new Uri($"{_options.OutputUri}/_"), $"{e.Header.MessageReferenceNumber}.xml");
            e.Stream = _services.GetRequiredKeyedService<IStreamFactory>(uri.Scheme).GetOutputStream(uri);
        }
    }
}