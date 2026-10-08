using ExGrid.Clipboard;
using ExGrid.Selection;
using Microsoft.JSInterop;

namespace ExGrid.Components;

// The Copied Range (ADR-0170): what the grid copied, outlined with dashes for as long as the
// clipboard still holds it. A copy's outline is made with its payload and kept only once the script
// says the write landed. The script also says when the clipboard changed by any write but this
// grid's own copy, through its clipboardchange listener (ADR-0021's clipboard entry); a browser
// without that event never reports a landing, so it shows no outline. The other ends are the grid's
// to see: Escape, an edit opening, the coordinates dropped as the Selection's are, and a copied cell
// that reads other text than it was copied with.
public partial class ExGrid<TRow>
{
    // The copy built last, until its write lands or another is built; and the one outlined.
    private CopiedRange? _builtCopy;
    private CopiedRange? _copied;
    private int _copyLandings;

    /// <summary>The rectangles the Copied Range outlines, or none (ADR-0170).</summary>
    private IReadOnlyList<SelectionRange> CopiedRectangles => _copied?.Rectangles ?? [];

    /// <summary>
    /// A payload made ready to outline (ADR-0170): numbered, with the copy it carries kept until the
    /// script says its write landed. For each copied row the grid has — <paramref name="rowAt"/>
    /// answers null for one it does not — the painted text of its copied cells is kept as a
    /// fingerprint, which is what a later read of the row is compared with. A refusal, and the
    /// asynchronous route's "ask again", carry nothing to outline.
    /// </summary>
    private ClipboardPayload Outlined(ClipboardPayload payload, CopyPlan plan, Func<int, TRow?> rowAt)
    {
        if (payload.Kind != "data")
            return payload;
        return payload with { Landing = PrepareCopyOutline(plan, rowAt) };
    }

    /// <summary>Captures the copy's coordinates and painted text before an asynchronous Consumer
    /// answer can yield (ADR-0152, ADR-0170): a later render or copy invalidates this landing by the
    /// same rules as a payload waiting for the browser's write.</summary>
    private int PrepareCopyOutline(CopyPlan plan, Func<int, TRow?> rowAt)
    {
        var rectangles = plan.Segments.ToArray();
        var fingerprints = new int[rectangles.Length][];
        for (var k = 0; k < rectangles.Length; k++)
        {
            var rectangle = rectangles[k];
            var prints = fingerprints[k] = new int[rectangle.RowCount];
            for (var i = 0; i < prints.Length; i++)
            {
                if (rowAt(rectangle.TopRow + i) is { } row)
                    prints[i] = Fingerprint(row, rectangle);
            }
        }
        _builtCopy = new CopiedRange(++_copyLandings, rectangles, _sequenceVersion, fingerprints);
        return _copyLandings;
    }

    /// <summary>
    /// The painted text of a row's cells across a rectangle's columns, as one number: never 0, which
    /// stands for a row the grid did not have when it copied (ADR-0170).
    /// </summary>
    private int Fingerprint(TRow row, SelectionRange rectangle)
    {
        var hash = new HashCode();
        for (var c = rectangle.LeftColumn; c <= rectangle.RightColumn; c++)
            hash.Add(ExGridRow<TRow>.CellText(Columns[c], row), StringComparer.Ordinal);
        var print = hash.ToHashCode();
        return print == 0 ? 1 : print;
    }

