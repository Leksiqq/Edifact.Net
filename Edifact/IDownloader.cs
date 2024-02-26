namespace Net.Leksi.Edifact;

public interface IDownloader
{
    event DirectoryDownloadedEventHandler? DirectoryDownloaded;
    event DirectoryNotFoundEventHandler? DirectoryNotFound;
    Task DownloadAsync(CancellationToken stoppingToken);
}
