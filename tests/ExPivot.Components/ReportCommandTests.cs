using Bunit;
using ExGrid.Selection;
using ExPivot.Chrome;
using ExPivot.Components.Tests.Support;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using PivotComponent = ExPivot.Components.ExPivot;

namespace ExPivot.Components.Tests;

/// <summary>The report's Context Menu, its double click and Show Details (ADR-0059/0063), the
/// layout's binding, a substituted Chrome, and two pivots on one page.</summary>
public class ReportCommandTests : PivotTestContext
{
    private static readonly PivotLayout RegionProduct = new()
    {
        Rows = [P("Region"), P("Product")],
        Values = [Sum("Amount")],
    };

    private static IEnumerable<string> Ids(IReadOnlyList<ExGrid.Chrome.GridCommand> commands) => commands.Select(c => c.Id);

    [Fact] // ADR-0059: on a value cell — collapse its group, sort by it, Show Details, the Value Field's settings
    public void Context_commands_on_a_value_cell()
    {
        var cut = RenderPivot(RegionProduct, ps => ps.Add(p => p.OnShowDetails, (PivotDetails _) => { }));
        var valueColumn = Grid(cut).Instance.Columns[1].Name;

        var commands = ContextCommands(cut, 1, valueColumn);

        Assert.Equal(
        [
            PivotCommandIds.Expand, PivotCommandIds.Collapse, PivotCommandIds.ExpandField, PivotCommandIds.CollapseField,
            PivotCommandIds.SortSmallestToLargest, PivotCommandIds.SortLargestToSmallest, PivotCommandIds.ShowDetails,
            PivotCommandIds.ValueFieldSettings, PivotCommandIds.RemoveNamedField + ":Product", PivotCommandIds.HideFieldList,
        ], Ids(commands));
        Assert.False(commands[0].Enabled);
        Assert.True(commands[1].Enabled);
        Assert.Equal("Remove \"Product\"", Grid(cut).Instance.CommandLabel!(PivotCommandIds.RemoveNamedField + ":Product"));
        Assert.Equal("Show Details", Grid(cut).Instance.CommandLabel!(PivotCommandIds.ShowDetails));
    }

    [Fact] // ADR-0059: Collapse on an innermost row collapses the group it is in, and the Focus stays on it
    public async Task Collapse_from_the_context_menu()
    {
        var cut = RenderPivot(RegionProduct);
        var collapse = ContextCommands(cut, 1, "row-labels").Single(c => c.Id == PivotCommandIds.Collapse);

        await cut.InvokeAsync(collapse.Invoke);

        Assert.Equal("+East | 180", RowTexts(cut)[0]);
        Assert.True(cut.Instance.CurrentLayout.Rows[0].IsCollapsed(PivotItemKey.Text("East")));
    }

