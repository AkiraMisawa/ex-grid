namespace ExGrid.Docs.Examples.Snapshot;

using System.Globalization;
using ExGrid.Data;

/// <summary>A Snapshot's row as a grid row: ExGrid's rows are objects.</summary>
public sealed record SnapshotLine(SnapshotRow Row);

/// <summary>An ExGrid over a Snapshot: a line per row the Snapshot holds, in its order, and a column
/// per Snapshot column reading its value from the Snapshot. A Blank is null, painted empty.</summary>
public static class SnapshotGrid
{
    public static SnapshotLine[] Rows(Snapshot snapshot) => [.. snapshot.Rows.Select(row => new SnapshotLine(row))];

    public static GridColumn<SnapshotLine>[] Columns(Snapshot snapshot) =>
    [
        .. snapshot.Columns.Select(column => column.Kind switch
        {
            SnapshotKind.Text => new GridColumn<SnapshotLine>(column.Name, ColumnType.Text,
                line => snapshot.ValueAt(line.Row, column), header: column.Caption),
            SnapshotKind.Boolean => new GridColumn<SnapshotLine>(column.Name, ColumnType.Boolean,
                line => snapshot.ValueAt(line.Row, column), header: column.Caption),
            // A Date comes as a DateTime holding the clock value.
            SnapshotKind.Date => new GridColumn<SnapshotLine>(column.Name, ColumnType.Date,
                line => snapshot.ValueAt(line.Row, column), header: column.Caption,
                format: v => ((DateTime)v).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            // Decimal, Double and Integer come as decimal, double and long.
            _ => new GridColumn<SnapshotLine>(column.Name, ColumnType.Number,
                line => snapshot.ValueAt(line.Row, column), header: column.Caption),
        }),
    ];
}
