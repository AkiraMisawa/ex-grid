using Bunit;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Counts renders across the row boundary — the reason this bUnit layer exists.
/// Transcribes ADR-0003: the boundary sits at the row, ShouldRender is hand-written,
/// and the change signal is a different instance, never rewritten contents.
/// </summary>
public class RowMemoisationTests : BunitContext
{
    [Fact] // ADR-0003: an unchanged row instance skips re-rendering entirely
    public void Re_pushing_the_same_window_and_columns_renders_no_row_again()
    {
        var rows = TestRows.Window();
        var columns = TestRows.Columns();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, columns));

        // This is also the net that catches a future delegate parameter passed as a
        // method group: a new delegate instance per parent render would defeat the
        // ShouldRender skip and fail this assertion (ADR-0003 "cache delegates").
        cut.Render(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, columns));

        foreach (var row in cut.FindComponents<ExGridRow<TestRow>>())
        {
            Assert.Equal(1, row.RenderCount);
        }
    }

    [Fact] // ADR-0003: the change signal is a different instance — replacing one row repaints only that row
    public void Replacing_one_row_instance_repaints_only_that_row()
    {
        var rows = TestRows.Window();
        var columns = TestRows.Columns();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, columns));

        var replaced = new TestRow { Book = "Delta", Amount = 9m, AsOf = rows[1].AsOf, Active = true };
        TestRow[] next = [rows[0], replaced, rows[2]];
        cut.Render(ps => ps
            .Add(g => g.Window, next)
            .Add(g => g.Columns, columns));

        // With @key="row" the replaced instance surfaces as a fresh row component, so
        // the contract asserted is: no row rendered twice, and the new value is on
        // screen — the untouched rows were skipped.
        foreach (var row in cut.FindComponents<ExGridRow<TestRow>>())
        {
            Assert.Equal(1, row.RenderCount);
        }
        Assert.Contains("Delta", cut.Markup);
        Assert.DoesNotContain("Beta", cut.Markup);
    }

    [Fact] // ADR-0003: an in-place rewrite does not reach the screen
    public void Rewriting_a_row_in_place_does_not_repaint_it()
    {
        var rows = TestRows.Window();
        var columns = TestRows.Columns();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, columns));

        rows[0].Book = "Rewritten";
        cut.Render(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, columns));

        Assert.Contains("Alpha", cut.Markup);
        Assert.DoesNotContain("Rewritten", cut.Markup);
    }

    [Fact] // ADR-0003: a replaced columns array repaints every row
    public void Replacing_the_columns_array_repaints_every_row()
    {
        var rows = TestRows.Window();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, TestRows.Columns()));

        // Structurally identical, but a different array instance: the rows cannot know
        // the contents match, so identity forces the repaint.
        cut.Render(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, TestRows.Columns()));

        foreach (var row in cut.FindComponents<ExGridRow<TestRow>>())
        {
            Assert.Equal(2, row.RenderCount);
        }
    }
}
