using System;
using System.Globalization;

namespace Net.Leksi.Edifact;

public class DateTimeOfEvent
{
    public string? Date { get; set; }
    public string? Time { get; set; }
    public string? UtcOffset { get; set; }
    public DateTimeOfEvent() 
    {
        Set(DateTime.Now);
    }
    public DateTimeOfEvent(DateTime dateTime)
    {
        Set(dateTime);
    }
    public void Set(DateTime dateTime)
    {
        Date = dateTime.ToString(Formats.s_yyMMdd);
        Time = dateTime.ToString(Formats.s_HHmm);
        UtcOffset = dateTime.ToString("%z");
    }
    public DateTime ToDateTime()
    {
        return DateTime.ParseExact($"{Date}{Time}{(UtcOffset is null ? "0" : UtcOffset)}", $"{Formats.s_yyMMdd}{Formats.s_HHmm}z", CultureInfo.InvariantCulture, DateTimeStyles.None);
    }
}
