namespace ExGrid.Components;

/// <summary>
/// The earliest end among the Change Highlights each painted row shows (ADR-0067). A row
/// tells it what it painted after every render in which that moved, and tells it nothing
/// once it leaves the DOM; the grid keeps one timer, for the earliest end of them all. The
/// rows are the ones that ask the Consumer — once per painted value cell of a row that
/// renders, as for Cell State — so the grid learns where the marks are without asking a
/// second time.
/// </summary>
internal sealed class ChangeHighlightEnds
{
    // Keyed by the row component, not by its data: a component is what paints, and it is
    // what leaves.
    private readonly Dictionary<object, DateTimeOffset> _byRow = new(ReferenceEqualityComparer.Instance);
    private DateTimeOffset? _earliest;
    private bool _stale;

    /// <summary>What one row painted: the earliest end among its marks, or null when it
    /// painted none — or when it has left, and painted nothing more.</summary>
    internal void Painted(object row, DateTimeOffset? earliestEnd)
    {
        if (earliestEnd is { } end)
            _byRow[row] = end;
        else if (!_byRow.Remove(row))
            return;
        _stale = true;
    }

    /// <summary>The earliest end among the marks on screen, or null while none is.</summary>
    internal DateTimeOffset? Earliest
    {
        get
        {
            if (_stale)
            {
                _stale = false;
                _earliest = null;
                foreach (var end in _byRow.Values)
                {
                    if (_earliest is not { } earliest || end < earliest)
                        _earliest = end;
                }
            }
            return _earliest;
        }
    }
}
