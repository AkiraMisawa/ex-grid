using ExGrid.Cells;
using Microsoft.AspNetCore.Components;

namespace ExGrid.Components;

// The Change Highlight (ADR-0068): the Consumer says when a cell's shown value last changed,
// and the grid marks the cell with a class for a short time, then takes the class away in one
// step. Nothing animates, because rows are recycled (ADR-0027 P8). The rows ask, cell by cell,
// as they ask for Cell State (ADR-0006), and each tells the grid the earliest end among the
// marks it painted. The grid keeps one timer, for the earliest of those; when it fires, the root
// renders with the time now, and the rows whose marks have ended render again. No other row
// does.
public partial class ExGrid<TRow>
{
    /// <summary>
    /// A Consumer declaration, the Change Highlight (ADR-0068): when a cell's shown value last
    /// changed, asked by (row, column) as <see cref="CellState"/> is. A value cell is painted
    /// with the class <c>ex-changed</c> while the grid's time (<see cref="Clock"/>) is before
    /// the answer plus <see cref="ChangeHighlightDuration"/>, and the class is taken away in one
    /// step when that time comes: the grid keeps one timer, for the earliest end among the marks
    /// it has painted, and renders again only the rows whose marks end. Nothing animates, and no
    /// live region announces a mark. Asked of value cells only, as <see cref="CellType"/> is:
    /// an Action, Template or Mark cell paints no value that could change. The grid never
    /// compares values itself.
    ///
    /// <para><b>Hold it in a field and hand over a new one when the change times do.</b> Its
    /// identity is the change signal, as <see cref="CellState"/>'s is (ADR-0003/0006): a new
    /// delegate makes the painted rows ask again, and rewriting what an unchanged one answers
    /// changes no mark on screen. Null, the default, asks nothing, paints no class and keeps no
    /// timer (DC-1).</para>
    /// </summary>
    [Parameter] public CellChangeOf<TRow>? CellChangedAt { get; set; }

    /// <summary>
    /// How long a Change Highlight lasts after the change time <see cref="CellChangedAt"/>
    /// answers (ADR-0068): one second by default. A cell is marked while the grid's time is
    /// before the change time plus this. Zero marks only a change time still to come, and a
    /// negative duration is refused. The mark's colour is the Visual Token
    /// <c>--ex-change-highlight-background</c>, never a parameter (ADR-0027).
    /// </summary>
    [Parameter] public TimeSpan ChangeHighlightDuration { get; set; } = ChangeHighlightRules.DefaultDuration;

    /// <summary>
    /// The clock the Change Highlight reads the time from, and whose timer takes the marks away
    /// (ADR-0068); a test hands in its own. Null, the default, is the grid's own clock: the
    /// <see cref="TimeProvider"/> the host registered as a service, and
    /// <see cref="TimeProvider.System"/> where it registered none. Read only while
    /// <see cref="CellChangedAt"/> is declared.
    /// </summary>
    [Parameter] public TimeProvider? Clock { get; set; }

    // One for the grid's life, handed to every row through one held delegate, so that neither
    // is ever a parameter change: each row reports the earliest end among the marks it painted.
    private readonly ChangeHighlightEnds _highlightEnds = new();
    private Action<object, DateTimeOffset?>? _highlightPainted;

    // The one timer (ADR-0068), the clock that made it, and the end it is armed for: null
    // while it is not armed, which a one-shot timer stops being as it fires. The timer exists
    // only while a mark is painted.
    private ITimer? _highlightTimer;
    private TimeProvider? _highlightTimerClock;
    private DateTimeOffset? _highlightTimerEnd;

    // The longest wait the timer is armed for at once. A System.Threading.Timer refuses a due
    // time past about 49 days, and a change time far in the future is the Consumer's to give.
    // A wait cut short ends no mark: the timer is armed again for what remains.
    private static readonly TimeSpan LongestHighlightWait = TimeSpan.FromDays(1);

    // The wait for an end that has already passed when the timer is armed. Only the moment
    // between a render and its arming can bring that about, since a render leaves no mark
    // painted past the time it painted by. It is waited for by a millisecond rather than none,
    // so the timer never fires inside the render that armed it, and a defect that left a mark
    // standing past its end would show as a mark that stays, never as renders without pause.
    private static readonly TimeSpan ShortestHighlightWait = TimeSpan.FromMilliseconds(1);

    private TimeProvider HighlightClock => Clock ?? Time;

    /// <summary>The time every row of one render paints its marks by: read once per render of
    /// the root, and only while the declaration is made. Without it, nothing reads a
    /// clock.</summary>
    private DateTimeOffset HighlightNow() => CellChangedAt is null ? default : HighlightClock.GetUtcNow();

    /// <summary>Refuses a duration a mark could not last (ADR-0068).</summary>
    private void ValidateChangeHighlight()
    {
        if (ChangeHighlightDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ChangeHighlightDuration), ChangeHighlightDuration,
                "A Change Highlight cannot last a negative time: it would end before the change it marks (ADR-0068).");
        }
    }

    /// <summary>
    /// Arms the one timer for the earliest end among the marks painted now. Called after every
    /// render of the root, which is every render in which a row can paint or drop a mark: a
    /// row renders only when the root hands it something new. With no mark painted, no timer
    /// exists (ADR-0068, DC-64).
    /// </summary>
    private void ArmHighlightTimer()
    {
        if (_disposed)
            return;
        if (_highlightEnds.Earliest is not { } end)
        {
            DisposeHighlightTimer();
            return;
        }
        var clock = HighlightClock;
        if (!ReferenceEquals(_highlightTimerClock, clock))
            DisposeHighlightTimer();
        if (_highlightTimerEnd == end)
            return;
        _highlightTimerEnd = end;
        if (_highlightTimer is null)
        {
            _highlightTimerClock = clock;
            _highlightTimer = clock.CreateTimer(OnHighlightsEnded, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
        var wait = end - clock.GetUtcNow();
        _highlightTimer.Change(
            wait <= TimeSpan.Zero ? ShortestHighlightWait : wait < LongestHighlightWait ? wait : LongestHighlightWait,
            Timeout.InfiniteTimeSpan);
    }

    private void OnHighlightsEnded(object? state)
    {
        // The timer fires off the renderer's synchronisation context; everything it touches
        // has to go back onto it.
        _ = InvokeAsync(() =>
        {
            if (_disposed)
                return;
            try
            {
                _highlightTimerEnd = null;
                // A timer's tick and the clock are two sources, so a timer can fire a moment
                // before the clock reaches the end it was armed for, and a wait cut short fires
                // long before it. Neither ends a mark, and with none left there is none to end:
                // nothing renders, and the timer is armed again for what remains, or goes.
                if (_highlightEnds.Earliest is not { } end || HighlightClock.GetUtcNow() < end)
                {
                    ArmHighlightTimer();
                    return;
                }
                // The root renders and hands every row the time now: a row whose earliest mark
                // has ended renders and asks again, and every other row skips
                // (ExGridRow.ShouldRender). A pointer's suppression is not this render's to
                // swallow.
                _suppressRender = false;
                StateHasChanged();
            }
            catch (Exception ex)
            {
                _ = DispatchExceptionAsync(ex);
            }
        });
    }

    private void DisposeHighlightTimer()
    {
        _highlightTimer?.Dispose();
        _highlightTimer = null;
        _highlightTimerClock = null;
        _highlightTimerEnd = null;
    }
}
