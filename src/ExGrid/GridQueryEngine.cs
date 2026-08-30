using System.Runtime.ExceptionServices;

namespace ExGrid;

/// <summary>
/// The reference implementation of Filter and Sort semantics — its behaviour is the
/// specification, and server-side implementations are written to match (ADR-0001,
/// ADR-0023). Stateless, so instances can never interfere (ADR-0018).
///
/// Anything ambiguous is refused with an exception naming the column, never guessed:
/// unknown column names, operators the declared type does not allow, values whose
/// runtime type contradicts the declaration, clauses missing their operand. A whole
/// Query is validated up front, before any row is looked at — a malformed Query is
/// refused even over an empty or all-Blank row set.
/// </summary>
public static class GridQueryEngine
{
    /// <summary>Applies <paramref name="filter"/> then <paramref name="sorts"/>. The sort is stable.</summary>
    public static IReadOnlyList<TRow> Apply<TRow>(
        IEnumerable<TRow> rows,
        IReadOnlyList<ColumnInfo<TRow>> columns,
        GridFilter? filter,
        IReadOnlyList<SortSpec>? sorts)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var byName = IndexColumns(columns);
        var prepared = filter is null ? null : Prepare(byName, filter);

        IEnumerable<TRow> result = rows;
        if (prepared is not null)
            result = result.Where(row => Matches(row, prepared));
        if (sorts is { Count: > 0 })
            return Sort(result, byName, sorts);
        return result.ToList();
    }

    public static bool Matches<TRow>(TRow row, IReadOnlyList<ColumnInfo<TRow>> columns, GridFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return Matches(row, Prepare(IndexColumns(columns), filter));
    }

    private static Dictionary<string, ColumnInfo<TRow>> IndexColumns<TRow>(IReadOnlyList<ColumnInfo<TRow>> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        var byName = new Dictionary<string, ColumnInfo<TRow>>(columns.Count, StringComparer.Ordinal);
        foreach (var column in columns)
        {
            if (!byName.TryAdd(column.Name, column))
                throw new InvalidOperationException($"Two columns are named '{column.Name}'.");
        }

        return byName;
    }

    private static ColumnInfo<TRow> Resolve<TRow>(Dictionary<string, ColumnInfo<TRow>> columns, string name)
        => columns.TryGetValue(name, out var column)
            ? column
            : throw new InvalidOperationException($"Unknown column '{name}'.");

    // ---- Filter: validate the Query once, then evaluate rows against it --------

    private sealed record PreparedClause(
        FilterOperator Operator,
        object? Operand,
        IReadOnlyList<object?>? Operands);

    private sealed record PreparedColumn<TRow>(
        ColumnInfo<TRow> Column,
        FilterCombinator Combinator,
        IReadOnlyList<PreparedClause> Clauses);

    private static List<PreparedColumn<TRow>> Prepare<TRow>(
        Dictionary<string, ColumnInfo<TRow>> columns, GridFilter filter)
    {
        // Columns combine with And (ADR-0009). The Opaque Filter is ignored (ADR-0023).
        var prepared = new List<PreparedColumn<TRow>>(filter.Columns.Count);
        foreach (var (name, spec) in filter.Columns)
        {
            var column = Resolve(columns, name);
            if (spec.Clauses is not { Count: > 0 })
                throw new InvalidOperationException(
                    $"The filter on column '{column.Name}' has no clauses. Remove the column from the Filter instead.");

            var clauses = new List<PreparedClause>(spec.Clauses.Count);
            foreach (var clause in spec.Clauses)
            {
                if (!FilterOperators.AllowedFor(column.Type).Contains(clause.Operator))
                    throw new InvalidOperationException(
                        $"Operator {clause.Operator} is not allowed on column '{column.Name}' ({column.Type}).");
                ValidateOperands(column, clause);

                clauses.Add(clause.Operator switch
                {
                    FilterOperator.IsBlank or FilterOperator.IsNotBlank
                        => new PreparedClause(clause.Operator, null, null),
                    FilterOperator.In => new PreparedClause(clause.Operator, null,
                        clause.Values!
                            .Select(v => v is null ? null : NormalizeOperand(column, v))
                            .ToList()),
                    _ => new PreparedClause(clause.Operator, NormalizeOperand(column, clause.Value!), null),
                });
            }

            prepared.Add(new PreparedColumn<TRow>(column, spec.Combinator, clauses));
        }

        return prepared;
    }

    private static bool Matches<TRow>(TRow row, List<PreparedColumn<TRow>> prepared)
    {
        foreach (var (column, combinator, clauses) in prepared)
        {
            var cell = NormalizeCell(column, column.Value(row));
            var matches = combinator switch
            {
                FilterCombinator.And => clauses.All(clause => MatchesClause(column, cell, clause)),
                FilterCombinator.Or => clauses.Any(clause => MatchesClause(column, cell, clause)),
                _ => throw new ArgumentOutOfRangeException(nameof(prepared), combinator, null),
            };
            if (!matches)
                return false;
        }

        return true;
    }

    private static bool MatchesClause<TRow>(ColumnInfo<TRow> column, object? cell, PreparedClause clause)
    {
        // A Blank matches only IsBlank, and an In list explicitly containing null (ADR-0023).
        if (cell is null)
        {
            return clause.Operator switch
            {
                FilterOperator.IsBlank => true,
                FilterOperator.In => clause.Operands!.Contains(null),
                _ => false,
            };
        }

        return clause.Operator switch
        {
            FilterOperator.IsBlank => false,
            FilterOperator.IsNotBlank => true,
            FilterOperator.In => clause.Operands!.Any(
                candidate => candidate is not null && AreEqual(column, cell, candidate)),
            FilterOperator.Equals => AreEqual(column, cell, clause.Operand!),
            FilterOperator.NotEquals => !AreEqual(column, cell, clause.Operand!),
            FilterOperator.GreaterThan => Compare(column, cell, clause.Operand!) > 0,
            FilterOperator.GreaterThanOrEqual => Compare(column, cell, clause.Operand!) >= 0,
            FilterOperator.LessThan => Compare(column, cell, clause.Operand!) < 0,
            FilterOperator.LessThanOrEqual => Compare(column, cell, clause.Operand!) <= 0,
            FilterOperator.Contains => ((string)cell).Contains((string)clause.Operand!, StringComparison.OrdinalIgnoreCase),
            FilterOperator.DoesNotContain => !((string)cell).Contains((string)clause.Operand!, StringComparison.OrdinalIgnoreCase),
            FilterOperator.StartsWith => ((string)cell).StartsWith((string)clause.Operand!, StringComparison.OrdinalIgnoreCase),
            FilterOperator.EndsWith => ((string)cell).EndsWith((string)clause.Operand!, StringComparison.OrdinalIgnoreCase),
            _ => throw new ArgumentOutOfRangeException(nameof(clause), clause.Operator, null),
        };
    }

    private static bool AreEqual<TRow>(ColumnInfo<TRow> column, object cell, object operand)
        => column.Type switch
        {
            ColumnType.Text => string.Equals((string)cell, (string)operand, StringComparison.OrdinalIgnoreCase),
            ColumnType.Number => (decimal)cell == (decimal)operand,
            ColumnType.Date => CompareDates(column.Name, cell, operand) == 0,
            ColumnType.Boolean => (bool)cell == (bool)operand,
            _ => throw new ArgumentOutOfRangeException(nameof(column), column.Type, null),
        };

    private static int Compare<TRow>(ColumnInfo<TRow> column, object cell, object operand)
        => column.Type switch
        {
            // Only Number and Date allow ordering operators (ADR-0023).
            ColumnType.Number => ((decimal)cell).CompareTo((decimal)operand),
            ColumnType.Date => CompareDates(column.Name, cell, operand),
            _ => throw new ArgumentOutOfRangeException(nameof(column), column.Type, null),
        };

    private static void ValidateOperands<TRow>(ColumnInfo<TRow> column, FilterClause clause)
    {
        var (wantsValue, wantsValues) = clause.Operator switch
        {
            FilterOperator.IsBlank or FilterOperator.IsNotBlank => (false, false),
            FilterOperator.In => (false, true),
            _ => (true, false),
        };

        if (wantsValues && clause.Values is not { Count: > 0 })
            throw new InvalidOperationException(
                $"Operator In on column '{column.Name}' requires a non-empty Values list.");
        if (!wantsValues && clause.Values is not null)
            throw new InvalidOperationException(
                $"Operator {clause.Operator} on column '{column.Name}' does not take a Values list.");
        if (wantsValue && clause.Value is null)
            throw new InvalidOperationException(
                $"Operator {clause.Operator} on column '{column.Name}' requires a value. To select Blanks, use IsBlank.");
        if (!wantsValue && clause.Value is not null)
            throw new InvalidOperationException(
                $"Operator {clause.Operator} on column '{column.Name}' does not take a value.");
    }

    // ---- Sort ------------------------------------------------------------------

    private static List<TRow> Sort<TRow>(
        IEnumerable<TRow> rows,
        Dictionary<string, ColumnInfo<TRow>> columns,
        IReadOnlyList<SortSpec> sorts)
    {
        IOrderedEnumerable<TRow>? ordered = null;
        foreach (var spec in sorts)
        {
            var column = Resolve(columns, spec.Column);
            object? Key(TRow row) => NormalizeCell(column, column.Value(row));

            // Always ThenBy: the direction lives inside the comparer, so the fixed
            // Blank ordering is never inverted with it (ADR-0023).
            var comparer = new CellComparer(column.Name, column.Type, spec.Direction);
            ordered = ordered is null ? rows.OrderBy(Key, comparer) : ordered.ThenBy(Key, comparer);
        }

        try
        {
            return ordered!.ToList(); // sorts is non-empty; LINQ's OrderBy keeps ties stable (ADR-0023)
        }
        catch (InvalidOperationException wrapped) when (wrapped.InnerException is InvalidOperationException refusal)
        {
            // LINQ wraps comparer exceptions as "Failed to compare two elements in the
            // array"; resurface the refusal that names the column (ADR-0023).
            ExceptionDispatchInfo.Capture(refusal).Throw();
            throw; // unreachable
        }
    }

    private sealed class CellComparer(string columnName, ColumnType type, SortDirection direction) : IComparer<object?>
    {
        public int Compare(object? x, object? y)
        {
            // Blanks last in BOTH directions (ADR-0023): their ordering is fixed and
            // deliberately outside the direction inversion below.
            if (x is null || y is null)
                return (x is null ? 1 : 0) - (y is null ? 1 : 0);

            var result = type switch
            {
                ColumnType.Text => StringComparer.OrdinalIgnoreCase.Compare((string)x, (string)y),
                ColumnType.Number => ((decimal)x).CompareTo((decimal)y),
                ColumnType.Date => CompareDates(columnName, x, y),
                ColumnType.Boolean => ((bool)x).CompareTo((bool)y),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
            };
            return direction == SortDirection.Ascending ? result : -Math.Sign(result);
        }
    }

    // ---- Value normalisation (declared type is the law — ADR-0002, ADR-0023) ----

    private static object? NormalizeCell<TRow>(ColumnInfo<TRow> column, object? value)
        => Normalize(column, value, isOperand: false);

    private static object NormalizeOperand<TRow>(ColumnInfo<TRow> column, object value)
        => Normalize(column, value, isOperand: true)!;

    private static object? Normalize<TRow>(ColumnInfo<TRow> column, object? value, bool isOperand)
    {
        if (value is null)
            return null;

        switch (column.Type)
        {
            case ColumnType.Text when value is string:
            case ColumnType.Boolean when value is bool:
            case ColumnType.Date when value is DateTime or DateTimeOffset or DateOnly:
                return value;
            case ColumnType.Number:
                return value switch
                {
                    decimal d => d,
                    byte or sbyte or short or ushort or int or uint or long or ulong
                        => Convert.ToDecimal(value),
                    float f when !float.IsFinite(f) => throw Mismatch(column, value, isOperand, "is not a finite number"),
                    double f when !double.IsFinite(f) => throw Mismatch(column, value, isOperand, "is not a finite number"),
                    float f => ToDecimalChecked(column, f, isOperand),
                    double f => ToDecimalChecked(column, f, isOperand),
                    _ => throw Mismatch(column, value, isOperand, null),
                };
            default:
                throw Mismatch(column, value, isOperand, null);
        }
    }

    private static object ToDecimalChecked<TRow>(ColumnInfo<TRow> column, double value, bool isOperand)
    {
        try
        {
            return (decimal)value;
        }
        catch (OverflowException)
        {
            throw Mismatch(column, value, isOperand, "is outside the range a Number can represent");
        }
    }

    private static int CompareDates(string columnName, object x, object y)
        => x.GetType() == y.GetType()
            ? ((IComparable)x).CompareTo(y)
            : throw new InvalidOperationException(
                $"Column '{columnName}' (Date) mixes {x.GetType().Name} and {y.GetType().Name}; one date type per column.");

    private static InvalidOperationException Mismatch<TRow>(
        ColumnInfo<TRow> column, object value, bool isOperand, string? reason)
    {
        var role = isOperand ? "the filter value" : "the value accessor returned";
        reason ??= "contradicts the declared type";
        return new InvalidOperationException(
            $"Column '{column.Name}' ({column.Type}): {role} '{value}' ({value.GetType().Name}) {reason}.");
    }
}
