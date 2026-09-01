using ExGrid.Cells;
using ExGrid.Validation;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The bundled ruleset (ADR-0034): the Consumer's to own and drive, shipped with the
/// library so that one implementation answers whether a change arrived by a single edit,
/// by a paste, or from program code.
/// </summary>
public class GridRulesetTests
{
    private sealed class Trade
    {
        public string Book = "";
        public decimal Amount;
        public DateTime Start;
        public DateTime End;
    }

    private static GridColumn<Trade>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book),
        new("Amount", ColumnType.Number, r => r.Amount),
        new("End", ColumnType.Date, r => r.End),
    ];

    private static GridRuleset<Trade> Ruleset() => new GridRuleset<Trade>()
        // The ADR's intended split: unparseable Rejects, a disliked value Flags.
        .Column("Amount", (_, text) => !decimal.TryParse(text, out var value)
            ? EditVerdict.Reject($"'{text}' is not a number")
            : value < 0
                ? EditVerdict.Flag("a negative notional needs approval")
                : EditVerdict.Accept)
        .Row(row => row.End < row.Start
            ? [new RuleViolation("End", "the end date precedes the start")]
            : []);

    [Fact] // ADR-0034: a column rule is what a GridColumn's Validate is set to
    public void The_column_rule_is_handed_out_for_the_editor()
    {
        var rules = Ruleset();

        var validate = rules.ValidatorFor("Amount");

        Assert.NotNull(validate);
        Assert.Equal(EditVerdictKind.Reject, validate(new Trade(), "abc").Kind);
        Assert.Equal(EditVerdictKind.Flag, validate(new Trade(), "-1").Kind);
        Assert.Equal(EditVerdictKind.Accept, validate(new Trade(), "1").Kind);
        // A column with no rule declared answers nothing, which the grid reads as Accept.
        Assert.Null(rules.ValidatorFor("Book"));
    }

    [Fact] // ADR-0034: row rules span the row, so they are judged after the value is applied
    public void A_row_rule_marks_the_column_it_names()
    {
        var rules = Ruleset();
        var bad = new Trade { Start = new(2026, 3, 1), End = new(2026, 1, 1) };

        rules.Evaluate([bad], Columns());

        Assert.Equal(CellState.Error, rules.StateOf(bad, Columns()[2]));
        Assert.Equal("the end date precedes the start", rules.MessageOf(bad, Columns()[2]));
        // And nothing else on the row is marked.
        Assert.Equal(CellState.Normal, rules.StateOf(bad, Columns()[0]));
    }

    [Fact] // ADR-0034: a paste and a typed edit wear the same marks — one implementation
    public void A_column_rule_marks_a_value_that_arrived_without_an_editor()
    {
        var rules = Ruleset();
        var pasted = new Trade { Amount = -5 };

        rules.Evaluate([pasted], Columns());

        Assert.Equal(CellState.Error, rules.StateOf(pasted, Columns()[1]));
        Assert.Equal("a negative notional needs approval", rules.MessageOf(pasted, Columns()[1]));
    }

    [Fact] // ADR-0034: applying returns new instances, so what was known about the old ones goes
    public void Evaluating_again_replaces_what_was_known()
    {
        var rules = Ruleset();
        var bad = new Trade { Amount = -5 };
        rules.Evaluate([bad], Columns());

        var fixedUp = new Trade { Amount = 5 };
        rules.Evaluate([fixedUp], Columns());

        Assert.Equal(CellState.Normal, rules.StateOf(fixedUp, Columns()[1]));
        // The old instance is not in the map at all — it is not on screen either.
        Assert.Equal(CellState.Normal, rules.StateOf(bad, Columns()[1]));
    }

    [Fact] // ADR-0034: a clean row costs nothing to answer for
    public void A_row_nothing_dislikes_is_normal_everywhere()
    {
        var rules = Ruleset();
        var good = new Trade { Amount = 5, Start = new(2026, 1, 1), End = new(2026, 3, 1) };

        rules.Evaluate([good], Columns());

        foreach (var column in Columns())
        {
            Assert.Equal(CellState.Normal, rules.StateOf(good, column));
            Assert.Null(rules.MessageOf(good, column));
        }
    }
}
