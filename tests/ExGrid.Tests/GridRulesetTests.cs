using ExGrid.Cells;
using ExGrid.Selection;
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

    [Fact] // ADR-0003/0034: the lookup's identity is what tells a memoised row to repaint
    public void Evaluating_hands_over_new_lookups()
    {
        var rules = Ruleset();
        var before = rules.State;
        var beforeMessage = rules.Message;

        rules.Evaluate([new Trade { Amount = -5 }], Columns());

        Assert.NotSame(before, rules.State);
        Assert.NotSame(beforeMessage, rules.Message);
        // And they answer the same as the methods behind them.
        var row = new Trade { Amount = -5 };
        rules.Evaluate([row], Columns());
        Assert.Equal(CellState.Error, rules.State(row, Columns()[1]));
        Assert.Equal("a negative notional needs approval", rules.Message(row, Columns()[1]));
    }

    private sealed record Deal(string Id, string Csa, string Curve);

    private static GridColumn<Deal>[] DealColumns() =>
    [
        new("Id", ColumnType.Text, r => r.Id),
        new("Csa", ColumnType.Text, r => r.Csa),
        new("Curve", ColumnType.Text, r => r.Curve),
    ];

    // One discount curve per CSA: every trade of a CSA with two curves breaches, and the message names the
    // other side by trade id, never by position (ADR-0381's example).
    private static IEnumerable<Breach> OneCurvePerCsa(IReadOnlyList<Deal> deals)
    {
        foreach (var csa in deals.GroupBy(d => d.Csa))
        {
            var curves = csa.GroupBy(d => d.Curve).OrderByDescending(g => g.Count()).ToList();
            if (curves.Count < 2)
                continue;
            var told = string.Join(", ", curves.Select(g => $"{g.Key} on {string.Join(" ", g.Select(d => d.Id))}"));
            yield return new Breach("one-curve-per-csa", $"CSA {csa.Key} has {curves.Count} discount curves: {told}.",
                [.. csa.Select(d => new BreachCell(d.Id, "Curve"))]);
        }
    }

    private static GridRuleset<Deal> DealRules() => new GridRuleset<Deal>(d => d.Id).Set(OneCurvePerCsa);

    [Fact] // ADR-0381 / ED-36: every trade of the CSA wears Error, by Row Key, the rows that never changed included
    public void ED36_a_set_rule_marks_every_cell_its_breach_names_by_row_key()
    {
        var rules = DealRules();
        Deal[] deals = [new("T1", "ABC", "SOFR"), new("T2", "ABC", "SOFR"), new("T3", "ABC", "FF"), new("T4", "XYZ", "FF")];

        rules.Evaluate(deals, DealColumns());

        var curve = DealColumns()[2];
        Assert.All(deals[..3], d => Assert.Equal(CellState.Error, rules.StateOf(d, curve)));
        Assert.Equal(CellState.Normal, rules.StateOf(deals[3], curve));
        Assert.Equal(CellState.Normal, rules.StateOf(deals[0], DealColumns()[1]));
        // Keyed by Row Key: a new instance of the same trade, the next version a Source hands over, is still marked.
        Assert.Equal(CellState.Error, rules.StateOf(deals[0] with { }, curve));
        Assert.Single(rules.Breaches);
    }

    [Fact] // ADR-0381 / ED-37: each involved cell carries the Breach's message, which names the others, not positions
    public void ED37_every_involved_cell_carries_the_breach_message()
    {
        var rules = DealRules();
        Deal[] deals = [new("T1", "ABC", "SOFR"), new("T2", "ABC", "SOFR"), new("T3", "ABC", "FF")];

        rules.Evaluate(deals, DealColumns());

        var expected = "CSA ABC has 2 discount curves: SOFR on T1 T2, FF on T3.";
        Assert.All(deals, d => Assert.Equal(expected, rules.MessageOf(d, DealColumns()[2])));
        Assert.Equal(expected, Assert.Single(rules.Breaches).Message);
    }

    [Fact] // ADR-0381 / ED-36: the last edit that restores one curve clears every mark, and the save may go
    public void ED36_restoring_the_rule_clears_every_mark()
    {
        var rules = DealRules();
        Deal[] broken = [new("T1", "ABC", "SOFR"), new("T2", "ABC", "FF")];
        rules.Evaluate(broken, DealColumns());

        Deal[] fixedUp = [broken[0], broken[1] with { Curve = "SOFR" }];
        rules.Evaluate(fixedUp, DealColumns());

        Assert.All(fixedUp, d => Assert.Equal(CellState.Normal, rules.StateOf(d, DealColumns()[2])));
        Assert.Empty(rules.Breaches);
    }

    [Fact] // ADR-0381 section 2: a set rule names rows by key, which an instance-keyed ruleset cannot
    public void A_set_rule_without_a_row_key_throws()
    {
        var error = Assert.Throws<InvalidOperationException>(() => new GridRuleset<Deal>().Set(OneCurvePerCsa));
        Assert.Contains("Row Key", error.Message);
    }

    [Fact] // ADR-0381, spine rule 1: a Breach naming a key no row has would mark nothing and refuse the save unseen
    public void A_breach_naming_an_unknown_row_throws()
    {
        var rules = new GridRuleset<Deal>(d => d.Id)
            .Set(_ => [new Breach("ghost", "T9 is wrong.", [new BreachCell("T9", "Curve")])]);

        var error = Assert.Throws<ArgumentException>(() => rules.Evaluate([new Deal("T1", "ABC", "SOFR")], DealColumns()));
        Assert.Contains("T9", error.Message);
    }

    [Fact] // ADR-0381, spine rule 1: a Breach naming a column no declared column is would mark nothing and refuse the save unseen
    public void A_breach_naming_an_unknown_column_throws()
    {
        var rules = new GridRuleset<Deal>(d => d.Id)
            .Set(_ => [new Breach("typo", "T1 is wrong.", [new BreachCell("T1", "curve")])]);

        var error = Assert.Throws<ArgumentException>(() => rules.Evaluate([new Deal("T1", "ABC", "SOFR")], DealColumns()));
        Assert.Contains("'curve'", error.Message);
    }

    [Fact] // ADR-0140 / ADR-0381: two rows under one Row Key would share one set of marks, and one row's would be lost
    public void Two_rows_under_one_row_key_throw()
    {
        var error = Assert.Throws<ArgumentException>(() => DealRules().Evaluate([new Deal("T1", "ABC", "SOFR"), new Deal("T1", "XYZ", "FF")], DealColumns()));
        Assert.Contains("'T1'", error.Message);
    }

    [Fact] // ADR-0381 section 3, spine rule 1: a Focus outside the rows handed over means they are not the grid's order
    public void The_error_step_refuses_a_focus_outside_the_rows_it_was_handed()
    {
        var rules = DealRules();
        Deal[] deals = [new("A", "X", "SOFR"), new("B", "X", "FF")];
        rules.Evaluate(deals, DealColumns());

        Assert.Throws<ArgumentOutOfRangeException>(() => rules.FindError(
            new GridErrorStepRequest { From = new CellPosition(120, 0), Columns = ["Id", "Csa", "Curve"], RowSequenceVersion = 1 }, deals));
    }

    [Fact] // ADR-0381 section 3 / ED-38: the step reaches an error far below the Window, in the order the grid shows
    public void ED38_the_error_step_walks_the_whole_result_in_find_order()
    {
        var rules = DealRules();
        var deals = Enumerable.Range(0, 1000).Select(i => new Deal($"T{i}", $"C{i}", "SOFR")).ToArray();
        deals[900] = deals[900] with { Csa = "C1", Curve = "FF" };
        rules.Evaluate(deals, DealColumns());
        string[] columns = ["Id", "Csa", "Curve"];

        var next = rules.FindError(new GridErrorStepRequest { From = new CellPosition(0, 0), Columns = columns, RowSequenceVersion = 1 }, deals);
        Assert.True(next.IsFound);
        Assert.Equal((1, "Curve"), (next.Row, next.Column));

        var after = rules.FindError(new GridErrorStepRequest { From = new CellPosition(1, 2), Columns = columns, RowSequenceVersion = 1 }, deals);
        Assert.Equal((900, "Curve"), (after.Row, after.Column));

        // It wraps, as Find does, and runs backward.
        var wrapped = rules.FindError(new GridErrorStepRequest { From = new CellPosition(900, 2), Columns = columns, RowSequenceVersion = 1 }, deals);
        Assert.Equal((1, "Curve"), (wrapped.Row, wrapped.Column));
        var back = rules.FindError(new GridErrorStepRequest { From = new CellPosition(1, 2), Backward = true, Columns = columns, RowSequenceVersion = 1 }, deals);
        Assert.Equal((900, "Curve"), (back.Row, back.Column));

        // A lone error cell finds itself; a hidden column is not stepped to.
        Deal[] lone = [new("A", "X", "SOFR"), new("B", "X", "FF")];
        rules.Evaluate(lone, DealColumns());
        var self = rules.FindError(new GridErrorStepRequest { From = new CellPosition(0, 2), Columns = columns, RowSequenceVersion = 1 }, [lone[0]]);
        Assert.Equal((0, "Curve"), (self.Row, self.Column));
        Assert.False(rules.FindError(new GridErrorStepRequest { Columns = ["Id", "Csa"], RowSequenceVersion = 1 }, lone).IsFound);
    }
}