    [Fact] // ADR-0059: Sort Z to A on a label cell orders that field's Items
    public async Task Sort_from_a_label_cell()
    {
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] });
        var sort = ContextCommands(cut, 0, "row-labels").Single(c => c.Id == PivotCommandIds.SortDescending);

        await cut.InvokeAsync(sort.Invoke);

        Assert.Equal(["West | 90", "North | 10", "East | 180", "(blank) | 5", "Grand Total | 285"], RowTexts(cut));
    }

    [Fact] // ADR-0063/0059: a double click on a value cell hands the Consumer that listens the records behind it, paged from the source
    public async Task A_double_click_on_a_value_shows_its_details()
    {
        PivotDetails? shown = null;
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Columns = [P("Product")], Values = [Sum("Amount")] },
            ps => ps.Add(p => p.OnShowDetails, (PivotDetails details) => shown = details));

        await cut.InvokeAsync(() => Grid(cut).Instance.OnCellDoubleClick.InvokeAsync(new CellPosition(0, 1)));

        Assert.NotNull(shown);
        var page = await shown!.DetailsAsync(0, 100, Xunit.TestContext.Current.CancellationToken);
        Assert.Equal([Sales[0], Sales[2]], page.Records.Select(r => r.Record));
        Assert.Equal(2, page.Total);
        Assert.Equal([new PivotDetailItem("Region", "East")], shown.RowItems);
        Assert.Equal([new PivotDetailItem("Product", "Apples")], shown.ColumnItems);
        Assert.Equal("Sum of Amount", shown.ValueField);
        Assert.Equal("Details: East / Apples", shown.Title);
        Assert.Equal(cut.Instance.Report!.Cube.SourceVersion, shown.SourceVersion);
    }

    [Fact] // ADR-0063: an empty cell has no records to show, and a double click there shows nothing
    public async Task A_double_click_on_an_empty_cell_shows_nothing()
    {
        var shown = 0;
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Columns = [P("Product")], Values = [Sum("Amount")] },
            ps => ps.Add(p => p.OnShowDetails, (PivotDetails _) => shown++));

        await cut.InvokeAsync(() => Grid(cut).Instance.OnCellDoubleClick.InvokeAsync(new CellPosition(0, 3)));

        Assert.Equal(0, shown);
    }

    [Fact] // ADR-0063: a double click on an outer Item's label expands or collapses it
    public async Task A_double_click_on_an_outer_label_toggles_it()
    {
        var cut = RenderPivot(RegionProduct);

        await cut.InvokeAsync(() => Grid(cut).Instance.OnCellDoubleClick.InvokeAsync(new CellPosition(0, 0)));

        Assert.Equal("+East | 180", RowTexts(cut)[0]);
    }

    [Fact] // ADR-0059: a layout the Consumer hands in replaces the one on screen; one it binds back does not reset
    public async Task The_layout_binds_both_ways()
    {
        var page = RenderPage<BoundPage>();
        var cut = page.FindComponent<PivotComponent>();

        await FieldItem(cut, "Online").QuerySelector("input")!.ChangeAsync(new ChangeEventArgs { Value = true });
        Assert.Equal(["Region", "Online"], page.Instance.Layout.Rows.Select(p => p.Field));
        Assert.Equal(["Region", "Online"], AreaEntries(cut, "Rows"));

        await page.InvokeAsync(() => page.Instance.Replace(new PivotLayout { Columns = [P("Product")], Values = [Sum("Amount")] }));
        Assert.Empty(AreaEntries(cut, "Rows"));
        Assert.Equal(["Product"], AreaEntries(cut, "Columns"));
    }

    [Fact] // ADR-0061: a substituted Chrome is handed the same rules — its calls make the same layouts
    public async Task A_substituted_chrome_applies_the_same_rules()
    {
        var chrome = new RecordingChrome();
        var cut = RenderPivot(new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] }, ps => ps.Add(p => p.PivotChrome, chrome));

        Assert.Single(cut.FindAll(".stub-field-list"));
        Assert.Empty(cut.FindAll(".ex-pivot-pane"));
        var context = chrome.FieldList!;
        Assert.Equal(["Filters", "Columns", "Rows", "Values"], context.Areas.Select(a => a.Title));

        await cut.InvokeAsync(() => chrome.FieldList!.StartDrag(PivotDragSubject.FromList("Product")));
        await cut.InvokeAsync(() => chrome.FieldList!.Drop(PivotArea.Rows, 0));
        Assert.Equal(["Product", "Region"], cut.Instance.CurrentLayout.Rows.Select(p => p.Field));

        await cut.InvokeAsync(() => chrome.FieldList!.Areas[2].Entries[1].OpenMenu());
        var menu = chrome.Menu!;
        await cut.InvokeAsync(() => menu.Commands.Single(c => c.Id == PivotCommandIds.MoveToBeginning).Invoke());
        Assert.Equal(["Region", "Product"], cut.Instance.CurrentLayout.Rows.Select(p => p.Field));
    }

    [Fact] // ADR-0018/0061: two pivots on one page share nothing — a menu open in one is not open in the other
    public async Task Two_pivots_are_independent()
    {
        var page = RenderPage<TwoPivots>();
        var pivots = page.FindComponents<PivotComponent>();

        await OpenMenuAsync(pivots[0], "Rows", "Region");

        Assert.Single(pivots[0].FindAll(".ex-pivot-popup"));
        Assert.Empty(pivots[1].FindAll(".ex-pivot-popup"));
        await OpenMenuAsync(pivots[1], "Rows", "Region");
        await pivots[1].Find(".ex-pivot-search").FocusInAsync(new FocusEventArgs());
        Assert.Single(pivots[0].FindAll(".ex-pivot-popup"));
        Assert.Empty(pivots[1].FindAll(".ex-pivot-popup"));
        await RunMenuAsync(pivots[0], "Move to Column Labels");
        Assert.Equal(["Region"], AreaEntries(pivots[0], "Columns"));
        Assert.Equal(["Region"], AreaEntries(pivots[1], "Rows"));
    }

    /// <summary>A Chrome that draws a stub and keeps the contexts it was last handed.</summary>
    private sealed class RecordingChrome : IPivotChrome
    {
        public PivotFieldListContext? FieldList { get; private set; }

        public PivotMenuContext? Menu { get; private set; }

        RenderFragment? IPivotChrome.FieldList(PivotFieldListContext context)
        {
            FieldList = context;
            // A Chrome places each entry's open menu or panel under the entry (ADR-0061); the
            // stub places them after its own markup.
            return builder =>
            {
                builder.AddMarkupContent(0, "<div class='stub-field-list'></div>");
                builder.OpenRegion(1);
                foreach (var popup in context.Areas.SelectMany(a => a.Entries).Select(e => e.Popup).OfType<RenderFragment>())
                    builder.AddContent(0, popup);
                builder.CloseRegion();
            };
        }

        RenderFragment? IPivotChrome.Menu(PivotMenuContext context)
        {
            Menu = context;
            return builder => builder.AddMarkupContent(0, "<div class='stub-menu'></div>");
        }
    }

    /// <summary>A Consumer's page binding the layout both ways.</summary>
    private sealed class BoundPage : ComponentBase
    {
        // Held in a field: a new source is a refresh (ADR-0066).
        private readonly PivotSource _source = Bundled();

        public PivotLayout Layout { get; private set; } = new() { Rows = [P("Region")], Values = [Sum("Amount")] };

        public void Replace(PivotLayout layout)
        {
            Layout = layout;
            StateHasChanged();
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<PivotComponent>(0);
            builder.AddComponentParameter(1, nameof(PivotComponent.Source), _source);
            builder.AddComponentParameter(3, nameof(PivotComponent.Layout), Layout);
            builder.AddComponentParameter(4, nameof(PivotComponent.LayoutChanged),
                EventCallback.Factory.Create<PivotLayout>(this, layout => Layout = layout));
            builder.AddComponentParameter(5, nameof(PivotComponent.ViewportHeight), (ExGrid.ViewportSize)300);
            builder.AddComponentParameter(6, nameof(PivotComponent.ViewportWidth), (ExGrid.ViewportSize)600);
            builder.CloseComponent();
        }
    }

    /// <summary>Two pivots over the same records, each with a layout of its own.</summary>
    private sealed class TwoPivots : ComponentBase
    {
        // One source serves both: a source holds no state of a pivot's.
        private readonly PivotSource _source = Bundled();

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            for (var i = 0; i < 2; i++)
            {
                builder.OpenComponent<PivotComponent>(0);
                builder.AddComponentParameter(1, nameof(PivotComponent.Source), _source);
                builder.AddComponentParameter(3, nameof(PivotComponent.Layout), new PivotLayout { Rows = [P("Region")], Values = [Sum("Amount")] });
                builder.AddComponentParameter(4, nameof(PivotComponent.ViewportHeight), (ExGrid.ViewportSize)300);
                builder.AddComponentParameter(5, nameof(PivotComponent.ViewportWidth), (ExGrid.ViewportSize)600);
                builder.CloseComponent();
            }
        }
    }
}
