using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

/// <summary>The Formulas that read the Sheet Day (ADR-0121), the day <c>TODAY</c> answers, which the Workbook is given as data.</summary>
public sealed partial class Sheet
{
    /// <summary>The Formula cells that call <c>TODAY</c>, recalculated when the Sheet Day changes.</summary>
    private readonly HashSet<CellAddress> _todayReaders = [];

    /// <summary>The Sheet Day, which the Workbook holds for all its Sheets (<see cref="Workbook.Today"/>; ADR-0121, ADR-0331).</summary>
    internal DateOnly? Today => Workbook.Today;

    /// <summary>Sets the Workbook's Sheet Day (<see cref="Workbook.SetToday"/>): what its recalculation changed on this Sheet.</summary>
    internal SheetChange SetToday(DateOnly? today) => Workbook.SetToday(today).For(this);

    /// <summary>The Formula cells of this Sheet that call <c>TODAY</c>.</summary>
    internal IEnumerable<SheetCell> TodayReaders => _todayReaders.Select(address => new SheetCell(this, address));

    private void RegisterTodayReader(CellAddress formulaCell, Node formula)
    {
        if (CallsToday(formula)) _todayReaders.Add(formulaCell);
    }

    /// <summary>Whether a Formula calls <c>TODAY</c> while the Sheet Day is not known: it waits, whichever branch it takes.</summary>
    private bool WaitsForToday(Node node) => Today is null && CallsToday(node);

    private static bool CallsToday(Node node) => node.Calls.Any(call => call.Function is not null && call.Name == "TODAY");
}
