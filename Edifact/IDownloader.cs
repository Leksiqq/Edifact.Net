namespace Net.Leksi.Edifact;

public interface IDownloader
{
    event DirectoryNotFoundEventHandler? DirectoryNotFound;
    Task DownloadAsync(CancellationToken stoppingToken);
}
