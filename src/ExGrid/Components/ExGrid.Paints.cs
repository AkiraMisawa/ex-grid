using ExGrid.Cells;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ExGrid.Components;

// The paints a gesture is told against (ADR-0142, rewritten 2026-10-07; ADR-0021's notes of
// 2026-10-05 to 2026-10-07). Each render that paints another order, other row instances or other
// columns is a new paint, named on the Viewport (data-ex-paint, beside data-ex-sequence and
// data-ex-layout); the browser reads that name with a key, a paste or a press on an action and tells it
// with the gesture. The grid uses it for which order a gesture was aimed under (ADR-0011) and which row
// component painted the button a press was made on (ExGrid.ActionPress.cs), never for what a cell
// holds. Of its last paints it keeps the first paint under the order in force, and for each its first
// row, its columns and a serial for each painted row's component; Row Keys only of the rows painted
// now, and never a row (ADR-0160).
public partial class ExGrid<TRow>
{
    // What a gesture says when the browser told no paint: a press made by script with no mousedown
    // before it, or a component test driving the core directly. It is taken as aimed at the newest
    // paint — what the core last painted.
    private const int PaintNotTold = -1;

    // The newest paint's name, and what it was painted from. A render painted from the same is the
    // same paint, and keeps the name.
    private int _paintId;
    private PaintBasis? _painted;

    // The first paint painted under the order the newest paint was painted under: a gesture told an
    // earlier one was aimed under another order (ADR-0011).
    private int _orderPaintedFrom;

    // Moved whenever a new Window replaces a row instance at a position the newest paint painted. It
    // stands in for the rows of that paint, which the grid does not keep (ADR-0160): compared while
    // the old Window and the new one are both in hand, never later.
    private int _paintedRowsVersion;

    /// <summary>What a paint was painted from, as far as its order and its row components go: the
    /// order, the painted rows, the columns their actions come from, and the Row Key the components
    /// are keyed by. References and numbers only, never a row: the painted rows are named by
    /// <see cref="RowsVersion"/>.</summary>
    private sealed record PaintBasis(
        int Sequence, int FirstRow, int Count, int RowsVersion, IReadOnlyList<GridColumn<TRow>> Columns,
        Func<TRow, object>? RowKey)
    {
        /// <summary>Whether a render painted from <paramref name="other"/> paints what this one
        /// painted: the columns compare by reference, as the rows render, and the Row Key as Blazor
        /// compares a delegate, by its method and target.</summary>
        public bool Paints(PaintBasis other)
            => Sequence == other.Sequence && FirstRow == other.FirstRow && Count == other.Count
               && RowsVersion == other.RowsVersion && ReferenceEquals(Columns, other.Columns) && Equals(RowKey, other.RowKey);
    }

    /// <summary>
    /// Names the paint this render paints, starting a new one if it paints another order, other row
    /// instances, other columns or rows keyed otherwise (ADR-0142). Called from the Viewport's own
    /// attribute, so whatever path led to the render, the name is computed from the very state the
    /// rows below it are painted from. A render that changes none of them — a Selection moved, a
    /// popover opened, a row off screen replaced — keeps the name.
    /// </summary>
    private int NotePaint()
    {
        EndRowComponents();
        var basis = new PaintBasis(_sequenceVersion, _visible?.Start ?? 0, _visible?.Count ?? 0, _paintedRowsVersion,
            Columns, _rowKey);
        if (_painted is { } last && last.Paints(basis))
            return _paintId;
        var id = ++_paintId;
        if (_painted is null || _painted.Sequence != basis.Sequence)
            _orderPaintedFrom = id;
        _painted = basis;
        StartRowComponents(id, basis.FirstRow, basis.Count);
        return id;
    }

    /// <summary>
    /// Notes whether the Window just taken in replaced a row instance at a position the newest paint
    /// painted, comparing it with <paramref name="previous"/>, the Window it replaced, while both are
    /// in hand (ADR-0160). A pass over the painted positions only.
    /// </summary>
    private void NotePaintedRowsReplaced(IReadOnlyList<TRow> previous, int previousStart)
    {
        if (_painted is not { } painted || (ReferenceEquals(previous, _window) && previousStart == _windowStart))
            return;
        for (var row = painted.FirstRow; row < painted.FirstRow + painted.Count; row++)
        {
            if (!ReferenceEquals(RowOf(previous, previousStart, row), RowInHand(row)))
            {
                _paintedRowsVersion++;
                return;
            }
        }
    }

