using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// ERR-1: every refusal names which rule it is. Each rule has its own tests elsewhere (CopyRuleTests,
/// PasteRuleTests, FillKeyRuleTests, GridColumnTests, ViewportBoxTests); this one sets them side by
/// side, so a reason shared by two rules, or a rule that stops naming itself, fails by name. The
/// both-or-neither rule for Window and Source is the component's, and layer 2's
/// (<c>GridSourceBindingTests.ERR1_both_and_neither_are_refused_each_by_its_own_rule</c>).
/// </summary>
public class RefusalRuleTests
{
    private static readonly GridExtent Grid = new(1_000_000, 50);

    private static readonly Func<int, bool> Editable = _ => true;

    private static GridSelection Range(int top, int left, int bottom, int right)
        => GridSelection.Empty.Click(new(top, left), Grid).ExtendTo(new(bottom, right), Grid);

    private sealed class Row
    {
        public string Book = "";
    }

    [Fact] // ERR-1: each listed rule refuses with a distinct, inspectable reason of its own
    public void ERR1_every_rule_refuses_with_a_reason_of_its_own()
    {
        var reasons = new Dictionary<string, string>
        {
            ["copy cap"] = "copy " + ClipboardRules.PlanCopy(GridSelection.Empty.SelectAll(Grid)).Reason,
            ["misalignment"] = "copy " + ClipboardRules.PlanCopy(
                Range(0, 0, 1, 1).ToggleRange(new(5, 5), Grid).ExtendTo(new(7, 7), Grid)).Reason,
            ["paste shape"] = "paste " + ClipboardRules.PlanPaste(Range(0, 0, 4, 1), new PasteShape(3, 2), Editable).Reason,
            ["target editability"] = "paste " + ClipboardRules.PlanPaste(
                Range(0, 0, 1, 1), new PasteShape(1, 1), column => column != 1).Reason,
            ["empty selection"] = "copy " + ClipboardRules.PlanCopy(GridSelection.Empty).Reason,
            ["oversized result"] = "fill " + ClipboardRules.PlanFill(Range(0, 1, 999, 2), GridDirection.Right, Editable, cellCap: 999).Reason,
            ["contradictory width"] = Assert.Throws<ArgumentOutOfRangeException>(() => new GridColumn<Row>(
                "Tenor", ColumnType.Text, r => r.Book,
                width: new ColumnWidthSpec(ColumnWidth.Fixed(10), minWidthPx: 40))).ParamName ?? "",
            ["gutter larger than the Viewport"] = Assert.Throws<ArgumentOutOfRangeException>(
                () => new ViewportBox(12, 600, 15, 0)).ParamName ?? "",
        };

        Assert.Equal("copy TooLarge", reasons["copy cap"]);
        Assert.Equal("copy MisalignedShape", reasons["misalignment"]);
        Assert.Equal("paste ShapeMismatch", reasons["paste shape"]);
        Assert.Equal("paste TargetNotEditable", reasons["target editability"]);
        Assert.Equal("copy EmptySelection", reasons["empty selection"]);
        Assert.Equal("fill TooLarge", reasons["oversized result"]);
        Assert.Equal("gutterWidthPx", reasons["gutter larger than the Viewport"]);
        Assert.NotEqual("", reasons["contradictory width"]);
        // No two rules answer with the same reason.
        Assert.Equal(reasons.Count, reasons.Values.Distinct(StringComparer.Ordinal).Count());
        // And the width's refusal says which rule it is, in its message as in its parameter.
        var width = Assert.Throws<ArgumentOutOfRangeException>(() => new GridColumn<Row>(
            "Tenor", ColumnType.Text, r => r.Book,
            width: new ColumnWidthSpec(ColumnWidth.Fixed(10), minWidthPx: 40)));
        Assert.Contains("MinWidth (40)", width.Message);
    }
}
