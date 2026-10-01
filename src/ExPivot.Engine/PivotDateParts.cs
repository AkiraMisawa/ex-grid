using System.Globalization;

namespace ExPivot.Engine;

/// <summary>
/// A part of a Date column that a Pivot Field can be declared as (ADR-0059, "Date parts"): the
/// common case of Excel's automatic date grouping, declared in one line rather than inferred.
/// A part's Items are numbers — 2026, 3, 9 — so a Hidden Item is written <c>Number:9</c> under any
/// culture; they are labelled as Excel labels them, in the report's words (<see cref="PivotDateWords"/>),
/// and ordered by the calendar. The part of a Blank is a Blank.
/// </summary>
public enum PivotDatePart
{
    /// <summary>The year: <c>2026</c>.</summary>
    Year = 0,

    /// <summary>The quarter of the year: <c>Qtr1</c> to <c>Qtr4</c>.</summary>
    Quarter,

    /// <summary>The month: <c>Jan</c> to <c>Dec</c>.</summary>
    Month,
}

/// <summary>
/// The words a date part's Items are painted with, by id, with their English (ADR-0059). They are
/// resolved as <see cref="PivotWords"/>' are: through the Consumer's <see cref="PivotOptions.Label"/>,
/// then the English, then the id itself. A word with <c>{0}</c> is a template, filled with the
/// part's number.
/// </summary>
public static class PivotDateWords
{
    // Static fields are made in the order they are written: the months' ids before Ids, which
    // reads them.
    private static readonly string[] MonthIds = [.. Enumerable.Range(1, 12).Select(m => "date-month-" + m.ToString(CultureInfo.InvariantCulture))];

    /// <summary>A year's Item: <c>{0}</c>, {0} the year — <c>2026</c>.</summary>
    public const string Year = "date-year";

    /// <summary>A quarter's Item: <c>Qtr{0}</c>, {0} the quarter from 1 to 4 — <c>Qtr3</c>.</summary>
    public const string Quarter = "date-quarter";

    /// <summary>The id of a month's Item, from 1 to 12 — <c>date-month-9</c> is <c>Sep</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The month is not from 1 to 12.</exception>
    public static string Month(int month)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(month, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(month, 12);
        return MonthIds[month - 1];
    }

    /// <summary>Every id this class defines, in order: the year, the quarter, then the twelve months.</summary>
    public static IReadOnlyList<string> Ids { get; } = [Year, Quarter, .. Enumerable.Range(1, 12).Select(Month)];

    /// <summary>The English for an id, or null for an id this class does not know.</summary>
    public static string? EnglishFor(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return English.TryGetValue(id, out var word) ? word : null;
    }

    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        [Year] = "{0}",
        [Quarter] = "Qtr{0}",
        ["date-month-1"] = "Jan",
        ["date-month-2"] = "Feb",
        ["date-month-3"] = "Mar",
        ["date-month-4"] = "Apr",
        ["date-month-5"] = "May",
        ["date-month-6"] = "Jun",
        ["date-month-7"] = "Jul",
        ["date-month-8"] = "Aug",
        ["date-month-9"] = "Sep",
        ["date-month-10"] = "Oct",
        ["date-month-11"] = "Nov",
        ["date-month-12"] = "Dec",
    };

    /// <summary>The label of a date part's Item <paramref name="value"/> in the report's words.</summary>
    internal static string Label(PivotDatePart part, int value, PivotOptions options) => part switch
    {
        PivotDatePart.Year => PivotWords.Fill(Resolve(Year, options), value.ToString(CultureInfo.InvariantCulture)),
        PivotDatePart.Quarter => PivotWords.Fill(Resolve(Quarter, options), value.ToString(CultureInfo.InvariantCulture)),
        _ => Resolve(Month(value), options),
    };

    /// <summary>The part of a date.</summary>
    internal static int Of(PivotDatePart part, DateTime date) => part switch
    {
        PivotDatePart.Year => date.Year,
        PivotDatePart.Quarter => ((date.Month - 1) / 3) + 1,
        _ => date.Month,
    };

    /// <summary>Whether <paramref name="value"/> is a value <paramref name="part"/> takes.</summary>
    internal static bool Takes(PivotDatePart part, double value) => part switch
    {
        PivotDatePart.Year => value is >= 1 and <= 9999 && value == Math.Floor(value),
        PivotDatePart.Quarter => value is >= 1 and <= 4 && value == Math.Floor(value),
        _ => value is >= 1 and <= 12 && value == Math.Floor(value),
    };

    private static string Resolve(string id, PivotOptions options) => options.Label?.Invoke(id) ?? EnglishFor(id) ?? id;
}
