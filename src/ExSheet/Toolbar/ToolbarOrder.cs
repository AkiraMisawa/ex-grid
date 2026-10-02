namespace ExSheet;

/// <summary>
/// Where a Toolbar Row or a Toolbar Item stands on the Sheet Toolbar, as a path through the
/// components that hold it (ADR-0100). Blazor initialises the components one render places in that
/// render's order, but a component nested a level deeper is initialised after its parent's later
/// siblings: the items of <see cref="DefaultToolbarRow"/> after a Consumer row beside it. So each
/// container cascades a scope, and a child takes the next place in the nearest one when it is first
/// initialised; the toolbar's keys follow the paths, which are the markup's order. A component of
/// the Consumer's own that holds items stands inside a <see cref="ToolbarRow"/>, whose scope it then
/// shares.
/// </summary>
public sealed class ToolbarOrder : IComparable<ToolbarOrder>
{
    private readonly int[] _path;
    private int _next;

    internal ToolbarOrder() => _path = [];

    private ToolbarOrder(int[] path) => _path = path;

    /// <summary>The next place in this scope.</summary>
    internal ToolbarOrder Next() => new([.. _path, _next++]);

    /// <inheritdoc />
    public int CompareTo(ToolbarOrder? other)
    {
        if (other is null) return 1;
        for (var at = 0; at < Math.Min(_path.Length, other._path.Length); at++)
        {
            var compared = _path[at].CompareTo(other._path[at]);
            if (compared != 0) return compared;
        }
        return _path.Length.CompareTo(other._path.Length);
    }
}
