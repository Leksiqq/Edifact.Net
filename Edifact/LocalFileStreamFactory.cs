using Net.Leksi.Streams;
using System.Web;
using static Net.Leksi.Edifact.Constants;

namespace Net.Leksi.Edifact;

internal class LocalFileStreamFactory : IStreamFactory
{
    public Stream GetInputStream(Uri uri)
    {
        if(uri.Scheme == s_file)
        {
            return File.OpenRead(HttpUtility.UrlDecode(uri.AbsolutePath));
        }
        throw new NotSupportedException(uri.Scheme);
    }

    public Stream GetOutputStream(Uri uri)
    {
        if (uri.Scheme == s_file)
        {
            string path = HttpUtility.UrlDecode(uri.AbsolutePath);
            if(!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            }
            return new FileStream(path, FileMode.Create);
        }
        throw new NotSupportedException(uri.Scheme);
    }

}
