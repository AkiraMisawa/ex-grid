using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

public class OverflowRuleTests
{
    // Digit 7px, padding 4px per side: "1,234.56" (8 chars) estimates at 8×7 + 8 = 64px.
    private static readonly CellTextMetrics Metrics = new(digitWidthPx: 7, cellHorizontalPaddingPx: 4);

    [Fact] // ADR-0016: a Number that fits shows the formatted value
    public void A_fitting_number_shows_its_value()
    {
        var decision = OverflowRules.Decide(ColumnType.Number, "1,234.56", 120, Metrics);

        Assert.False(decision.IsHashed);
        Assert.Equal("1,234.56", decision.DisplayText);
    }

    [Fact] // ADR-0016: a Number that does not fit becomes #### — nothing number-shaped is painted
    public void An_overflowing_number_is_hashed()
    {
        var decision = OverflowRules.Decide(ColumnType.Number, "1,234.56", 40, Metrics);

        Assert.True(decision.IsHashed);
        Assert.DoesNotContain(decision.DisplayText, c => c != '#');
    }

    [Fact] // PF-3 / ADR-0027 P5: a run of #### is the width's string, interned — never composed per cell
    public void The_same_run_of_hashes_is_the_same_string()
    {
        var one = OverflowRules.Decide(ColumnType.Number, "1,234.56", 40, Metrics);
        var another = OverflowRules.Decide(ColumnType.Number, "9,876.54", 40, Metrics);

        Assert.Same(one.DisplayText, another.DisplayText);
    }

    [Fact] // ADR-0027 P5: interning a very long run costs that run, not every shorter one on the way
    public void A_very_wide_hashed_cell_costs_only_its_own_run()
    {
        var text = new string('9', 10_000);
        // Some 5,000 hashes: a table that filled every shorter run as it grew would spend
        // about 25 MB of characters reaching this one.
        var before = GC.GetAllocatedBytesForCurrentThread();
        var decision = OverflowRules.Decide(ColumnType.Number, text, 35_000, Metrics);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(decision.IsHashed);
        Assert.True(allocated < 1_000_000, $"reaching one run of {decision.DisplayText.Length} allocated {allocated:N0} bytes");
    }

    [Fact] // ADR-0016 / ADR-0005: exactly fitting still shows — hashing is strictly past the bound
    public void An_exactly_fitting_number_shows_and_a_hair_less_hashes()
    {
        Assert.False(OverflowRules.Decide(ColumnType.Number, "1,234.56", 64, Metrics).IsHashed);
        Assert.True(OverflowRules.Decide(ColumnType.Number, "1,234.56", 63.9, Metrics).IsHashed);
    }

    [Fact] // ADR-0016: numbers AND dates — a truncated date is a different-looking valid date
    public void An_overflowing_date_is_hashed()
    {
        Assert.True(OverflowRules.Decide(ColumnType.Date, "2026-08-30", 40, Metrics).IsHashed);
    }

    [Fact] // ADR-0016: Text is cut with a CSS ellipsis — never hashed, at any width
    public void Text_never_hashes()
    {
        var decision = OverflowRules.Decide(ColumnType.Text, "Very Long Description", 0, Metrics);

        Assert.False(decision.IsHashed);
        Assert.Equal("Very Long Description", decision.DisplayText);
    }

    [Fact] // ADR-0016: Boolean is classified with Text — visibly truncated, not quietly wrong
    public void Boolean_never_hashes()
    {
        Assert.False(OverflowRules.Decide(ColumnType.Boolean, "false", 0, Metrics).IsHashed);
    }

    [Fact] // ADR-0016 / ADR-0021: pure arithmetic — every character counts one tabular digit
    public void Separators_count_at_digit_width_so_the_estimate_never_under_counts()
    {
        // "1,234" and "12345" have the same length, so they decide identically at any width.
        Assert.Equal(5 * 7 + 8, Metrics.EstimatePx(5));
        Assert.False(OverflowRules.Decide(ColumnType.Number, "1,234", 43, Metrics).IsHashed);
        Assert.True(OverflowRules.Decide(ColumnType.Number, "1,234", 42.9, Metrics).IsHashed);
        Assert.True(OverflowRules.Decide(ColumnType.Number, "12345", 42.9, Metrics).IsHashed);
    }

