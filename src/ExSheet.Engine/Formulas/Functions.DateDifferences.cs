namespace ExSheet.Engine.Formulas;

/// <summary>YEARFRAC and DATEDIF, pinned by the 2026-10-03 Windows observations (ADR-0047).</summary>
internal static partial class FunctionLibrary
{
    private static Operand DateDif(FunctionCall call)
    {
        if (!TryDifferenceDate(call, 0, allowBoolean: true, out var start, out var failure)) return failure;
        if (!TryDifferenceDate(call, 1, allowBoolean: true, out var end, out failure)) return failure;
        if (!TryText(call, 2, out var unit, out failure)) return failure;
        if (start > end) return Operand.Of(ErrorValue.Num);
        var first = DateSerial.ToDate(start);
        var last = DateSerial.ToDate(end);
        var beforeAnniversary = last.Month < first.Month || (last.Month == first.Month && last.Day < first.Day);
        var years = last.Year - first.Year - (beforeAnniversary ? 1 : 0);
        var months = (last.Year - first.Year) * 12 + last.Month - first.Month - (last.Day < first.Day ? 1 : 0);
        return unit.ToUpperInvariant() switch
        {
            "Y" => Operand.Of(Value.FromNumber(years)),
            "M" => Operand.Of(Value.FromNumber(months)),
            "D" => Operand.Of(Value.FromNumber(end - start)),
            "YM" => Operand.Of(Value.FromNumber(months % 12)),
            // Every observed YD interval precedes its first completed anniversary. The run
            // does not say which year's leap day survives removing one or more years, so
            // those intervals are refused until that choice has been observed.
            "YD" => years == 0 ? Operand.Of(Value.FromNumber(end - start)) : Operand.Of(ErrorValue.Value),
            "MD" => Operand.Of(ErrorValue.Value), // The catalogue's existing, explicit refusal.
            _ => Operand.Of(ErrorValue.Num),
        };
    }

    private static Operand YearFrac(FunctionCall call)
    {
        if (!TryDifferenceDate(call, 0, allowBoolean: false, out var start, out var failure)) return failure;
        if (!TryDifferenceDate(call, 1, allowBoolean: false, out var end, out failure)) return failure;
        var basis = 0.0;
        if (call.Has(2) && !TryNumber(call, 2, out basis, out failure)) return failure;
        basis = Math.Truncate(basis);
        if (basis < 0 || basis > 4) return Operand.Of(ErrorValue.Num);
        if (start > end) (start, end) = (end, start);
        var first = DateSerial.ToDate(start);
        var last = DateSerial.ToDate(end);
        var result = basis switch
        {
            0 => ThirtyDayCount(first, last, european: false) / 360.0,
            1 => (end - start) / ActualYearLength(first, last),
            2 => (end - start) / 360.0,
            3 => (end - start) / 365.0,
            _ => ThirtyDayCount(first, last, european: true) / 360.0,
        };
        return Operand.Of(Value.FromNumber(result));
    }

    /// <summary>
    /// Basis 0's order matters: adjust the 31st before February ends. In the observed
    /// February-end to March-31 interval, the end stays 31, then the start becomes 30;
    /// changing that order would produce 30 days where Excel answers 31. Basis 4 only
    /// clamps the 31st. Both retain January 0 and Excel's fictitious February 29.
    /// </summary>
    private static int ThirtyDayCount((int Year, int Month, int Day) first, (int Year, int Month, int Day) last, bool european)
    {
        var firstDay = Math.Min(first.Day, 30);
        var lastDay = last.Day;
        if (european || firstDay >= 30) lastDay = Math.Min(lastDay, 30);
        if (!european && FebruaryEnd(first))
        {
            firstDay = 30;
            if (FebruaryEnd(last)) lastDay = 30;
        }
        return (last.Year - first.Year) * 360 + (last.Month - first.Month) * 30 + lastDay - firstDay;
    }

    private static bool FebruaryEnd((int Year, int Month, int Day) date) =>
        date.Month == 2 && date.Day >= DateTime.DaysInMonth(date.Year, 2);

    /// <summary>
    /// Actual/actual: at most one anniversary uses the containing year's length or a leap
    /// day in the interval. Longer intervals use the average Gregorian length of all years
    /// touched (MS-OI29500, 2.1.1072). The numerator still counts Excel's fictitious 1900 day.
    /// </summary>
    private static double ActualYearLength((int Year, int Month, int Day) first, (int Year, int Month, int Day) last)
    {
        if (first.Year == last.Year) return DateTime.IsLeapYear(first.Year) ? 366 : 365;
        var withinAnniversary = last.Year == first.Year + 1
            && (last.Month < first.Month || (last.Month == first.Month && last.Day <= first.Day));
        if (withinAnniversary)
        {
            var startsBeforeLeapDay = DateTime.IsLeapYear(first.Year)
                && (first.Month < 2 || (first.Month == 2 && first.Day <= 29));
            var endsAfterLeapDay = DateTime.IsLeapYear(last.Year)
                && (last.Month > 2 || (last.Month == 2 && last.Day >= 29));
            return startsBeforeLeapDay || endsAfterLeapDay ? 366 : 365;
        }
        var days = 0;
        for (var year = first.Year; year <= last.Year; year++) days += DateTime.IsLeapYear(year) ? 366 : 365;
        return days / (double)(last.Year - first.Year + 1);
    }

    /// <summary>Dates accept numeric text and date text under the Sheet's culture, and discard the time.</summary>
    private static bool TryDifferenceDate(FunctionCall call, int index, bool allowBoolean, out int serial, out Operand failure)
    {
        serial = 0;
        if (!TryScalar(call, index, out var value, out failure)) return false;
        if (!allowBoolean && value is { Kind: ValueKind.Boolean })
        {
            failure = Operand.Of(ErrorValue.Value);
            return false;
        }
        if (!TryNumber(call, index, out var number, out failure)) return false;
        if (number < 0 || Math.Truncate(number) > DateSerial.Maximum)
        {
            failure = Operand.Of(ErrorValue.Num);
            return false;
        }
        serial = (int)Math.Truncate(number);
        return true;
    }
}
