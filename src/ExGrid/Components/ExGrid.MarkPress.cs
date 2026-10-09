using ExGrid.Rows;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ExGrid.Components;

// A press on a mark — a row's checkbox, the header's, or "Mark all N rows" — is judged against the paint
// it was made on (ADR-0043's note of 2026-10-08, decided with the user on 2026-10-09; MK-9; ADR-0142).
// What it can still name exactly is honoured: a row's checkbox names its row by identity, and the header's
// checkbox under a pager names the page it was pressed on, as positions under the order it was pressed
// in. The rest marks nothing and is refused through OnMarkRefused, once: SourceChanged for a press made on
// what a replaced Source painted, OrderMoved for one naming rows by position under an order that has moved
// since.
public partial class ExGrid<TRow>
{
    /// <summary>
    /// A press on a mark that marked nothing, saying why (ADR-0043's note of 2026-10-08, decided with the
    /// user on 2026-10-09; MK-9). A press on a row's checkbox, the header's or "Mark all N rows" is judged
    /// against the paint it was made on (<see cref="MarkPressTakenAt"/>), and what it can still name
    /// exactly is honoured: a row's checkbox marks its row by identity wherever the order has moved it,
    /// and the header's checkbox under a pager marks the page it was pressed on — as positions under the
    /// order it was pressed in, which the marks resolve whether or not the page is on screen — after the
    /// page has turned. The rest marks nothing, raises no Row Mark intent, and is raised here once:
    /// <see cref="MarkRefusalReason.SourceChanged"/> for a press made on what a <see cref="Source"/> since
    /// replaced by another instance painted, a row's checkbox included, and
    /// <see cref="MarkRefusalReason.OrderMoved"/> for the header's checkbox or "Mark all N rows" pressed
    /// under an order that has moved since. A press told the current paint, or told none, marks as
    /// before; a press pointed at from outside marks nothing and says nothing (ADR-0058). The grid holds
    /// no string for it; a Consumer words it, into its refusal live region (A11Y-16).
    /// </summary>
    [Parameter] public EventCallback<MarkRefusalReason> OnMarkRefused { get; set; }

    /// <summary>A press on one of this grid's marks, told by its listener until its click is heard: the
    /// paint it was taken against, and, for the header's checkbox under a pager, the first row of the
    /// page it named, or −1 (ADR-0142, MK-9). Numbers only (ADR-0160).</summary>
    private readonly record struct MarkPress(int Paint, int PageStart);

    // The press being told, until its click is heard.
    private MarkPress? _markPress;

    /// <summary>
    /// What the next press on one of this grid's marks — a row's checkbox, the header's, or "Mark all
    /// N rows" — was taken against (ADR-0142, MK-9): the paint the Viewport named at its mousedown, and,
    /// for the header's checkbox under a pager, the first row of the page it named, read at the same
    /// mousedown from the attribute the render wrote. Told by the grid's listener at the release on the
    /// same mark, just before Blazor dispatches the click, so the click the core hears next is the one it
    /// describes, as for an action (<see cref="ActionPressTakenAt"/>). The click is judged against that
    /// paint (<see cref="OnMarkRefused"/>): a press made on what a <see cref="Source"/> since replaced
    /// painted marks nothing; the header's checkbox and "Mark all N rows", which name rows by position,
    /// mark nothing under an order that has moved since; a row's checkbox names its row by identity,
    /// which an order move leaves it; and the header's checkbox under a pager marks the page it named,
    /// though the page has turned since. Reading what the render wrote is not a measurement, and nothing
    /// per cell crosses (ADR-0021).
    ///
    /// <para>Called by the grid's own script module and not for Consumers: it is public
    /// only because JavaScript interop requires it.</para>
    /// </summary>
    /// <param name="paint">The paint the Viewport named at the press (<c>data-ex-paint</c>).</param>
    /// <param name="pageStart">The first row of the page the header's checkbox named under a pager
    /// (<c>data-ex-page</c>), or −1.</param>
    [JSInvokable]
    public Task MarkPressTakenAt(int paint, int pageStart = -1)
    {
        if (!_disposed)
            _markPress = new MarkPress(paint, pageStart);
        return Task.CompletedTask;
    }

    /// <summary>The told press a mark's click completes, taken; null for a click nobody told of — made by
    /// key or by script, with no mousedown before it — which is taken as aimed at the newest paint.</summary>
    private MarkPress? TakeMarkPress()
    {
        var press = _markPress;
        _markPress = null;
        return press;
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
        var press = TakeMarkPress();
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
