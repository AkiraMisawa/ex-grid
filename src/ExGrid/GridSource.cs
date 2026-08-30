namespace ExGrid;

/// <summary>
/// The bundled convenience layer on top of the push interface (ADR-0001). It drives the
/// push form internally; there is only one behavioural path. <c>Fetch</c> (server paging)
/// arrives in a later change.
/// </summary>
public static class GridSource
{
    public static InMemoryGridSource<TRow> From<TRow>(IReadOnlyList<TRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return new InMemoryGridSource<TRow>(rows);
    }
}
