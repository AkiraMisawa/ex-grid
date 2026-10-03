using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

/// <summary>Volatile functions (ADR-0124): Formulas recalculated in every recalculation, and the moment <c>NOW</c> answers.</summary>
public sealed partial class Sheet
{
    /// <summary>The functions whose answer can change while no cell their Formula names changes.</summary>
    private static readonly HashSet<string> VolatileFunctions = new(StringComparer.Ordinal) { "NOW", "OFFSET" };

    /// <summary>The Formula cells that call a volatile function.</summary>
    private readonly HashSet<CellAddress> _volatileFormulas = [];

    /// <summary>The moment read at the start of the recalculation under way.</summary>
    private DateTime? _moment;

    /// <summary>
    /// Where the engine reads the current moment for <c>NOW</c>: the date and time of day in the
    /// Sheet's zone, or <see langword="null"/> while it is not known, when <c>NOW</c> is
    /// <c>#GETTING_DATA</c> (ADR-0124). The engine reads no clock itself; it calls this once at the
    /// start of each recalculation, so every <c>NOW</c> in one recalculation answers the same moment.
    /// Null, the default, leaves <c>NOW</c> waiting.
    /// </summary>
    public Func<DateTime?>? NowSource { get; set; }

    /// <summary>Whether any Formula calls a volatile function, so that moving the clock on can change a Value.</summary>
    public bool HasVolatileFormulas => _volatileFormulas.Count > 0;

    /// <summary>
    /// Recalculates the Formulas that call a volatile function, and what reads them, reading the
    /// moment again: what ExSheet does as the minute turns (ADR-0124). It is not an operation a
    /// user undoes.
    /// </summary>
    /// <returns>What the recalculation changed.</returns>
    public SheetChange RecalculateVolatile() =>
        _volatileFormulas.Count == 0 ? SheetChange.None : Recalculate([.. _volatileFormulas], []);

    private void RegisterVolatile(CellAddress formulaCell, Node formula)
    {
        if (formula.Calls.Any(call => call.Function is not null && VolatileFunctions.Contains(call.Name))) _volatileFormulas.Add(formulaCell);
    }

    /// <summary>Whether a Formula calls <c>NOW</c> while the moment is not known: it waits, whichever branch it takes.</summary>
    private bool WaitsForNow(Node node) =>
        _moment is null && node.Calls.Any(call => call.Function is not null && call.Name == "NOW");
}
