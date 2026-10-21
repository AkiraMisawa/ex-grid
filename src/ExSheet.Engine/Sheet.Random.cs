using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

/// <summary>
/// The draws of <c>RAND</c> and <c>RANDBETWEEN</c> on this Sheet (ADR-0180), from the Workbook's seed
/// and count of changes (ADR-0331 §9) and the Sheet's own name.
/// </summary>
public sealed partial class Sheet
{
    /// <summary>The Formula cells that call <c>RAND</c> or <c>RANDBETWEEN</c>, recalculated when the seed or the count is set.</summary>
    private readonly HashSet<CellAddress> _randomReaders = [];

    /// <summary>The Formula cells of this Sheet that call <c>RAND</c> or <c>RANDBETWEEN</c>, recalculated when the Workbook's seed or count is set.</summary>
    internal IEnumerable<SheetCell> RandomReaders => _randomReaders.Select(address => new SheetCell(this, address));

    /// <summary>The Workbook's seed (<see cref="Workbook.Seed"/>; ADR-0180, ADR-0331 §9).</summary>
    internal ulong? Seed => Workbook.Seed;

    /// <summary>The Workbook's count of changes (<see cref="Workbook.Changes"/>).</summary>
    internal long Changes => Workbook.Changes;

    /// <summary>Sets the Workbook's seed and keeps its count (<see cref="Workbook.SetSeed(ulong?)"/>): what it changed on this Sheet.</summary>
    internal SheetChange SetSeed(ulong? seed) => Workbook.SetSeed(seed).For(this);

    /// <summary>Sets the Workbook's seed and count (<see cref="Workbook.SetSeed(ulong?, long)"/>): what it changed on this Sheet.</summary>
    internal SheetChange SetSeed(ulong? seed, long changes) => Workbook.SetSeed(seed, changes).For(this);

    /// <summary>Does <paramref name="operation"/> as one operation of the Workbook (<see cref="Workbook.AsOneChange"/>).</summary>
    internal T AsOneChange<T>(Func<T> operation) => Workbook.AsOneChange(operation);

    /// <summary>Counts a change of the Workbook (<see cref="Workbook.CountChange"/>).</summary>
    internal void CountChange() => Workbook.CountChange();

    /// <summary>The draw for the call at <paramref name="place"/> in the Formula of <paramref name="cell"/>, or <see langword="null"/> without a seed.</summary>
    private ulong? Draw(CellAddress cell, int place) =>
        Seed is { } seed ? Draws.Of(seed, Name, cell, place, Changes) : null;

    private void RegisterRandomReader(CellAddress formulaCell, Node formula)
    {
        if (CallsRandom(formula)) _randomReaders.Add(formulaCell);
    }

    /// <summary>Whether a Formula draws while the Sheet has no seed: it waits, whichever branch it takes.</summary>
    private bool WaitsForSeed(Node node) => Seed is null && CallsRandom(node);

    private static bool CallsRandom(Node node) =>
        node.Calls.Any(call => call.Function is not null && call.Name is "RAND" or "RANDBETWEEN");
}