    [Fact] // ADR-0016: the #### fill covers the content width, as in Excel
    public void The_hash_fill_matches_the_content_width()
    {
        // 32px column - 8px padding = 24px content; 24 / 7 = 3 whole hashes.
        var decision = OverflowRules.Decide(ColumnType.Number, "12,345,678,901", 32, Metrics);

        Assert.Equal("###", decision.DisplayText);
    }

    [Fact] // ADR-0016 (2026-09-25): the fill counts # at its own width, so the run fits its cell
    public void The_hash_fill_counts_the_hash_at_its_own_width()
    {
        // # charges wide: 60px column - 16px padding = 44px content; 44 / 14 = 3 hashes.
        // Counted at the digit (9px) it was 4, and 4 x 11.731px overflowed on DejaVu Sans.
        var metrics = new CellTextMetrics(14, 9, 5, 8);

        var decision = OverflowRules.Decide(ColumnType.Number, "12,345,678,901", 60, metrics);

        Assert.Equal("###", decision.DisplayText);
    }

    [Fact] // ADR-0016: at least one # even when padding eats the whole width
    public void At_least_one_hash_survives_a_crushed_column()
    {
        var crushed = new CellTextMetrics(digitWidthPx: 7, cellHorizontalPaddingPx: 20);

        var decision = OverflowRules.Decide(ColumnType.Number, "1,234.56", 30, crushed);

        Assert.True(decision.IsHashed);
        Assert.Equal("#", decision.DisplayText);
    }

    [Fact] // ADR-0016: Observe takes EstimatePx output — the round trip fits, never hashes its own value
    public void An_auto_sized_column_shows_the_value_it_grew_for()
    {
        var spec = new ColumnWidthSpec(ColumnWidth.Auto, minWidthPx: 40, maxWidthPx: 400);
        const string value = "1,234,567.89";

        var required = Metrics.EstimatePx(value.Length);
        var resolved = spec.ResolveWidthPx(new AutoWidth(spec).Observe(required));
        var decision = OverflowRules.Decide(ColumnType.Number, value, resolved, Metrics);

        Assert.False(decision.IsHashed);

        // Size-to-fit takes the same unit: fitting to the widest value keeps it readable.
        var refitted = spec.SizeToFit(required);
        Assert.False(OverflowRules.Decide(ColumnType.Number, value, refitted.FixedPx, Metrics).IsHashed);
    }

    [Fact] // ADR-0016: nonsense inputs are refused by name, not decided quietly
    public void Invalid_metrics_and_arguments_throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CellTextMetrics(0, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CellTextMetrics(double.NaN, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CellTextMetrics(7, -1));
        Assert.Throws<ArgumentNullException>(() => OverflowRules.Decide(ColumnType.Number, null!, 40, Metrics));
        Assert.Throws<ArgumentOutOfRangeException>(() => OverflowRules.Decide(ColumnType.Number, "1", double.NaN, Metrics));
        // default(CellTextMetrics) sidesteps the ctor — refused by name, not a quiet fit-everything
        Assert.Throws<ArgumentOutOfRangeException>(() => OverflowRules.Decide(ColumnType.Number, "1", 40, default));
    }

    [Fact] // ADR-0016: the Number/Date-vs-Text/Boolean split has one home, and it refuses unknowns
    public void The_hashing_classification_is_total_and_refuses_undefined_types()
    {
        Assert.True(OverflowRules.HashesWhenOverflowing(ColumnType.Number));
        Assert.True(OverflowRules.HashesWhenOverflowing(ColumnType.Date));
        Assert.False(OverflowRules.HashesWhenOverflowing(ColumnType.Text));
        Assert.False(OverflowRules.HashesWhenOverflowing(ColumnType.Boolean));
        Assert.Throws<ArgumentOutOfRangeException>(() => OverflowRules.HashesWhenOverflowing((ColumnType)9));
    }
}
