using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Net.Leksi.Streams;
using System.Xml;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

public class Schema2TreeCLI(IServiceProvider services) : BackgroundService
{
    private const string s_schema2treeUsage = "SCHEMA2TREE_USAGE";
    private readonly Schema2Tree _schema2Tree = new();
    private readonly Schema2TreeOptions _optoins = services.GetRequiredService<Schema2TreeOptions>();
    public static async Task RunAsync(string[] args, Action<IHostApplicationBuilder>? config = null)
    {
        Schema2TreeOptions? options = Create(args);

        if (options is null)
        {
            return;
        }
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);


        builder.Services.AddSingleton<XmlResolver, Resolver>();
        builder.Services.AddSingleton(options);
        builder.Services.AddHostedService<Schema2TreeCLI>();
        builder.Services.AddKeyedSingleton<IStreamFactory, LocalFileStreamFactory>(s_file);
        config?.Invoke(builder);
        IHost host = builder.Build();
        await host.RunAsync();

    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _schema2Tree.RenderAsync(
            _optoins.SchemaDocument!,
            output: services.GetKeyedService<IStreamFactory>(_optoins.OutputUri!.Scheme)!
                .GetOutputStream(_optoins.OutputUri!),
            xmlResolver: services.GetRequiredService<XmlResolver>(),
            width: _optoins.PaddingLength is int p ? p : Schema2Tree.s_deafultWidth
        );
        await services.GetRequiredService<IHost>().StopAsync(stoppingToken);
    }
    private static Schema2TreeOptions? Create(string[] args)
    {
        Schema2TreeOptions result = new();
        string? prevArg = null;
        foreach (string arg in args)
        {
            Waiting waiting = GetWaiting(prevArg);
            if (waiting is Waiting.SchemaDocument)
            {
                try
                {
                    result.SchemaDocument = new Uri(arg);
                    prevArg = null;
                }
                catch (Exception)
                {
                    SchemaDocumentError(arg);
                    return null;
                }
            }
            else if (waiting is Waiting.OutputUri)
            {
                try
                {
                    result.OutputUri = new Uri(arg);
                    prevArg = null;
                }
                catch (Exception)
                {
                    OpenOutputFileError(arg);
                    return null;
                }
            }
            else if (waiting is Waiting.Width)
            {
                if (int.TryParse(arg, out int paddingLength))
                {
                    result.PaddingLength = paddingLength;
                    prevArg = null;
                }
                else
                {
                    ParsePaddingLengthError(arg);
                    return null;
                }
            }
            else if (waiting is not Waiting.None)
            {
                CommonCLI.MissedArgumentError(prevArg!);
                Usage();
                return null;
            }
            else
            {
                waiting = GetWaiting(arg);
                if (waiting is Waiting.SchemaDocument)
                {
                    if (result.SchemaDocument is { })
                    {
                        CommonCLI.AlreadyUsed(arg);
                        Usage();
                        return null;
                    }
                    prevArg = arg;
                }
                else if (waiting is Waiting.OutputUri)
                {
                    if (result.Output is { })
                    {
                        CommonCLI.AlreadyUsed(arg);
                        Usage();
                        return null;
                    }
                    prevArg = arg;
                }
                else if (waiting is Waiting.Width)
                {
                    if (result.PaddingLength is { })
                    {
                        CommonCLI.AlreadyUsed(arg);
                        Usage();
                        return null;
                    }
                    prevArg = arg;
                }
                else
                {
                    CommonCLI.UnknownArgumentError(arg);
                    Usage();
                    return null;
                }
            }
        }
        if (GetWaiting(prevArg) is not Waiting.None)
        {
            CommonCLI.MissedArgumentError(prevArg!);
            Usage();
            return null;
        }
        if (result.SchemaDocument is null)
        {
            SchemaMissedError();
            Usage();
            return null;
        }
        if (result.OutputUri is null)
        {
            OutputMissedError();
            Usage();
            return null;
        }
        return result;
    }

    private static void SchemaDocumentError(string arg)
    {
        throw new NotImplementedException();
    }

    private static void OutputMissedError()
    {
        throw new NotImplementedException();
    }

    private static void SchemaMissedError()
    {
        throw new NotImplementedException();
    }

    private static void Usage()
    {
        Console.WriteLine(
            string.Format(
                s_rmLabels.GetString(s_schema2treeUsage)!,
                Path.GetFileName(Environment.ProcessPath)
            )
        );
    }
    private static void OpenOutputFileError(string arg)
    {
        throw new NotImplementedException();
    }
    private static void ParsePaddingLengthError(string arg)
    {
        throw new NotImplementedException();
    }
    private static Waiting GetWaiting(string? arg)
    {
        return arg switch
        {
            "/s" or "--schema" => Waiting.SchemaDocument,
            "/o" => Waiting.OutputUri,
            "/w" or "--width" => Waiting.Width,
            null => Waiting.None,
            _ => Waiting.Unknown
        };
    }
}
