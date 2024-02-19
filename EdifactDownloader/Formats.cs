using System.Collections;
using System.Globalization;

namespace Net.Leksi.Edifact;

public class Formats
{
    static private Hashtable dt_formats = new Hashtable();
    static public DateTime ParseDateTime(string data, string format)
    {
        lock (dt_formats)
        {
            string fmt = null;
            if (dt_formats.ContainsKey(format))
            {
                fmt = dt_formats[format] as string;
            }
            else
            {
                int code = int.Parse(format);
                switch (code)
                {
                    case 2:
                        fmt = "ddMMyy";
                        break;
                    case 3:
                        fmt = "MMddyy";
                        break;
                    case 4:
                        fmt = "ddMMyyyy";
                        break;
                    case 5:
                        fmt = "ddMMyyyyHHmm";
                        break;
                    case 101:
                        fmt = "yyMMdd";
                        break;
                    case 102:
                        fmt = "yyyyMMdd";
                        break;
                    case 106:
                        fmt = "MMdd";
                        break;
                    case 109:
                        fmt = "MM";
                        break;
                    case 110:
                        fmt = "dd";
                        break;
                    case 201:
                        fmt = "yyMMddHHmm";
                        break;
                    case 202:
                        fmt = "yyMMddHHmmss";
                        break;
                    case 203:
                        fmt = "yyyyMMddHHmm";
                        break;
                    case 204:
                        fmt = "yyyyMMddHHmmss";
                        break;
                    case 205:
                        fmt = "yyyyMMddHHmmzzz";
                        break;
                    case 301:
                        fmt = "yyMMddHHmmz";
                        break;
                    case 302:
                        fmt = "yyMMddHHmmssz";
                        break;
                    case 303:
                        fmt = "yyyyMMddHHmmz";
                        break;
                    case 304:
                        fmt = "yyyyMMddHHmmssz";
                        break;
                    case 305:
                        fmt = "MMddHHmm";
                        break;
                    case 306:
                        fmt = "ddHHmm";
                        break;
                    case 401:
                        fmt = "HHmm";
                        break;
                    case 402:
                        fmt = "HHmmss";
                        break;
                    case 404:
                        fmt = "HHmmssz";
                        break;
                    case 405:
                        fmt = "mmss";
                        break;
                    case 406:
                        fmt = "zzz";
                        break;
                    case 601:
                        fmt = "yy";
                        break;
                    case 602:
                        fmt = "yyyy";
                        break;
                    case 609:
                        fmt = "yyMM";
                        break;
                    case 610:
                        fmt = "yyyyMM";
                        break;
                    default:
                        throw new NotImplementedException();
                }
                if (fmt != null)
                {
                    dt_formats.Add(format, fmt);
                }
            }
            if (fmt != null)
            {
                return (DateTime.ParseExact(data, fmt, CultureInfo.CurrentCulture, DateTimeStyles.None));
            }
            return DateTime.MinValue;
        }
    }
}
