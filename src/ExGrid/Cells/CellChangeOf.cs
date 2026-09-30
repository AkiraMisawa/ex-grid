namespace ExGrid.Cells;

/// <summary>
/// How the grid asks when a cell's shown value last changed, for the Change Highlight
/// (ADR-0067): by (row, column), as <see cref="CellStateOf{TRow}"/> asks for Cell State
/// (ADR-0006). The answer is the time the value the cell shows last changed, or null for a
/// cell nobody said changed. The grid never compares values itself: it holds none between
/// Windows, and Row Identity is a reference (ADR-0003). The Consumer answers from what it
/// knows — a server's notice that a trade's P&amp;L moved is enough.
///
/// <para><b>Keep it light.</b> It is asked once per painted value cell on every render of
/// its row — roughly one dictionary probe, with no string building and no allocation, as a
/// Cell State lookup is.</para>
///
/// <para><b>The delegate's identity is the change signal.</b> Hold it in a field and hand
/// over a <em>new</em> one when the times behind it change: the painted rows then ask again.
/// Rewriting what an unchanged delegate answers leaves the marks on screen as they were,
/// deliberately, exactly as it does for <see cref="CellStateOf{TRow}"/>. A row the grid
/// repaints for a reason of its own — a new row instance, a scroll that brings it into view —
/// asks again whatever the delegate is.</para>
/// </summary>
/// <param name="row">The row being painted.</param>
/// <param name="column">The column of the cell.</param>
/// <returns>When the cell's shown value last changed, or null.</returns>
public delegate DateTimeOffset? CellChangeOf<TRow>(TRow row, GridColumn<TRow> column);
