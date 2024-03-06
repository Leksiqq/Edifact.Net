using System.Text;

namespace Net.Leksi.Edifact;

internal class BOM
{
    internal static readonly BOM Utf8 = new(nameof(Utf8));
    internal static readonly BOM Utf16Be = new(nameof(Utf16Be));
    internal static readonly BOM Utf16Le = new(nameof(Utf16Le));
    internal static readonly BOM Utf32Be = new(nameof(Utf32Be));
    internal static readonly BOM Utf32Le = new(nameof(Utf32Le));
    internal static readonly BOM Utf7 = new(nameof(Utf7));
    internal static readonly BOM Utf1 = new(nameof(Utf1));
    internal static readonly BOM UtfEbcdic = new(nameof(UtfEbcdic));
    internal static readonly BOM Scsu = new(nameof(Scsu));
    internal static readonly BOM Bocu1 = new(nameof(Bocu1));
    internal static readonly BOM Gb18030 = new(nameof(Gb18030));

    internal string Name {  get; private set; }
    private BOM(string name) 
    {
        Name = name;
    }

}
