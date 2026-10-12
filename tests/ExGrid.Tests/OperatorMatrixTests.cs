using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// FL-6 (ADR-0023): every operator × every column type, against the reference engine that
/// <c>GridSource.From</c> runs. An offered pair has cases of its own here — a value either
/// side of the operand, the boundary, and a Blank — and a pair not offered is refused naming
/// the column. The completeness test reads both enums, so an operator or a column type added
/// later fails here until its row of the matrix is written.
/// </summary>
public class OperatorMatrixTests
{
    private static readonly DateTime Day = new(2026, 8, 30, 9, 15, 0);

    /// <summary>One column per type: the cell is put on it, the clause is applied to it.</summary>
    private static (string Column, Trade Row) Cell(ColumnType type, object? value) => type switch
    {
        ColumnType.Text => ("Book", new Trade(Book: (string?)value)),
        ColumnType.Number => ("Amount", new Trade(Amount: value)),
        ColumnType.Date => ("TradedOn", new Trade(TradedOn: value)),
        ColumnType.Boolean => ("Cleared", new Trade(Cleared: value)),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private static bool Matches(ColumnType type, object? cell, FilterClause clause)
    {
        var (column, row) = Cell(type, cell);
        return TradeColumns.Matches(row, TradeColumns.FilterOn(column, clause));
    }

    /// <summary>(type, operator, cell, operand, In list, expected). A null cell is a Blank.</summary>
    private static readonly (ColumnType Type, FilterOperator Op, object? Cell, object? Operand, object?[]? Values, bool Expected)[] Table =
    [
        // ── Text: ordinal, case-insensitive, culture-free; a Blank matches only IsBlank.
        (ColumnType.Text, FilterOperator.Equals, "Alpha", "ALPHA", null, true),
        (ColumnType.Text, FilterOperator.Equals, "Alpha", "Alph", null, false),
        (ColumnType.Text, FilterOperator.Equals, null, "", null, false),
        (ColumnType.Text, FilterOperator.NotEquals, "Alpha", "Beta", null, true),
        (ColumnType.Text, FilterOperator.NotEquals, "Alpha", "aLPHA", null, false),
        (ColumnType.Text, FilterOperator.NotEquals, null, "Beta", null, false),
        (ColumnType.Text, FilterOperator.Contains, "Alpha", "LPH", null, true),
        (ColumnType.Text, FilterOperator.Contains, "Alpha", "x", null, false),
        (ColumnType.Text, FilterOperator.Contains, null, "", null, false),
        (ColumnType.Text, FilterOperator.DoesNotContain, "Alpha", "x", null, true),
        (ColumnType.Text, FilterOperator.DoesNotContain, "Alpha", "lph", null, false),
        (ColumnType.Text, FilterOperator.DoesNotContain, null, "x", null, false),
        (ColumnType.Text, FilterOperator.StartsWith, "Alpha", "aL", null, true),
        (ColumnType.Text, FilterOperator.StartsWith, "Alpha", "lpha", null, false),
        (ColumnType.Text, FilterOperator.StartsWith, null, "", null, false),
        (ColumnType.Text, FilterOperator.EndsWith, "Alpha", "PHA", null, true),
        (ColumnType.Text, FilterOperator.EndsWith, "Alpha", "Alp", null, false),
        (ColumnType.Text, FilterOperator.EndsWith, null, "", null, false),
        // The Turkish dotless i is not folded to "i": ordinal, never the current culture.
        (ColumnType.Text, FilterOperator.Equals, "ı", "I", null, false),
        (ColumnType.Text, FilterOperator.In, "beta", null, ["Alpha", "BETA"], true),
        (ColumnType.Text, FilterOperator.In, "Gamma", null, ["Alpha", "Beta"], false),
        (ColumnType.Text, FilterOperator.In, null, null, ["Alpha", null], true),
        (ColumnType.Text, FilterOperator.In, null, null, ["Alpha"], false),
        (ColumnType.Text, FilterOperator.IsBlank, null, null, null, true),
        (ColumnType.Text, FilterOperator.IsBlank, "", null, null, false),
        (ColumnType.Text, FilterOperator.IsNotBlank, "", null, null, true),
        (ColumnType.Text, FilterOperator.IsNotBlank, null, null, null, false),

        // ── Number: one decimal number line across runtime types.
        (ColumnType.Number, FilterOperator.Equals, 2.5m, 2.5d, null, true),
        (ColumnType.Number, FilterOperator.Equals, 2, 2.5m, null, false),
        (ColumnType.Number, FilterOperator.Equals, null, 0, null, false),
        (ColumnType.Number, FilterOperator.NotEquals, 2, 3L, null, true),
        (ColumnType.Number, FilterOperator.NotEquals, 3, 3m, null, false),
        (ColumnType.Number, FilterOperator.NotEquals, null, 3, null, false),
        (ColumnType.Number, FilterOperator.GreaterThan, 3, 2.99m, null, true),
        (ColumnType.Number, FilterOperator.GreaterThan, 3, 3, null, false),
        (ColumnType.Number, FilterOperator.GreaterThan, null, -1_000_000, null, false),
        (ColumnType.Number, FilterOperator.GreaterThanOrEqual, 3, 3m, null, true),
        (ColumnType.Number, FilterOperator.GreaterThanOrEqual, 2.99m, 3, null, false),
        (ColumnType.Number, FilterOperator.GreaterThanOrEqual, null, -1_000_000, null, false),
        (ColumnType.Number, FilterOperator.LessThan, -1, 0, null, true),
        (ColumnType.Number, FilterOperator.LessThan, 0, 0m, null, false),
        (ColumnType.Number, FilterOperator.LessThan, null, 1_000_000, null, false),
        (ColumnType.Number, FilterOperator.LessThanOrEqual, 0, 0m, null, true),
        (ColumnType.Number, FilterOperator.LessThanOrEqual, 0.01m, 0, null, false),
        (ColumnType.Number, FilterOperator.LessThanOrEqual, null, 1_000_000, null, false),
        (ColumnType.Number, FilterOperator.In, 2L, null, [1m, 2d], true),
        (ColumnType.Number, FilterOperator.In, 3, null, [1m, 2d], false),
        (ColumnType.Number, FilterOperator.In, null, null, [1m, null], true),
        (ColumnType.Number, FilterOperator.IsBlank, null, null, null, true),
        (ColumnType.Number, FilterOperator.IsBlank, 0, null, null, false),
        (ColumnType.Number, FilterOperator.IsNotBlank, 0, null, null, true),
        (ColumnType.Number, FilterOperator.IsNotBlank, null, null, null, false),

        // ── Date: chronological, exact to the tick, never truncated to the day.
        (ColumnType.Date, FilterOperator.Equals, Day, Day, null, true),
        (ColumnType.Date, FilterOperator.Equals, Day, Day.Date, null, false),
        (ColumnType.Date, FilterOperator.Equals, null, Day, null, false),
        (ColumnType.Date, FilterOperator.NotEquals, Day, Day.Date, null, true),
        (ColumnType.Date, FilterOperator.NotEquals, Day, Day, null, false),
        (ColumnType.Date, FilterOperator.NotEquals, null, Day, null, false),
        (ColumnType.Date, FilterOperator.GreaterThan, Day, Day.AddTicks(-1), null, true),
        (ColumnType.Date, FilterOperator.GreaterThan, Day, Day, null, false),
        (ColumnType.Date, FilterOperator.GreaterThan, null, DateTime.MinValue, null, false),
        (ColumnType.Date, FilterOperator.GreaterThanOrEqual, Day, Day, null, true),
        (ColumnType.Date, FilterOperator.GreaterThanOrEqual, Day.AddTicks(-1), Day, null, false),
        (ColumnType.Date, FilterOperator.GreaterThanOrEqual, null, DateTime.MinValue, null, false),
        (ColumnType.Date, FilterOperator.LessThan, Day.Date, Day, null, true),
        (ColumnType.Date, FilterOperator.LessThan, Day, Day, null, false),
        (ColumnType.Date, FilterOperator.LessThan, null, DateTime.MaxValue, null, false),
        (ColumnType.Date, FilterOperator.LessThanOrEqual, Day, Day, null, true),
        (ColumnType.Date, FilterOperator.LessThanOrEqual, Day.AddTicks(1), Day, null, false),
        (ColumnType.Date, FilterOperator.LessThanOrEqual, null, DateTime.MaxValue, null, false),
        (ColumnType.Date, FilterOperator.In, Day, null, [Day.Date, Day], true),
        (ColumnType.Date, FilterOperator.In, Day, null, [Day.Date], false),
        (ColumnType.Date, FilterOperator.In, null, null, [Day, null], true),
        (ColumnType.Date, FilterOperator.IsBlank, null, null, null, true),
        (ColumnType.Date, FilterOperator.IsBlank, DateTime.MinValue, null, null, false),
        (ColumnType.Date, FilterOperator.IsNotBlank, DateTime.MinValue, null, null, true),
        (ColumnType.Date, FilterOperator.IsNotBlank, null, null, null, false),

        // ── Boolean: the flag, the value list, and blankness.
        (ColumnType.Boolean, FilterOperator.Equals, true, true, null, true),
        (ColumnType.Boolean, FilterOperator.Equals, false, true, null, false),
        (ColumnType.Boolean, FilterOperator.Equals, null, false, null, false),
        (ColumnType.Boolean, FilterOperator.In, false, null, [false], true),
        (ColumnType.Boolean, FilterOperator.In, true, null, [false], false),
        (ColumnType.Boolean, FilterOperator.In, null, null, [true, null], true),
        (ColumnType.Boolean, FilterOperator.IsBlank, null, null, null, true),
        (ColumnType.Boolean, FilterOperator.IsBlank, false, null, null, false),
        (ColumnType.Boolean, FilterOperator.IsNotBlank, false, null, null, true),
        (ColumnType.Boolean, FilterOperator.IsNotBlank, null, null, null, false),
    ];

    public static IEnumerable<object?[]> Cases
        => Table.Select(c => new object?[] { c.Type, c.Op, c.Cell, c.Operand, c.Values, c.Expected });

    [Theory] // FL-6, ADR-0023: one case of the operator × column type matrix
    [MemberData(nameof(Cases))]
    public void FL6_an_offered_operator_matches_by_its_types_semantics(
        ColumnType type, FilterOperator op, object? cell, object? operand, object?[]? values, bool expected)
    {
        Assert.Contains(op, FilterOperators.AllowedFor(type));
        Assert.Equal(expected, Matches(type, cell, new FilterClause(op, operand, values)));
    }

    [Fact] // FL-6, ADR-0023/0009: every pair is either offered and has its cases, or refused naming the column
    public void FL6_every_operator_on_every_column_type_is_either_tested_or_refused()
    {
        var tested = Table.Select(c => (c.Type, c.Op)).ToHashSet();
        foreach (var type in Enum.GetValues<ColumnType>())
        {
            foreach (var op in Enum.GetValues<FilterOperator>())
            {
                if (FilterOperators.AllowedFor(type).Contains(op))
                {
                    // Offered: at least one case that matches and one that does not, and a Blank.
                    var cases = Table.Where(c => c.Type == type && c.Op == op).ToArray();
                    Assert.True(cases.Any(c => c.Expected) && cases.Any(c => !c.Expected),
                        $"{op} on {type} needs a matching and a non-matching case");
                    Assert.True(cases.Any(c => c.Cell is null), $"{op} on {type} needs a Blank case");
                }
                else
                {
                    Assert.DoesNotContain((type, op), tested);
                    var (column, row) = Cell(type, Operand(type));
                    var refusal = Assert.Throws<InvalidOperationException>(() => TradeColumns.Matches(
                        row, TradeColumns.FilterOn(column, new FilterClause(op, Operand(type)))));
                    Assert.Contains(column, refusal.Message);
                }
            }
        }
    }

    private static object Operand(ColumnType type) => type switch
    {
        ColumnType.Text => "Alpha",
        ColumnType.Number => 1m,
        ColumnType.Date => Day,
        _ => true,
    };
}
