using AngleSharp.Dom;
using Bunit;
using ExGrid.Components.Tests.Support;
using ExGrid.Rows;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>Row Kind reaching the DOM without touching the row's geometry (ADR-0024).</summary>
public class RowKindTests : GridTestContext
{
    private static Func<TestRow, RowKind> KindOf(string totalBook)
        => row => row.Book == totalBook ? RowKind.Total : RowKind.Detail;

    [Fact] // ADR-0024: the role the Consumer declares is painted on that row alone
    public void A_declared_role_paints_on_that_row_only()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.RowKind, KindOf("Gamma")));

        var total = cut.FindAll(".ex-row-total");
        Assert.Single(total);
        Assert.Contains("Gamma", total[0].TextContent);
        Assert.Empty(cut.FindAll(".ex-row-group"));
    }

    [Fact] // ADR-0024: no delegate means every row is a detail row and nothing is painted for it
    public void Without_a_delegate_every_row_is_a_detail_row()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, TestRows.Columns()));

        Assert.Empty(cut.FindAll("[class*='ex-row-']"));
    }

    [Fact] // ADR-0013 / ADR-0024: a group or total row is an ordinary row of ordinary height
    public void A_role_changes_no_geometry()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.RowKind, (Func<TestRow, RowKind>)(row => row.Book == "Beta" ? RowKind.Group : RowKind.Detail)));

        // Height comes from --ex-row-height on the instance root, for every row alike:
        // no row may carry a height of its own, or the virtualisation arithmetic, the
        // selection overlay and the editor drift apart (ADR-0013).
        foreach (var row in cut.FindAll(".ex-row"))
        {
            Assert.DoesNotContain("height", row.GetAttribute("style") ?? "");
        }
        Assert.Equal(
            cut.Find(".ex-row:not(.ex-row-group)").Children.Length,
            cut.Find(".ex-row-group").Children.Length);
    }

    [Fact] // ADR-0024: rewriting what an unchanged delegate answers does NOT repaint
    public void Rewriting_the_roles_behind_an_unchanged_delegate_stays_off_the_screen()
    {
        var rows = TestRows.Window();
        var columns = TestRows.Columns();
        var totals = new HashSet<string> { "Gamma" };
        // One delegate instance for the whole test — the discipline ADR-0024 asks for is
        // to REPLACE it when the roles change, exactly as with a Cell State lookup.
        Func<TestRow, RowKind> kind = row => totals.Contains(row.Book) ? RowKind.Total : RowKind.Detail;
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows).Add(g => g.Columns, columns).Add(g => g.RowKind, kind));

        totals.Clear();
        totals.Add("Beta");
        cut.Render(ps => ps
            .Add(g => g.Window, rows).Add(g => g.Columns, columns).Add(g => g.RowKind, kind));

        // Still Gamma's. The delegate is invoked inside the row, so an unchanged
        // delegate means an unskipped row never runs it again — a role cannot arrive on
        // the next scroll, at a moment nobody chose.
        Assert.Contains("Gamma", cut.Find(".ex-row-total").TextContent);
        foreach (var row in cut.FindComponents<ExGridRow<TestRow>>())
        {
            Assert.Equal(1, row.RenderCount);
        }
    }

    [Fact] // ADR-0003: an unchanged delegate skips every row; a new one repaints
    public void The_delegates_identity_is_the_change_signal()
    {
        var rows = TestRows.Window();
        var columns = TestRows.Columns();
        var kind = KindOf("Gamma");
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows).Add(g => g.Columns, columns).Add(g => g.RowKind, kind));

        cut.Render(ps => ps
            .Add(g => g.Window, rows).Add(g => g.Columns, columns).Add(g => g.RowKind, kind));
        foreach (var row in cut.FindComponents<ExGridRow<TestRow>>())
        {
            Assert.Equal(1, row.RenderCount);
        }

        cut.Render(ps => ps
            .Add(g => g.Window, rows).Add(g => g.Columns, columns).Add(g => g.RowKind, KindOf("Beta")));
        Assert.Contains("Beta", cut.Find(".ex-row-total").TextContent);
    }

    private static string? Winning(IElement element, string property) => ShippedStylesheetTests.Winning(element, property);

    /// <summary>The declarations of the unconditional rules that style an element's ::after.</summary>
    private static List<(string Property, string Value)> AfterOf(IElement element)
        => ShippedStylesheetTests.UnconditionalRules()
            .Where(rule => rule.Selectors.Any(selector =>
                selector.EndsWith("::after", StringComparison.Ordinal) && element.Matches(selector[..^"::after".Length])))
            .SelectMany(rule => ShippedStylesheetTests.Declarations(rule.Body))
            .ToList();

    [Fact] // ADR-0024 / ADR-0038 / ticket 85: a group or total row's tint is painted once over every cell, pinned or scrollable, and on the row only where no cell is
    public async Task A_group_or_total_rows_tint_is_painted_once_over_every_cell()
    {
        // Rows by position: a group, a total, a detail row, and so on, so the first screen holds a
        // group row at a striped position (3) and an unstriped one (0). 20px rows in a 100px
        // Viewport: a move of more than five rows is a fling (ADR-0004).
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(1000))
            .Add(g => g.TotalCount, 1000)
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.RowHeight, 20)
            .Add(g => g.ViewportHeight, 100)
            .Add(g => g.PinnedColumnCount, 1)
            .Add(g => g.StripeRows, true)
            .Add(g => g.RowKind, (Func<TestRow, RowKind>)(row => ((int)row.Amount % 3) switch
            {
                0 => RowKind.Group,
                1 => RowKind.Total,
                _ => RowKind.Detail,
            })));
        var painted = cut.FindAll(".ex-viewport [role=row]");
        Assert.Contains("ex-row-stripe", painted[3].ClassList);

        foreach (var (index, token) in new[] { (0, "--ex-row-group-background"), (1, "--ex-row-total-rule-color"), (3, "--ex-row-group-background") })
        {
            var row = painted[index];
            Assert.Contains(index == 1 ? "ex-row-total" : "ex-row-group", row.ClassList);
            var tint = Winning(row, "--ex-tint");
            Assert.Contains(token, tint);
            // Nothing beneath the cells: a scrollable cell is transparent over the row, so a tint
            // there too would paint it two tints deep, and a pinned cell, over its opaque ground, one.
            Assert.Equal("none", Winning(row, "background-image"));

            var cells = row.QuerySelectorAll(".ex-cell");
            Assert.Contains(cells, cell => cell.ClassList.Contains("ex-pinned"));
            Assert.Contains(cells, cell => !cell.ClassList.Contains("ex-pinned"));
            foreach (var cell in cells)
            {
                // Each cell paints its row's tint once, over whatever ground it has: the role's
                // tint, not the stripe's (UX-16).
                Assert.Equal("var(--ex-tint)", Winning(cell, "background-image"));
                Assert.Equal(tint, Winning(cell, "--ex-tint"));
            }

            // The row's own box past the last cell — the space beyond the last column — paints it
            // once, from the one box that grows into it.
            var after = AfterOf(row);
            Assert.Contains(after, declared => declared.Property == "content");
            Assert.Contains(after, declared => declared.Property == "flex" && declared.Value.StartsWith("1 ", StringComparison.Ordinal));
            Assert.Contains(("background-image", "var(--ex-tint)"), after);
        }
        // A detail row is left as it was.
        Assert.Null(Winning(painted[2], "--ex-tint"));
        Assert.Empty(AfterOf(painted[2]));

        // A Placeholder paints no scrollable cell, only its pinned ones and its bar: the row keeps
        // the tint, beside cells that paint it over their opaque ground, and its ::after is the bar.
        await ScrollToAsync(cut.Find(".ex-scroller"), 101 * 20);
        var flung = cut.FindAll(".ex-placeholder.ex-row-group, .ex-placeholder.ex-row-total");
        Assert.True(flung.Count >= 3, $"{flung.Count} group or total Placeholders");
        foreach (var row in flung)
        {
            Assert.Equal("var(--ex-tint)", Winning(row, "background-image"));
            Assert.Empty(row.QuerySelectorAll(".ex-cell:not(.ex-pinned)"));
            Assert.All(row.QuerySelectorAll(".ex-cell"), cell => Assert.Equal("var(--ex-tint)", Winning(cell, "background-image")));
            Assert.DoesNotContain(("background-image", "var(--ex-tint)"), AfterOf(row));
        }
    }
}
