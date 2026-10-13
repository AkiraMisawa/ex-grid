namespace ExSheet.Engine.Formulas;

/// <summary>
/// DATE, TODAY, YEAR, MONTH, DAY, EOMONTH, EDATE, WEEKDAY, DAYS, NETWORKDAYS and WORKDAY over
/// Excel's 1900 serials, 29 February 1900 included (ADR-0047, <see cref="DateSerial"/>).
/// </summary>
internal static partial class FunctionLibrary
{
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
    /// YEAR, MONTH and DAY: the serial cut down to its day; serial 0 is 0 January 1900, and serial
    /// 60 is 29 February 1900. A serial below 0 or after 31 December 9999 is <c>#NUM!</c>, -0.5
    /// included, which is cut down to -1, as Excel was observed to answer (YEAR-008, MONTH-005, DAY-005).
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
    /// EOMONTH and EDATE: the start date is cut down to its day, as YEAR, MONTH and DAY cut a serial,
    /// and the months are truncated to a whole number. The start is read as YEAR, MONTH and DAY read
    /// it, so serial 0 is 0 January 1900 and serial 60 is 29 February 1900. EDATE keeps the start's
    /// day, or the month's last day where the month is shorter; EOMONTH gives the month's last day.
    /// The months are counted on the real calendar, where February 1900 has 28 days, and a day before
    /// 1 March 1900 is its own serial, 1 to 59: <c>EOMONTH(31,1)</c> is 28 February 1900, 59,
    /// <c>EOMONTH(0,1)</c> is 59 too, <c>EDATE(DATE(1900,3,15),-1)</c> is 15 February, 46, and
    /// <c>EDATE(60,1)</c> is 29 March, 89, as Excel was observed to answer (ADR-0047; EOMONTH-008 and
    /// EDATE-008, the seventeenth Windows run; EOMONTH-010 and EDATE-010, the area pass of
    /// 2026-10-12). EDATE from serial 0 keeps its day 0, which is the day before the 1st of the month
    /// it reaches: <c>EDATE(0,1)</c> is 31 January 1900, 31 (EDATE-016, the second area pass). A start
    /// below 0, -0.5 included, or a result before 1900, is <c>#NUM!</c>, as Excel was observed to
    /// answer under both (EOMONTH-004, 015 to 017; EDATE-007, 012, 017). So is a result after 9999,
    /// observed under EOMONTH (EOMONTH-005) and EDATE's by generalisation. A boolean typed as EOMONTH's
    /// months or as EDATE's start is <c>#VALUE!</c>, as Excel was observed to answer (EOMONTH-009,
    /// EDATE-009, the seventeenth Windows run).
    /// </summary>
    /// <remarks>
    /// Refused (<see cref="Evaluator.Refuse"/>: <c>#VALUE!</c>, the Formula's) until Excel is asked:
    /// <list type="bullet">
    /// <item>EDATE from serial 0 to January 1900, to March 1900 and to January 10000 (EDATE-018 to
    /// 020). The readings that give every observed answer part there
    /// (<c>spikes/exsheet-date-readings/</c>): the day before 1 January 1900 is serial 0 by DATE's
    /// arithmetic, and 31 December 1899, <c>#NUM!</c>, on the real calendar; the day before 1 March
    /// 1900 is the day that never existed, 60, or 28 February, 59; and January 10000 is past the last
    /// year, where the month before's last day is 31 December 9999.</item>
    /// <item>A boolean typed as EOMONTH's start or as EDATE's months (EOMONTH-018, EDATE-023), which
    /// no run has typed one into. Excel's reading of a boolean typed as a start date differs between
    /// functions: DATEDIF reads it as 1 (DATEDIF-046) and YEARFRAC gives <c>#VALUE!</c>
    /// (YEARFRAC-054), so the boolean each function was observed to refuse in one argument says
    /// nothing of the other. Read as 1, <c>EOMONTH(TRUE,1)</c> would be 28 February 1900, 59, and
    /// <c>EDATE(45000,TRUE)</c> 15 April 2023, 45031.</item>
    /// </list>
    /// </remarks>
    private static Operand MonthsAway(FunctionCall call, bool endOfMonth)
    {
        for (var i = 0; i < 2; i++)
        {
            if (!TryScalar(call, i, out var value, out var failure)) return failure;
            // EOMONTH's months and EDATE's start were observed; the other argument of each is refused.
            if (value is { Kind: ValueKind.Boolean }) return (i == 1) == endOfMonth ? Operand.Of(ErrorValue.Value) : call.Evaluator.Refuse();
        }
        if (!TryNumber(call, 0, out var start, out var error)) return error;
        if (!TryNumber(call, 1, out var months, out error)) return error;
        start = Math.Floor(start);
        months = Math.Truncate(months);
        if (start < 0 || start > DateSerial.Maximum) return Operand.Of(ErrorValue.Num);

        var (year, month, day) = DateSerial.ToDate((int)start);
        var total = year * 12.0 + (month - 1) + months;
        var toYear = Math.Floor(total / 12);
        if (toYear < 1900 || toYear > 10000) return Operand.Of(ErrorValue.Num);
        var toMonth = (int)(total - toYear * 12) + 1;
        var fromDayZero = !endOfMonth && day == 0;
        if (fromDayZero && ((int)toYear, toMonth) is (1900, 1) or (1900, 3) or (10000, 1)) return call.Evaluator.Refuse();
        if (toYear > 9999) return Operand.Of(ErrorValue.Num);
        if (fromDayZero) return Operand.Of(Value.FromNumber(DateSerial.FromDate((int)toYear, toMonth, 1)!.Value - 1));
        var length = DateTime.DaysInMonth((int)toYear, toMonth);
        var serial = DateSerial.FromDate((int)toYear, toMonth, endOfMonth ? length : Math.Min(day, length))!.Value;
        return Operand.Of(Value.FromNumber(serial));
    }

