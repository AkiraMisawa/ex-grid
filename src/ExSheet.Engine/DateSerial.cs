namespace ExSheet.Engine;

/// <summary>
/// Excel's 1900 date system (ADR-0047): serial 1 is 1 January 1900, and serial 60 is 29 February
/// 1900, a day that never existed. Excel keeps it for compatibility with Lotus 1-2-3; matching
/// Excel's serials is the promise, and every serial from 1 March 1900 onward disagrees with Excel
/// if that day is left out. Serial 0 is written as 0 January 1900, as Excel writes it.
/// </summary>
internal static class DateSerial
{
    /// <summary>The last day Excel shows: 31 December 9999.</summary>
    public const int Maximum = 2_958_465;

    private static readonly int Base = new DateOnly(1899, 12, 31).DayNumber;

    /// <summary>The serial of a calendar day, or <see langword="null"/> outside 1900-01-01 … 9999-12-31. 1900-02-29 is 60.</summary>
    public static int? FromDate(int year, int month, int day)
    {
        if (year == 1900 && month == 2 && day == 29) return 60;
        if (year < 1900 || year > 9999 || month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)) return null;
        var serial = new DateOnly(year, month, day).DayNumber - Base;
        return serial >= 60 ? serial + 1 : serial;
    }

    /// <summary>The calendar day of a whole serial, 0 to <see cref="Maximum"/>.</summary>
    public static (int Year, int Month, int Day) ToDate(int serial)
    {
        if (serial == 0) return (1900, 1, 0);
        if (serial == 60) return (1900, 2, 29);
        var date = DateOnly.FromDayNumber(Base + (serial > 60 ? serial - 1 : serial));
        return (date.Year, date.Month, date.Day);
    }

    /// <summary>The weekday Excel gives a serial, 0 for Sunday: Excel counts 1 January 1900 as a Sunday.</summary>
    public static int DayOfWeek(int serial) => (serial + 6) % 7;
}
