using System.Runtime.CompilerServices;
using ExGrid.Cells;
using ExGrid.Clipboard;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ExGrid.Components;

// ADR-0154: values may change while a gesture is in flight. Only its target and command
// must remain identifiable. Display-only grids keep one address epoch, not row/text history.
public partial class ExGrid<TRow>
{
    /// <summary>An Action whose original row or command can no longer be identified (ADR-0154).
    /// Value changes never raise this callback. The refusal carries detached address evidence,
    /// not an obsolete row; business conflicts belong to the Consumer receiving OnAction.</summary>
    [Parameter] public EventCallback<GridActionRefusal> OnActionRefused { get; set; }

    private const int PaintNotTold = -1;
    private const int ActionPaintsKept = 64;
    private static readonly ConditionalWeakTable<object, object> PaintIdentityKeys = new();
    private static object? PaintIdentityOf(object? value)
        => value is null ? null : PaintIdentityKeys.GetValue(value, static _ => new object());

    private sealed record PaintedColumn(string Name, string[] Actions);
    private sealed record PaintedRow(object Identity, object? Key);
    private sealed record Paint(int Id, int SequenceVersion, int FirstRow, PaintedRow?[] Rows,
        PaintedColumn[] Columns, object? KeyIdentity, object ColumnsIdentity);
    private readonly List<Paint> _paints = [];
    private int _paintId;
    // All IDs since the last row/column-order change share one positional address. This
    // survives pruning Action identities without retaining any row or displayed value.
    private int _firstAddressPaintId;
    private int _paintSequence;
    private string[] _paintColumns = [];

    // The DOM attribute keeps its existing name. Its token identifies addresses only.
    private int NotePaint()
    {
        var sameAddress = _paintSequence == _sequenceVersion && SameNames(_paintColumns);
        if (!sameAddress || _paintId == 0) _firstAddressPaintId = _paintId + 1;
        var hasActions = Columns.Any(column => column.Actions.Count != 0);
        if (!hasActions)
        {
            if (!sameAddress || _paintId == 0 || _paints.Count != 0) _paintId++;
            _paints.Clear();
            _paintSequence = _sequenceVersion;
            if (!sameAddress) _paintColumns = Columns.Select(column => column.Name).ToArray();
            return _paintId;
        }
        var first = _visible?.Start ?? _windowStart;
        var count = _visible?.Count ?? 0;
        var last = _paints.LastOrDefault();
        var columnsIdentity = PaintIdentityOf(Columns)!;
        var keyIdentity = PaintIdentityOf(_rowKey);
        if (sameAddress && last is not null && last.FirstRow == first && last.Rows.Length == count
            && ReferenceEquals(last.ColumnsIdentity, columnsIdentity) && ReferenceEquals(last.KeyIdentity, keyIdentity)
            && Enumerable.Range(0, count).All(i => ReferenceEquals(last.Rows[i]?.Identity,
                RowInHand(first + i) is { } row ? RowInstanceKey(row) : null)))
            return last.Id;
        var columns = last is not null && ReferenceEquals(last.ColumnsIdentity, columnsIdentity)
            ? last.Columns : Columns.Select(c => new PaintedColumn(c.Name, c.Actions.Select(a => a.Name).ToArray())).ToArray();
        var rows = new PaintedRow?[count];
        for (var i = 0; i < count; i++)
            if (RowInHand(first + i) is { } row) rows[i] = new(RowInstanceKey(row), _rowKey?.Invoke(row));
        var paint = new Paint(++_paintId, _sequenceVersion, first, rows, columns, keyIdentity, columnsIdentity);
        _paints.Add(paint);
        if (_paints.Count > ActionPaintsKept) _paints.RemoveAt(0);
        _paintSequence = _sequenceVersion;
        if (!sameAddress) _paintColumns = Columns.Select(column => column.Name).ToArray();
        return paint.Id;
    }

    private Paint? PaintNamed(int told) => told == PaintNotTold ? _paints.LastOrDefault() : _paints.FindLast(p => p.Id == told);
    private TRow? RowInHand(int row)
    {
        var index = row - _windowStart;
        return index >= 0 && index < _window.Count ? _window[index] : null;
    }

    private bool AddressStillCurrent(int told)
        => told == PaintNotTold || told >= _firstAddressPaintId && told <= _paintId
            && _paintSequence == _sequenceVersion && SameNames(_paintColumns);

    private async Task<bool> RefuseStaleWriteAsync(int told)
    {
        if (AddressStillCurrent(told)) return false;
        if (OnPasteRefused.HasDelegate) await OnPasteRefused.InvokeAsync(PasteRefusalReason.RenderNoLongerKept);
        return true;
    }

    // Immutable positional evidence for one paste, held only while its stream is read or its
    // Consumer answers. It retains no row, column accessor, or displayed value (ADR-0154).
    private sealed record PasteTarget(GridSelection Selection, int SequenceVersion, string[] Columns,
        int Paint, object? SourceIdentity);

    private PasteTarget CapturePasteTarget(int paint)
        => new(_selection.Selection, _sequenceVersion, _columnNames,
            paint == PaintNotTold ? NotePaint() : paint, PaintIdentityOf(Source));

