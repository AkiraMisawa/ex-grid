using global::ExChart.Engine;
using global::ExGrid.Data;

namespace ExChart;

/// <summary>
/// A Snapshot read by column name, as a Chart Spec reads its Data Handles (ADR-0322): the Engine's own
/// seam over the family's data, so a capability that returns a Snapshot can be charted as it is. A
/// Snapshot never changes; its next version is a new one, which a chart reads by being given it.
/// </summary>
public sealed class SnapshotChartData : IChartData
{
    private readonly Snapshot _snapshot;

    /// <summary>The chart's view of <paramref name="snapshot"/>.</summary>
    public SnapshotChartData(Snapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _snapshot = snapshot;
        Columns = snapshot.Columns.Select(c => c.Name).ToArray();
    }

    /// <summary>The Snapshot's version: each Change Batch applied moves it on by one (ADR-0067).</summary>
    public long Version => _snapshot.Version;

    /// <inheritdoc />
    public IReadOnlyList<string> Columns { get; }

    /// <inheritdoc />
    public int RowCount => _snapshot.RowCount;

    /// <inheritdoc />
    public object? Value(string column, int row) => _snapshot.ValueAt(_snapshot.Rows[row], _snapshot[column]);
}
