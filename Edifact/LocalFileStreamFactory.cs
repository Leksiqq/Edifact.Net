using Net.Leksi.Streams;

namespace Net.Leksi.Edifact;

internal class LocalFileOutputStreamFactory : IStreamFactory
{
    private const string s_file = "file";

    public Stream GetInputStream(Uri uri)
    {
        if(uri.Scheme == s_file)
        {
            return File.OpenRead(uri.AbsolutePath);
        }
        throw new NotSupportedException(uri.Scheme);
    }

    public Stream GetOutputStream(Uri uri)
    {
        if (uri.Scheme == s_file)
        {
            return File.OpenWrite(uri.AbsolutePath);
        }
        throw new NotSupportedException(uri.Scheme);
    }

}
