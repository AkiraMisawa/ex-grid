using ExGrid.Rows;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ExGrid.Components;

// A press on a mark — a row's checkbox, the header's, or "Mark all N rows" — is judged against the paint
// it was made on (ADR-0043's note of 2026-10-08, decided with the user on 2026-10-09; MK-9; ADR-0142).
// What it can still name exactly is honoured: a row's checkbox names its row by identity, and the
// header's checkbox what it named by the mode in force at the press — under a pager, the page it was
// pressed on, as positions under the order it was pressed in; with none, the whole result — whatever the
// grid pages by now. The rest marks nothing and is refused through OnMarkRefused, once: SourceChanged for
// a press made on what a replaced Source painted, OrderMoved for one naming rows by position under an
// order that has moved since. A press on a row's checkbox whose click Blazor will not deliver is answered
// by the core, as an Action press is (ExGrid.ActionPress.cs), and a told press never reaches the next one.
public partial class ExGrid<TRow>
{
    /// <summary>
    /// A press on a mark that marked nothing, saying why (ADR-0043's note of 2026-10-08, decided with the
    /// user on 2026-10-09; MK-9). A press on a row's checkbox, the header's or "Mark all N rows" is judged
    /// against the paint it was made on (<see cref="MarkPressTakenAt"/>), and what it can still name
    /// exactly is honoured: a row's checkbox marks its row by identity wherever the order has moved it,
    /// and the header's checkbox marks what it named, by the mode in force at the press — under a pager,
    /// the page it was pressed on, as positions under the order it was pressed in, which the marks resolve
    /// whether or not the page is on screen, though the page has turned or the pager gone since; with
    /// none, the whole result, though a pager has come since. The rest marks nothing, raises no Row Mark
    /// intent, and is raised here once:
    /// <see cref="MarkRefusalReason.SourceChanged"/> for a press made on what a <see cref="Source"/> since
    /// replaced by another instance painted, a row's checkbox included, and
    /// <see cref="MarkRefusalReason.OrderMoved"/> for the header's checkbox or "Mark all N rows" pressed
    /// under an order that has moved since. A press on a row's checkbox whose click Blazor will not
    /// deliver — a render disposed the checkbox's row before the click was heard — is answered by the
    /// grid as its click would have been, on the row at the position it was pressed at; under an order
    /// that has moved since, that position is all that names its row (ADR-0160), so it is refused as
    /// <see cref="MarkRefusalReason.OrderMoved"/> too. A press told the current paint, or told none, marks
    /// as before; a press pointed at from outside marks nothing and says nothing (ADR-0058). The grid
    /// holds no string for it; a Consumer words it, into its refusal live region (A11Y-16).
    /// </summary>
    [Parameter] public EventCallback<MarkRefusalReason> OnMarkRefused { get; set; }

    /// <summary>A press on one of this grid's marks, told by its listener, until its click is heard or the
    /// core answers it (ADR-0142, MK-9): the paint it was taken against; for the header's checkbox under a
    /// pager, the first row of the page it named and how many rows that page held, or −1 and −1; and for a
    /// row's checkbox, the row its cell's id named in that paint and the serial of the row component that
    /// painted the checkbox there — or −1, and 0 where the grid kept no serial for that paint. Numbers only
    /// (ADR-0160).</summary>
    private readonly record struct MarkPress(int Paint, int PageStart, int PageRows, int Row, int Serial)
    {
        /// <summary>Whether it was made on a row's checkbox, rather than the header's or "Mark all N
        /// rows", which the root renders and whose click always comes.</summary>
        public bool OnARow => Row >= 0;

        /// <summary>Whether it named a page: made on the header's checkbox while a pager was in force. Made
        /// on it with none, it named the whole result (ADR-0043, MK-9).</summary>
        public bool NamedAPage => PageStart >= 0;

        /// <summary>The rows of the page it named, as positions in the whole result — its first row and the
        /// rows it held at the press — or null where it named none, or a page of no rows.</summary>
        public RowRange? Page => PageStart >= 0 && PageRows > 0 ? new RowRange(PageStart, PageRows) : null;
    }

    // The press being told, until its click is heard or the core answers it or lets it go.
    private MarkPress? _markPress;

    // Whether the core answered a told press on a row's checkbox whose click had not come. A click the
    // checkbox's disposed component still delivers is that press's, and marks nothing more: it is
    // answered once (MK-9), whether or not the click comes.
    private bool _markPressAnswered;

