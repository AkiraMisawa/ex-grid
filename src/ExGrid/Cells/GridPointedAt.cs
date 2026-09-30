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

    /// <summary>Raised after <see cref="IsPointedAt"/>, <see cref="Dashes"/> or
    /// <see cref="OutlinedColumns"/> changed, so the grid repaints.</summary>
    public event Action? Changed;
}
