using System.Globalization;

namespace Net.Leksi.Edifact;

public class Formats
{
    internal const string s_ddMMyy = "ddMMyy";
    internal const string s_MMddyy = "MMddyy";
    internal const string s_ddMMyyyy = "ddMMyyyy";
    internal const string s_ddMMyyyyHHmm = "ddMMyyyyHHmm";
    internal const string s_yyMMdd = "yyMMdd";
    internal const string s_yyyyMMdd = "yyyyMMdd";
    internal const string s_MMdd = "MMdd";
    internal const string s_MM = "MM";
    internal const string s_dd = "dd";
    internal const string s_yyMMddHHmm = "yyMMddHHmm";
    internal const string s_yyMMddHHmmss = "yyMMddHHmmss";
    internal const string s_yyyyMMddHHmm = "yyyyMMddHHmm";
    internal const string s_yyyyMMddHHmmss = "yyyyMMddHHmmss";
    internal const string s_yyyyMMddHHmmzzz = "yyyyMMddHHmmzzz";
    internal const string s_yyMMddHHmmz = "yyMMddHHmmz";
    internal const string s_yyMMddHHmmssz = "yyMMddHHmmssz";
    internal const string s_yyyyMMddHHmmz = "yyyyMMddHHmmz";
    internal const string s_yyyyMMddHHmmssz = "yyyyMMddHHmmssz";
    internal const string s_MMddHHmm = "MMddHHmm";
    internal const string s_ddHHmm = "ddHHmm";
    internal const string s_HHmm = "HHmm";
    internal const string s_HHmmss = "HHmmss";
    internal const string s_HHmmssz = "HHmmssz";
    internal const string s_mmss = "mmss";
    internal const string s_zzz = "zzz";
    internal const string s_yy = "yy";
    internal const string s_yyyy = "yyyy";
    internal const string s_yyMM = "yyMM";
    internal const string s_yyyyMM = "yyyyMM";
    static public DateTime ParseDateTime(string data, string format)
    {
        int code = int.Parse(format);
        string fmt = code switch
        {
            2 => s_ddMMyy,
            3 => s_MMddyy,
            4 => s_ddMMyyyy,
            5 => s_ddMMyyyyHHmm,
            101 => s_yyMMdd,
            102 => s_yyyyMMdd,
            106 => s_MMdd,
            109 => s_MM,
            110 => s_dd,
            201 => s_yyMMddHHmm,
            202 => s_yyMMddHHmmss,
            203 => s_yyyyMMddHHmm,
            204 => s_yyyyMMddHHmmss,
            205 => s_yyyyMMddHHmmzzz,
            301 => s_yyMMddHHmmz,
            302 => s_yyMMddHHmmssz,
            303 => s_yyyyMMddHHmmz,
            304 => s_yyyyMMddHHmmssz,
            305 => s_MMddHHmm,
            306 => s_ddHHmm,
            401 => s_HHmm,
            402 => s_HHmmss,
            404 => s_HHmmssz,
            405 => s_mmss,
            406 => s_zzz,
            601 => s_yy,
            602 => s_yyyy,
            609 => s_yyMM,
            610 => s_yyyyMM,
            _ => throw new NotImplementedException()
        };
        if (fmt is { })
        {
            return (DateTime.ParseExact(data, fmt, CultureInfo.CurrentCulture, DateTimeStyles.None));
        }
        return DateTime.MinValue;
    }
}

