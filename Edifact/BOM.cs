using System.Text;

namespace Net.Leksi.Edifact;

internal class BOM
{
    internal static readonly BOM Utf8 = new();
    internal static readonly BOM Utf16Be = new();
    internal static readonly BOM Utf16Le = new();
    internal static readonly BOM Utf32Be = new();
    internal static readonly BOM Utf32Le = new();
    internal static readonly BOM Utf7 = new();
    internal static readonly BOM Utf1 = new();
    internal static readonly BOM UtfEbcdic = new();
    internal static readonly BOM Scsu = new();
    internal static readonly BOM Bocu1 = new();
    internal static readonly BOM Gb18030 = new();
    private BOM() { }

}
