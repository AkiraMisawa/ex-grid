using System.Text.Json;
using Bunit;
using ExChart.Chrome;
using ExGrid.Chrome;
using ExGrid.Data;
using ExPivot.Chrome;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ScriptBoard = ExBoard.Script.ExBoard;

namespace ExBoard.Tests;

/// <summary>
/// ADR-0410: Blazor's ExBoard over the Script Core dresses its views with the Chromes it is handed as the Razor component
/// does. The grid Chrome draws the grids and the pivots' report grids, the pivot Chrome the pivots, and its grid Chrome the
/// report grids where no grid Chrome is given. The slots each Chrome fills cross as the Board attaches, and a slot the
/// script mounts is drawn beside the root by the Chrome of its kind.
/// </summary>
public sealed class ScriptBoardChromeTests : BunitContext
{
    private static readonly BoardReading Board = BoardSpec.Read("""
        {"version": 1, "inputs": {"trades": {"columns": [{"name": "desk", "type": "text"}]}},
         "views": [{"id": "blotter", "grid": {"data": "trades", "columns": [{"column": "desk"}]}}],
         "layout": [{"views": ["blotter"]}]}
        """);

    private readonly BunitJSModuleInterop _module;
    private readonly BunitJSModuleInterop _view;

    public ScriptBoardChromeTests()
    {
        _module = JSInterop.SetupModule(invocation => invocation.Identifier == "import");
        _module.Setup<string?>("stamp").SetResult(global::ExBoard.Script.ScriptCoreAsset.Stamp);
        _view = _module.SetupModule("attach", _ => true);
        _view.SetupVoid("update", _ => true).SetVoidResult();
        _view.SetupVoid("slotRendered", _ => true).SetVoidResult();
    }

    /// <summary>A grid Chrome of the Consumer's own, filling the slots it is made with, each saying whose it is.</summary>
    private sealed class GridChrome(string whose, ScriptSlots slots) : IGridChrome
    {
        public ScriptSlots ScriptSlots => slots;

        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? CellEditor(CellEditorContext context) => null;

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;

        public RenderFragment? ContextMenu(GridMenuView view) => builder => builder.AddContent(0, $"{whose}'s menu: {view.Items[0].Label}");

        public RenderFragment? LoadingIndicator(GridLoadingView view) => builder => builder.AddContent(0, $"{whose}'s loading bar");
    }

    /// <summary>A pivot Chrome filling the pivot's menu, and its report grid's slots through the grid Chrome it answers.</summary>
    private sealed class PivotChrome(IGridChrome? grid) : IPivotChrome
    {
        public PivotScriptSlots ScriptSlots => PivotScriptSlots.Menu;

        /// <summary>The words the Board handed for ExPivot's commands, as the grid Chrome asks them.</summary>
        public Func<string, string?>? CommandLabel { get; private set; }

        public IGridChrome? GridChrome(Func<string, string?> commandLabel)
        {
            CommandLabel = commandLabel;
            return grid;
        }

        public RenderFragment? Menu(PivotMenuView view) => builder => builder.AddContent(0, $"pivot's {view.Name} menu");
    }

    /// <summary>A chart Chrome filling the chart's context menu.</summary>
    private sealed class ChartChrome : IChartChrome
    {
        public ChartScriptSlots ScriptSlots => ChartScriptSlots.Menu;

        public RenderFragment? Menu(ChartMenuView view) => builder => builder.AddContent(0, $"chart's {view.Name} menu");
    }

    private IRenderedComponent<ScriptBoard> Draw(IGridChrome? chrome, IPivotChrome? pivotChrome, IChartChrome? chartChrome = null) => Render<ScriptBoard>(ps => ps
        .Add(p => p.Board, Board)
        .Add(p => p.Data, new Dictionary<string, Snapshot> { ["trades"] = Board.Input("trades")!.FromJson("""[{"desk": "Rates"}]""") })
        .Add(p => p.Chrome, chrome)
        .Add(p => p.PivotChrome, pivotChrome)
        .Add(p => p.ChartChrome, chartChrome));

    private string SlotsSent()
    {
        using var document = JsonDocument.Parse(Assert.IsType<string>(Assert.Single(_module.Invocations["attach"]).Arguments[2]));
        return document.RootElement.GetProperty("razorSlots").GetRawText();
    }

    private global::ExBoard.Script.ScriptBridge Bridge()
        => Assert.IsType<DotNetObjectReference<global::ExBoard.Script.ScriptBridge>>(Assert.Single(_module.Invocations["attach"]).Arguments[1]).Value;

    [Fact] // ADR-0410: the slots each Chrome fills cross as the Board attaches, the grid Chrome's for the report grids where one is given, as the Razor ExBoard has it
    public void ADR0410_the_slots_the_Chromes_fill_cross_as_the_Board_attaches()
    {
        Draw(new GridChrome("grid", ScriptSlots.LoadingIndicator), new PivotChrome(new GridChrome("report", ScriptSlots.ContextMenu)));
        Assert.Equal("""{"grid":["loadingIndicator"],"pivot":["menu"],"pivotGrid":["loadingIndicator"],"chart":[]}""", SlotsSent());
    }

