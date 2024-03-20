namespace Net.Leksi.Edifact;

public class EdifactBuilder
{
    private readonly IServiceProvider _services;
    private EdifactInterchangeOptions _options = null!;
    public EdifactBuilder(IServiceProvider services)
    {
        _services = services;

    }
    public async Task BeginInterchange(EdifactInterchangeOptions options, InterchangeHeader header)
    {
        _options = options;
        await Task.CompletedTask;
    }
    public async Task BeginGroup(GroupHeader header)
    {
        await Task.CompletedTask;
    }
    public async Task SendMessage(MessageHeader header, Stream input)
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
