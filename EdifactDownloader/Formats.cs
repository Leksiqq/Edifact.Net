using System.Globalization;

namespace Net.Leksi.Edifact;

public class Formats
{
    private const string s_ddMMyy = "ddMMyy";
    private const string s_MMddyy = "MMddyy";
    private const string s_ddMMyyyy = "ddMMyyyy";
    private const string s_ddMMyyyyHHmm = "ddMMyyyyHHmm";
    private const string s_yyMMdd = "yyMMdd";
    private const string s_yyyyMMdd = "yyyyMMdd";
    private const string s_MMdd = "MMdd";
    private const string s_MM = "MM";
    private const string s_dd = "dd";
    private const string s_yyMMddHHmm = "yyMMddHHmm";
    private const string s_yyMMddHHmmss = "yyMMddHHmmss";
    private const string s_yyyyMMddHHmm = "yyyyMMddHHmm";
    private const string s_yyyyMMddHHmmss = "yyyyMMddHHmmss";
    private const string s_yyyyMMddHHmmzzz = "yyyyMMddHHmmzzz";
    private const string s_yyMMddHHmmz = "yyMMddHHmmz";
    private const string s_yyMMddHHmmssz = "yyMMddHHmmssz";
    private const string s_yyyyMMddHHmmz = "yyyyMMddHHmmz";
    private const string s_yyyyMMddHHmmssz = "yyyyMMddHHmmssz";
    private const string s_MMddHHmm = "MMddHHmm";
    private const string s_ddHHmm = "ddHHmm";
    private const string s_HHmm = "HHmm";
    private const string s_HHmmss = "HHmmss";
    private const string s_HHmmssz = "HHmmssz";
    private const string s_mmss = "mmss";
    private const string s_zzz = "zzz";
    private const string s_yy = "yy";
    private const string s_yyyy = "yyyy";
    private const string s_yyMM = "yyMM";
    private const string s_yyyyMM = "yyyyMM";
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

