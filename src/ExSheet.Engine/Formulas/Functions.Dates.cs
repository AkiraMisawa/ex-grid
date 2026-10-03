namespace ExSheet.Engine.Formulas;

/// <summary>
/// DATE, TODAY, YEAR, MONTH, DAY, EOMONTH, EDATE, WEEKDAY, DAYS, NETWORKDAYS and WORKDAY over
/// Excel's 1900 serials, 29 February 1900 included (ADR-0047, <see cref="DateSerial"/>).
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

    // ---- WEEKDAY, DAYS, NETWORKDAYS, WORKDAY ----------------------------------------------------

    /// <summary>
    /// WEEKDAY: the day of the week of the serial, on Excel's calendar (serial 1 is a Sunday), as
    /// <c>return_type</c> numbers it — 1 or 17 from Sunday, 2 or 11 from Monday, 3 from Monday at 0,
    /// and 12 to 16 from Tuesday to Saturday. Another type, or a serial outside 0 to 31 December
    /// 9999, is <c>#NUM!</c>.
    /// </summary>
    private static Operand Weekday(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var serial, out var failure)) return failure;
        serial = Math.Floor(serial);
        if (serial < 0 || serial > DateSerial.Maximum) return Operand.Of(ErrorValue.Num);
        var type = 1.0;
        if (call.Has(1) && !TryNumber(call, 1, out type, out failure)) return failure;
        var day = DateSerial.DayOfWeek((int)serial);
        int? number = Math.Truncate(type) switch
        {
            1 or 17 => day + 1,
            2 or 11 => ((day + 6) % 7) + 1,
            3 => (day + 6) % 7,
            12 => ((day + 5) % 7) + 1,
            13 => ((day + 4) % 7) + 1,
            14 => ((day + 3) % 7) + 1,
            15 => ((day + 2) % 7) + 1,
            16 => ((day + 1) % 7) + 1,
            _ => null,
        };
        return number is { } n ? Operand.Of(Value.FromNumber(n)) : Operand.Of(ErrorValue.Num);
    }

    /// <summary>DAYS: <c>end_date - start_date</c>, each truncated to its day; a serial outside 0 to 31 December 9999 is <c>#NUM!</c>.</summary>
    private static Operand Days(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var end, out var failure)) return failure;
        if (!TryNumber(call, 1, out var start, out failure)) return failure;
        end = Math.Floor(end);
        start = Math.Floor(start);
        if (end < 0 || start < 0 || end > DateSerial.Maximum || start > DateSerial.Maximum) return Operand.Of(ErrorValue.Num);
        return Operand.Of(Value.FromNumber(end - start));
    }

    /// <summary>
    /// A date argument of NETWORKDAYS and WORKDAY, truncated to its day, as EOMONTH reads one: a
    /// boolean typed is refused with <c>#VALUE!</c> until Excel is asked; below 0 or past
    /// 31 December 9999 is <c>#NUM!</c>; before 1 March 1900 is refused with <c>#VALUE!</c>.
    /// </summary>
    private static bool TryWorkdayDate(FunctionCall call, int index, out int serial, out Operand failure)
    {
        serial = 0;
        if (!TryScalar(call, index, out var value, out failure)) return false;
        if (value is { Kind: ValueKind.Boolean })
        {
            failure = Operand.Of(ErrorValue.Value);
            return false;
        }
        if (!TryNumber(call, index, out var number, out failure)) return false;
        number = Math.Floor(number);
        if (number < 0 || number > DateSerial.Maximum)
        {
            failure = Operand.Of(ErrorValue.Num);
            return false;
        }
        if (number < FirstMarch1900)
        {
            failure = Operand.Of(ErrorValue.Value);
            return false;
        }
        serial = (int)number;
        return true;
    }

    /// <summary>
    /// The holidays argument: a range's numbers and a typed number, each truncated to its day. Text
    /// or a boolean among them is refused with <c>#VALUE!</c> until Excel is asked; an Error Value
    /// is the result.
    /// </summary>
    private static bool TryHolidays(FunctionCall call, int index, out HashSet<int> holidays, out Operand failure)
    {
        holidays = [];
        failure = default;
        if (!call.Has(index)) return true;
        var operand = call.Operand(index);
        IEnumerable<Value> values;
        if (operand.IsRange)
        {
            values = call.Evaluator.RangeValues(operand);
        }
        else if (operand.Scalar is { } scalar)
        {
            values = [scalar];
        }
        else
        {
            return true;
        }
        foreach (var value in values)
        {
            if (value.IsError)
            {
                failure = Operand.Of(value);
                return false;
            }
            if (value.Kind != ValueKind.Number)
            {
                failure = Operand.Of(ErrorValue.Value);
                return false;
            }
            holidays.Add((int)Math.Floor(value.Number));
        }
        return true;
    }

    private static bool IsWorkday(int serial, HashSet<int> holidays) =>
        DateSerial.DayOfWeek(serial) is not (0 or 6) && !holidays.Contains(serial);

    /// <summary>
    /// NETWORKDAYS: the days from <c>start_date</c> to <c>end_date</c>, both counted, that fall from
    /// Monday to Friday and are not holidays; negative when the start is after the end.
    /// </summary>
    private static Operand NetworkDays(FunctionCall call)
    {
        if (!TryWorkdayDate(call, 0, out var start, out var failure)) return failure;
        if (!TryWorkdayDate(call, 1, out var end, out failure)) return failure;
        if (!TryHolidays(call, 2, out var holidays, out failure)) return failure;
        var (from, to, sign) = start <= end ? (start, end, 1) : (end, start, -1);
        var span = to - from + 1;
        var count = (span / 7) * 5;
        for (var serial = from + ((span / 7) * 7); serial <= to; serial++)
        {
            if (DateSerial.DayOfWeek(serial) is not (0 or 6)) count++;
        }
        count -= holidays.Count(h => h >= from && h <= to && DateSerial.DayOfWeek(h) is not (0 or 6));
        return Operand.Of(Value.FromNumber(sign * count));
    }

    /// <summary>
    /// WORKDAY: the day <c>days</c> working days after <c>start_date</c> (before it when negative),
    /// passing over weekends and holidays; <c>days</c> truncated, and 0 gives the start itself. A
    /// result past 31 December 9999 is <c>#NUM!</c>, and one before 1 March 1900 is refused with
    /// <c>#VALUE!</c>.
    /// </summary>
    private static Operand Workday(FunctionCall call)
    {
        if (!TryWorkdayDate(call, 0, out var start, out var failure)) return failure;
        if (!TryNumber(call, 1, out var days, out failure)) return failure;
        if (!TryHolidays(call, 2, out var holidays, out failure)) return failure;
        days = Math.Truncate(days);
        var step = days < 0 ? -1 : 1;
        var remaining = Math.Abs(days);
        var serial = start;
        while (remaining > 0)
        {
            serial += step;
            if (serial > DateSerial.Maximum) return Operand.Of(ErrorValue.Num);
            if (serial < FirstMarch1900) return Operand.Of(ErrorValue.Value);
            if (IsWorkday(serial, holidays)) remaining--;
        }
        return Operand.Of(Value.FromNumber(serial));
    }

    // ---- TIME, HOUR, MINUTE, SECOND -----------------------------------------------------------------

    private const int SecondsPerDay = 86_400;

    /// <summary>
    /// TIME: the fraction of a day the hour, minute and second make, each truncated; past 24 hours
    /// it wraps, as Excel's does. An argument above 32767, or a time before midnight, is <c>#NUM!</c>.
    /// </summary>
    private static Operand Time(FunctionCall call)
    {
        if (!TryNumber(call, 0, out var hour, out var failure)) return failure;
        if (!TryNumber(call, 1, out var minute, out failure)) return failure;
        if (!TryNumber(call, 2, out var second, out failure)) return failure;
        hour = Math.Truncate(hour);
        minute = Math.Truncate(minute);
        second = Math.Truncate(second);
        if (hour > 32767 || minute > 32767 || second > 32767) return Operand.Of(ErrorValue.Num);
        var total = (hour * 3600) + (minute * 60) + second;
        if (total < 0) return Operand.Of(ErrorValue.Num);
        return Operand.Of(Value.FromNumber(total % SecondsPerDay / SecondsPerDay));
    }

    private static Operand Hour(FunctionCall call) => TimePart(call, seconds => seconds / 3600);

    private static Operand Minute(FunctionCall call) => TimePart(call, seconds => seconds / 60 % 60);

    private static Operand Second(FunctionCall call) => TimePart(call, seconds => seconds % 60);

    /// <summary>
    /// HOUR, MINUTE and SECOND: the serial's time of day, rounded to the nearest second, as Excel shows
    /// a time; a serial below 0 or past 31 December 9999 is <c>#NUM!</c>.
    /// </summary>
    private static Operand TimePart(FunctionCall call, Func<long, long> part)
    {
        if (!TryNumber(call, 0, out var serial, out var failure)) return failure;
        if (serial < 0 || serial >= DateSerial.Maximum + 1) return Operand.Of(ErrorValue.Num);
        var seconds = (long)Math.Round((serial - Math.Floor(serial)) * SecondsPerDay, MidpointRounding.AwayFromZero) % SecondsPerDay;
        return Operand.Of(Value.FromNumber(part(seconds)));
    }
}
