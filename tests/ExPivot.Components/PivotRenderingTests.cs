using Bunit;
using ExGrid.Components;
using ExGrid.Rows;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExPivot.Components.Tests;

/// <summary>The report drawn by one ExGrid, as that grid's Consumer (ADR-0059).</summary>
public class PivotRenderingTests : PivotTestContext
{
    private static readonly PivotLayout RegionProduct = new()
    {
        Rows = [P("Region"), P("Product")],
        Values = [Sum("Amount")],
    };

    [Fact] // ADR-0059: an empty layout asks for fields, and no grid is drawn
    public void An_empty_layout_asks_for_fields()
    {
        var cut = RenderPivot();

        Assert.Empty(cut.FindComponents<ExGrid.Components.ExGrid<PivotDisplayRow>>());
        Assert.Equal("To build a report, choose fields from the PivotTable Fields list.", cut.Find(".ex-pivot-empty").TextContent);
    }

    [Fact] // ADR-0059: one ExGrid, the requested Window and full extent, the label column pinned
    public void The_report_is_one_grid_with_its_labels_pinned()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] });

        var grid = Grid(cut).Instance;
        Assert.Single(cut.FindComponents<ExGrid.Components.ExGrid<PivotDisplayRow>>());
        Assert.Equal(1, grid.PinnedColumnCount);
        Assert.Equal(5, grid.TotalCount);
        Assert.Equal(["Row Labels", "Sum of Amount"], HeaderTexts(cut));
        Assert.Equal(["East | 180", "North | 10", "West | 90", "(blank) | 5", "Grand Total | 285"], RowTexts(cut));
    }

    [Fact] // ADR-0059/0024: group rows paint as Group, totals as Total, items as Detail
    public void Roles_paint_as_row_kinds()
    {
        var cut = RenderPivot(RegionProduct with { SubtotalsAtTop = false });

        var rows = cut.FindAll(".ex-viewport .ex-row");
        Assert.Contains("ex-row-group", rows[0].ClassName);
        Assert.DoesNotContain("ex-row-group", rows[1].ClassName);
        Assert.Contains("ex-row-total", rows[3].ClassName);
        var kinds = Grid(cut).Instance.RowKind!;
        Assert.Equal(RowKind.Total, kinds(Grid(cut).Instance.Window[^1]));
    }

    [Fact] // ADR-0059: the Compact form's indent is written inline, and an outer Item carries its button
    public void Labels_are_indented_and_outer_items_have_a_button()
    {
        var cut = RenderPivot(RegionProduct);

        var first = cut.FindAll(".ex-pivot-label")[0];
        var toggle = first.QuerySelector(".ex-pivot-toggle")!;
        Assert.Equal("true", toggle.GetAttribute("aria-expanded"));
        Assert.Equal("Collapse East", toggle.GetAttribute("aria-label"));
        Assert.Equal("-1", toggle.GetAttribute("tabindex"));
        Assert.Contains("ex-interactive", toggle.ClassName);
        var child = cut.FindAll(".ex-pivot-label")[1];
        Assert.Null(child.QuerySelector(".ex-pivot-toggle"));
        Assert.StartsWith("padding-left: 14px", child.GetAttribute("style"));
    }

    [Fact] // ADR-0059: the button collapses the Item, the layout says so, and the grid's rows follow
    public async Task The_button_collapses_the_item()
    {
        PivotLayout? told = null;
        var cut = RenderPivot(RegionProduct, ps => ps.Add(p => p.LayoutChanged, layout => told = layout));
        var version = Grid(cut).Instance.RowSequenceVersion;

        await cut.FindAll(".ex-pivot-toggle")[0].ClickAsync(new MouseEventArgs());

        Assert.NotNull(told);
        Assert.True(told!.Rows[0].IsCollapsed(PivotItemKey.Text("East")));
        Assert.Equal("+East | 180", RowTexts(cut)[0]);
        Assert.Equal("−North | 10", RowTexts(cut)[1]);
        Assert.NotEqual(version, Grid(cut).Instance.RowSequenceVersion);
        Assert.Equal("false", cut.FindAll(".ex-pivot-toggle")[0].GetAttribute("aria-expanded"));
    }

    [Fact] // ADR-0059/0032: the column Items are Header Groups over the value columns
    public void Column_items_are_header_groups()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Columns = [P("Online"), P("Product")], Values = [Sum("Amount")] });

        var groups = Grid(cut).Instance.HeaderGroups!;
        Assert.Equal(["FALSE", "TRUE"], groups.Select(g => g.Label));
        Assert.Equal(["FALSE", "TRUE"], cut.FindAll(".ex-header-group").Select(g => g.TextContent.Trim()));
        Assert.Contains("FALSE Total", HeaderTexts(cut));
    }

    [Fact] // ADR-0059/0060: a value paints its engine text; its raw form for a copy is the number
    public void Values_paint_their_text_and_copy_their_number()
    {
        var records = new[] { new Sale("East", "Apples", 1234.5m, 1, true) };
        var cut = RenderPivot(new PivotLayout
        {
            Rows = [P("Region")],
            Values = [Sum("Amount") with { NumberFormat = "#,##0.00" }],
        }, records: records);

        Assert.Equal("East | 1,234.50", RowTexts(cut)[0]);
        var value = (PivotDisplayValue)Grid(cut).Instance.Columns[1].Value(Grid(cut).Instance.Window[0])!;
        Assert.Equal("1234.5", value.ToString(null, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact] // ADR-0059: the report is not editable — no column declares it
    public void No_column_is_editable()
    {
        var cut = RenderPivot(RegionProduct);

        Assert.All(Grid(cut).Instance.Columns, column => Assert.False(column.Editable));
        Assert.True(Grid(cut).Instance.HeaderClickSelects);
        Assert.True(Grid(cut).Instance.HideColumnMenu);
        Assert.False(Grid(cut).Instance.OnSortChanged.HasDelegate);
        Assert.False(Grid(cut).Instance.OnFilterChanged.HasDelegate);
    }

    [Fact] // ADR-0059/0011: a refresh that changes only values keeps the row sequence, and so the Selection
    public void A_refresh_of_values_keeps_the_row_sequence()
    {
        var cut = RenderPivot(RegionProduct);
        var version = Grid(cut).Instance.RowSequenceVersion;

        cut.Render(ps => ps.Add(p => p.DataSource, Bundled(Sales.Select(s => s with { Amount = s.Amount + 1 }).ToArray())));

        Assert.Equal(version, Grid(cut).Instance.RowSequenceVersion);
        Assert.Equal("−East | 183", RowTexts(cut)[0]);
        cut.Render(ps => ps.Add(p => p.DataSource, Bundled(Sales[..6])));
        Assert.NotEqual(version, Grid(cut).Instance.RowSequenceVersion);
    }

    [Fact] // ADR-0059/0066: a new source is a refresh, and asks again; the same source handed back asks nothing
    public void A_new_source_is_a_refresh()
    {
        var first = new OnDemandSource(Bundled()) { AnswersAtOnce = true };
        var cut = RenderPivot(new PivotLayout { Values = [Sum("Amount")] }, source: first);
        Assert.Single(first.Questions);

        cut.Render(ps => ps.Add(p => p.DataSource, first));
        Assert.Single(first.Questions);
        Assert.Equal("285", RowTexts(cut)[0]);

        var second = new OnDemandSource(Bundled([.. Sales, new Sale("South", "Apples", 1000m, 1, true)])) { AnswersAtOnce = true };
        cut.Render(ps => ps.Add(p => p.DataSource, second));
        Assert.Single(second.Questions);
        Assert.Equal("1285", RowTexts(cut)[0]);
    }

    private sealed record Position(string Desk, decimal Pnl);

    // A source of another shape than the Sales: what a new file, read under another Schema, brings.
    private static PivotSource Positions() => PivotSource.From(
        new[] { new Position("Rates", 1m), new Position("Credit", 2m), new Position("Rates", 4m) },
        new PivotField<Position>[]
        {
            new("Desk", PivotFieldType.Text, p => p.Desk),
            new("Pnl", PivotFieldType.Number, p => p.Pnl),
        });

    [Fact] // ADR-0059/0066: a new source and a new layout handed in together are taken together — the source is checked against the layout it comes with
    public void A_new_source_and_a_new_layout_handed_in_together_are_taken_together()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] });

        cut.Render(ps => ps
            .Add(p => p.DataSource, Positions())
            .Add(p => p.Layout, new PivotLayout { Rows = [P("Desk")], Values = [Sum("Pnl")] }));

        Assert.Equal(["Credit | 2", "Rates | 5", "Grand Total | 7"], RowTexts(cut));
    }

    [Fact] // ADR-0059: a new source that lacks a field the layout on screen places, handed in without a layout of its own, is refused by name
    public void A_new_source_the_layout_on_screen_does_not_fit_is_refused()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] });

        var refusal = Assert.Throws<InvalidOperationException>(() => cut.Render(ps => ps.Add(p => p.DataSource, Positions())));

        Assert.Contains("'Region'", refusal.Message);
    }

    [Fact] // ADR-0059: a layout naming a field the source does not offer is refused by name
    public void A_layout_naming_an_undeclared_field_is_refused()
    {
        var refusal = Assert.Throws<InvalidOperationException>(() => RenderPivot(new PivotLayout { Rows = [P("Desk")] }));

        Assert.Contains("'Desk'", refusal.Message);
    }

    [Fact] // ADR-0059/0003: a Field List interaction does not reach the grid's rows
    public async Task Field_list_interactions_do_not_render_the_grid()
    {
        var cut = RenderPivot(RegionProduct);
        var before = cut.FindComponents<ExGridRow<PivotDisplayRow>>().Sum(r => r.RenderCount);
        var grid = Grid(cut).RenderCount;

        await cut.Find(".ex-pivot-search").InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "Reg" });
        await OpenMenuAsync(cut, "Rows", "Region");
        await cut.Find(".ex-pivot-search").FocusInAsync(new FocusEventArgs());
        Assert.Empty(cut.FindAll(".ex-pivot-popup"));

        Assert.Equal(before, cut.FindComponents<ExGridRow<PivotDisplayRow>>().Sum(r => r.RenderCount));
        Assert.Equal(grid, Grid(cut).RenderCount);
    }

    [Fact] // ADR-0059/0016: a width the user dragged is kept across a refresh
    public async Task A_dragged_width_is_kept()
    {
        var cut = RenderPivot(RegionProduct);
        var name = Grid(cut).Instance.Columns[1].Name;

        await cut.InvokeAsync(() => Grid(cut).Instance.OnColumnWidthChanged.InvokeAsync(new ExGrid.ColumnWidthChange(name, 150)));
        cut.Render(ps => ps.Add(p => p.DataSource, Bundled()));

        var column = Grid(cut).Instance.Columns[1];
        Assert.Equal(150, column.Width.Width.FixedPx);
    }

    [Fact] // ADR-0059: a label column is wide enough for its labels, indent and button
    public void The_label_column_is_sized_from_its_labels()
    {
        var cut = RenderPivot(RegionProduct);

        var width = Grid(cut).Instance.Columns[0].Width.Width.FixedPx;
        // "Grand Total" at the Compact preset's widths, and the header's need, both fit.
        Assert.InRange(width, 100, 200);
    }
}