    /// <summary>The row <paramref name="window"/>, starting at <paramref name="start"/>, holds at
    /// absolute position <paramref name="row"/>, or null.</summary>
    private static TRow? RowOf(IReadOnlyList<TRow> window, int start, int row)
    {
        var slice = row - start;
        return slice >= 0 && slice < window.Count ? window[slice] : null;
    }

    /// <summary>Whether a gesture told the paint <paramref name="told"/> was aimed under an order
    /// that has moved since (ADR-0011): its positions name other rows now. One nobody told of is
    /// taken as aimed at the newest paint, under the order in force.</summary>
    private bool AimedUnderAnotherOrder(int told)
        => told != PaintNotTold
           && (told < _orderPaintedFrom || _painted is not { } newest || newest.Sequence != _sequenceVersion);

    /// <summary>The row the Window holds at absolute position <paramref name="row"/>, or null.</summary>
    private TRow? RowInHand(int row) => RowOf(_window, _windowStart, row);

    /// <summary>
    /// The painted text of one value cell: the Consumer's painted text where it supplies one
    /// (ADR-0050, item 11), fitted to the column's width with the bold widths for a bold cell, and
    /// the value's own text otherwise — what <c>ExGridRow</c> paints, before the <c>####</c>
    /// decision. A Number or Date the column is too narrow for paints <c>####</c>, but its value's
    /// text is still its accessible name and the Formula Bar's, so a change hidden behind
    /// <c>####</c> is a change (principle 1). Null for an Action, Template or Mark cell, which
    /// paints no text.
    /// </summary>
    private static string? PaintedTextFor(TRow row, GridColumn<TRow> column, int index, ColumnGeometry geometry,
        CellTextMetrics metrics, PaintedTextOf<TRow>? paintedText, CellAppearanceOf<TRow>? appearance)
    {
        if (!column.PaintsValue)
            return null;
        var text = column.Info.TextFor(column.Value(row));
        if (paintedText is null)
            return text;
        var measured = appearance?.Invoke(row, column).Bold == true ? metrics.Bold : metrics;
        return paintedText(row, column, measured.ContentWidthPx(geometry.DeclaredWidthPxOf(index)), measured) ?? text;
    }

    /// <summary>The painted text the cell at (<paramref name="row"/>, <paramref name="column"/>)
    /// has now, from the row the Window holds there; null with no row in hand, or for a cell that
    /// paints no text.</summary>
    private string? PaintedTextNow(int row, int column)
        => column >= 0 && column < Columns.Count && RowInHand(row) is { } data
            ? PaintedTextFor(data, Columns[column], column, _columnStyles.Geometry, _metrics.CellMetrics, PaintedText, CellAppearance)
            : null;

    /// <summary>
    /// Refuses a positional write whose gesture was aimed under an order that has moved since
    /// (ADR-0142, LV-13; ADR-0011), through <see cref="OnPasteRefused"/> — the one gate paste, fill
    /// and Delete go through (ADR-0035). The Selection the gesture was aimed with went with that
    /// order, so the write is refused as aimed at no Selection. Answers whether it refused.
    /// </summary>
    private async Task<bool> RefuseWriteAimedUnderAnotherOrderAsync(int told)
    {
        if (!AimedUnderAnotherOrder(told))
            return false;
        if (OnPasteRefused.HasDelegate)
            await OnPasteRefused.InvokeAsync(PasteRefusalReason.EmptySelection);
        return true;
    }

    // ---- The row components each paint painted ----

    // How many paints keep their row components' serials, for a press told one of them. A paint is a
    // render that changed what a painted cell can show — a scroll step, a new row instance on screen —
    // so a fling makes one a frame; a press is told a few round trips after it is made at most. A
    // press told a paint older than all of them waits for its click.
    private const int PaintRowsKept = 64;

    /// <summary>A paint's row components: the serial of the component each painted position
    /// rendered (0 for a Placeholder), and the columns it painted their actions from.</summary>
    private sealed record PaintRows(int Id, int FirstRow, int[] Serials, IReadOnlyList<GridColumn<TRow>> Columns);

    private readonly List<PaintRows> _paintRows = [];

    // Each painted row component's serial, by its component key — its Row Key, or the object that
    // stands for its instance (ADR-0140) — for the newest paint, and for the one before while the
    // newest is being painted. Rebuilt from the rows each new paint paints, so they hold the keys of
    // rows painted now and nothing older (ADR-0160). Kept only while a column has actions: a serial
    // serves an Action press alone.
    private Dictionary<object, int> _rowSerials = [];
    private Dictionary<object, int> _previousRowSerials = [];
    private int[]? _paintingSerials;
    private int _paintingFirstRow;
    private int _lastRowSerial;