    /// <summary>
    /// A copy's write landed (ADR-0170): its outline replaces the one before, unless another copy
    /// was built since. A later build will land or fail on its own, and an outline of the earlier
    /// one would stand round what the clipboard is about to stop holding.
    ///
    /// <para>Called by the grid's own script module and not for Consumers: it is public only because
    /// JavaScript interop requires it.</para>
    /// </summary>
    /// <param name="landing">The number the copy's payload carried.</param>
    [JSInvokable]
    public Task OnCopyLandedAsync(int landing)
    {
        if (_disposed || _builtCopy is not { } built || built.Number != landing)
            return Task.CompletedTask;
        _builtCopy = null;
        if (built.Version != _sequenceVersion)
            return Task.CompletedTask;
        _copied = built;
        // Checked at once against the Window it lands in: the asynchronous route gathered its rows
        // while the Window went on changing.
        ReconcileCopiedRange(columnsMoved: false);
        _suppressRender = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    /// <summary>
    /// The clipboard changed by a write other than this grid's own copy — another application,
    /// another grid, a text field — so it no longer holds what the outline marks (ADR-0170).
    ///
    /// <para>Called by the grid's own script module and not for Consumers: it is public only because
    /// JavaScript interop requires it.</para>
    /// </summary>
    [JSInvokable]
    public Task OnClipboardChangedAsync()
    {
        if (!_disposed && DropCopiedRange())
        {
            _suppressRender = false;
            StateHasChanged();
        }
        return Task.CompletedTask;
    }

    /// <summary>Removes the outline, if there is one, and says whether there was (ADR-0170).</summary>
    private bool DropCopiedRange()
    {
        if (_copied is null)
            return false;
        _copied = null;
        return true;
    }

    /// <summary>
    /// Keeps the outline only while it marks what the clipboard holds (ADR-0170): dropped when the
    /// coordinates stop meaning what they meant, as the Selection is (ADR-0011), and when a copied
    /// row the grid has now paints other text in its copied cells than it was copied with (the
    /// painted text, not the row object). A row is read again only when it is another instance
    /// than the one last read, or the columns are another list; a row the grid did not have when it
    /// copied carries no fingerprint and is not compared.
    /// </summary>
    private void ReconcileCopiedRange(bool columnsMoved)
    {
        // A copy still being written is written in the columns it was built in: landed under others,
        // its rectangles would stand on other cells. A Row Sequence Version it was not built under
        // is caught when it lands.
        if (columnsMoved)
            _builtCopy = null;
        if (_copied is not { } copied)
            return;
        if (columnsMoved || copied.Version != _sequenceVersion)
        {
            _copied = null;
            return;
        }
        var sameColumns = ReferenceEquals(copied.CheckedColumns, Columns);
        var checkedNow = new Dictionary<int, TRow>();
        for (var i = 0; i < _window.Count; i++)
        {
            var position = _windowStart + i;
            var row = _window[i];
            var reread = !sameColumns || !copied.Checked.TryGetValue(position, out var was) || !ReferenceEquals(was, row);
            var inside = false;
            for (var k = 0; k < copied.Rectangles.Count; k++)
            {
                var rectangle = copied.Rectangles[k];
                if (position < rectangle.TopRow || position > rectangle.BottomRow)
                    continue;
                inside = true;
                var print = copied.Fingerprints[k][position - rectangle.TopRow];
                if (reread && print != 0 && print != Fingerprint(row, rectangle))
                {
                    _copied = null;
                    return;
                }
            }
            if (inside)
                checkedNow[position] = row;
        }
        copied.Checked = checkedNow;
        copied.CheckedColumns = Columns;
    }

    /// <summary>
    /// A copy as the clipboard holds it (ADR-0170): its rectangles, the Row Sequence Version they
    /// were written under, and a fingerprint per copied row and rectangle (0 for a row the grid did
    /// not have). <see cref="Checked"/> is the instance last read at each position in the Window, so
    /// a row that did not change is not read again.
    /// </summary>
    private sealed class CopiedRange(int number, IReadOnlyList<SelectionRange> rectangles, int version, int[][] fingerprints)
    {
        public int Number { get; } = number;
        public IReadOnlyList<SelectionRange> Rectangles { get; } = rectangles;
        public int Version { get; } = version;
        public int[][] Fingerprints { get; } = fingerprints;
        public Dictionary<int, TRow> Checked { get; set; } = [];
        public IReadOnlyList<GridColumn<TRow>>? CheckedColumns { get; set; }
    }
}