    /// <summary>
    /// What the next press on one of this grid's marks — a row's checkbox, the header's, or "Mark all
    /// N rows" — was taken against (ADR-0142, MK-9): the paint the Viewport named at its mousedown; for the
    /// header's checkbox under a pager, the first row of the page it named and how many rows it held; and
    /// for a row's checkbox, the row its cell's id named, all read at that mousedown from what the render
    /// wrote. Told by the grid's
    /// listener at the release on the same mark, just before Blazor dispatches the click, so the click the
    /// core hears next is the one it describes, as for an action (<see cref="ActionPressTakenAt"/>). The
    /// click is judged against that paint (<see cref="OnMarkRefused"/>): a press made on what a
    /// <see cref="Source"/> since replaced painted marks nothing; the header's checkbox and "Mark all N
    /// rows", which name rows by position, mark nothing under an order that has moved since; a row's
    /// checkbox names its row by identity, which an order move leaves it; and the header's checkbox marks
    /// what it named, by the mode in force at the press — under a pager, the page it named, though the page
    /// has turned, or the pager gone, since; with none, the whole result, though a pager has come since.
    /// Reading what the render wrote is not a measurement, and nothing per cell crosses (ADR-0021).
    ///
    /// <para>Blazor does not deliver an event whose attribute a component since disposed had rendered. A
    /// row's checkbox is rendered by its row's component, which a render disposes when the row leaves the
    /// painted rows, or, without a Row Key, when a new instance of it arrives (ADR-0140): the click on it
    /// would be lost without a word. So a press whose row component is no longer rendered is answered here,
    /// and one whose component a later render disposes before its click arrives is answered after that
    /// render, as its click would have been. A press's click follows its own release, ahead of the next
    /// press: a press still waiting when the next is told never hears its click, and is answered, or let
    /// go, first. The header's checkbox and "Mark all N rows" are the root's own, and their click always
    /// comes.</para>
    ///
    /// <para>Called by the grid's own script module and not for Consumers: it is public
    /// only because JavaScript interop requires it.</para>
    /// </summary>
    /// <param name="paint">The paint the Viewport named at the press (<c>data-ex-paint</c>).</param>
    /// <param name="pageStart">The first row of the page the header's checkbox named under a pager
    /// (<c>data-ex-page</c>), or −1.</param>
    /// <param name="row">The row a row's checkbox stood in, as its cell's id names it, or −1.</param>
    /// <param name="pageRows">How many rows the page the header's checkbox named under a pager held
    /// (<c>data-ex-page-rows</c>), or −1.</param>
    [JSInvokable]
    public async Task MarkPressTakenAt(int paint, int pageStart = -1, int row = -1, int pageRows = -1)
    {
        if (_disposed)
            return;
        await LetGoOfMarkPressAsync();
        _markPressAnswered = false;
        _markPress = new MarkPress(paint, pageStart, pageRows, row, row >= 0 ? SerialPaintedAt(paint, row) : 0);
        await AnswerMarkPressWithNoClickAsync();
    }

    /// <summary>The serial of the row component the paint <paramref name="paint"/> painted at
    /// <paramref name="row"/>, or 0 where the grid keeps none: a paint older than every one it keeps, a
    /// position that paint did not paint, or a Placeholder.</summary>
    private int SerialPaintedAt(int paint, int row)
    {
        var painted = _paintRows.FindLast(p => p.Id == paint);
        var at = painted is null ? -1 : row - painted.FirstRow;
        return painted is not null && at >= 0 && at < painted.Serials.Length ? painted.Serials[at] : 0;
    }

    /// <summary>The serial of the row component the newest paint rendered for <paramref name="row"/>, or 0
    /// where it rendered none.</summary>
    private int RowSerialOf(TRow row) => _rowSerials.TryGetValue(ComponentIdentityOf(row), out var serial) ? serial : 0;

    /// <summary>
    /// Answers a told press on a row's checkbox whose click Blazor will not deliver, because the row
    /// component that rendered the checkbox is no longer rendered (ADR-0142, MK-9; as
    /// <see cref="AnswerActionPressWithNoClickAsync"/> answers an action's): as its click would have been
    /// (<see cref="AnswerRowMarkPressAsync"/>). A press whose component is still rendered waits for its
    /// click, and so does one told a paint the grid keeps no serial for. Decided by the order the core
    /// hears things in, never by time.
    /// </summary>
    private async Task AnswerMarkPressWithNoClickAsync()
    {
        if (_markPress is not { OnARow: true, Serial: > 0 } press || _disposed || _rowSerials.ContainsValue(press.Serial))
            return;
        _markPress = null;
        _markPressAnswered = true;
        await AnswerRowMarkPressAsync(press);
    }

