using ExGrid;
using ExGrid.Data;
using ExPivot.Engine;

namespace ExBoard;

/// <summary>
/// An input held where the data lives (ADR-0343): the host's own code, which says the input's shape,
/// fetches a grid's rows and, if it can, makes a pivot's report source. The Board never talks to a server
/// itself; each call here is handed the narrowing the Board's links put on the view, for the host to
/// execute where the data lives.
/// </summary>
/// <param name="shape">Answers the input's shape, before any row (ADR-0341).</param>
/// <param name="fetch">Answers a grid view's query, narrowed by its links, with a page of rows.</param>
public sealed class BoardSource(Func<CancellationToken, ValueTask<DataShape>> shape, Func<BoardQuery, CancellationToken, ValueTask<ShapedPage>> fetch)
    : IShapedSource
{
    private readonly Func<CancellationToken, ValueTask<DataShape>> _shape = shape ?? throw new ArgumentNullException(nameof(shape));

    /// <summary>Answers a grid view's query, narrowed by its links, with a page of rows.</summary>
    public Func<BoardQuery, CancellationToken, ValueTask<ShapedPage>> Fetch { get; } = fetch ?? throw new ArgumentNullException(nameof(fetch));

    /// <summary>
    /// Makes a pivot view's report source over the input's rows that the given conditions let through
    /// (all of them; none for the whole input), or null where the host cannot. Called again each time
    /// the view's narrowing changes. A Board with a pivot over this input is refused when null.
    /// </summary>
    public Func<IReadOnlyList<LinkCondition>, PivotReportSource>? Report { get; init; }

    /// <inheritdoc />
    public async ValueTask<DataShape?> ShapeAsync(CancellationToken cancellationToken = default) => await _shape(cancellationToken).ConfigureAwait(false);
}

/// <summary>
/// What a grid view over a <see cref="BoardSource"/> asks (ADR-0343): the grid's own query, its range, sort
/// and filter (ADR-0025), and the conditions the Board's links narrow the view by now, every one of which
/// a row must meet. Empty while nothing narrows it.
/// </summary>
/// <param name="View">The id of the grid view asking.</param>
/// <param name="Grid">The grid's query.</param>
/// <param name="Narrowing">The links' conditions, each over its link's columns.</param>
public sealed record BoardQuery(string View, GridQuery Grid, IReadOnlyList<LinkCondition> Narrowing);
