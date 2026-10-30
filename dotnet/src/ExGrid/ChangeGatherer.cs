namespace ExGrid;

/// <summary>
/// When a bundled source's live changes reach the grid (ADR-0141, on ADR-0067's rules): the first
/// change after a quiet interval at once, and the changes that follow it within the interval as
/// one, at the interval's end. Shared by <c>GridSource.From</c>, whose question is answered at once
/// from what it holds, and <c>GridSource.Fetch</c>, whose question goes to a server and lands later.
///
/// <para>It decides only <em>when</em>. Its owner holds the changes and asks for them, and calls it
/// under the owner's own lock or on the owner's one context, so it needs no lock of its own. The
/// timer is the one thing that runs elsewhere: it calls <c>due</c> on the clock's thread, and the
/// owner takes its lock or goes to its context from there.</para>
///
/// <para>The clock is the source's, never the grid's: gathering belongs to the source, and the
/// grid's core has no clock for data (ADR-0141). A test hands in its own.</para>
/// </summary>
internal sealed class ChangeGatherer
{
    /// <summary>ADR-0067's interval: a value that changes four times a second still feels live,
    /// and one that changes forty times a second cannot be read.</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(250);

    // The longest wait the timer is armed for at once. A System.Threading.Timer refuses a due time
    // past about 49 days; a wait cut short asks nothing, and is armed again for what remains.
    private static readonly TimeSpan LongestWait = TimeSpan.FromDays(1);

    private readonly TimeProvider _clock;
    private readonly Action _due;
    private TimeSpan _interval = DefaultInterval;
    private ITimer? _timer;
    private DateTimeOffset? _armedFor;

    /// <param name="clock">The source's clock.</param>
    /// <param name="due">Called on the clock's thread when an armed wait ends; the owner then
    /// asks <see cref="ShouldAskNow"/> again, under its lock or on its context.</param>
    public ChangeGatherer(TimeProvider clock, Action due)
    {
        _clock = clock;
        _due = due;
    }

    /// <summary>The clock the source reads change times from too.</summary>
    public TimeProvider Clock => _clock;

    /// <summary>
    /// The shortest time between two changes reaching the grid. Zero passes every change as it
    /// comes; a negative interval is refused by name, since no wait could end before it began.
    /// </summary>
    public TimeSpan Interval
    {
        get => _interval;
        set
        {
            if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "A gathering interval cannot be negative: zero passes every change as it comes (ADR-0141/0067).");
            }
            _interval = value;
        }
    }

    /// <summary>When a change last reached the grid — shown, or found unshowable — from which the
    /// next one waits <see cref="Interval"/>. Null before the first.</summary>
    public DateTimeOffset? LastShown { get; private set; }

    /// <summary>
    /// A change is waiting. True when it should be asked for now: <see cref="Interval"/> has passed
    /// since the last change reached the grid, and nothing is out (<paramref name="busy"/>). False
    /// when it waits — for the timer, armed here for the interval's end, or for the question out,
    /// whose landing asks again.
    /// </summary>
    public bool ShouldAskNow(bool busy = false)
    {
        if (busy)
            return false;
        var now = _clock.GetUtcNow();
        var due = LastShown is { } last ? last + _interval : now;
        if (due <= now)
        {
            Disarm();
            return true;
        }
        Arm(due, due - now);
        return false;
    }

    /// <summary>A change reached the grid now, shown or found unshowable: the next one waits
    /// <see cref="Interval"/> from here.</summary>
    public void Shown() => LastShown = _clock.GetUtcNow();

    /// <summary>Nothing waits any longer: a question that carries the changes was asked
    /// another way, such as a user's gesture.</summary>
    public void Disarm()
    {
        if (_armedFor is null)
            return;
        _armedFor = null;
        _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private void Arm(DateTimeOffset due, TimeSpan wait)
    {
        if (_armedFor == due)
            return;
        // One timer for the source's life, re-armed rather than made anew: an unarmed timer holds
        // nothing, so a source left without being disposed costs nothing once it is quiet.
        _timer ??= _clock.CreateTimer(static state => ((ChangeGatherer)state!)._due(), this,
            Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _armedFor = due;
        _timer.Change(wait < LongestWait ? wait : LongestWait, Timeout.InfiniteTimeSpan);
    }

    /// <summary>The armed wait is over. Called by the owner, under its lock or on its context, before
    /// it asks <see cref="ShouldAskNow"/> again — which arms a new wait when a timer and the clock
    /// disagree and the tick came a moment early.</summary>
    public void Fired() => _armedFor = null;
}