    // ---- WEEKDAY, DAYS, NETWORKDAYS, WORKDAY ----------------------------------------------------

    /// <summary>
    /// WEEKDAY: the day of the week of the serial, on Excel's calendar (serial 1 is a Sunday), as
    /// <c>return_type</c> numbers it — 1 or 17 from Sunday, 2 or 11 from Monday, 3 from Monday at 0,
    /// and 12 to 16 from Tuesday to Saturday. The serial is cut down to its day. Another type, or a
    /// serial outside 0 to 31 December 9999, is <c>#NUM!</c>, -0.5 included (WEEKDAY-008).
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

    /// <summary>DAYS: <c>end_date - start_date</c>, each cut down to its day; a serial outside 0 to 31 December 9999 is <c>#NUM!</c>, -0.5 included (DAYS-005).</summary>
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
    /// A date argument of NETWORKDAYS and WORKDAY, cut down to its day: a boolean typed is refused
    /// (<see cref="Evaluator.Refuse"/>: <c>#VALUE!</c>, the Formula's) until Excel is asked (NETWORKDAYS-010, WORKDAY-011); below 0 or past 31
    /// December 9999 is <c>#NUM!</c>, -0.5 included (NETWORKDAYS-009, WORKDAY-010).
    /// Every serial from 0 is a day, the ones before 1 March 1900 included, and takes the day of
    /// the week WEEKDAY gives it (<see cref="DateSerial.DayOfWeek"/>): serial 0 a Saturday, 1 a
    /// Sunday, 60 a Wednesday. <c>NETWORKDAYS(30,100)</c> is 51, as Excel was observed to answer
    /// (ADR-0047; NETWORKDAYS-005, the seventeenth Windows run).
    /// </summary>
    private static bool TryWorkdayDate(FunctionCall call, int index, out int serial, out Operand failure)
    {
        serial = 0;
        if (!TryScalar(call, index, out var value, out failure)) return false;
        if (value is { Kind: ValueKind.Boolean })
        {
            failure = call.Evaluator.Refuse();
            return false;
        }
        if (!TryNumber(call, index, out var number, out failure)) return false;
        number = Math.Floor(number);
        if (number < 0 || number > DateSerial.Maximum)
        {
            failure = Operand.Of(ErrorValue.Num);
            return false;
        }
        serial = (int)number;
        return true;
    }

    /// <summary>
    /// The holidays argument: a range's numbers and a typed number, each truncated to its day. Text
    /// among them is <c>#VALUE!</c>, as Excel's is (WORKDAY-005); a boolean among them is refused
    /// (<see cref="Evaluator.Refuse"/>: <c>#VALUE!</c>, the Formula's) until Excel is asked; an Error Value is the result.
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
                failure = value.Kind == ValueKind.Boolean ? call.Evaluator.Refuse() : Operand.Of(ErrorValue.Value);
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
    /// passing over weekends and holidays; <c>days</c> truncated, and 0 gives the start itself. The
    /// days before 1 March 1900 count as NETWORKDAYS counts them (<see cref="TryWorkdayDate"/>), and
    /// a result before serial 0 or past 31 December 9999 is <c>#NUM!</c>.
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
            if (serial < 0 || serial > DateSerial.Maximum) return Operand.Of(ErrorValue.Num);
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
    /// a time; a serial below 0 or past 31 December 9999 is <c>#NUM!</c>, but one between -1 and 0, which
    /// is refused (<see cref="Evaluator.Refuse"/>) until Excel is asked (HOUR-005, MINUTE-003, SECOND-003;
    /// 2026-10-13): read as a time of day, <c>HOUR(-0.5)</c> is 12, and below 0 <c>#NUM!</c>, and no run
    /// has read which Excel takes. -1 and below stay <c>#NUM!</c>, as Excel's was at -1.
    /// </summary>
    private static Operand TimePart(FunctionCall call, Func<long, long> part)
    {
        if (!TryNumber(call, 0, out var serial, out var failure)) return failure;
        if (serial < 0 && serial > -1) return call.Evaluator.Refuse();
        if (serial < 0 || serial >= DateSerial.Maximum + 1) return Operand.Of(ErrorValue.Num);
        var seconds = (long)Math.Round((serial - Math.Floor(serial)) * SecondsPerDay, MidpointRounding.AwayFromZero) % SecondsPerDay;
        return Operand.Of(Value.FromNumber(part(seconds)));
    }

    /// <summary>
    /// NOW (ADR-0124): the moment the Sheet was given for this recalculation, as a serial with the
    /// time of day; <c>#GETTING_DATA</c> while it is not known.
    /// </summary>
    private static Operand Now(FunctionCall call)
    {
        if (call.Evaluator.Cells.Now is not { } moment) return Operand.Of(ErrorValue.GettingData);
        if (DateSerial.FromDate(moment.Year, moment.Month, moment.Day) is not { } day) return Operand.Of(ErrorValue.Num);
        return Operand.Of(Value.FromNumber(day + (moment.TimeOfDay.TotalSeconds / SecondsPerDay)));
    }
}