    // No row instance or accessor is retained. A key is the Consumer's declared identity;
    // a reference token has no path back to its row. Column/action names survive a redeclaration.
    private sealed record ActionTarget(int Paint, int? Sequence, int? Row, object? Identity,
        object? Key, object? KeyDeclaration, string? Column, string? Command);
    private int? _actionPressTold;
    private ActionTarget? _actionPressPending;
    private ActionTarget? _answeredActionPress;

    /// <summary>The original address of an Action press (ADR-0154), captured before Blazor's
    /// click. A disposed or moved row may lose its click; the core resolves this address itself.
    /// Public only for the grid's JavaScript interop, not for Consumers.</summary>
    /// <param name="paint">The address token written on the Viewport.</param>
    /// <param name="row">The original absolute row index, or minus one.</param>
    /// <param name="column">The original column index, or minus one.</param>
    /// <param name="action">The original action index, or minus one.</param>
    [JSInvokable]
    public async Task ActionPressTakenAt(int paint, int row = -1, int column = -1, int action = -1)
    {
        if (_disposed) return;
        _actionPressTold = paint;
        _actionPressPending = null;
        _answeredActionPress = null;
        if (row < 0 || column < 0 || action < 0) return;
        var painted = PaintNamed(paint);
        var index = row - (painted?.FirstRow ?? 0);
        var target = painted is not null && index >= 0 && index < painted.Rows.Length ? painted.Rows[index] : null;
        var declared = painted is not null && column < painted.Columns.Length ? painted.Columns[column] : null;
        _actionPressPending = new(paint, painted?.SequenceVersion, row, target?.Identity, target?.Key,
            painted?.KeyIdentity, declared?.Name, declared is not null && action < declared.Actions.Length ? declared.Actions[action] : null);
        await AnswerActionPressWithNoClickAsync();
    }

    private bool RendersActionTarget(ActionTarget target)
    {
        var current = _paints.LastOrDefault();
        var original = PaintNamed(target.Paint);
        if (current is null || original is null) return false;
        // Redeclaring columns can replace event attributes or move the original command's
        // button. A command existing elsewhere does not prove that its native click survived.
        if (!ReferenceEquals(current.ColumnsIdentity, original.ColumnsIdentity)) return false;
        // A column/command removed since the press can dispose its event attribute too.
        if (!current.Columns.Any(c => c.Name == target.Column && c.Actions.Contains(target.Command))) return false;
        // Moving a keyed component can lose the browser's native click even when the same
        // button survives. Only its original position can still be awaiting that click.
        var index = (target.Row ?? -1) - current.FirstRow;
        if (index < 0 || index >= current.Rows.Length || current.Rows[index] is not { } row) return false;
        return target.Key is not null && ReferenceEquals(current.KeyIdentity, target.KeyDeclaration)
            ? Equals(row.Key, target.Key) : ReferenceEquals(row.Identity, target.Identity);
    }

    private async Task AnswerActionPressWithNoClickAsync()
    {
        if (_actionPressPending is not { } target || _disposed || RendersActionTarget(target)) return;
        _actionPressPending = null;
        _actionPressTold = null;
        _answeredActionPress = target;
        await RaiseAddressedActionAsync(target);
    }

    private ActionTarget CaptureAction(GridActionEventArgs<TRow> args, int told, int? atRow)
    {
        var paint = PaintNamed(told);
        var identity = RowInstanceKey(args.Row);
        var key = _rowKey?.Invoke(args.Row);
        int? row = null;
        if (paint is not null)
        {
            var index = Array.FindIndex(paint.Rows, r => r is not null && (key is not null
                && ReferenceEquals(paint.KeyIdentity, PaintIdentityOf(_rowKey)) ? Equals(r.Key, key) : ReferenceEquals(r.Identity, identity)));
            if (index >= 0) row = paint.FirstRow + index;
        }
        // A click from a replacement component is not evidence of its old position after
        // reordering. The capture listener supplies an explicit address for real presses.
        if (paint is not null && paint.SequenceVersion != _sequenceVersion && row is null && atRow is null)
            return new(told, paint.SequenceVersion, null, null, null, paint.KeyIdentity, args.ColumnName, args.ActionName);
        row ??= atRow ?? PositionInHand(identity);
        // When a newer instance's click supplies the payload, unchanged position evidence
        // still names the original no-key target. A different order cannot supply that proof.
        var original = paint is not null && row is { } r && r >= paint.FirstRow && r < paint.FirstRow + paint.Rows.Length
            ? paint.Rows[r - paint.FirstRow] : null;
        return new(told, paint?.SequenceVersion ?? (told == PaintNotTold ? _sequenceVersion : null), row,
            original?.Identity ?? identity, original?.Key ?? key, paint?.KeyIdentity ?? PaintIdentityOf(_rowKey),
            args.ColumnName, args.ActionName);
    }

    private int? PositionInHand(object identity)
    {
        for (var i = 0; i < _window.Count; i++)
            if (ReferenceEquals(RowInstanceKey(_window[i]), identity)) return _windowStart + i;
        return null;
    }

