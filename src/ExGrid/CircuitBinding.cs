namespace ExGrid;

/// <summary>
/// Implemented by the bundled Grid Sources, which hold their Sort, Filter and Window as
/// mutable state for one circuit (ADR-0018 §5). Internal on purpose: a Consumer's own
/// <see cref="IGridSource{TRow}"/> may be written to be shared, and the grid cannot tell
/// that one from one that was not, so it does not check it.
/// </summary>
internal interface IBindsToOneCircuit
{
    CircuitBinding Binding { get; }
}

/// <summary>
/// Which renderer's grids hold a bundled source. A renderer is identified by the
/// synchronisation context its grids render on — one per circuit on Blazor Server; none
/// at all on WebAssembly, where there is one renderer in the tab — so two grids on one
/// page never trip it, and a source registered once for a whole Server application does
/// on the second user's grid.
/// </summary>
internal sealed class CircuitBinding
{
    private readonly object _gate = new();
    private readonly HashSet<object> _grids = new(ReferenceEqualityComparer.Instance);
    private SynchronizationContext? _owner;

    public void Attach(object grid, SynchronizationContext? context)
    {
        lock (_gate)
        {
            if (_grids.Count > 0 && !ReferenceEquals(_owner, context))
                throw new InvalidOperationException(
                    "This Grid Source is already bound to a grid rendering on another renderer — on Blazor " +
                    "Server, another user's circuit. A bundled Grid Source holds its Sort, Filter and Window " +
                    "for one circuit, without locks, so sharing it would let one user's sort reorder another " +
                    "user's grid. Create a Grid Source per circuit and share the data behind it instead " +
                    "(ADR-0018).");
            _owner = context;
            _grids.Add(grid);
        }
    }

    public void Detach(object grid)
    {
        lock (_gate)
        {
            _grids.Remove(grid);
            if (_grids.Count == 0)
                _owner = null;
        }
    }
}
