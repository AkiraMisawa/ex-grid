using Bunit;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// FN-22 (ADR-0003, its note of 2026-10-13): a Columns, HeaderGroups or Sorts list handed again
/// as the same instance whose elements changed is taken exactly as a new list, and the column
/// state the grid holds is carried across by column Name. Each Consumer here keeps its lists in
/// fields, changes them in place, and renders.
/// </summary>
public class ListsChangedInPlaceTests : GridTestContext
{
    /// <summary>Records the column whose menu the grid last asked for.</summary>
    private sealed class MenuChrome : IGridChrome
    {
        public string? MenuColumn { get; private set; }

        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context)
        {
            MenuColumn = context.Column;
            return b => b.AddMarkupContent(0, "<div class='stub-menu'></div>");
        }

        public RenderFragment? CellEditor(CellEditorContext context) => null;

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;
    }

    /// <summary>A Consumer holding its lists in fields and changing them in place.</summary>
    private sealed class InPlaceConsumer : ComponentBase
    {
        public TestRow[] Window { get; } = WithOneWideBook(TestRows.Many(60));

        // Row 50's Book is far wider than the rest: its column grows once that row is painted.
        private static TestRow[] WithOneWideBook(TestRow[] rows)
        {
            rows[50] = new TestRow { Book = "A Book label far wider than any other in the window", Amount = 50 };
            return rows;
        }

        public List<GridColumn<TestRow>> Columns { get; } = [.. TestRows.Columns()];

        public List<HeaderGroup> Groups { get; } = [];

        public List<SortSpec> Sorts { get; } = [];

        public MenuChrome Chrome { get; } = new();

        [Parameter] public int Tick { get; set; }

        private void HeardSort(IReadOnlyList<SortSpec> sorts)
        {
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "data-tick", Tick);
            builder.OpenComponent<ExGrid<TestRow>>(2);
            builder.AddComponentParameter(3, nameof(ExGrid<TestRow>.Window), Window);
            builder.AddComponentParameter(4, nameof(ExGrid<TestRow>.TotalCount), (int?)Window.Length);
            builder.AddComponentParameter(5, nameof(ExGrid<TestRow>.Columns), (IReadOnlyList<GridColumn<TestRow>>)Columns);
            builder.AddComponentParameter(6, nameof(ExGrid<TestRow>.HeaderGroups), (IReadOnlyList<HeaderGroup>)Groups);
            builder.AddComponentParameter(7, nameof(ExGrid<TestRow>.Sorts), (IReadOnlyList<SortSpec>)Sorts);
            builder.AddComponentParameter(8, nameof(ExGrid<TestRow>.RowHeight), 20d);
            builder.AddComponentParameter(9, nameof(ExGrid<TestRow>.ViewportHeight), (ViewportSize)200);
            builder.AddComponentParameter(10, nameof(ExGrid<TestRow>.ViewportWidth), (ViewportSize)900);
            builder.AddComponentParameter(11, nameof(ExGrid<TestRow>.Chrome), (IGridChrome)Chrome);
            // A sort listener, so the headers carry their menu buttons.
            builder.AddComponentParameter(12, nameof(ExGrid<TestRow>.OnSortChanged),
                EventCallback.Factory.Create<IReadOnlyList<SortSpec>>(this, HeardSort));
            builder.CloseComponent();
            builder.CloseElement();
        }
    }

    private IRenderedComponent<InPlaceConsumer> RenderConsumer() => Render<InPlaceConsumer>(ps => ps.Add(c => c.Tick, 0));

    /// <summary>The Consumer renders again, handing every list as the same instance.</summary>
    private static void Rerender(IRenderedComponent<InPlaceConsumer> host)
        => host.Render(ps => ps.Add(c => c.Tick, host.Instance.Tick + 1));

    private static string[] Headers(IRenderedComponent<InPlaceConsumer> host)
        => [.. host.FindAll(".ex-header-cell").Select(h => h.TextContent.Trim())];

    private static string[] FirstRowCells(IRenderedComponent<InPlaceConsumer> host)
        => [.. host.FindAll(".ex-row")[0].QuerySelectorAll(".ex-cell").Select(c => c.TextContent.Trim())];

    [Fact] // FN-22, ADR-0003 (2026-10-13): a column added to the same Columns list is taken as a new list
    public void FN22_a_column_added_in_place_repaints()
    {
        var host = RenderConsumer();

        host.Instance.Columns.Add(new GridColumn<TestRow>("Extra", ColumnType.Text, r => "x" + r.Book));
        Rerender(host);

        Assert.Equal("Extra", Headers(host)[^1]);
        Assert.Equal("xRow 000000", FirstRowCells(host)[^1]);
    }

    [Fact] // FN-22, ADR-0003 (2026-10-13): a column replaced in the same list repaints its cells, not only its header
    public void FN22_a_column_replaced_in_place_repaints_its_cells()
    {
        var host = RenderConsumer();

        host.Instance.Columns[1] = new GridColumn<TestRow>("Replaced", ColumnType.Text, r => "y" + r.Book);
        Rerender(host);

        Assert.Equal("Replaced", Headers(host)[1]);
        Assert.Equal("yRow 000000", FirstRowCells(host)[1]);
    }

    [Fact] // FN-22, ADR-0003 (2026-10-13): a column removed from the same list is gone from header and cells
    public void FN22_a_column_removed_in_place_repaints()
    {
        var host = RenderConsumer();
        var before = Headers(host).Length;

        host.Instance.Columns.RemoveAt(1);
        Rerender(host);

        Assert.Equal(before - 1, Headers(host).Length);
        Assert.DoesNotContain("Amount", Headers(host));
        Assert.Equal(before - 1, FirstRowCells(host).Length);
    }

    [Fact] // FN-22, ADR-0003 (2026-10-13) / ADR-0032: Header Groups edited in place are resolved again
    public void FN22_header_groups_edited_in_place_repaint()
    {
        var host = RenderConsumer();
        Assert.Empty(host.FindAll(".ex-header-group"));

        host.Instance.Groups.Add(new HeaderGroup("Pair", ["Book", "Amount"]));
        Rerender(host);

        Assert.Equal("Pair", Assert.Single(host.FindAll(".ex-header-group")).TextContent);
    }

    [Fact] // FN-22, ADR-0003 (2026-10-13) / ADR-0012: Sorts edited in place reach the headers
    public void FN22_sorts_edited_in_place_repaint()
    {
        var host = RenderConsumer();
        Assert.NotEqual("ascending", host.FindAll(".ex-header-cell")[0].GetAttribute("aria-sort"));

        host.Instance.Sorts.Add(new SortSpec("Book", SortDirection.Ascending));
        Rerender(host);

        Assert.Equal("ascending", host.FindAll(".ex-header-cell")[0].GetAttribute("aria-sort"));
    }

    [Fact] // FN-22, ADR-0003 (2026-10-13): column state the grid holds follows the column by Name — an open column menu stays on its column when the list is reordered in place
    public async Task FN22_column_state_the_grid_holds_is_carried_by_name()
    {
        var host = RenderConsumer();
        await host.FindAll(".ex-menu-button")[1].ClickAsync(new MouseEventArgs()); // Amount's menu
        Assert.Equal("Amount", host.Instance.Chrome.MenuColumn);

        // The Consumer moves Amount to the front of the same list.
        var amount = host.Instance.Columns[1];
        host.Instance.Columns.RemoveAt(1);
        host.Instance.Columns.Insert(0, amount);
        Rerender(host);
        // And the grid renders again for something else, an empty push.
        var grid = host.FindComponent<ExGrid<TestRow>>();
        grid.Render();

        Assert.Equal("Amount", Headers(host)[0]);
        Assert.Equal("Amount", host.Instance.Chrome.MenuColumn);
        Assert.Single(host.FindAll(".stub-menu"));
    }

    [Fact] // FN-22 / UX-5: lists handed again unchanged still render nothing
    public void FN22_lists_handed_again_unchanged_render_nothing()
    {
        var host = RenderConsumer();
        var grid = host.FindComponent<ExGrid<TestRow>>();
        var renders = grid.RenderCount;

        Rerender(host);

        Assert.Equal(renders, grid.RenderCount);
    }

    private static double BookWidthPx(IRenderedComponent<InPlaceConsumer> host)
    {
        var header = host.FindAll(".ex-header-cell").First(h => h.TextContent.Trim() == "Book");
        var match = System.Text.RegularExpressions.Regex.Match(header.GetAttribute("style")!, @"(?:^|;)\s*width: ([0-9.]+)px");
        return double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Paints row 50, whose wide Book grows the Auto column, and comes back to the top.</summary>
    private async Task<double> WidenBookAsync(IRenderedComponent<InPlaceConsumer> host)
    {
        var narrow = BookWidthPx(host);
        await ScrollToAsync(host.Find(".ex-scroller"), 50 * 20);
        await host.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));
        await ScrollToAsync(host.Find(".ex-scroller"), 0);
        await host.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));
        var wide = BookWidthPx(host);
        Assert.True(wide > narrow, $"Book grew from {narrow} to {wide}");
        return wide;
    }

    [Fact] // FN-22, ADR-0003 (2026-10-13) / ADR-0016: the same column instance moved in place keeps the Auto width it measured
    public async Task FN22_a_column_moved_in_place_keeps_its_measured_auto_width()
    {
        var host = RenderConsumer();
        var wide = await WidenBookAsync(host);

        var amount = host.Instance.Columns[1];
        host.Instance.Columns.RemoveAt(1);
        host.Instance.Columns.Insert(0, amount);
        Rerender(host);

        Assert.Equal("Amount", Headers(host)[0]);
        Assert.Equal(wide, BookWidthPx(host));
    }

    [Fact] // FN-22, ADR-0003 (2026-10-11 and 2026-10-13): a new instance under the same Name measures afresh — what is derived from a definition is not carried
    public async Task FN22_a_new_column_instance_under_the_same_name_measures_afresh()
    {
        var host = RenderConsumer();
        var wide = await WidenBookAsync(host);

        host.Instance.Columns[0] = new GridColumn<TestRow>("Book", ColumnType.Text, r => r.Book);
        Rerender(host);

        Assert.True(BookWidthPx(host) < wide, $"Book measured afresh at {BookWidthPx(host)}, under {wide}");
    }

    [Fact] // FN-22, ADR-0003 (2026-10-13): the open column menu goes with its column when the column is removed in place
    public async Task FN22_the_open_menu_closes_when_its_column_is_removed_in_place()
    {
        var host = RenderConsumer();
        await host.FindAll(".ex-menu-button")[1].ClickAsync(new MouseEventArgs()); // Amount's menu
        Assert.Single(host.FindAll(".stub-menu"));

        host.Instance.Columns.RemoveAt(1);
        Rerender(host);

        Assert.Empty(host.FindAll(".stub-menu"));
    }

    [Fact] // FN-22, ADR-0003 (2026-10-13): the grid draws from the copy it took when handed the list — a change in place shows when the list is handed again, never through a render of the grid's own in between
    public async Task FN22_a_change_in_place_waits_for_the_list_to_be_handed_again()
    {
        var host = RenderConsumer();
        var grid = host.FindComponent<ExGrid<TestRow>>();

        host.Instance.Columns.Add(new GridColumn<TestRow>("Extra", ColumnType.Text, r => "x" + r.Book));
        host.Instance.Columns[1] = new GridColumn<TestRow>("Replaced", ColumnType.Text, r => "y" + r.Book);
        var renders = grid.RenderCount;
        // The grid renders for its own reasons before the Consumer renders again: a scroll.
        await ScrollToAsync(host.Find(".ex-scroller"), 3 * 20);
        await host.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));

        Assert.True(grid.RenderCount > renders, "the scroll rendered nothing, so this proves nothing");
        Assert.Equal(["Book", "Amount", "AsOf"], Headers(host).Take(3));
        Assert.DoesNotContain("Extra", Headers(host));
        Assert.Equal("Row 000003", FirstRowCells(host)[0]);
        Assert.DoesNotContain(FirstRowCells(host), cell => cell.StartsWith('x') || cell.StartsWith('y'));

        // Handed again, the list is taken anew and the change shows.
        Rerender(host);

        Assert.Equal("Replaced", Headers(host)[1]);
        Assert.Equal("Extra", Headers(host)[^1]);
        Assert.Equal("yRow 000003", FirstRowCells(host)[1]);
        Assert.Equal("xRow 000003", FirstRowCells(host)[^1]);
    }
}
