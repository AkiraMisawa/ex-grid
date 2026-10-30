using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The Overflow decision (ADR-0016). Its answers are shared cases (<c>tests/cases/columns/overflow.json</c>);
/// what stays here only C# can say: the interned run, what reaching it allocates, a null text, default
/// metrics, an undefined type, and the round trip through Auto width.
/// </summary>
public class OverflowRuleTests
{
    // Digit 7px, padding 4px per side: "1,234.56" (8 chars) estimates at 8×7 + 8 = 64px.
    private static readonly CellTextMetrics Metrics = new(digitWidthPx: 7, cellHorizontalPaddingPx: 4);

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
        Assert.Throws<ArgumentNullException>(() => OverflowRules.Decide(ColumnType.Number, null!, 40, Metrics));
        // default(CellTextMetrics) sidesteps the ctor — refused by name, not a quiet fit-everything
        Assert.Throws<ArgumentOutOfRangeException>(() => OverflowRules.Decide(ColumnType.Number, "1", 40, default));
    }

    [Fact] // ADR-0016: the Number/Date-vs-Text/Boolean split refuses a type it does not know
    public void An_undefined_column_type_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OverflowRules.HashesWhenOverflowing((ColumnType)9));
    }
}
