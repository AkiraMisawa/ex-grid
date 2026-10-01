namespace ExPivot.Engine;

/// <summary>
/// One piece of sliced work's share of the thread (ADR-0065, PV-27/PV-40). A slice begins when the
/// work begins and after every yield, and ends once it has run for
/// <see cref="PivotSlicing.Budget"/>; the thread is then yielded, and cancelled work throws there.
/// <list type="bullet">
/// <item>The pass over a Snapshot's rows looks at the clock after every
/// <see cref="PivotSlicing.RecordsPerCheck"/> rows and yields between its slices
/// (<see cref="PassAsync"/>), as it always has.</item>
/// <item>The work after it — the answer assembled, the cube made, the report laid out — goes on in
/// the slice the pass ended in. It counts its units of work (<see cref="Done"/>) and looks at the
/// clock every <see cref="PivotSlicing.UnitsPerCheck"/> of them; once the slice is found spent, the
/// next piece of work yields first (<see cref="ForAsync(int, Action{int, int}, int)"/>,
/// <see cref="PauseAsync"/>). So a small answer never reads the clock, and work that is over never
/// yields.</item>
/// </list>
/// <see cref="Unsliced"/> never yields and never reads the clock: the synchronous forms run the same
/// steps in one go, so the sliced and the synchronous forms give the same result by construction.
/// </summary>
internal sealed class Slicer
{
    /// <summary>The units a piece of work is cut to, so that a look at the clock falls about every
    /// <see cref="PivotSlicing.UnitsPerCheck"/> units of the default.</summary>
    internal const int PieceUnits = 1024;

    private readonly PivotSlicing? _slicing;
    private readonly CancellationToken _cancellationToken;
    private long _sliceStart;
    private int _units;
    private bool _spent;

    private Slicer(PivotSlicing? slicing, CancellationToken cancellationToken)
    {
        _slicing = slicing;
        _cancellationToken = cancellationToken;
    }

    /// <summary>Work that never yields and never reads the clock: the synchronous forms. It holds
    /// no state that changes, so one instance serves every caller.</summary>
    public static Slicer Unsliced { get; } = new(null, default);

    /// <summary>Begins a piece of sliced work: its first slice starts now. Throws when it is
    /// cancelled already.</summary>
    public static Slicer Of(PivotSlicing slicing, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slicing);
        cancellationToken.ThrowIfCancellationRequested();
        return new Slicer(slicing, cancellationToken)
        {
            _sliceStart = slicing.TimeProvider.GetTimestamp(),
        };
    }

    /// <summary>Whether the slice was found spent, so that the next piece of work yields first.</summary>
    public bool IsSpent => _spent;

    /// <summary>
    /// The pass over stored rows [0, <paramref name="count"/>), in steps of
    /// <see cref="PivotSlicing.RecordsPerCheck"/> rows, the clock read after each, a slice ended once
    /// it has run for the budget and the thread yielded between slices. A cancelled pass throws at
    /// the next slice; a step that answers false has stopped it.
    /// </summary>
    public async ValueTask PassAsync(long count, Func<long, long, bool> step)
    {
        if (_slicing is not { } slicing)
        {
            if (count > 0)
                step(0, count);
            return;
        }
        _cancellationToken.ThrowIfCancellationRequested();
        var clock = slicing.TimeProvider;
        long at = 0;
        while (true)
        {
            while (at < count)
            {
                var end = Math.Min(count, at + slicing.RecordsPerCheck);
                if (!step(at, end))
                    return;
                at = end;
                if (clock.GetElapsedTime(_sliceStart) >= slicing.Budget)
                    break;
            }
            if (at >= count)
                break;
            await YieldAsync().ConfigureAwait(false);
        }
        _cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Counts <paramref name="units"/> of work done after the pass. Every
    /// <see cref="PivotSlicing.UnitsPerCheck"/> units the clock is read, and a slice that has run for
    /// the budget is spent: the next piece of work yields first. Answers whether it is.
    /// </summary>
    public bool Done(int units)
    {
        if (_slicing is not { } slicing)
            return false;
        _units += units;
        if (_units >= slicing.UnitsPerCheck)
        {
            _units = 0;
            if (slicing.TimeProvider.GetElapsedTime(_sliceStart) >= slicing.Budget)
                _spent = true;
        }
        return _spent;
    }

    /// <summary>Before a piece of work: yields when the slice was found spent, and goes on at once
    /// otherwise.</summary>
    public ValueTask PauseAsync() => _spent ? YieldAsync() : default;

    /// <summary>
    /// Runs <paramref name="step"/> over [0, <paramref name="count"/>) in pieces of about
    /// <see cref="PieceUnits"/> units, each element worth <paramref name="weight"/> of them, yielding
    /// before a piece when the slice is spent. Unsliced, it is one step over the whole range.
    /// </summary>
    public ValueTask ForAsync(int count, Action<int, int> step, int weight = 1)
        => ForAsync(count, (from, to) =>
        {
            step(from, to);
            return true;
        }, weight);

    /// <summary>As <see cref="ForAsync(int, Action{int, int}, int)"/>, and a step that answers false
    /// has stopped the work.</summary>
    public async ValueTask ForAsync(int count, Func<int, int, bool> step, int weight = 1)
    {
        if (_slicing is null)
        {
            if (count > 0)
                step(0, count);
            return;
        }
        var per = Math.Max(1, PieceUnits / Math.Max(1, weight));
        for (var at = 0; at < count;)
        {
            if (_spent)
                await YieldAsync().ConfigureAwait(false);
            var end = (int)Math.Min(count, (long)at + per);
            if (!step(at, end))
                return;
            Done((end - at) * weight);
            at = end;
        }
    }

    /// <summary>The result of work begun with <see cref="Unsliced"/>, which never yields, so it is
    /// complete when it returns; a failure is thrown as it was raised.</summary>
    public static T Run<T>(ValueTask<T> work)
    {
        if (!work.IsCompleted)
            throw new InvalidOperationException("Work that never yields did not complete on the calling thread.");
        return work.GetAwaiter().GetResult();
    }

    /// <summary>As <see cref="Run{T}"/>, for work with no result.</summary>
    public static void Run(ValueTask work)
    {
        if (!work.IsCompleted)
            throw new InvalidOperationException("Work that never yields did not complete on the calling thread.");
        work.GetAwaiter().GetResult();
    }

    private async ValueTask YieldAsync()
    {
        var slicing = _slicing!;
        await slicing.YieldAsync(_cancellationToken).ConfigureAwait(false);
        _cancellationToken.ThrowIfCancellationRequested();
        _sliceStart = slicing.TimeProvider.GetTimestamp();
        _units = 0;
        _spent = false;
    }
}
