using ExGrid.Docs.Examples.Data;

namespace ExGrid.Docs.Examples.Grid;

/// <summary>
/// The million trades as a server would send them, a Window at a time, for a grid that may ask
/// before its Viewport runs out (ADR-0183). Each question is answered after a round trip with the
/// rows asked for and <see cref="ReadAhead"/> either side. A question in flight whose rows cover the
/// rows on screen is kept rather than asked again (ADR-0182): replacing it at every scroll would
/// restart the round trip for as long as a key is held. It counts the questions, and how many of
/// them found every row on screen already held.
/// </summary>
public sealed class ReadAheadTrades
{
    /// <summary>Rows read either side of the rows asked for.</summary>
    public const int ReadAhead = 50;

    /// <summary>How far short of the Window's edge the grid asks, when it asks early: never more
    /// than <see cref="ReadAhead"/>, or the answer would not reach it and the next row would ask
    /// again.</summary>
    public const int Margin = 10;

    private static readonly TimeSpan RoundTrip = TimeSpan.FromMilliseconds(150);

    // The rows asked of the server and not yet answered, if any.
    private (int Start, int Count)? _asked;

    /// <summary>The rows held, which the grid paints.</summary>
    public IReadOnlyList<Trade> Window { get; private set; } = MillionTrades.Range(0, 100);

    /// <summary>Where <see cref="Window"/> starts among the million.</summary>
    public int WindowStart { get; private set; }

    /// <summary>Whether a question is out.</summary>
    public bool IsLoading => _asked is not null;

    /// <summary>Questions asked while every row on screen was held: asked early.</summary>
    public int Early { get; private set; }

    /// <summary>Questions asked while rows on screen waited for them as placeholders.</summary>
    public int Late { get; private set; }

    /// <summary>Starts the counts again.</summary>
    public void ResetCounts() => (Early, Late) = (0, 0);

    /// <summary>The grid's Range Request: it names the rows on screen, early or not.</summary>
    public async Task RangeNeededAsync(RowRange range)
    {
        if (_asked is { } inFlight && Covers(inFlight.Start, inFlight.Count, range))
            return;
        if (Covers(WindowStart, Window.Count, range))
            Early++;
        else
            Late++;
        var start = Math.Max(0, range.Start - ReadAhead);
        var end = Math.Min(MillionTrades.Count, range.Start + range.Count + ReadAhead);
        var asked = (start, end - start);
        _asked = asked;
        await Task.Delay(RoundTrip); // where a server call would go

        // A newer question replaced this one while it waited: its answer would be stale.
        if (_asked != asked)
            return;
        Window = MillionTrades.Range(start, end - start);
        WindowStart = start;
        _asked = null;
    }

    private static bool Covers(int start, int count, RowRange range)
        => range.Start >= start && range.Start + range.Count <= start + count;
}
