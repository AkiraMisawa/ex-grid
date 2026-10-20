using Bunit;
using ExChart.Engine;
using Xunit;
using ChartComponent = ExChart.Components.ExChart;

namespace ExChart.Components.Tests;

public class SurfaceTests : BunitContext
{
    private static string ModuleAddress => $"./{ShippedAssets.ScriptPath}?v={ShippedAssets.ScriptVersion}";

    private readonly BunitJSModuleInterop _handle;

    public SurfaceTests()
    {
        var module = JSInterop.SetupModule(ModuleAddress);
        module.Setup<string>("stamp").SetResult(ShippedAssets.ScriptStamp);
        _handle = module.SetupModule("attachSurface", _ => true);
        _handle.SetupVoid("draw", _ => true).SetVoidResult();
        _handle.SetupVoid("dispose", _ => true).SetVoidResult();
        _handle.Setup<string?>("svgText").SetResult("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");
    }

    private static SurfaceDefinition Vol(double? hole = 9.6) => new(
        SurfaceAxis.Of("Delta", new[] { "10P", "ATM", "10C" }),
        SurfaceAxis.Of("Tenor", new[] { "1M", "1Y" }),
        [[10.5, 9.2, 9.9], [10.9, hole is { } h ? ChartValue.Of(h) : null, 10.2]],
        "Volatility",
        v => ((double)v).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "%");

    private IRenderedComponent<ExSurface> Render(SurfaceDefinition definition, Action<ComponentParameterCollectionBuilder<ExSurface>>? more = null) =>
        Render<ExSurface>(ps =>
        {
            ps.Add(c => c.Definition, definition);
            more?.Invoke(ps);
        });

    [Fact] // ADR-0255: .NET hands the 3D entry the mesh whole, with its version, and the box holds no markup of Blazor's
    public void The_mesh_is_handed_to_the_3D_entry_with_its_version()
    {
        var cut = Render(Vol());
        var draw = _handle.VerifyInvoke("draw");
        Assert.Equal(cut.Instance.Version, draw.Arguments[0]);
        var mesh = Assert.IsType<SurfaceMesh>(draw.Arguments[1]);
        Assert.Equal(["10P", "ATM", "10C"], mesh.XLines.Select(t => t.Label));
        Assert.Empty(cut.Find(".ex-chart-surface-box").Children);
        Assert.Null(cut.Find(".ex-chart-surface").GetAttribute("aria-busy"));
    }

    [Fact] // ADR-0274, ADR-0275: the box is read aloud as the surface's summary, holes included
    public void The_box_is_labelled_with_the_summary()
    {
        var cut = Render(Vol(hole: null), ps => ps.Add(c => c.Title, "EURUSD"));
        var label = cut.Find(".ex-chart-surface-box").GetAttribute("aria-label");
        Assert.StartsWith("EURUSD, a 3D surface of Volatility over Delta", label);
        Assert.EndsWith("with 1 hole where the data has no value.", label);
    }

    [Fact] // ADR-0274: the tooltip names the grid point nearest the pointer, each value through its format
    public async Task The_tooltip_names_the_nearest_grid_point()
    {
        var cut = Render(Vol());
        await cut.InvokeAsync(() => cut.Instance.OnPoint(cut.Instance.Version, 4, 100, 80));
        var values = cut.FindAll(".ex-chart-surface-tooltip .ex-chart-tooltip-value").Select(e => e.TextContent).ToArray();
        Assert.Equal(["ATM", "1Y", "9.60%"], values);

        await cut.InvokeAsync(() => cut.Instance.OnPoint(cut.Instance.Version, -1, 0, 0));
        Assert.Empty(cut.FindAll(".ex-chart-surface-tooltip"));
    }

    [Fact] // ADR-0274: a hole has no value to read; the tooltip shows nothing there, never 0
    public async Task A_hole_shows_no_tooltip()
    {
        var cut = Render(Vol(hole: null));
        await cut.InvokeAsync(() => cut.Instance.OnPoint(cut.Instance.Version, 4, 100, 80));
        Assert.Empty(cut.FindAll(".ex-chart-surface-tooltip"));
    }

