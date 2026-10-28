using System.Globalization;
using ExGrid.Data;

namespace ExChart.Engine;

/// <summary>
/// An axis' or a series' Excel format code (ADR-0420), read once by the family's reader, the one both cores
/// write a code with (ADR-0342), and checked as the definition is.
/// </summary>
internal static class ChartCodes
{
    /// <summary>Reads <paramref name="code"/>: the code, the reader's, or why it cannot be read.</summary>
    public static (string? Code, FormatCode? Read, string? Refusal) Read(string? code)
    {
        if (code is null)
            return (null, null, null);
        return FormatCode.TryParse(code, out var read, out var reason) ? (code, read, null) : (code, null, reason);
    }

    /// <summary>
    /// A position through <paramref name="code"/>: a Dates axis' day number as the date and time it is, a Numbers
    /// axis' as the number. The ticks of a fraction of a day are rounded half away from zero, as the Script Core rounds them.
    /// </summary>
    public static string WritePosition(FormatCode code, AxisValues values, double position)
    {
        if (values != AxisValues.Dates)
            return code.Format(position, CultureInfo.InvariantCulture);
        var day = Math.Floor(position);
        var ticks = (long)day * TimeSpan.TicksPerDay + (long)Math.Round((position - day) * TimeSpan.TicksPerDay, MidpointRounding.AwayFromZero);
        return code.Format(new DateTime(ticks), CultureInfo.InvariantCulture);
    }

    /// <summary>An axis' code the reader cannot read, said by name.</summary>
    public static string AxisRefusal(string code, string reason) => $"The axis' format code \"{code}\" {reason}.";

    /// <summary>A series' code the reader cannot read, said by name.</summary>
    public static string SeriesRefusal(string name, string code, string reason) => $"'{name}''s format code \"{code}\" {reason}.";

    /// <summary>Refuses an axis' code it cannot write with, naming it (ADR-0420).</summary>
    public static void CheckAxis(ChartAxis axis)
    {
        if (axis.FormatCode is not { } code)
            return;
        if (axis.CodeRefusal is { } refusal)
            throw new ChartRefusedException(AxisRefusal(code, refusal));
        if (axis.Format is not null)
            throw new ChartRefusedException($"The axis has both a Format and the format code \"{code}\": it would write one position two ways.");
        switch (axis.Values)
        {
            case AxisValues.Labels:
                throw new ChartRefusedException($"The axis places labels, which it writes as they are: the format code \"{code}\" has nothing to write.");
            case AxisValues.Dates when axis.CodeWritesDates == false:
                throw new ChartRefusedException($"The axis' format code \"{code}\" writes numbers, and the axis places dates.");
            case AxisValues.Dates when TickLayout.CodeResolution(axis.Write) is null:
                throw new ChartRefusedException($"The axis' format code \"{code}\" writes no day, month or year, and the axis places dates.");
            case AxisValues.Numbers when axis.CodeWritesDates == true:
                throw new ChartRefusedException($"The axis' format code \"{code}\" writes dates, and the axis places numbers.");
        }
    }

    /// <summary>Refuses a series' code it cannot write with, naming the series (ADR-0420).</summary>
    public static void CheckSeries(ChartSeries s)
    {
        if (s.FormatCode is not { } code)
            return;
        if (s.CodeRefusal is { } refusal)
            throw new ChartRefusedException(SeriesRefusal(s.Name, code, refusal));
        if (s.Format is not null)
            throw new ChartRefusedException($"'{s.Name}' has both a Format and the format code \"{code}\": it would write one value two ways.");
        if (s.CodeWritesDates)
            throw new ChartRefusedException($"'{s.Name}''s format code \"{code}\" writes dates, and its values are numbers.");
    }
}
