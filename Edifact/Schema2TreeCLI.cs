using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Xml;

namespace Net.Leksi.Edifact;

public class Schema2TreeCLI(IServiceProvider services): BackgroundService
{
    private readonly Schema2Tree _schema2Tree = new();
    private readonly Schema2TreeOptions _optoins = services.GetRequiredService<Schema2TreeOptions>();
    public static async Task RunAsync(string[] args)
    {
        Schema2TreeOptions? options = Create(args);

        if (options is null)
        {
            return;
        }
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddSingleton(options);
        builder.Services.AddHostedService<Schema2TreeCLI>();

        IHost host = builder.Build();
        await host.RunAsync();

    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _schema2Tree.TranslateAsync(
            _optoins.SchemaDocument!, 
            _optoins.Output!, 
            _optoins.PaddingLength is int p ? p : Schema2Tree.s_deafultPadLen
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
                    result.SchemaDocument = XmlReader.Create(arg);
                    prevArg = null;
                }
                catch (Exception)
                {
                    OpenSchemaFileError(arg);
                    return null;
                }
            }
            else if (waiting is Waiting.Output)
            {
                try
                {
                    result.Output = new StreamWriter(File.OpenWrite(arg));
                    prevArg = null;
                }
                catch (Exception)
                {
                    OpenOutputFileError(arg);
                    return null;
                }
            }
            else if (waiting is Waiting.PaddingLength)
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
                else if (waiting is Waiting.Output)
                {
                    if (result.Output is { })
                    {
                        CommonCLI.AlreadyUsed(arg);
                        Usage();
                        return null;
                    }
                    prevArg = arg;
                }
                else if (waiting is Waiting.PaddingLength)
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
        if(GetWaiting(prevArg) is not Waiting.None)
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
        if (result.Output is null)
        {
            OutputMissedError();
            Usage();
            return null;
        }
        return result;
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
                CommonCLI.LabelsResourceManager.GetString("SCHEMA2TREE_USAGE")!,
                Path.GetFileName(Environment.ProcessPath)
            )
        );
    }
    private static void OpenOutputFileError(string arg)
    {
        throw new NotImplementedException();
    }
    private static void OpenSchemaFileError(string arg)
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
            "/o" => Waiting.Output,
            "/p" or "--padding" => Waiting.PaddingLength,
            null => Waiting.None,
            _ => Waiting.Unknown
        };
    }
}
