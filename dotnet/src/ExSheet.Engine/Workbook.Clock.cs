namespace ExSheet.Engine;

/// <summary>
/// The Sheet Day and the moment <c>NOW</c> answers (ADR-0121, ADR-0124), held by the Workbook for every
/// one of its Sheets (ADR-0331 §9): a Sheet added or copied reads the same day as the others, and one
/// recalculation reads the moment once for the whole Workbook.
/// </summary>
public sealed partial class Workbook
{
    /// <summary>
    /// The Sheet Day: the calendar day <c>TODAY()</c> answers on every Sheet, or <see langword="null"/>
    /// while it is not known, when every Formula that calls <c>TODAY</c> is <c>#GETTING_DATA</c>
    /// (ADR-0121). The engine never reads a clock; whoever runs it says which day it is. It is not part
    /// of a document, so a Workbook opened from one waits for its day again.
    /// </summary>
    public DateOnly? Today { get; private set; }

    /// <summary>
    /// Where the engine reads the current moment for <c>NOW</c>: the date and time of day in the
    /// Workbook's zone, or <see langword="null"/> while it is not known, when <c>NOW</c> is
    /// <c>#GETTING_DATA</c> (ADR-0124). The engine reads no clock itself; it calls this once at the
    /// start of each recalculation, so every <c>NOW</c> on every Sheet in one recalculation answers the
    /// same moment. Null, the default, leaves <c>NOW</c> waiting.
    /// </summary>
    public Func<DateTime?>? NowSource { get; set; }

    /// <summary>Whether any Formula on any Sheet calls a volatile function, so that moving the clock on can change a Value.</summary>
    public bool HasVolatileFormulas => _sheets.Any(sheet => sheet.HasVolatileFormulas);

    /// <summary>
    /// Sets the Sheet Day, and recalculates only the Formulas that call <c>TODAY</c>, on every Sheet, and
    /// what reads them, in one recalculation. Setting the day the Workbook already has changes nothing.
    /// It is not an operation a user undoes.
    /// </summary>
    /// <param name="today">The day, or <see langword="null"/> to make <c>TODAY</c> wait again.</param>
    /// <returns>What the recalculation changed, Sheet by Sheet.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The day is before 1 January 1900, where Excel's dates begin (ADR-0047).
    /// </exception>
    public WorkbookChange SetToday(DateOnly? today)
    {
        if (today is { } day && DateSerial.FromDate(day.Year, day.Month, day.Day) is null)
        {
            throw new ArgumentOutOfRangeException(nameof(today), day, "The Sheet Day is a day from 1 January 1900, where Excel's dates begin.");
        }
        var shown = _shown;
        if (today == Today) return Collecting(shown, () => SheetChange.None);
        Today = today;
        var readers = new HashSet<SheetCell>(_sheets.SelectMany(sheet => sheet.TodayReaders));
        return Collecting(shown, () => readers.Count == 0 ? SheetChange.None : shown.RecalculateCells(readers));
    }

    /// <summary>
    /// Recalculates the Formulas that call a volatile function, on every Sheet, and what reads them,
    /// reading the moment again: what ExSheet does as the minute turns (ADR-0124). It is not an
    /// operation a user undoes.
    /// </summary>
    /// <returns>What the recalculation changed, Sheet by Sheet.</returns>
    public WorkbookChange RecalculateVolatile()
    {
        var shown = _shown;
        return Collecting(shown, () => HasVolatileFormulas ? shown.RecalculateCells([]) : SheetChange.None);
    }
}