    [Fact] // ADR-0410: with no grid Chrome, the grids are the Script Core's own and the report grids the pivot Chrome's grid Chrome's, asked with ExPivot's words as the Razor ExBoard asks it
    public void ADR0410_with_no_grid_Chrome_the_report_grids_are_the_pivot_Chromes()
    {
        var pivotChrome = new PivotChrome(new GridChrome("report", ScriptSlots.ContextMenu));
        Draw(null, pivotChrome);
        Assert.Equal("""{"grid":[],"pivot":["menu"],"pivotGrid":["contextMenu"],"chart":[]}""", SlotsSent());
        var label = Assert.IsType<Func<string, string?>>(pivotChrome.CommandLabel);
        Assert.Equal("Move Up", label(global::ExPivot.PivotCommandIds.MoveUp));
        Assert.Equal("Remove \"Desk\"", label(global::ExPivot.PivotCommandIds.RemoveNamedField + ":Desk"));
        Assert.Null(label("copy"));
    }

    [Fact] // ADR-0410: Chromes that fill no slot leave every part the Script Core's own
    public void ADR0410_Chromes_that_fill_no_slot_send_none()
    {
        Draw(new GridChrome("grid", ScriptSlots.None), null);
        Assert.Equal("null", SlotsSent());
    }

    [Fact] // ADR-0410 sections 1 and 3: a mounted slot is drawn beside the root by the Chrome of its kind, handed to the script, and goes when unmounted
    public async Task ADR0410_a_mounted_slot_is_drawn_by_the_Chrome_of_its_kind()
    {
        var cut = Draw(null, new PivotChrome(new GridChrome("report", ScriptSlots.ContextMenu)));
        await cut.InvokeAsync(() => Bridge().MountSlot(1, 1, "contextMenu",
            """{"column":"desk","columnType":"text","commands":[{"id":"copy","label":"Copy","enabled":true}]}"""));
        await cut.InvokeAsync(() => Bridge().MountSlot(2, 1, "menu",
            """{"name":"Layout","commands":[{"id":"refresh","label":"Refresh","enabled":true,"checked":null,"heading":null}]}"""));

        Assert.Equal(["report's menu: Copy", "pivot's Layout menu"], cut.FindAll("div[hidden] > .ex-razor-slot").Select(slot => slot.TextContent));
        // Beside the root, never inside it.
        Assert.Equal(3, cut.Nodes.OfType<AngleSharp.Dom.IElement>().Count());
        Assert.Equal([[1L, 1], [2L, 1]], _view.Invocations["slotRendered"].Select(handed => handed.Arguments.Take(2).ToArray()));

        await cut.InvokeAsync(() => Bridge().UnmountSlot(1));
        Assert.Equal("pivot's Layout menu", Assert.Single(cut.FindAll(".ex-razor-slot")).TextContent);
    }

    [Fact] // ADR-0410: a chart's slots cross apart from the pivot's of the same name, and are drawn by the chart Chrome
    public async Task ADR0410_a_charts_slot_crosses_apart_and_is_drawn_by_the_chart_Chrome()
    {
        var cut = Draw(null, new PivotChrome(null), new ChartChrome());
        Assert.Equal("""{"grid":[],"pivot":["menu"],"pivotGrid":[],"chart":["menu"]}""", SlotsSent());

        await cut.InvokeAsync(() => Bridge().MountSlot(1, 1, "chart.menu",
            """{"name":"P&L","commands":[{"id":"download-csv","label":"CSV","enabled":true,"expanded":null,"title":null}]}"""));
        await cut.InvokeAsync(() => Bridge().MountSlot(2, 1, "menu",
            """{"name":"Layout","commands":[{"id":"refresh","label":"Refresh","enabled":true,"checked":null,"heading":null}]}"""));
        Assert.Equal(["chart's P&L menu", "pivot's Layout menu"], cut.FindAll(".ex-razor-slot").Select(slot => slot.TextContent));
    }

    [Fact] // ADR-0410: the slots are read as the Board attaches, so Chromes naming other slots are refused by name, never drawn half
    public void ADR0410_Chromes_naming_other_slots_after_the_Board_attached_are_refused()
    {
        var cut = Draw(new GridChrome("grid", ScriptSlots.LoadingIndicator), null);
        // Another Chrome naming the same slots draws them standing.
        cut.Render(ps => ps.Add(p => p.Chrome, new GridChrome("another", ScriptSlots.LoadingIndicator)));
        var refused = Assert.Throws<NotSupportedException>(() => cut.Render(ps => ps.Add(p => p.PivotChrome, new PivotChrome(null))));
        Assert.Contains("Chromes naming other slots need a new ExBoard", refused.Message);
    }
}