    [Fact] // ADR-0272, spine rule 6: a report taken against an earlier mesh is dropped
    public async Task A_report_against_an_earlier_mesh_is_dropped()
    {
        var cut = Render(Vol());
        var stale = cut.Instance.Version;
        cut.Render(ps => ps.Add(c => c.Definition, Vol(9.7)));
        await cut.InvokeAsync(() => cut.Instance.OnPoint(stale, 4, 100, 80));
        Assert.Empty(cut.FindAll(".ex-chart-surface-tooltip"));

        SurfaceView? raised = null;
        cut.Render(ps => ps.Add(c => c.OnViewChanged, v => raised = v));
        await cut.InvokeAsync(() => cut.Instance.OnViewAsync(stale, 10, 40, 2, 0, 0));
        Assert.Null(raised);
    }

    [Fact] // ADR-0274: a click on a tenor draws that slice beside the surface as a 2D chart, raised in the data's terms
    public async Task A_click_on_a_tenor_draws_its_slice_and_raises_it()
    {
        SurfaceSlice? raised = null;
        var cut = Render(Vol(hole: null), ps => ps.Add(c => c.OnSliceChanged, s => raised = s));
        await cut.InvokeAsync(() => cut.Instance.OnSliceAsync(cut.Instance.Version, "y", 3));
        Assert.Equal(SurfaceSlice.AtY("1Y"), raised);
        var chart = cut.FindComponent<ChartComponent>();
        Assert.Equal("Volatility at 1Y", chart.Instance.Definition.Series[0].Name);
        Assert.Null(chart.Instance.Definition.Series[0].Points[1].Y);
        var draw = _handle.Invocations["draw"][^1];
        // The slice's line on the surface, along the 1Y row and broken at its hole.
        var path = Assert.IsAssignableFrom<IReadOnlyList<IReadOnlyList<SurfaceMeshPoint>>>(draw.Arguments[4]);
        Assert.Equal(2, path.Count);
        Assert.All(path.SelectMany(r => r), p => Assert.Equal(1, p.Y));
    }

    [Fact] // ADR-0272: a slice the new data does not hold is said on the surface, not dropped quietly
    public void A_slice_the_data_does_not_hold_is_said()
    {
        var cut = Render(Vol(), ps => ps.Add(c => c.Slice, SurfaceSlice.AtY("5Y")));
        Assert.Contains("No slice at Tenor 5Y", cut.Find(".ex-chart-notes").TextContent);
        Assert.Empty(cut.FindComponents<ChartComponent>());
    }

    [Fact] // ADR-0272: the view is raised when a gesture ends, and taken back as a parameter
    public async Task The_view_is_raised_and_taken_back()
    {
        SurfaceView? raised = null;
        var cut = Render(Vol(), ps => ps.Add(c => c.OnViewChanged, v => raised = v));
        await cut.InvokeAsync(() => cut.Instance.OnViewAsync(cut.Instance.Version, 12.3456, 40, 2, 0.1, -0.05));
        Assert.Equal(new SurfaceView(12.35, 40, 2, 0.1, -0.05), raised);

        // The view handed back is an echo of the report: the revision holds, so the 3D entry keeps
        // the view it has, which may already be newer (spine rule 6).
        var revision = (int)_handle.Invocations["draw"][^1].Arguments[3]!;
        cut.Render(ps => ps.Add(c => c.View, raised));
        Assert.Equal(revision, _handle.Invocations["draw"][^1].Arguments[3]);

        var before = _handle.Invocations["draw"].Count;
        cut.Render(ps => ps.Add(c => c.View, new SurfaceView(90, 10)));
        var draw = _handle.Invocations["draw"][^1];
        Assert.Equal(before + 1, _handle.Invocations["draw"].Count);
        Assert.Equal(new SurfaceView(90, 10), draw.Arguments[2]);
        Assert.Equal(revision + 1, draw.Arguments[3]);
    }

    [Fact] // ADR-0282: the drawing as it stands is handed over as SVG text, for a file
    public async Task The_drawing_is_handed_over_as_svg_text()
    {
        var cut = Render(Vol());
        Assert.StartsWith("<svg", await cut.Instance.SvgTextAsync());
    }

