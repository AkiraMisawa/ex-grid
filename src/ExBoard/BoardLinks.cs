using ExGrid.Data;

namespace ExBoard;

/// <summary>
/// A link (ADR-0340, Q5): a selection in view <paramref name="From"/> narrows each view of
/// <paramref name="To"/> to the rows whose values in the columns <paramref name="On"/> are those the
/// selection holds.
/// </summary>
/// <param name="From">The view whose selection narrows; a grid, whose selected rows are taken.</param>
/// <param name="To">The views it narrows.</param>
/// <param name="On">The columns compared, declared by the input of <paramref name="From"/> and by every
/// input each of <paramref name="To"/> reads, of the same kind in each.</param>
public sealed record BoardLink(string From, IReadOnlyList<string> To, IReadOnlyList<string> On);

/// <summary>
/// What a link's selection narrows to (ADR-0340, Q5): the columns it compares, and each tuple of
/// their values the selection holds, once, in the order first selected. A row is let through when its
/// own tuple is one of them: text compared as the grid's value list compares it, without regard to
/// case, every other value by its type and value, and a Blank only to a Blank.
/// <para>It is the tuples, never a list of values per column: two selected rows (Rates, Jan) and
/// (Credit, Feb) let through no (Rates, Feb), which per-column lists would let in quietly (spine
/// rule 1). With one column it is exactly the grid's value list filter on that column. It is not
/// capped (spine rule 5).</para>
/// </summary>
public sealed class LinkCondition
{
    private readonly HashSet<object?[]> _set;

    /// <summary>The condition on <paramref name="on"/> that lets through <paramref name="values"/>,
    /// each a tuple in the order of <paramref name="on"/>; a tuple given twice is kept once.</summary>
    /// <exception cref="ArgumentException">No column is named, or a tuple's length is not the columns'.</exception>
    public LinkCondition(IReadOnlyList<string> on, IEnumerable<IReadOnlyList<object?>> values)
    {
        ArgumentNullException.ThrowIfNull(on);
        ArgumentNullException.ThrowIfNull(values);
        if (on.Count == 0)
            throw new ArgumentException("A link compares at least one column.", nameof(on));
        On = [.. on];
        _set = new HashSet<object?[]>(TupleComparer.Instance);
        var kept = new List<IReadOnlyList<object?>>();
        foreach (var value in values)
        {
            ArgumentNullException.ThrowIfNull(value, nameof(values));
            if (value.Count != On.Count)
                throw new ArgumentException($"A tuple holds one value per column; {On.Count} are compared.", nameof(values));
            var tuple = value.ToArray();
            if (_set.Add(tuple))
                kept.Add(Array.AsReadOnly(tuple));
        }
        Values = kept.AsReadOnly();
    }

    /// <summary>The columns compared.</summary>
    public IReadOnlyList<string> On { get; }

    /// <summary>The tuples let through, each once, in the order first selected.</summary>
    public IReadOnlyList<IReadOnlyList<object?>> Values { get; }

    /// <summary>Whether <paramref name="tuple"/>, the values of <see cref="On"/> in their order, is let through.</summary>
    public bool Matches(object?[] tuple) => _set.Contains(tuple);

    /// <summary>Whether the row <paramref name="row"/> of <paramref name="snapshot"/> is let through.</summary>
    internal bool Matches(Snapshot snapshot, SnapshotColumn[] columns, SnapshotRow row, object?[] scratch)
    {
        for (var i = 0; i < columns.Length; i++)
            scratch[i] = snapshot.ValueAt(row, columns[i]);
        return _set.Contains(scratch);
    }

    private sealed class TupleComparer : IEqualityComparer<object?[]>
    {
        public static readonly TupleComparer Instance = new();

        public bool Equals(object?[]? x, object?[]? y)
        {
            if (x is null || y is null)
                return x is null && y is null;
            if (x.Length != y.Length)
                return false;
            for (var i = 0; i < x.Length; i++)
            {
                if (!Same(x[i], y[i]))
                    return false;
            }
            return true;
        }

        public int GetHashCode(object?[] tuple)
        {
            var hash = new HashCode();
            foreach (var value in tuple)
                hash.Add(value is string text ? StringComparer.OrdinalIgnoreCase.GetHashCode(text) : value?.GetHashCode() ?? 0);
            return hash.ToHashCode();
        }

        // As the grid's value list compares (GridSource.From): text without regard to case.
        private static bool Same(object? a, object? b) => a is string x && b is string y
            ? string.Equals(x, y, StringComparison.OrdinalIgnoreCase)
            : Equals(a, b);
    }
}

/// <summary>Narrowed copies of a Snapshot, for the views that read a Snapshot whole.</summary>
internal static class Narrowing
{
    /// <summary>The positions in <paramref name="snapshot"/>'s base order of the rows every one of
    /// <paramref name="conditions"/> lets through.</summary>
    public static List<int> Positions(Snapshot snapshot, IReadOnlyList<LinkCondition> conditions)
    {
        var tests = conditions.Select(c => (Condition: c, Columns: c.On.Select(n => snapshot[n]).ToArray(), Scratch: new object?[c.On.Count])).ToArray();
        var rows = snapshot.Rows;
        var kept = new List<int>();
        for (var i = 0; i < rows.Count; i++)
        {
            var through = true;
            foreach (var (condition, columns, scratch) in tests)
            {
                if (!condition.Matches(snapshot, columns, rows[i], scratch))
                {
                    through = false;
                    break;
                }
            }
            if (through)
                kept.Add(i);
        }
        return kept;
    }

    /// <summary>A Snapshot of the rows of <paramref name="snapshot"/> at <paramref name="positions"/>,
    /// in that order, its columns as they are.</summary>
    public static Snapshot Copy(Snapshot snapshot, IReadOnlyList<int> positions)
    {
        var builder = new SnapshotColumnsBuilder { Version = snapshot.Version };
        var rows = snapshot.Rows;
        foreach (var column in snapshot.Columns)
        {
            ColumnBuilder target = column.Kind switch
            {
                SnapshotKind.Text => builder.Text(column.Name, column.Caption),
                SnapshotKind.Decimal => builder.Decimal(column.Name, column.Caption),
                SnapshotKind.Double => builder.Double(column.Name, column.Caption),
                SnapshotKind.Integer => builder.Integer(column.Name, column.Caption),
                SnapshotKind.Date => builder.Date(column.Name, column.Caption),
                SnapshotKind.Boolean => builder.Boolean(column.Name, column.Caption),
                _ => throw new InvalidOperationException($"Unknown kind {column.Kind}."),
            };
            foreach (var position in positions)
            {
                switch (snapshot.ValueAt(rows[position], column), target)
                {
                    case (null, _):
                        target.AppendBlank();
                        break;
                    case (string text, TextColumnBuilder to):
                        to.Append(text);
                        break;
                    case (decimal number, DecimalColumnBuilder to):
                        to.Append(number);
                        break;
                    case (double number, DoubleColumnBuilder to):
                        to.Append(number);
                        break;
                    case (long number, IntegerColumnBuilder to):
                        to.Append(number);
                        break;
                    case (DateTime date, DateColumnBuilder to):
                        to.Append(date);
                        break;
                    case (bool flag, BooleanColumnBuilder to):
                        to.Append(flag);
                        break;
                    case var (value, _):
                        throw new InvalidOperationException($"Column '{column.Name}' holds a {value.GetType().Name}.");
                }
            }
        }
        return builder.Build();
    }
}
