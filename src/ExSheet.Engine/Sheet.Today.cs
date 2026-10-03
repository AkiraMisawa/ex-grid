using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

/// <summary>The Sheet Day (ADR-0121): the day <c>TODAY</c> answers, given to the Sheet as data.</summary>
public sealed partial class Sheet
{
    /// <summary>The Formula cells that call <c>TODAY</c>, recalculated when the Sheet Day changes.</summary>
    private readonly HashSet<CellAddress> _todayReaders = [];

    /// <summary>
    /// The Sheet Day: the calendar day <c>TODAY()</c> answers, or <see langword="null"/> while it is
    /// not known, when every Formula that calls <c>TODAY</c> is <c>#GETTING_DATA</c> (ADR-0121). The
    /// engine never reads a clock; whoever runs it says which day it is. It is not part of a
    /// <see cref="SheetDocument"/>, so a Sheet opened from one waits for its day again.
    /// </summary>
    public DateOnly? Today { get; private set; }

    /// <summary>
    /// Sets the Sheet Day, and recalculates only the Formulas that call <c>TODAY</c> and what reads
    /// them, as a Linked Table's snapshot recalculates only its readers. Setting the day the Sheet
    /// already has changes nothing. It is not an operation a user undoes.
    /// </summary>
    /// <param name="today">The day, or <see langword="null"/> to make <c>TODAY</c> wait again.</param>
    /// <returns>What the recalculation changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The day is before 1 January 1900, where Excel's dates begin (ADR-0047).
    /// </exception>
    public SheetChange SetToday(DateOnly? today)
    {
        if (today is { } day && DateSerial.FromDate(day.Year, day.Month, day.Day) is null)
        {
            throw new ArgumentOutOfRangeException(nameof(today), day, "The Sheet Day is a day from 1 January 1900, where Excel's dates begin.");
        }
        if (today == Today) return SheetChange.None;
        Today = today;
        return _todayReaders.Count == 0 ? SheetChange.None : Recalculate([.. _todayReaders], []);
    }

    private void RegisterTodayReader(CellAddress formulaCell, Node formula)
    {
        if (CallsToday(formula)) _todayReaders.Add(formulaCell);
    }

    /// <summary>Whether a Formula calls <c>TODAY</c> while the Sheet Day is not known: it waits, whichever branch it takes.</summary>
    private bool WaitsForToday(Node node) => Today is null && CallsToday(node);

    private static bool CallsToday(Node node) => node.Calls.Any(call => call.Function is not null && call.Name == "TODAY");
}