    private TRow? ResolveActionTarget(ActionTarget target)
    {
        if (target.Command is null || target.Column is null) return null;
        if (!Columns.Any(column => column.Name == target.Column && column.Actions.Any(action => action.Name == target.Command))) return null;
        if (target.Paint != PaintNotTold && target.Sequence is null) return null;
        if (target.Key is not null && _rowKey is { } key && ReferenceEquals(target.KeyDeclaration, PaintIdentityOf(key)))
            return _window.FirstOrDefault(row => Equals(key(row), target.Key));
        if (target.Key is not null) return null; // A different key declaration cannot rebind an old key.
        if (target.Identity is { } identity && PositionInHand(identity) is { } current) return RowInHand(current);
        return target.Sequence == _sequenceVersion && target.Row is { } position ? RowInHand(position) : null;
    }

    private async Task FireOrRefuseActionAsync(ActionTarget target)
    {
        if (ResolveActionTarget(target) is not { } current)
        {
            if (OnActionRefused.HasDelegate)
                await OnActionRefused.InvokeAsync(new(target.Column, target.Command, ActionRefusalReason.RenderNoLongerKept,
                    target.Key, target.Row, target.Sequence));
            return;
        }
        if (OnAction.HasDelegate) await OnAction.InvokeAsync(new(current, target.Column!, target.Command!));
    }

    private async Task RaiseAddressedActionAsync(ActionTarget target)
    {
        if (_disposed || PointedAtNow) return;
        if (EndInteractive()) { _suppressRender = false; StateHasChanged(); }
        if (!await TakeInGatheredAsync()) return;
        await FireOrRefuseActionAsync(target);
    }

    // Gathered data is published before building the current-row intent (ADR-0154).
    private bool _askingGathered;
    private bool _gatheredHeard;

    /// <summary>
    /// Asks the bound source to put out what it has gathered, on the grid's own synchronization
    /// context, and takes its Window in again through the state application, as a change it
    /// announced is taken in (ADR-0141/0154; LV-16). Called before a commit, Action, paste, fill
    /// or clear so that the intent carries the current target. A change gathered while the user typed is brought forward by the
    /// gesture, as in ExPivot (ADR-0067), never waited for. Without a source, or with nothing
    /// gathered, nothing happens. A gathered change that moved the order drops the Selection and
    /// discards an open edit, as any such change does (ADR-0011): the caller reads the state
    /// again before it goes on. Answers false when the source or the new state was refused; the
    /// failure is reported as a source's is, and the write is not made.
    /// </summary>
    private async Task<bool> TakeInGatheredAsync()
    {
        if (_disposed)
            return false;
        if (Source is not { } source)
            return true;
        _gatheredHeard = false;
        try
        {
            _askingGathered = true;
            try
            {
                source.PublishGathered();
            }
            finally
            {
                _askingGathered = false;
            }
            var heard = _gatheredHeard;
            _gatheredHeard = false;
            // A source that moved without saying so inside the call is taken in all the same.
            if (!heard && !SourceMovedOn(source))
                return true;
            ApplyState();
        }
        catch (Exception ex)
        {
            await DispatchExceptionAsync(ex);
            return false;
        }
        // The newest version is painted as well as used: a press heard through a row's own
        // handler renders that row, not this root.
        _suppressRender = false;
        StateHasChanged();
        return !_disposed;
    }

    /// <summary>Whether the source's state is not what the grid last took in.</summary>
    private bool SourceMovedOn(IGridSource<TRow> source)
        => !ReferenceEquals(source.Window, _window) || source.WindowStart != _windowStart
           || source.TotalCount != _total || source.IsLoading != _loading
           || source.RowSequenceVersion != _sequenceVersion;

    private int? _barPressTold;
    private int _barPressPaint = PaintNotTold;

    /// <summary>
    /// The address context of the next press into this grid's Formula Bar (ADR-0154):
    /// the paint the Viewport named at its mousedown. Told by the grid's listener at the press,
    /// before the focus it gives the bar is dispatched, so the focus the core hears next — which
    /// opens the editor — is the one it describes. Only target identity is checked; values may change.
    /// Reading what the render wrote is not a measurement, and nothing per cell crosses
    /// (ADR-0021, note of 2026-10-05).
    ///
    /// <para>Called by the grid's own script module and not for Consumers: it is public
    /// only because JavaScript interop requires it.</para>
    /// </summary>
    /// <param name="paint">The paint the Viewport named at the press (<c>data-ex-paint</c>).</param>
    [JSInvokable]
    public void BarPressTakenAt(int paint)
    {
        if (!_disposed)
            _barPressTold = paint;
    }

    /// <summary>The paint the focus being heard was told, once; kept as the last press's for its
    /// answer (<see cref="BarPressAnsweredAsync"/>). A focus nobody told of — Tab, a Chrome's own
    /// field — uses the current address context.</summary>
    private int TakeBarPressTold()
    {
        var told = _barPressTold ?? PaintNotTold;
        _barPressTold = null;
        _barPressPaint = told;
        return told;
    }
}
