using Net.Leksi.Streams;
using System.Web;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

public class LocalFileStreamFactory : IStreamFactory
{
    public Stream GetInputStream(Uri uri)
    {
        if(uri.Scheme == s_file)
        {
            return File.OpenRead(HttpUtility.UrlDecode(uri.AbsolutePath));
        }
        throw new NotSupportedException(uri.Scheme);
    }

    public Stream GetOutputStream(Uri uri, FileMode mode = FileMode.Create)
    {
        if (uri.Scheme == s_file)
        {
            string path = HttpUtility.UrlDecode(uri.AbsolutePath);
            if(
                (mode is FileMode.CreateNew || mode is FileMode.Create) 
                && !Directory.Exists(Path.GetDirectoryName(path))
            )
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            }
            return new FileStream(path, mode);
        }
        throw new NotSupportedException(uri.Scheme);
    }
}
