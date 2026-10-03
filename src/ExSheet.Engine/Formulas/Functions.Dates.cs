namespace ExSheet.Engine.Formulas;

/// <summary>
/// DATE, TODAY, YEAR, MONTH, DAY, EOMONTH and EDATE over Excel's 1900 serials, 29 February 1900
/// included (ADR-0047, <see cref="DateSerial"/>).
/// </summary>
internal static partial class FunctionLibrary
{
    /// <summary>The serial of 1 March 1900, the first day after the one that never existed.</summary>
    private const int FirstMarch1900 = 61;

    /// <summary>
    /// DATE: each argument truncated to a whole number. A year from 0 to 1899 has 1900 added, and a
    /// year below 0 or from 10000 is <c>#NUM!</c>. Months and days outside their ranges roll over
    /// into the years and months beside them, so DATE(2008,14,2) is 2 February 2009 and
    /// DATE(2008,1,-15) is 16 December 2007; the days run on Excel's calendar, so DATE(1900,2,29)
    /// is serial 60 and DATE(1900,2,30) is 1 March. A result outside serials 0 to 31 December 9999
    /// is <c>#NUM!</c>.
    /// </summary>
    private static Operand Date(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var year, out var failure)) return failure;
        if (!TryNumber(call, 1, out var month, out failure)) return failure;
        if (!TryNumber(call, 2, out var day, out failure)) return failure;
        year = Math.Truncate(year);
        month = Math.Truncate(month);
        day = Math.Truncate(day);
        if (year < 0 || year >= 10000) return Operand.Of(ErrorValue.Num);
        if (year < 1900) year += 1900;

        // The first of the month the year and month name, with the months rolled over.
        var months = year * 12 + (month - 1);
        var firstYear = Math.Floor(months / 12);
        if (firstYear < 1 || firstYear > 9999) return Operand.Of(ErrorValue.Num);
        var firstMonth = (int)(months - firstYear * 12) + 1;
        var first = SerialOfFirst((int)firstYear, firstMonth);

        var serial = first + (day - 1);
        if (serial < 0 || serial > DateSerial.Maximum) return Operand.Of(ErrorValue.Num);
        return Operand.Of(Value.FromNumber(serial));
    }

    /// <summary>
    /// The serial of the first of a month, counted on Excel's calendar from any year from 1: before
    /// 1900 it is negative, and from March 1900 it counts the day that never existed.
    /// </summary>
    private static int SerialOfFirst(int year, int month)
    {
        var days = new DateOnly(year, month, 1).DayNumber - new DateOnly(1899, 12, 31).DayNumber;
        return days >= 60 ? days + 1 : days;
    }

    /// <summary>
    /// TODAY: the Sheet Day's serial (ADR-0121). While the day is not known it is
    /// <c>#GETTING_DATA</c>; the Formula waits as a whole (<c>Sheet.Taint</c>), so this is only
    /// what the call itself answers.
    /// </summary>
    private static Operand Today(FunctionCall call) =>
        call.Evaluator.Cells.Today is { } day && DateSerial.FromDate(day.Year, day.Month, day.Day) is { } serial
            ? Operand.Of(Value.FromNumber(serial))
            : Operand.Of(ErrorValue.GettingData);

    private static Operand Year(FunctionCall call) => DatePart(call, date => date.Year);

    private static Operand Month(FunctionCall call) => DatePart(call, date => date.Month);

    private static Operand Day(FunctionCall call) => DatePart(call, date => date.Day);

    /// <summary>
    /// YEAR, MONTH and DAY: the serial truncated to its day; serial 0 is 0 January 1900, and serial
    /// 60 is 29 February 1900. A serial below 0 or after 31 December 9999 is <c>#NUM!</c>.
    /// </summary>
    private static Operand DatePart(FunctionCall call, Func<(int Year, int Month, int Day), int> part)
    {
        if (!TryNumber(call, 0, out var serial, out var failure)) return failure;
        serial = Math.Floor(serial);
        if (serial < 0 || serial > DateSerial.Maximum) return Operand.Of(ErrorValue.Num);
        return Operand.Of(Value.FromNumber(part(DateSerial.ToDate((int)serial))));
    }

    private static Operand EoMonth(FunctionCall call) => MonthsAway(call, endOfMonth: true);

    private static Operand EDate(FunctionCall call) => MonthsAway(call, endOfMonth: false);

    /// <summary>
    /// EOMONTH and EDATE: the start date and the months are truncated to whole numbers. EDATE keeps
    /// the start's day, or the month's last day where the month is shorter; EOMONTH gives the
    /// month's last day. A start below 0, or a result outside 1900 to 9999, is <c>#NUM!</c>.
    /// </summary>
    /// <remarks>
    /// Refused with <c>#VALUE!</c> until Excel is asked (EOMONTH-008 and 009, EDATE-008 and 009): a boolean typed
    /// as either argument, and a start or a result before 1 March 1900, where Excel's calendar has
    /// the day that never existed and which answer these functions give around it is not
    /// documented.
    /// </remarks>
    private static Operand MonthsAway(FunctionCall call, bool endOfMonth)
    {
        for (var i = 0; i < 2; i++)
        {
            if (!TryScalar(call, i, out var value, out var failure)) return failure;
            if (value is { Kind: ValueKind.Boolean }) return Operand.Of(ErrorValue.Value);
        }
        if (!TryNumber(call, 0, out var start, out var error)) return error;
        if (!TryNumber(call, 1, out var months, out error)) return error;
        start = Math.Truncate(start);
        months = Math.Truncate(months);
        if (start < 0 || start > DateSerial.Maximum) return Operand.Of(ErrorValue.Num);
        if (start < FirstMarch1900) return Operand.Of(ErrorValue.Value);

        var (year, month, day) = DateSerial.ToDate((int)start);
        var total = year * 12.0 + (month - 1) + months;
        var toYear = Math.Floor(total / 12);
        if (toYear < 1900 || toYear > 9999) return Operand.Of(ErrorValue.Num);
        var toMonth = (int)(total - toYear * 12) + 1;
        var length = DateTime.DaysInMonth((int)toYear, toMonth);
        var serial = DateSerial.FromDate((int)toYear, toMonth, endOfMonth ? length : Math.Min(day, length))!.Value;
        if (serial < FirstMarch1900) return Operand.Of(ErrorValue.Value);
        return Operand.Of(Value.FromNumber(serial));
    }
}
