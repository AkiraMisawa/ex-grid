using System.Collections;

namespace ExChart.Engine;

/// <summary>
/// A table a chart reads by column name (ADR-0320, ADR-0322): the Engine's own seam, so it references
/// no package. A host adapts its data to it, such as a Snapshot in ExChart.
/// </summary>
public interface IChartData
{
    /// <summary>The columns' names, in the table's order.</summary>
    IReadOnlyList<string> Columns { get; }

    /// <summary>How many rows it has.</summary>
    int RowCount { get; }

    /// <summary>The value in <paramref name="column"/> at <paramref name="row"/>, as the table holds it; null where it is missing.</summary>
    object? Value(string column, int row);
}

/// <summary>A table of columns held in memory: the reference implementation of <see cref="IChartData"/>.</summary>
public sealed class ChartData : IChartData
{
    private readonly Dictionary<string, IList> _columns;

    private ChartData(IReadOnlyList<string> names, Dictionary<string, IList> columns, int rows)
    {
        Columns = names;
        _columns = columns;
        RowCount = rows;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Columns { get; }

    /// <inheritdoc />
    public int RowCount { get; }

    /// <inheritdoc />
    public object? Value(string column, int row) => _columns[column][row];

    /// <summary>A table of these columns, each a list of the same length.</summary>
    /// <exception cref="ArgumentException">Two columns share a name, or their lengths differ.</exception>
    public static ChartData Of(params (string Name, IList Values)[] columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        var byName = new Dictionary<string, IList>(StringComparer.Ordinal);
        var rows = columns.Length == 0 ? 0 : columns[0].Values.Count;
        foreach (var (name, values) in columns)
        {
            if (!byName.TryAdd(name, values))
                throw new ArgumentException($"Two columns are named '{name}'.", nameof(columns));
            if (values.Count != rows)
                throw new ArgumentException($"Column '{name}' has {values.Count} rows and '{columns[0].Name}' has {rows}; a table's columns are one length.", nameof(columns));
        }
        return new ChartData(columns.Select(c => c.Name).ToArray(), byName, rows);
    }
}
