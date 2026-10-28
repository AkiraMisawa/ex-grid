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

    /// <summary>Refuses a surface axis' code it cannot write the axis' values with, naming the axis (ADR-0420).</summary>
    public static void CheckSurfaceAxis(SurfaceAxis axis, string code, FormatCode? read, string? refusal)
    {
        var name = axis.Title.Length > 0 ? $"the surface axis '{axis.Title}'" : "a surface axis";
        if (refusal is not null || read is null)
            throw new ChartRefusedException($"The format code \"{code}\" of {name} {refusal}.");
        if (axis.Format is not null)
            throw new ChartRefusedException($"The format code \"{code}\" of {name} stands beside its Format: it would write one value two ways.");
        if (axis.Values.FirstOrDefault(v => read.IsDate ? !SurfaceAxis.IsDate(v) : !SurfaceAxis.IsNumber(v)) is { } other)
        {
            throw new ChartRefusedException(
                $"The format code \"{code}\" of {name} writes {(read.IsDate ? "dates" : "numbers")}, and the axis holds {Describe(other)}.");
        }
        // A grid line, a slice and a tooltip are named by the text: two values written alike could not be told apart.
        var written = new Dictionary<string, object>();
        foreach (var value in axis.Values)
        {
            var text = read.Format(value, CultureInfo.InvariantCulture);
            if (written.TryGetValue(text, out var before))
                throw new ChartRefusedException($"The format code \"{code}\" of {name} writes {Describe(before)} and {Describe(value)} alike, as \"{text}\".");
            written[text] = value;
        }
    }

    /// <summary>Refuses a surface's code for its values it cannot write them with (ADR-0420).</summary>
    public static void CheckZ(SurfaceDefinition surface, string code, FormatCode? read, string? refusal)
    {
        var name = surface.ZTitle.Length > 0 ? $"the surface's '{surface.ZTitle}'" : "the surface's values";
        if (refusal is not null || read is null)
            throw new ChartRefusedException($"The format code \"{code}\" of {name} {refusal}.");
        if (surface.ZFormat is not null)
            throw new ChartRefusedException($"The format code \"{code}\" of {name} stands beside its ZFormat: it would write one value two ways.");
        if (read.IsDate)
            throw new ChartRefusedException($"The format code \"{code}\" of {name} writes dates, and its values are numbers.");
    }

    private static string Describe(object value) => value switch
    {
        string text => $"the text \"{text}\"",
        _ when SurfaceAxis.IsNumber(value) => $"the number {Convert.ToString(value, CultureInfo.InvariantCulture)}",
        DateOnly date => $"the date {date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
        DateTime date => $"the date {date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}",
        _ => $"a {value.GetType().Name}",
    };

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
