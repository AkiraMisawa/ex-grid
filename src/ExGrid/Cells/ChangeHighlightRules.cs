namespace ExGrid.Cells;

/// <summary>
/// The Change Highlight's one rule (ADR-0067): a cell is marked while the current time is
/// before the time its shown value changed plus the duration. The grid asks the Consumer for
/// the change time (<see cref="CellChangeOf{TRow}"/>) and its clock for the current time; this
/// answers whether the mark shows and when it ends, which is all the grid needs to paint the
/// mark and to know when to take it away.
/// </summary>
public static class ChangeHighlightRules
{
    /// <summary>How long a mark lasts unless the Consumer says otherwise: one second
    /// (ADR-0067).</summary>
    public static TimeSpan DefaultDuration { get; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// When the mark of a value that changed at <paramref name="changedAt"/> ends: that time
    /// plus <paramref name="duration"/>, in UTC, and held at
    /// <see cref="DateTimeOffset.MaxValue"/> rather than overflowing past it.
    /// </summary>
    /// <param name="changedAt">When the cell's shown value changed.</param>
    /// <param name="duration">How long the mark lasts; zero marks nothing that is not in the
    /// future.</param>
    /// <returns>The first instant at which the cell is no longer marked.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="duration"/> is negative:
    /// a mark cannot end before the change it marks.</exception>
    public static DateTimeOffset EndOf(DateTimeOffset changedAt, TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        // In UTC first, so the sum can only overflow the one way the guard below catches: an
        // offset carried along would let a sum that is valid as an instant be refused as a
        // local time.
        var at = changedAt.ToUniversalTime();
        return duration > DateTimeOffset.MaxValue - at ? DateTimeOffset.MaxValue : at + duration;
    }

    /// <summary>
    /// The end of the mark a cell shows at <paramref name="now"/>, or null while it shows none:
    /// null when nobody said the cell changed, and from the mark's end on — the end itself is
    /// already unmarked. A change time later than <paramref name="now"/>, such as a server's
    /// clock running ahead of the grid's, is taken as given: the cell is marked until that time
    /// plus the duration, as the rule reads. Moving it to now would need the grid to remember
    /// which cells it had already seen, and a mark is keyed by row and column, never held.
    /// </summary>
    /// <param name="changedAt">The Consumer's answer: when the cell's shown value last
    /// changed, or null.</param>
    /// <param name="duration">How long a mark lasts.</param>
    /// <param name="now">The grid's current time.</param>
    /// <returns>When the mark ends, or null when the cell is not marked.</returns>
    public static DateTimeOffset? ShowingUntil(DateTimeOffset? changedAt, TimeSpan duration, DateTimeOffset now)
    {
        if (changedAt is not { } at)
            return null;
        var end = EndOf(at, duration);
        return now < end ? end : null;
    }
}
