using System.Xml;

namespace Net.Leksi.Edifact;

public class EdifactInterchange
{
    private readonly IServiceProvider _services;
    private EdifactInterchangeOptions _options = null!;
    public EdifactInterchange(IServiceProvider services)
    {
        _services = services;
    }
    public async Task BeginInterchange(EdifactInterchangeOptions options)
    {
        _options = options;
        await Task.CompletedTask;
    }
    public async Task BeginGroup(XmlDocument groupHeader)
    {
        await Task.CompletedTask;
    }
    public async Task SendMessage(XmlDocument messageHeader, Stream input)
    {
        await Task.CompletedTask;
    }
    public async Task EndGroup()
    {
        await Task.CompletedTask;
    }
    public async Task EndInterchange()
    {
        await Task.CompletedTask;
    }
}
