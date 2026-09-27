using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Resize grips without the column menu (ADR-0050, item 12; DC-36): the headers keep their
/// grips, lose the menu button, and a double-click on an edge still sizes to fit
/// (ADR-0016). Without the declaration the header is painted exactly as before (DC-1).
/// </summary>
public class GripsWithoutMenuTests : GridTestContext
{
    private static readonly GridMetrics Metrics = GridMetrics.Resolve(GridDensity.Compact);

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        List<ColumnWidthChange> changes, bool hideColumnMenu, GridColumn<TestRow>[]? columns = null)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(50))
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, columns ?? TestRows.Wide(3))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnColumnWidthChanged, changes.Add)
            .Add(g => g.OnSortChanged, (IReadOnlyList<SortSpec> _) => { })
            .Add(g => g.HideColumnMenu, hideColumnMenu));

    [Fact] // ADR-0050 item 12 / DC-36: grips are painted and the column-menu button is not
    public void Grips_are_painted_without_the_menu_button()
    {
        var cut = RenderGrid([], hideColumnMenu: true);

        Assert.Empty(cut.FindAll(".ex-menu-button"));
        Assert.Equal(cut.FindAll(".ex-header-cell").Count, cut.FindAll(".ex-resize-grip").Count);
        Assert.NotEmpty(cut.FindAll(".ex-resize-grip"));
    }

    [Fact] // ADR-0050 item 12 / DC-36 / ADR-0016: a double-click on an edge still sizes the column to fit
    public async Task A_double_click_on_an_edge_still_sizes_to_fit()
    {
        var changes = new List<ColumnWidthChange>();
        var rows = TestRows.Many(50);
        rows[40].Book = new string('W', 30);
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, rows)
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, TestRows.Wide(3))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnColumnWidthChanged, changes.Add)
            .Add(g => g.HideColumnMenu, true));

        await cut.FindAll(".ex-resize-grip")[1].DoubleClickAsync(new MouseEventArgs { Button = 0 });

        var expected = Metrics.CellMetrics.EstimatePx(rows[40].Book + "/01");
        Assert.Equal([new ColumnWidthChange(TestRows.ColumnName(1), expected)], changes);
    }

    [Fact] // ADR-0050 item 12 / DC-36: a drag on a grip still reports the width it was dragged to
    public async Task A_drag_on_a_grip_still_resizes()
    {
        var changes = new List<ColumnWidthChange>();
        var cut = RenderGrid(changes, hideColumnMenu: true);

        await cut.FindAll(".ex-resize-grip")[0].MouseDownAsync(new MouseEventArgs { Button = 0, ClientX = 100 });
        await cut.Find(".ex-grid").MouseUpAsync(new MouseEventArgs { Button = 0, ClientX = 140 });

        Assert.Equal([new ColumnWidthChange(TestRows.ColumnName(0), 140)], changes);
    }

    [Fact] // ADR-0050 item 12 / ADR-0016: a header without the button is not charged the button's band
    public void A_header_without_the_button_is_not_charged_its_band()
    {
        GridColumn<TestRow>[] columns = [new("Amount", ColumnType.Number, r => r.Amount % 10)];
        var cut = RenderGrid([], hideColumnMenu: true, columns);

        var expected = Metrics.HeaderRequiredPx("Amount", menuButton: false, sortable: true);
        Assert.Contains(FormattableString.Invariant($"width: {expected}px"),
            cut.Find(".ex-header-cell").GetAttribute("style"));
    }

    [Fact] // ADR-0050 item 12 / ADR-0039: with no menu declared, Alt+Down opens none
    public async Task Alt_down_opens_no_menu()
    {
        var cut = RenderGrid([], hideColumnMenu: true);
        await cut.Find(".ex-viewport").MouseDownAsync(
            new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 150, OffsetY = 10 });

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
            "ArrowDown", ctrl: false, shift: false, alt: true, meta: false, metaIsPrimary: false,
            fromDescendant: false));

        Assert.Empty(cut.FindAll(".ex-popover"));
    }

    [Fact] // ADR-0050 item 12 / DC-1: without the declaration the header carries both, as before
    public void Without_the_declaration_the_menu_button_stays()
    {
        var cut = RenderGrid([], hideColumnMenu: false);

        Assert.Equal(cut.FindAll(".ex-header-cell").Count, cut.FindAll(".ex-menu-button").Count);
        Assert.Equal(cut.FindAll(".ex-header-cell").Count, cut.FindAll(".ex-resize-grip").Count);
    }
}
