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
}