    /// <summary>
    /// Ends a told press that something heard after it shows its click will not come: the next press told,
    /// or a click on another mark. A press's click follows its own release, ahead of the next press, so it
    /// never will. Answered when its row component is gone (<see cref="AnswerMarkPressWithNoClickAsync"/>),
    /// and let go otherwise — a press told a paint the grid keeps no serial for, whose row cannot be known
    /// to have gone: answered so late, it would land after presses the user made since, the one that
    /// repeated it among them — so that it never reaches the press after it (principle 6).
    /// </summary>
    private async Task LetGoOfMarkPressAsync()
    {
        await AnswerMarkPressWithNoClickAsync();
        _markPress = null;
    }

    /// <summary>
    /// The told press a click on a mark completes, taken: the one told last, when it was made on this
    /// mark — on a row's checkbox, by the row component that heard the click, and otherwise on the
    /// header's checkbox or "Mark all N rows". Any other press still told never hears its click, and is
    /// answered or let go first (<see cref="LetGoOfMarkPressAsync"/>). Null for a click nobody told of —
    /// made by key or by script, with no mousedown before it — which is taken as aimed at the newest
    /// paint.
    /// </summary>
    /// <param name="clicked">The row whose checkbox heard the click, or null for the header's checkbox
    /// and "Mark all N rows".</param>
    private async Task<MarkPress?> TakeMarkPressAsync(TRow? clicked)
    {
        if (_markPress is { } press && (press.OnARow
                ? clicked is not null && (press.Serial == 0 || RowSerialOf(clicked) == press.Serial)
                : clicked is null))
        {
            _markPress = null;
            return press;
        }
        await LetGoOfMarkPressAsync();
        return null;
    }

    /// <summary>
    /// A row's checkbox pressed (ADR-0043): its row, by identity — the instance its row component held,
    /// wherever an order move has taken it — asked for the state the checkbox did not show. Pointed at
    /// from outside, it marks nothing (ADR-0058), as an action does. Made on what a Source since replaced
    /// painted, it marks nothing and is refused as <see cref="MarkRefusalReason.SourceChanged"/>: the
    /// checkbox holds the new source's row now, which nobody pressed (ADR-0142, MK-9).
    /// </summary>
    private async Task RaiseRowMarkAsync(TRow row, bool marked)
    {
        var press = await TakeMarkPressAsync(row);
        var answered = _markPressAnswered;
        _markPressAnswered = false;
        // The click of a press the core has answered already, which its component, disposed since, still
        // delivered: the press was answered once.
        if (press is null && answered && !RendersRowComponentOf(row))
            return;
        if (PointedAtNow)
            return;
        if (AimedAtAReplacedSource(press?.Paint ?? PaintNotTold))
        {
            await RefuseMarkAsync(AimedAway.SourceReplaced);
            return;
        }
        if (_marks is not null)
            await _marks.OnMarkIntentAsync(new RowMarkIntent<TRow>.OneRow(row, marked));
    }

    /// <summary>
    /// A press on a row's checkbox answered by the core, its click never to be heard (ADR-0142, MK-9): as
    /// the click would have been. Made on what a Source since replaced painted, it marks nothing and is
    /// refused as <see cref="MarkRefusalReason.SourceChanged"/>. Under the order it was taken under, the
    /// position it was pressed at names its row still, and that row is asked for the state its mark does
    /// not have now: the row in hand there, or past the Window the position itself, which the marks
    /// resolve under that order. Under an order that has moved since, the position names another row, and
    /// the grid keeps no row and no Row Key of the render it was pressed on (ADR-0160): refused as
    /// <see cref="MarkRefusalReason.OrderMoved"/>.
    /// </summary>
    private async Task AnswerRowMarkPressAsync(MarkPress press)
    {
        if (_marks is null || PointedAtNow)
            return;
        if (AimedAwayFrom(press.Paint) is { } away)
        {
            await RefuseMarkAsync(away);
            return;
        }
        if (RowInHand(press.Row) is { } row)
            await _marks.OnMarkIntentAsync(new RowMarkIntent<TRow>.OneRow(row, !MarkedAt(row)));
        else
            await _marks.OnMarkIntentAsync(new RowMarkIntent<TRow>.Positions([new RowRange(press.Row, 1)], _sequenceVersion));
    }

    /// <summary>Raises the refusal of a press on a mark whose rows a replaced Source or a moved order took
    /// away (<see cref="OnMarkRefused"/>; ADR-0043, MK-9): once per press, with no Row Mark intent beside
    /// it.</summary>
    private async Task RefuseMarkAsync(AimedAway away)
    {
        if (OnMarkRefused.HasDelegate)
        {
            await OnMarkRefused.InvokeAsync(
                away == AimedAway.SourceReplaced ? MarkRefusalReason.SourceChanged : MarkRefusalReason.OrderMoved);
        }
    }
}
