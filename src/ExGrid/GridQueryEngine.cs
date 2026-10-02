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

    /// <summary>Whether one row passes <paramref name="filter"/>, under exactly the
    /// semantics <see cref="Apply{TRow}"/> filters by. The Filter is validated first, so a
    /// malformed one is refused whatever the row holds.</summary>
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
    {
        if (!columns.TryGetValue(name, out var column))
            throw new InvalidOperationException($"Unknown column '{name}'.");
        // An Action Column carries no value, so every row would compare equal and a sort
        // would shuffle the result into an order nobody could account for. Refused here,
        // where every other ambiguity is (ADR-0020).
        if (!column.IsQueryable)
        {
            throw new InvalidOperationException(
                $"Column '{name}' carries actions only and has no value, so it cannot be sorted or filtered.");
        }

        return column;
    }

    // ---- Filter: validate the Query once, then evaluate rows against it --------

    private sealed record PreparedClause(
        FilterOperator Operator,
        object? Operand,
        IReadOnlyList<object?>? Operands,
        object? OperandSet = null,
        bool OperandsContainBlank = false);

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
            if (spec.Combinator is not (FilterCombinator.And or FilterCombinator.Or))
                throw new InvalidOperationException(
                    $"The filter on column '{column.Name}' has an unknown combinator ({(int)spec.Combinator}).");

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
                    FilterOperator.In => PrepareIn(column, clause),
                    _ => new PreparedClause(clause.Operator, NormalizeOperand(column, clause.Value!), null),
                });
            }

            prepared.Add(new PreparedColumn<TRow>(column, spec.Combinator, clauses));
        }

        return prepared;
    }

    private static PreparedClause PrepareIn<TRow>(ColumnInfo<TRow> column, FilterClause clause)
    {
        var operands = clause.Values!
            .Select(v => v is null ? null : NormalizeOperand(column, v))
            .ToList();

        // A value list can hold tens of thousands of entries (ADR-0009); membership is a
        // set built once here, not a per-row scan. Date joins in: within the single date
        // type per column (checked at Prepare, held against every cell at evaluation)
        // each date type's own Equals agrees with CompareCells — DateTime by ticks,
        // DateTimeOffset by instant, DateOnly by day (ADR-0023).
        object? set = column.Type switch
        {
            ColumnType.Text => new HashSet<string>(operands.OfType<string>(), StringComparer.OrdinalIgnoreCase),
            ColumnType.Number => new HashSet<decimal>(operands.OfType<decimal>()),
            ColumnType.Boolean => new HashSet<bool>(operands.OfType<bool>()),
            ColumnType.Date => new HashSet<object>(operands.Where(o => o is not null)!),
            _ => null,
        };
        return new PreparedClause(FilterOperator.In, null, operands, set, operands.Contains(null));
    }

    private static bool Matches<TRow>(TRow row, List<PreparedColumn<TRow>> prepared)
    {
        foreach (var (column, combinator, clauses) in prepared)
        {
            // Held to the declared type eagerly at extraction, not lazily inside whichever
            // operator happens to compare — the same data must be accepted or refused
            // regardless of operator (ADR-0023).
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
                FilterOperator.In => clause.OperandsContainBlank,
                _ => false,
            };
        }

        return clause.Operator switch
        {
            FilterOperator.IsBlank => false,
            FilterOperator.IsNotBlank => true,
            FilterOperator.In => clause.OperandSet switch
            {
                HashSet<string> set => set.Contains((string)cell),
                HashSet<decimal> set => set.Contains((decimal)cell),
                HashSet<bool> set => set.Contains((bool)cell),
                HashSet<object> set => set.Contains(cell),
                _ => throw new ArgumentOutOfRangeException(nameof(clause), clause.OperandSet, null),
            },
            FilterOperator.Equals => CompareCells(column, cell, clause.Operand!) == 0,
            FilterOperator.NotEquals => CompareCells(column, cell, clause.Operand!) != 0,
            FilterOperator.GreaterThan => CompareCells(column, cell, clause.Operand!) > 0,
            FilterOperator.GreaterThanOrEqual => CompareCells(column, cell, clause.Operand!) >= 0,
            FilterOperator.LessThan => CompareCells(column, cell, clause.Operand!) < 0,
            FilterOperator.LessThanOrEqual => CompareCells(column, cell, clause.Operand!) <= 0,
            FilterOperator.Contains => ((string)cell).Contains((string)clause.Operand!, StringComparison.OrdinalIgnoreCase),
            FilterOperator.DoesNotContain => !((string)cell).Contains((string)clause.Operand!, StringComparison.OrdinalIgnoreCase),
            FilterOperator.StartsWith => ((string)cell).StartsWith((string)clause.Operand!, StringComparison.OrdinalIgnoreCase),
            FilterOperator.EndsWith => ((string)cell).EndsWith((string)clause.Operand!, StringComparison.OrdinalIgnoreCase),
            _ => throw new ArgumentOutOfRangeException(nameof(clause), clause.Operator, null),
        };
    }

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
        // The whole Sorts list is validated before any row is touched, like Prepare: an
        // out-of-range direction must not sort plausibly (refuse, do not coerce).
        var levels = new (ColumnInfo<TRow> Column, SortDirection Direction)[sorts.Count];
        for (var i = 0; i < sorts.Count; i++)
        {
            var spec = sorts[i];
            if (spec.Direction is not (SortDirection.Ascending or SortDirection.Descending))
                throw new InvalidOperationException(
                    $"Sort on column '{spec.Column}' has an unknown direction ({(int)spec.Direction}).");
            levels[i] = (Resolve(columns, spec.Column), spec.Direction);
        }

        var buffer = rows.ToArray();

        // Keys are extracted eagerly for every row and every level by this code — not
        // left to LINQ's buffering behaviour — so the one-date-type-per-column refusal
        // fires for the same Query and data regardless of what ties with what
        // (ADR-0023), and no comparer ever throws mid-sort.
        var keys = new object?[levels.Length][];
        for (var level = 0; level < levels.Length; level++)
        {
            var column = levels[level].Column;
            var levelKeys = new object?[buffer.Length];
            for (var i = 0; i < buffer.Length; i++)
                levelKeys[i] = NormalizeCell(column, column.Value(buffer[i]));
            keys[level] = levelKeys;
        }

        var indices = new int[buffer.Length];
        for (var i = 0; i < indices.Length; i++)
            indices[i] = i;
        Array.Sort(indices, (a, b) =>
        {
            // An indexed loop: this comparator runs ~n log n times, and iterator
            // allocations here (a Zip enumerator, say) multiply into GC pressure on
            // every sort gesture.
            for (var level = 0; level < levels.Length; level++)
            {
                var (column, direction) = levels[level];
                var result = CompareKeys(column, keys[level][a], keys[level][b], direction);
                if (result != 0)
                    return result;
            }
            return a - b; // Array.Sort is unstable; the original index keeps equal keys in input order (ADR-0023)
        });

        var sorted = new List<TRow>(buffer.Length);
        foreach (var index in indices)
            sorted.Add(buffer[index]);
        return sorted;
    }

    private static int CompareKeys<TRow>(ColumnInfo<TRow> column, object? x, object? y, SortDirection direction)
    {
        // Blanks last in BOTH directions (ADR-0023): their ordering is fixed and
        // deliberately outside the direction inversion below.
        if (x is null || y is null)
            return (x is null ? 1 : 0) - (y is null ? 1 : 0);
        var result = CompareCells(column, x, y);
        return direction == SortDirection.Ascending ? result : -Math.Sign(result);
    }

    /// <summary>The one collation filter and sort share (ADR-0023) — every two-cell
    /// comparison in the engine goes through here, so the two paths cannot drift.</summary>
    private static int CompareCells<TRow>(ColumnInfo<TRow> column, object x, object y)
        => column.Type switch
        {
            ColumnType.Text => StringComparer.OrdinalIgnoreCase.Compare((string)x, (string)y),
            ColumnType.Number => ((decimal)x).CompareTo((decimal)y),
            ColumnType.Date => ((IComparable)x).CompareTo(y),
            ColumnType.Boolean => ((bool)x).CompareTo((bool)y),
            _ => throw new ArgumentOutOfRangeException(nameof(column), column.Type, null),
        };

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
                return value;
            // Each date type follows its own native comparison (ADR-0023): DateTime by its
            // wall-clock ticks (DateTimeKind is not part of the value), DateTimeOffset by the
            // instant it names (the offset is presentation), DateOnly by its day. These match
            // what .NET itself and a SQL server do with the same data, so server-side
            // implementations agree for free. Which of them a column holds is declared, and
            // every cell and operand is held to it, so two values compared are always of one
            // type (section of 2026-10-02).
            case ColumnType.Date when value is DateTime or DateTimeOffset or DateOnly:
                return value.GetType() == DateClrType(column.DateType)
                    ? value
                    : throw Mismatch(column, value, isOperand, $"is not the column's declared {column.DateType}");
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

    private static Type DateClrType(DateType declared) => declared switch
    {
        DateType.DateTime => typeof(DateTime),
        DateType.DateOnly => typeof(DateOnly),
        DateType.DateTimeOffset => typeof(DateTimeOffset),
        _ => throw new ArgumentOutOfRangeException(nameof(declared), declared, null),
    };

    private static object ToDecimalChecked<TRow>(ColumnInfo<TRow> column, double value, bool isOperand)
    {
        try
        {
            var converted = (decimal)value;
            // "Beyond decimal's range" includes the small direction (ADR-0023): a
            // non-zero value that converts to 0m would silently pass Equals 0 and tie
            // with true zeros — quietly flattened, on a grid that displays risk numbers.
            if (converted == 0m && value != 0)
                throw Mismatch(column, value, isOperand, "is too small in magnitude for a Number to represent");
            return converted;
        }
        catch (OverflowException)
        {
            throw Mismatch(column, value, isOperand, "is outside the range a Number can represent");
        }
    }

    private static object ToDecimalChecked<TRow>(ColumnInfo<TRow> column, float value, bool isOperand)
    {
        // Cast the float directly: widening to double first manufactures phantom digits
        // ((decimal)(double)0.1f is 0.100000001490116) that break equality and ordering.
        try
        {
            var converted = (decimal)value;
            if (converted == 0m && value != 0)
                throw Mismatch(column, value, isOperand, "is too small in magnitude for a Number to represent");
            return converted;
        }
        catch (OverflowException)
        {
            throw Mismatch(column, value, isOperand, "is outside the range a Number can represent");
        }
    }

    private static InvalidOperationException Mismatch<TRow>(
        ColumnInfo<TRow> column, object value, bool isOperand, string? reason)
    {
        var role = isOperand ? "the filter value" : "the value accessor returned";
        reason ??= "contradicts the declared type";
        return new InvalidOperationException(
            $"Column '{column.Name}' ({column.Type}): {role} '{value}' ({value.GetType().Name}) {reason}.");
    }
}
