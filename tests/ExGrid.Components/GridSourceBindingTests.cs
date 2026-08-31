using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The pull entry point (ADR-0001): a Source holds the Window and answers Range
/// Requests, and the grid drives the push form from it. What is pinned here is that
/// binding one changes nothing else — the same pipeline, the same memoisation, the same
/// selection rules — and that the two entry points cannot be mixed.
/// </summary>
public class GridSourceBindingTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    [Fact] // ADR-0001: a Source alone is enough — it holds the Window
    public void A_source_alone_paints_its_window()
    {
        var source = GridSource.From(TestRows.Window());

        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns()));

        Assert.Equal(3, cut.FindComponents<ExGridRow<TestRow>>().Count);
        Assert.Contains("Alpha", cut.Markup);
    }

    [Fact] // ADR-0023: the markup's column declaration is what the source queries by
    public void The_columns_reach_the_source_at_bind_time()
    {
        var source = new TestSource();

        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns()));

        Assert.NotNull(source.Columns);
        Assert.Equal(["Book", "Amount"], source.Columns!.Select(c => c.Name));
        // Re-rendering with the same column array pushes nothing again: the source's own
        // no-op guard would absorb it, but the comparison belongs on this side too.
        cut.Render(ps => ps.Add(g => g.Source, source).Add(g => g.Columns, cut.Instance.Columns));
        Assert.Equal(1, source.ColumnPushes);
    }

    [Fact] // ADR-0003 / ADR-0013: a source's change runs the whole pipeline, not just a repaint
    public void A_source_change_rebuilds_the_geometry_and_the_widths()
    {
        var source = new TestSource();
        source.Push(TestRows.Window(), totalCount: 3);
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, TestRows.Columns())   // Auto widths
            .Add(g => g.RowHeight, 20)
            .Add(g => g.ViewportHeight, 100));
        var spacerBefore = cut.Find(".ex-spacer").GetAttribute("style")!;
        var bookBefore = cut.FindAll(".ex-header-cell")[0].GetAttribute("style")!;

        // More rows than before, and a longer value in the Auto column. Both are things
        // only the pipeline sees: a bare StateHasChanged would paint the new rows through
        // the old scroll height and the old column widths.
        var wider = TestRows.Window();
        wider[0].Book = "A book with a considerably longer name";
        source.Push(wider, totalCount: 500);

        Assert.NotEqual(spacerBefore, cut.Find(".ex-spacer").GetAttribute("style"));
        Assert.NotEqual(bookBefore, cut.FindAll(".ex-header-cell")[0].GetAttribute("style"));
    }

    [Fact] // ADR-0001: the two entry points are not peers, so they cannot be mixed
    public void A_source_passed_with_a_window_is_refused_by_name()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, new TestSource())
            .Add(g => g.Window, TestRows.Window())
            .Add(g => g.Columns, Columns())));

        Assert.Contains("Source", error.Message);
        Assert.Contains("Window", error.Message);
    }

    [Fact] // ADR-0001: an empty result is written as Window="[]", never as nothing at all
    public void Neither_a_window_nor_a_source_is_refused()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => Render<ExGrid<TestRow>>(ps => ps.Add(g => g.Columns, Columns())));

        Assert.Contains("Window", error.Message);
        Assert.Contains("Source", error.Message);
    }

    [Fact] // ADR-0001: a Source built in OnInitializedAsync is null on the first render
    public void A_source_supplied_as_null_is_a_pull_grid_waiting_for_it()
    {
        // Source="@_source" with _source not assigned yet. It was supplied, so this is a
        // pull grid — not a Consumer who passed nothing — and it paints an empty result
        // until the source arrives.
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, (IGridSource<TestRow>?)null)
            .Add(g => g.Columns, Columns()));

        Assert.Empty(cut.FindComponents<ExGridRow<TestRow>>());

        var source = new TestSource();
        source.Push(TestRows.Window(), totalCount: 3);
        cut.Render(ps => ps.Add(g => g.Source, source).Add(g => g.Columns, cut.Instance.Columns));

        Assert.Equal(3, cut.FindComponents<ExGridRow<TestRow>>().Count);
    }

    [Fact] // ADR-0001: what a Source refuses must not vanish because it arrived off-thread
    public async Task A_source_pushing_an_invalid_window_surfaces_its_refusal()
    {
        var source = new TestSource();
        source.Push(TestRows.Window(), totalCount: 3);
        var cut = Render<ExGrid<TestRow>>(ps => ps.Add(g => g.Source, source).Add(g => g.Columns, Columns()));

        // The same row instance twice: Row Identity cannot tell them apart (ADR-0003),
        // and the grid refuses the Window rather than paint it.
        var duplicated = TestRows.Window();
        duplicated[1] = duplicated[0];

        // Pushed from the source and nothing else: no parameter change follows, so if the
        // refusal were discarded with the task it would leave no trace at all and the
        // grid would go on painting the previous Window.
        source.Push(duplicated, totalCount: 3);

        var raised = await Renderer.UnhandledException.WaitAsync(
            TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        Assert.IsType<InvalidOperationException>(raised);
        Assert.Contains("same row instance", raised.Message);
    }

    [Fact] // ADR-0018: a replaced Source is released — it must not go on repainting a grid
    public void Replacing_the_source_releases_the_old_one()
    {
        var first = new TestSource();
        first.Push(TestRows.Window(), totalCount: 3);
        var second = new TestSource();
        second.Push([new TestRow { Book = "Second", Amount = 1m }], totalCount: 1);
        var cut = Render<ExGrid<TestRow>>(ps => ps.Add(g => g.Source, first).Add(g => g.Columns, Columns()));

        cut.Render(ps => ps.Add(g => g.Source, second).Add(g => g.Columns, cut.Instance.Columns));
        Assert.Contains("Second", cut.Markup);

        // The old source moves and the grid does not follow it.
        first.Push([new TestRow { Book = "Ghost", Amount = 2m }], totalCount: 1);
        Assert.DoesNotContain("Ghost", cut.Markup);
        Assert.Contains("Second", cut.Markup);
    }

    [Fact] // ADR-0018: after disposal a source's change reaches nothing
    public async Task A_disposed_grid_stops_listening()
    {
        var source = new TestSource();
        source.Push(TestRows.Window(), totalCount: 3);
        Render<ExGrid<TestRow>>(ps => ps.Add(g => g.Source, source).Add(g => g.Columns, Columns()));

        await DisposeComponentsAsync();

        // Raising into a disposed grid is what a real fetch answering late does.
        source.Push([new TestRow { Book = "Late", Amount = 1m }], totalCount: 1);
    }

    [Fact] // ADR-0011: the selection is dropped on the source's own sequence version
    public void A_new_sequence_version_from_the_source_drops_the_selection()
    {
        var source = new TestSource();
        source.Push(TestRows.Window(), totalCount: 3);
        GridSelection? raised = null;
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20)
            .Add(g => g.ViewportHeight, 100)
            .Add(g => g.SelectionChanged, s => raised = s));
        cut.Find(".ex-viewport").MouseDown(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 10 });
        Assert.Equal(1, raised!.CellCount);

        source.Push(TestRows.Window(), totalCount: 3, rowSequenceVersion: 7);

        Assert.Empty(cut.FindAll(".ex-range"));
        Assert.True(raised!.IsEmpty);
    }

    [Fact] // ADR-0003: going through a Source costs a row nothing
    public void Binding_a_source_does_not_defeat_row_memoisation()
    {
        var source = new TestSource();
        source.Push(TestRows.Window(), totalCount: 3);
        var columns = Columns();
        var cut = Render<ExGrid<TestRow>>(ps => ps.Add(g => g.Source, source).Add(g => g.Columns, columns));

        // The same Window instance again — the source moved something else (a load
        // finishing, say), and no row's data changed.
        source.Raise();
        cut.Render(ps => ps.Add(g => g.Source, source).Add(g => g.Columns, columns));

        foreach (var row in cut.FindComponents<ExGridRow<TestRow>>())
        {
            Assert.Equal(1, row.RenderCount);
        }
    }

    [Fact] // ADR-0001: the Range Request goes to the Source, and only when it is uncovered
    public void An_uncovered_viewport_asks_the_source()
    {
        var source = new TestSource();
        // Five rows painted of five hundred, and the Window holds none of them.
        source.Push([], totalCount: 500);
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20)
            .Add(g => g.ViewportHeight, 100));

        var asked = Assert.Single(source.Requested);
        Assert.Equal(0, asked.Start);

        // Answered, and it does not ask again.
        source.Push(TestRows.Many(20), totalCount: 500);
        Assert.Single(source.Requested);
    }
}
