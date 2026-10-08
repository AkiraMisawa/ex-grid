namespace ExPivot.Engine;

/// <summary>
/// When this Consumer first showed each data change a report source marks — the time a Change
/// Highlight starts (ADR-0068) — stamped on the Consumer's own clock as it adopts a Window, never
/// on the source's: a server's clock need not agree with the browser's, and a highlight that starts
/// or ends by the server's clock would start late, end early, or not show at all.
/// <para>
/// A source marks each changed cell with the Report Version whose data changed it
/// (<see cref="PivotDisplayRow.ChangedIn"/>), and lists the changes it still marks
/// (<see cref="PivotReportMetadata.ChangeMarks"/>). A change is stamped the first time a Window of
/// a report listing it is adopted — whether or not that Window holds one of its cells — so a row
/// first requested later, by a scroll, shows it as of that first time, and its highlight ends with
/// the others of the same change. A stamp is kept while the source still lists its change, or its
/// highlight has not ended, and once forgotten the change is never listed again: no change is ever
/// stamped twice. One instance serves a view for its lifetime, across new sources.
/// </para>
/// </summary>
public sealed class PivotChangeTimes
{
    private readonly Dictionary<PivotReportVersion, DateTimeOffset> _stamps = [];

    /// <summary>
    /// Takes a Window up as it is shown: each change its report lists, or its rows carry, that is not
    /// stamped yet is stamped <paramref name="now"/>, and each stamp whose change the source no
    /// longer lists and whose highlight has ended is forgotten.
    /// </summary>
    /// <param name="state">The Window adopted.</param>
    /// <param name="now">The Consumer's own time, as it shows the Window.</param>
    /// <param name="highlightDuration">How long a Change Highlight lasts on the Consumer's clock.</param>
    public void Adopt(PivotReportState state, DateTimeOffset now, TimeSpan highlightDuration)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (_stamps.Count > 0)
        {
            var listed = state.Metadata.ChangeMarks;
            List<PivotReportVersion>? forgotten = null;
            foreach (var (mark, at) in _stamps)
            {
                if (now - at >= highlightDuration && !listed.Contains(mark))
                    (forgotten ??= []).Add(mark);
            }
            if (forgotten is not null)
            {
                foreach (var mark in forgotten)
                    _stamps.Remove(mark);
            }
        }
        foreach (var mark in state.Metadata.ChangeMarks)
            _stamps.TryAdd(mark, now);
        foreach (var row in state.Rows)
        {
            foreach (var mark in row.ChangedIn)
            {
                if (mark is not null)
                    _stamps.TryAdd(mark, now);
            }
        }
    }

    /// <summary>When the change <paramref name="mark"/> names was first shown, or null when it names
    /// none, or one not stamped.</summary>
    /// <param name="mark">A cell's <see cref="PivotDisplayRow.ChangedIn"/>.</param>
    public DateTimeOffset? At(PivotReportVersion? mark)
        => mark is not null && _stamps.TryGetValue(mark, out var at) ? at : null;

    /// <summary>When the shown text of a row's value cell last changed with the data, as this
    /// Consumer first showed it; null when it did not change recently.</summary>
    /// <param name="row">A row of an adopted Window.</param>
    /// <param name="valueColumn">The value column's index.</param>
    public DateTimeOffset? ChangedAt(PivotDisplayRow row, int valueColumn)
    {
        ArgumentNullException.ThrowIfNull(row);
        return (uint)valueColumn < (uint)row.ChangedIn.Count ? At(row.ChangedIn[valueColumn]) : null;
    }
}
