using Bunit;
using ExGrid.Cells;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Cell State reaching the DOM, and the price of the rule that gets it there:
/// the lookup's identity is the change signal (ADR-0006 / ADR-0003).
/// </summary>
public class CellStateTests : GridTestContext
{
    private static CellStateOf<TestRow> StaleWhen(Func<TestRow, bool> predicate)
        => (row, column) => column.Name == "Amount" && predicate(row) ? CellState.Stale : CellState.Normal;

    [Fact] // ADR-0006: the state the Consumer answers is painted on that cell alone
    public void An_answered_state_paints_on_that_cell_only()
    {
        var rows = TestRows.Window();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.CellState, StaleWhen(r => r.Book == "Beta")));

        var stale = cut.FindAll(".ex-state-stale");
        Assert.Single(stale);
        Assert.Equal("-7", stale[0].TextContent);
        // The presentation classes are still there: a state joins them, never replaces them.
        Assert.Contains("ex-cell-numeric", stale[0].ClassName);
    }

    [Fact] // ADR-0006: no lookup means every cell is Normal, and nothing is painted for it
    public void Without_a_lookup_no_cell_carries_a_state()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, TestRows.Columns()));

        Assert.Empty(cut.FindAll("[class*='ex-state-']"));
    }

    [Fact] // ADR-0006: a Pinned Column carries state too — it is a cell like any other
    public void A_pinned_cell_carries_its_state()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.PinnedColumnCount, 1)
            .Add(g => g.CellState, (CellStateOf<TestRow>)((row, column) =>
                column.Name == "Book" ? CellState.Error : CellState.Normal)));

        foreach (var cell in cut.FindAll(".ex-state-error"))
        {
            Assert.Contains("ex-pinned", cell.ClassName);
        }
        Assert.Equal(3, cut.FindAll(".ex-state-error").Count);
    }

    [Fact] // ADR-0006 / ADR-0003: a new lookup is the change signal, and it repaints
    public void Handing_over_a_new_lookup_repaints_the_states()
    {
        var rows = TestRows.Window();
        var columns = TestRows.Columns();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, columns)
            .Add(g => g.CellState, StaleWhen(r => r.Book == "Beta")));

        cut.Render(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, columns)
            .Add(g => g.CellState, StaleWhen(r => r.Book == "Gamma")));

        var stale = cut.FindAll(".ex-state-stale");
        Assert.Single(stale);
        Assert.Equal("0", stale[0].TextContent);
    }

    [Fact] // ADR-0006: rewriting what a lookup reads, without handing over a new one, does NOT repaint
    public void Rewriting_the_metadata_behind_an_unchanged_lookup_stays_off_the_screen()
    {
        var rows = TestRows.Window();
        var columns = TestRows.Columns();
        var stale = new HashSet<string> { "Beta" };
        // One delegate instance for the whole test — the Consumer discipline the ADR
        // asks for is to REPLACE this when the set behind it changes.
        CellStateOf<TestRow> lookup = (row, column) =>
            column.Name == "Amount" && stale.Contains(row.Book) ? CellState.Stale : CellState.Normal;
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, columns)
            .Add(g => g.CellState, lookup));

        stale.Clear();
        stale.Add("Gamma");
        cut.Render(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, columns)
            .Add(g => g.CellState, lookup));

        // Still Beta's: the rows did not change and neither did the lookup, so no row
        // re-rendered. Documented, not accidental — the alternative is re-reading every
        // visible cell on every render (ADR-0006).
        Assert.Equal("-7", cut.Find(".ex-state-stale").TextContent);
        foreach (var row in cut.FindComponents<ExGridRow<TestRow>>())
        {
            Assert.Equal(1, row.RenderCount);
        }
    }

    [Fact] // ADR-0003: an unchanged lookup must not cost a render — it is a parameter like any other
    public void An_unchanged_lookup_skips_every_row()
    {
        var rows = TestRows.Window();
        var columns = TestRows.Columns();
        CellStateOf<TestRow> lookup = (_, _) => CellState.Modified;
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, columns)
            .Add(g => g.CellState, lookup));

        cut.Render(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.Columns, columns)
            .Add(g => g.CellState, lookup));

        foreach (var row in cut.FindComponents<ExGridRow<TestRow>>())
        {
            Assert.Equal(1, row.RenderCount);
        }
    }
}