    [Fact] // ADR-0275: "View as table" holds the grid, a hole as an empty cell
    public void The_table_holds_the_grid_with_a_hole_empty()
    {
        var cut = Render(Vol(hole: null));
        cut.Find(".ex-chart-table-toggle").Click();
        var cells = cut.FindAll(".ex-chart-table tbody tr:nth-child(2) td").Select(c => c.TextContent).ToArray();
        Assert.Equal(["10.90%", "", "10.20%"], cells);
    }

    [Fact] // ADR-0257: a surface the Consumer interpolated says so in its Notes, and the tooltip says which values are quoted
    public async Task An_interpolated_surface_says_its_method()
    {
        IReadOnlyList<IReadOnlyList<bool>> marks = [[false, true, false], [false, true, false]];
        var d = new SurfaceDefinition(SurfaceAxis.Of("Delta", new[] { "10P", "ATM", "10C" }), SurfaceAxis.Of("Tenor", new[] { "1M", "1Y" }),
            [[10.5, 9.2, 9.9], [10.9, 9.6, 10.2]], "Volatility", method: "bilinear", interpolated: marks);
        var cut = Render(d);
        Assert.Contains("Interpolated by the Consumer: bilinear.", cut.Find(".ex-chart-notes").TextContent);
        await cut.InvokeAsync(() => cut.Instance.OnPoint(cut.Instance.Version, 1, 10, 10));
        Assert.Equal("interpolated, bilinear", cut.Find(".ex-chart-surface-tooltip .ex-chart-tooltip-note").TextContent);
        await cut.InvokeAsync(() => cut.Instance.OnPoint(cut.Instance.Version, 0, 10, 10));
        Assert.Equal("quoted", cut.Find(".ex-chart-surface-tooltip .ex-chart-tooltip-note").TextContent);
    }
    private static SurfaceDefinition Plate() => SurfaceDefinition.FromTriangles(
        [new(0, 0, 0.0), new(1, 0, 1.0), new(0, 1, 1.0), new(1, 1, 2.0)], [new(0, 1, 3), new(0, 3, 2)], "x", "y", "u",
        xFormat: v => v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m");

    [Fact] // ADR-0311: on a mesh the tooltip names the nearest node, a click on it slices through its position, and the table lists the nodes
    public async Task A_mesh_reads_its_nodes_slices_through_them_and_lists_them()
    {
        SurfaceSlice? raised = null;
        var cut = Render(Plate(), ps => ps.Add(c => c.OnSliceChanged, s => raised = s));
        await cut.InvokeAsync(() => cut.Instance.OnPoint(cut.Instance.Version, 1, 10, 10));
        Assert.Equal(["1.0 m", "0", "1"], cut.FindAll(".ex-chart-surface-tooltip .ex-chart-tooltip-value").Select(e => e.TextContent));

        await cut.InvokeAsync(() => cut.Instance.OnSliceAsync(cut.Instance.Version, "x", 1));
        Assert.Equal(SurfaceSlice.AtX(1.0), raised);
        Assert.Single(cut.FindAll(".ex-chart-surface-slice-chart .ex-chart"));
        Assert.Contains("linear on the Consumer's triangles (P1)", cut.Find(".ex-chart-surface-slice-chart").TextContent);

        cut.Find(".ex-chart-table-toggle").Click();
        Assert.Equal(4, cut.FindAll(".ex-chart-table tbody tr").Count);
        Assert.Equal(["1.0 m", "1", "2"], cut.FindAll(".ex-chart-table tbody tr:nth-child(4) td").Select(c => c.TextContent));
    }

    [Fact] // ADR-0311: a slice the mesh has no triangle at is said, not drawn
    public void A_slice_off_the_mesh_is_said()
    {
        var cut = Render(Plate(), ps => ps.Add(c => c.Slice, SurfaceSlice.AtX(2.0)));
        Assert.Contains("no triangle of the mesh crosses there.", cut.Find(".ex-chart-notes").TextContent);
    }
}
