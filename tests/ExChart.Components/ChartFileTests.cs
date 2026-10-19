using System.Text;
using System.Xml.Linq;
using Bunit;
using ExChart.Engine;
using Xunit;
using ChartComponent = ExChart.Components.ExChart;

namespace ExChart.Components.Tests;

public class ChartFileTests : BunitContext
{
    private static string ModuleAddress => $"./{ShippedAssets.ScriptPath}?v={ShippedAssets.ScriptVersion}";
    private static readonly XNamespace SvgNs = "http://www.w3.org/2000/svg";

    private readonly BunitJSModuleInterop _module;

    public ChartFileTests()
    {
        _module = JSInterop.SetupModule(ModuleAddress);
        _module.Setup<string>("stamp").SetResult(ShippedAssets.ScriptStamp);
        var handle = _module.SetupModule("attach", _ => true);
        handle.SetupVoid("draw", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        _module.Setup<byte[]>("paint", _ => true).SetResult([0x89, 0x50, 0x4e, 0x47]);
        _module.SetupVoid("save", _ => true).SetVoidResult();
        // The screen's tokens: slot 0 as a theme set it, a value that would end the declaration, and the rest empty.
        var names = ChartFiles.TokenNames.Value;
        _module.Setup<string[]>("tokens", _ => true).SetResult(
            [.. names.Select(n => n switch { "colour-0" => "rgb(89, 74, 226)", "colour-1" => "red;}", _ => "" }), "Roboto, sans-serif"]);
    }

    private static ChartPoint P(double x, double y) => new(x, ChartValue.Of(y));

    private static ChartDefinition Exposure() => new(ChartAxis.Numbers("Years"),
    [
        ChartSeries.Of("epe", "EPE <before> & after", SeriesKind.Level, [P(0, 1), P(1, 5), P(2, 9)]) with { Panel = "EPE" },
        ChartSeries.Of("ene", "ENE", SeriesKind.Level, [P(0, -1), P(1, -3), P(2, -2)]) with { Panel = "ENE" },
    ]);

    private IRenderedComponent<ChartComponent> Render(string? title = "Exposure profile", string? fileName = null) =>
        Render<ChartComponent>(ps => ps
            .Add(c => c.Definition, Exposure())
            .Add(c => c.Title, title)
            .Add(c => c.FileName, fileName));

    private (string Name, string Type, byte[] Bytes) Saved()
    {
        var call = Assert.Single(_module.Invocations["save"]);
        return ((string)call.Arguments[0]!, (string)call.Arguments[1]!, (byte[])call.Arguments[2]!);
    }

    [Fact] // CH-12, ADR-0252: nothing is encoded during a render, and no data: URL is written
    public void Rendering_encodes_no_file()
    {
        var cut = Render();
        cut.Find(".ex-chart-filters-toggle").Click();
        cut.Find(".ex-chart-table-toggle").Click();
        Assert.DoesNotContain("data:", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(_module.Invocations["paint"]);
        Assert.Empty(_module.Invocations["save"]);
    }

    [Fact] // CH-12, ADR-0282: one click saves the CSV under the chart's file name
    public void The_csv_button_saves_the_visible_series_under_the_title()
    {
        var cut = Render();
        cut.Find(".ex-chart-download-csv").Click();
        var (name, type, bytes) = Saved();
        Assert.Equal("Exposure profile.csv", name);
        Assert.Equal("text/csv", type);
        var text = Encoding.UTF8.GetString(bytes);
        Assert.StartsWith("﻿", text, StringComparison.Ordinal);
        Assert.Contains("ENE", text, StringComparison.Ordinal);
    }

    [Fact] // ADR-0282: the SVG is the drawing on screen, standalone, with its stylesheet inside it
    public async Task The_svg_is_the_drawing_with_its_stylesheet()
    {
        var cut = Render(fileName: "epe/ene");
        Assert.True(await cut.InvokeAsync(() => cut.Instance.DownloadAsync(ChartFileFormat.Svg)));
        var (name, type, bytes) = Saved();
        Assert.Equal("epe_ene.svg", name);
        Assert.Equal("image/svg+xml", type);
        var svg = XDocument.Parse(Encoding.UTF8.GetString(bytes)).Root!;
        Assert.Equal(SvgNs + "svg", svg.Name);
        Assert.Equal("800", (string?)svg.Attribute("width"));
        Assert.Contains(".ex-chart-path", svg.Element(SvgNs + "style")!.Value, StringComparison.Ordinal);
        var classes = svg.Descendants().Select(e => (string?)e.Attribute("class") ?? "").ToList();
        Assert.Equal(cut.FindAll("g.ex-chart-series").Count, classes.Count(c => c.Split(' ').Contains("ex-chart-series")));
        Assert.Contains(classes, c => c.Contains("ex-chart-legend-entry", StringComparison.Ordinal));
        Assert.DoesNotContain(classes, c => c.Contains("ex-chart-guide", StringComparison.Ordinal) || c.Contains("ex-chart-tooltip", StringComparison.Ordinal));
        Assert.Contains(svg.Descendants(SvgNs + "text"), t => t.Value == "EPE <before> & after");
        // ADR-0282: the theme as the screen shows it is written on the file's root.
        var style = (string)svg.Attribute("style")!;
        Assert.Contains("--ex-chart-colour-0:rgb(89, 74, 226);", style, StringComparison.Ordinal);
        Assert.Contains("font-family:Roboto, sans-serif;", style, StringComparison.Ordinal);
        Assert.DoesNotContain("colour-1", style, StringComparison.Ordinal);
        Assert.Contains("colour-0", ChartFiles.TokenNames.Value);
    }

    [Fact] // ADR-0282: the PNG is that SVG painted by the browser, then saved
    public async Task The_png_is_the_svg_painted_by_the_files_entry()
    {
        var cut = Render(title: null);
        Assert.True(await cut.InvokeAsync(() => cut.Instance.DownloadAsync(ChartFileFormat.Png)));
        var paint = Assert.Single(_module.Invocations["paint"]);
        XDocument.Parse((string)paint.Arguments[0]!);
        Assert.Equal(800d, paint.Arguments[1]);
        var (name, type, bytes) = Saved();
        Assert.Equal("chart.png", name);
        Assert.Equal("image/png", type);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4e, 0x47 }, bytes);
    }

    [Fact] // ADR-0123, spine rule 1: without its script the chart cannot save, and says so by its buttons
    public async Task Without_its_script_no_file_is_saved()
    {
        _module.Setup<string>("stamp").SetResult("another version");
        var cut = Render();
        Assert.True(cut.Find(".ex-chart-download-png").HasAttribute("disabled"));
        Assert.False(await cut.InvokeAsync(() => cut.Instance.DownloadAsync(ChartFileFormat.Csv)));
        Assert.Empty(_module.Invocations["save"]);
    }
}
