using ExGrid.Selection;

namespace ExGrid.Cells;

/// <summary>
/// A Consumer's declaration that a grid is pointed at from outside (ADR-0058): a Formula edited
/// elsewhere on the page points at this grid's cells, and a press on them is the pointing Sheet's, not
/// the grid's. The Consumer passes one to the grid (<c>ExGrid.PointedAt</c>) and changes it as pointing
/// starts and ends; the grid re-renders when it is told of a change (<see cref="Changed"/>). Any
/// Consumer may make one. The grid learns nothing of Formulas, Sheets or Linked Tables from it.
///
/// <para>While <see cref="IsPointedAt"/> holds, a primary press on the grid's rows or column headers
/// does not act. It moves neither DOM focus nor the Selection and the Focus, runs no sort, column menu,
/// reorder, resize or Heading drag, and is handed to <see cref="OnPress"/> as what it landed on. The
/// root wears <c>ex-pointed-at</c>, and the pointer over the rows and headers is <c>cell</c>. Whether
/// or not it holds, the grid draws the <see cref="Dashes"/> and the <see cref="OutlinedColumns"/>
/// asked for here.</para>
///
/// <para>Changed from the Consumer's own renderer context, as a parameter is. Each property raises
/// <see cref="Changed"/> when it is set to something new, and nothing when it is set to what it
/// holds.</para>
/// </summary>
public sealed class GridPointedAt<TRow>
    where TRow : class
{
    private bool _isPointedAt;
    private string? _pointingRootId;
    private GridPointDashes<TRow>? _dashes;
    private IReadOnlyList<OutlinedColumn>? _outlinedColumns;

    /// <summary>A declaration, not yet pointed at, that hands each press to
    /// <paramref name="onPress"/> once it is.</summary>
    /// <param name="onPress">Receives each press handed over (<see cref="OnPress"/>).</param>
    /// <exception cref="ArgumentNullException">The function is null.</exception>
    public GridPointedAt(Func<GridPointedPress<TRow>, Task> onPress)
    {
        ArgumentNullException.ThrowIfNull(onPress);
        OnPress = onPress;
    }

    /// <summary>
    /// Receives each primary press the grid hands over while it is pointed at: once for the press,
    /// and once more, with <see cref="GridPointedPressKind.SeveralCells"/> or
    /// <see cref="GridPointedPressKind.SeveralColumns"/> and <see cref="GridPointedPress{TRow}.Dragged"/>,
    /// if that press is then dragged onto another cell or column. A secondary press is not handed
    /// over, and does nothing. The press has been answered once the task completes.
    ///
    /// <para>A press is handed over when the browser dispatched it to a grid painted as pointed at.
    /// Within the round trip after <see cref="IsPointedAt"/> changes, on a Server circuit, a press
    /// keeps the meaning the grid on screen had: one made before the grid was painted pointed at is an
    /// ordinary press, and one made before it was painted otherwise is still handed over, for the
    /// Consumer to take or leave (ADR-0058, "On a circuit").</para>
    /// </summary>
    public Func<GridPointedPress<TRow>, Task> OnPress { get; }

    /// <summary>
    /// Whether the grid is pointed at now. False — the default — leaves every press the grid's own.
    ///
    /// <para>A grid with an open edit of its own is not pointed at, whatever this says: a press on
    /// it goes to its edit (ADR-0058, ADR-0018 section 7). The grid reads its own edit, at once, so
    /// the Consumer need not follow it; the declaration takes effect again when the edit ends.</para>
    /// </summary>
    public bool IsPointedAt
    {
        get => _isPointedAt;
        set
        {
            if (_isPointedAt == value)
                return;
            _isPointedAt = value;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// The root of the grid that points — the grid whose edit a press handed over here writes into
    /// — named by its <c>ExGrid.RootId</c>, or null — the default — for none. While the grid is
    /// pointed at, its render names this root, and its script tells that root of each primary press
    /// it hands over, so that the press keeps its place among the keys typed there (ADR-0058, "On a
    /// circuit"; ADR-0021's note of 2026-09-30): the press is handed to <see cref="OnPress"/> only
    /// once that grid has handed on the keys typed before it, and the keys typed after it wait there
    /// until the task <see cref="OnPress"/> returned has completed. With none named, each press is
    /// handed over at once, and on a circuit a key typed meanwhile can reach the pointing grid's
    /// field before the text written for the press.
    /// </summary>
    public string? PointingRootId
    {
        get => _pointingRootId;
        set
        {
            if (string.Equals(_pointingRootId, value, StringComparison.Ordinal))
                return;
            _pointingRootId = value;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// The dashes to draw, over a cell or down a column (<see cref="GridPointDashes{TRow}"/>), or
    /// null — the default — for none. Drawn whether or not the grid is pointed at: the grid draws what
    /// it is told, and the Consumer takes the dashes away when pointing ends.
    /// </summary>
    public GridPointDashes<TRow>? Dashes
    {
        get => _dashes;
        set
        {
            if (Equals(_dashes, value))
                return;
            _dashes = value;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Columns to outline, each in a colour, drawn exactly as the grid's own
    /// <c>OutlinedColumns</c> parameter draws them (ADR-0057), after any that parameter lists: a
    /// column asked for by both is outlined twice, the later over the earlier. Null or empty — the
    /// default — outlines nothing more. Drawn whether or not the grid is pointed at, since the columns
    /// a Formula reads stay outlined while its edit is open. The list instance is the change signal:
    /// set a new list for each change, never an edited one.
    /// </summary>
    /// <exception cref="ArgumentNullException">The list holds a null column (ADR-0057).</exception>
    public IReadOnlyList<OutlinedColumn>? OutlinedColumns
    {
        get => _outlinedColumns;
        set
        {
            if (ReferenceEquals(_outlinedColumns, value))
                return;
            if (value is not null)
            {
                for (var i = 0; i < value.Count; i++)
                {
                    if (value[i] is null)
                        throw new ArgumentNullException(nameof(value), "OutlinedColumns holds a null column (ADR-0057).");
                }
            }
            _outlinedColumns = value;
            Changed?.Invoke();
        }
    }

    /// <summary>Raised after <see cref="IsPointedAt"/>, <see cref="PointingRootId"/>,
    /// <see cref="Dashes"/> or <see cref="OutlinedColumns"/> changed, so the grid repaints.</summary>
    public event Action? Changed;

    // The grid this declaration is passed to, which answers the requests below: set as the grid
    // takes the declaration, and cleared as it lets it go.
    internal IPointedAtGrid<TRow>? Grid { get; set; }

    /// <summary>
    /// Asks the grid this declaration is passed to for the cell one step from a cell (ADR-0058, "The
    /// keyboard"; DC-55), as arrow keys pressed elsewhere move what a press on the grid pointed at: up
    /// or down by one row in the grid's current order, or left or right to the nearest column that
    /// <paramref name="isColumn"/> answers true for, passing over the others. At the first or last row,
    /// or with no such column that way, there is none (<see cref="GridPointedStepKind.Edge"/>); a row
    /// that has not arrived is answered as such (<see cref="GridPointedStepKind.RowNotArrived"/>). The
    /// grid answers while it is pointed at, and moves nothing: neither its Selection nor its scroll.
    /// </summary>
    /// <param name="isRow">Which row the cell to step from is in: the row, of those the grid holds, it
    /// answers true for — named by something the Consumer reads from it, such as its key, so that it
    /// is found through a sort and through a Window of new instances. It must be cheap and must not
    /// throw.</param>
    /// <param name="column">The name of the column the cell to step from is in, as
    /// <see cref="GridColumn{TRow}.Name"/> names it.</param>
    /// <param name="direction">Which way to step.</param>
    /// <param name="isColumn">Which columns a step left or right may land on, by name. Not asked for
    /// a step up or down, which stays in <paramref name="column"/>.</param>
    /// <returns>The cell one step away, or why there is none — <see cref="GridPointedStepKind.NotHeld"/>
    /// when the grid does not hold the cell to step from, is not pointed at, or is not given this
    /// declaration.</returns>
    /// <exception cref="ArgumentNullException">A function or the name is null.</exception>
    /// <exception cref="ArgumentException">The name is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The direction is not one of
    /// <see cref="GridDirection"/>'s.</exception>
    public Task<GridPointedStep<TRow>> StepAsync(Func<TRow, bool> isRow, string column, GridDirection direction, Func<string, bool> isColumn)
    {
        ArgumentNullException.ThrowIfNull(isRow);
        ArgumentException.ThrowIfNullOrEmpty(column);
        ArgumentNullException.ThrowIfNull(isColumn);
        if (!Enum.IsDefined(direction))
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "An arrow's direction is Up, Down, Left or Right (ADR-0012).");
        return Grid is { } grid
            ? grid.StepAsync(isRow, column, direction, isColumn)
            : Task.FromResult(new GridPointedStep<TRow>(GridPointedStepKind.NotHeld));
    }

    /// <summary>
    /// Asks the grid this declaration is passed to for what one step from a whole column reaches
    /// (ADR-0058, "The keyboard", as Part B of the ninth Windows run settled it; DC-55), as arrow keys
    /// pressed elsewhere move what a press on the grid's column header pointed at. Down reaches the
    /// column's first row in the grid's current order, as a cell (<see cref="GridPointedStepKind.Cell"/>),
    /// or <see cref="GridPointedStepKind.RowNotArrived"/> while that row is a Placeholder. Left or right
    /// reaches the nearest column that <paramref name="isColumn"/> answers true for, passing over the
    /// others, as a column (<see cref="GridPointedStepKind.Column"/>). Up, and left or right with no
    /// such column that way, and down in a grid with no rows, are an edge
    /// (<see cref="GridPointedStepKind.Edge"/>). The grid answers while it is pointed at, and moves
    /// nothing: neither its Selection nor its scroll.
    /// </summary>
    /// <param name="column">The name of the column to step from, as
    /// <see cref="GridColumn{TRow}.Name"/> names it.</param>
    /// <param name="direction">Which way to step.</param>
    /// <param name="isColumn">Which columns a step left or right may land on, by name. Not asked for
    /// a step up or down.</param>
    /// <returns>The cell or the column one step away, or why there is none —
    /// <see cref="GridPointedStepKind.NotHeld"/> when the grid shows no column of that name, is not
    /// pointed at, or is not given this declaration.</returns>
    /// <exception cref="ArgumentNullException">The function or the name is null.</exception>
    /// <exception cref="ArgumentException">The name is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The direction is not one of
    /// <see cref="GridDirection"/>'s.</exception>
    public Task<GridPointedStep<TRow>> StepFromColumnAsync(string column, GridDirection direction, Func<string, bool> isColumn)
    {
        ArgumentException.ThrowIfNullOrEmpty(column);
        ArgumentNullException.ThrowIfNull(isColumn);
        if (!Enum.IsDefined(direction))
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "An arrow's direction is Up, Down, Left or Right (ADR-0012).");
        return Grid is { } grid
            ? grid.StepFromColumnAsync(column, direction, isColumn)
            : Task.FromResult(new GridPointedStep<TRow>(GridPointedStepKind.NotHeld));
    }

    /// <summary>
    /// Asks the grid this declaration is passed to to scroll a cell into view (ADR-0058, "The
    /// keyboard"; DC-55), as Point scrolls to keep its pointed cell in view (ADR-0051): the cell arrow
    /// keys pressed elsewhere have just moved to. It moves the grid's view only — not its Selection,
    /// and not DOM focus — and turns the page where a <c>PageSize</c> pages the grid. A cell already in
    /// view scrolls nothing. The dashes are not scrolled to (ADR-0057): only this request scrolls.
    /// </summary>
    /// <param name="isRow">Which row the cell is in, as <see cref="StepAsync"/> names it.</param>
    /// <param name="column">The name of the column the cell is in.</param>
    /// <returns>Whether the grid holds the cell, and so brought it into view: false when no row it holds
    /// is the one named, it shows no column of that name, or it is not given this declaration.</returns>
    /// <exception cref="ArgumentNullException">The function or the name is null.</exception>
    /// <exception cref="ArgumentException">The name is empty.</exception>
    public Task<bool> RevealAsync(Func<TRow, bool> isRow, string column)
    {
        ArgumentNullException.ThrowIfNull(isRow);
        ArgumentException.ThrowIfNullOrEmpty(column);
        return Grid is { } grid ? grid.RevealAsync(isRow, column) : Task.FromResult(false);
    }
}

/// <summary>What a grid given a <see cref="GridPointedAt{TRow}"/> answers for it (DC-55).</summary>
internal interface IPointedAtGrid<TRow>
    where TRow : class
{
    /// <summary>See <see cref="GridPointedAt{TRow}.StepAsync"/>.</summary>
    Task<GridPointedStep<TRow>> StepAsync(Func<TRow, bool> isRow, string column, GridDirection direction, Func<string, bool> isColumn);

    /// <summary>See <see cref="GridPointedAt{TRow}.StepFromColumnAsync"/>.</summary>
    Task<GridPointedStep<TRow>> StepFromColumnAsync(string column, GridDirection direction, Func<string, bool> isColumn);

    /// <summary>See <see cref="GridPointedAt{TRow}.RevealAsync"/>.</summary>
    Task<bool> RevealAsync(Func<TRow, bool> isRow, string column);
}
