using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

/// <summary>
/// The Sheet Seed and the count of changes (ADR-0180): what <c>RAND</c> and <c>RANDBETWEEN</c> draw
/// from, given to the Sheet as data, as the Sheet Day is (ADR-0121).
/// </summary>
public sealed partial class Sheet
{
    /// <summary>The Formula cells that call <c>RAND</c> or <c>RANDBETWEEN</c>, recalculated when the seed or the count is set.</summary>
    private readonly HashSet<CellAddress> _randomReaders = [];

    /// <summary>How deep the operations under way are nested: one operation counts as one change, however many steps it takes.</summary>
    private int _operationDepth;

    /// <summary>Whether the operation under way has counted its change already.</summary>
    private bool _operationCounted;

    /// <summary>
    /// The Sheet Seed: what every draw of <c>RAND</c> and <c>RANDBETWEEN</c> is made from, with the
    /// cell, the call's place in its Formula and <see cref="Changes"/> (ADR-0180). <see langword="null"/>
    /// while it is not set, when every Formula that calls either is <c>#GETTING_DATA</c>, as one that
    /// calls <c>TODAY</c> waits for the Sheet Day. The engine never chooses a random number of its own:
    /// whoever runs it gives the seed. It is not part of a <see cref="SheetDocument"/>.
    /// </summary>
    public ulong? Seed { get; private set; }

    /// <summary>
    /// How many changes the Sheet has had since it was made or opened (ADR-0180): each draws again.
    /// An edit of an Entry, an insertion or a deletion, the undo or redo of one of those, a Linked
    /// Table's snapshot, and a Linked Table's declaration that declares it or replaces it, each count
    /// once. A change of format or width, a recalculation by the clock,
    /// a rename, a new Sheet Day and a new seed do not. A Sheet opened from a Sheet Document starts
    /// from 0.
    /// </summary>
    public long Changes { get; private set; }

    /// <summary>
    /// Sets the Sheet Seed and keeps the count of changes, and recalculates only the Formulas that
    /// draw and what reads them. Setting the seed the Sheet already has changes nothing. It is not a
    /// change, and not an operation a user undoes.
    /// </summary>
    /// <param name="seed">The seed, or <see langword="null"/> to make the draws wait again.</param>
    /// <returns>What the recalculation changed.</returns>
    public SheetChange SetSeed(ulong? seed) => SetSeed(seed, Changes);

    /// <summary>
    /// Sets the Sheet Seed and the count of changes together: the same Entries, seed and count give
    /// the same draws, so a Sheet opened from a Sheet Document and given the seed and count another
    /// Sheet had shows that Sheet's Values (SH-17).
    /// </summary>
    /// <param name="seed">The seed, or <see langword="null"/> to make the draws wait again.</param>
    /// <param name="changes">The count of changes to draw at.</param>
    /// <returns>What the recalculation changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The count is below 0.</exception>
    public SheetChange SetSeed(ulong? seed, long changes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(changes);
        if (seed == Seed && changes == Changes) return SheetChange.None;
        Seed = seed;
        Changes = changes;
        return _randomReaders.Count == 0 ? SheetChange.None : Recalculate([.. _randomReaders], []);
    }

    /// <summary>Does <paramref name="operation"/> as one operation: whatever steps it takes, it counts as one change at most.</summary>
    internal T AsOneChange<T>(Func<T> operation)
    {
        if (_operationDepth++ == 0) _operationCounted = false;
        try
        {
            return operation();
        }
        finally
        {
            _operationDepth--;
        }
    }

    /// <summary>Counts a change (ADR-0180), before the recalculation it brings draws: once for the operation under way, or once on its own.</summary>
    private void CountChange()
    {
        if (_operationDepth > 0)
        {
            if (_operationCounted) return;
            _operationCounted = true;
        }
        Changes++;
    }

    /// <summary>The draw for the call at <paramref name="place"/> in the Formula of <paramref name="cell"/>, or <see langword="null"/> without a seed.</summary>
    private ulong? Draw(CellAddress cell, int place) =>
        Seed is { } seed ? Draws.Of(seed, cell, place, Changes) : null;

    private void RegisterRandomReader(CellAddress formulaCell, Node formula)
    {
        if (CallsRandom(formula)) _randomReaders.Add(formulaCell);
    }

    /// <summary>Whether a Formula draws while the Sheet has no seed: it waits, whichever branch it takes.</summary>
    private bool WaitsForSeed(Node node) => Seed is null && CallsRandom(node);

    private static bool CallsRandom(Node node) =>
        node.Calls.Any(call => call.Function is not null && call.Name is "RAND" or "RANDBETWEEN");
}