    private void StartRowComponents(int id, int first, int count)
    {
        if (!Columns.Any(static c => c.Actions.Count > 0))
        {
            _paintRows.Clear();
            _rowSerials.Clear();
            return;
        }
        (_previousRowSerials, _rowSerials) = (_rowSerials, _previousRowSerials);
        _rowSerials.Clear();
        _paintingSerials = new int[count];
        _paintingFirstRow = first;
        _paintRows.Add(new PaintRows(id, first, _paintingSerials, Columns));
        if (_paintRows.Count > PaintRowsKept)
            _paintRows.RemoveAt(0);
        // A paint of no rows paints no row components: the keys of the paint before go now, as no
        // loop over the rows will end it.
        if (count == 0)
            EndRowComponents();
    }

    /// <summary>A painted row's component key (<see cref="RowComponentKey"/>), noting the
    /// component's serial while a new paint is painted: the same key as in the paint before is the
    /// same component, which Blazor keeps (ADR-0140/0003).</summary>
    private object PaintedRowKey(int position, TRow row, int index)
    {
        var key = RowComponentKey(row, index);
        if (_paintingSerials is { } serials && position - _paintingFirstRow is var at && at >= 0 && at < serials.Length)
        {
            if (!_previousRowSerials.TryGetValue(key, out var serial))
                serial = ++_lastRowSerial;
            serials[at] = serial;
            _rowSerials[key] = serial;
        }
        return key;
    }

    /// <summary>Ends a new paint's rows: the keys of the paint before go.</summary>
    private void EndRowComponents()
    {
        _paintingSerials = null;
        _previousRowSerials.Clear();
    }

    /// <summary>Whether the newest paint rendered a row component for <paramref name="row"/>: under
    /// its Row Key, or, with none, for the instance itself — by reference, never by value (ADR-0003).</summary>
    private bool RendersRowComponentOf(TRow row) => _rowSerials.ContainsKey(ComponentIdentityOf(row));

    /// <summary>What names <paramref name="row"/>'s component, as <see cref="RowComponentKey"/>
    /// keys it, without checking it against the keys painted: for a lookup outside a render.</summary>
    private object ComponentIdentityOf(TRow row)
        => _rowKey is { } rowKey ? rowKey(row) : RowIdentityKeys.GetValue(row, static _ => new object());

    /// <summary>Where the Window holds the row under <paramref name="key"/>: at
    /// <paramref name="hint"/>, where it stood, if it still stands there, and wherever else it
    /// is otherwise (<see cref="PositionInHandByKey"/>).</summary>
    private int? PositionOfKey(Func<TRow, object> rowKey, object key, int? hint)
        => hint is { } at && RowInHand(at) is { } there && Equals(rowKey(there), key) ? at : PositionInHandByKey(rowKey, key);

    /// <summary>Where the Window holds the row under <paramref name="key"/>: among the rows painted
    /// now first, where a pressed row almost always is, and across the whole Window otherwise —
    /// a row the order moved out of view is still the row the user pressed. Null when the Window
    /// no longer holds it. A key repeats in no Window (ADR-0140, LV-2), so the answer is
    /// unambiguous.</summary>
    private int? PositionInHandByKey(Func<TRow, object> rowKey, object key)
    {
        if (PaintedPositionWhere(row => Equals(rowKey(row), key)) is { } painted)
            return painted;
        for (var i = 0; i < _window.Count; i++)
        {
            if (Equals(rowKey(_window[i]), key))
                return _windowStart + i;
        }
        return null;
    }

    /// <summary>Where the Window holds <paramref name="row"/> among the rows painted now, or null.
    /// The Window never holds one instance twice (ADR-0003), so the answer is unambiguous.</summary>
    private int? PositionInHand(TRow row) => PaintedPositionWhere(painted => ReferenceEquals(painted, row));

    /// <summary>The first position painted now whose row in hand <paramref name="matches"/>, or null.</summary>
    private int? PaintedPositionWhere(Func<TRow, bool> matches)
    {
        if (_visible is not { } visible)
            return null;
        for (var position = visible.Start; position < visible.Start + visible.Count; position++)
        {
            if (RowInHand(position) is { } row && matches(row))
                return position;
        }
        return null;
    }
}
