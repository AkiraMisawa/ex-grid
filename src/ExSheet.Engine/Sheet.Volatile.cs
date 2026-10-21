using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

/// <summary>Volatile functions (ADR-0124): Formulas recalculated in every recalculation, and the moment <c>NOW</c> answers.</summary>
public sealed partial class Sheet
{
    /// <summary>The functions whose answer can change while no cell their Formula names changes.</summary>
    private static readonly HashSet<string> VolatileFunctions = new(StringComparer.Ordinal) { "NOW", "OFFSET", "INDIRECT", "RAND", "RANDBETWEEN" };

    /// <summary>The Formula cells that call a volatile function.</summary>
    private readonly HashSet<CellAddress> _volatileFormulas = [];

    /// <summary>The moment read at the start of the recalculation under way.</summary>
    private DateTime? _moment;

    /// <summary>Where the Workbook reads the current moment for <c>NOW</c> (<see cref="Workbook.NowSource"/>; ADR-0124).</summary>
    internal Func<DateTime?>? NowSource
    {
        get => Workbook.NowSource;
        set => Workbook.NowSource = value;
    }

    /// <summary>Whether any Formula of this Sheet calls a volatile function.</summary>
    internal bool HasVolatileFormulas => _volatileFormulas.Count > 0;

    /// <summary>Recalculates the Workbook's volatile Formulas (<see cref="Workbook.RecalculateVolatile"/>): what it changed on this Sheet.</summary>
    internal SheetChange RecalculateVolatile() => Workbook.RecalculateVolatile().For(this);

    /// <summary>One recalculation of <paramref name="cells"/>, on any Sheet of the Workbook, started from this Sheet, with every volatile Formula of the Workbook: what it changed here.</summary>
    internal SheetChange RecalculateCells(HashSet<SheetCell> cells) => Recalculate(cells, []);

    private void RegisterVolatile(CellAddress formulaCell, Node formula)
    {
        if (formula.Calls.Any(call => call.Function is not null && VolatileFunctions.Contains(call.Name))) _volatileFormulas.Add(formulaCell);
    }

    /// <summary>Whether a Formula calls <c>NOW</c> while the moment is not known: it waits, whichever branch it takes.</summary>
    private bool WaitsForNow(Node node) =>
        _moment is null && node.Calls.Any(call => call.Function is not null && call.Name == "NOW");
}
