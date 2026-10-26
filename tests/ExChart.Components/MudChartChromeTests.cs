using System.Text.RegularExpressions;
using Bunit;
using ExChart.Chrome;
using ExChart.Engine;
using ExChart.MudBlazor;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MudBlazor.Services;
using Xunit;
using ChartComponent = ExChart.Components.ExChart;

namespace ExChart.Components.Tests;

/// <summary>
/// The chart's toolbar and context menu under either Chrome (ADR-0281): the same commands, in the
/// same order, doing the same things; the Wrapper's parts skip the renders a pointer move makes; and
/// its stylesheet gives the theme's leading colours to the first three slots and never a semantic one.
/// </summary>
public class MudChartChromeTests : BunitContext
{
    private static string ModuleAddress => $"./{ShippedAssets.ScriptPath}?v={ShippedAssets.ScriptVersion}";

    public MudChartChromeTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule(ModuleAddress);
        module.Setup<string>("stamp").SetResult(ShippedAssets.ScriptStamp);
        var handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("draw", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        module.SetupVoid("save", _ => true).SetVoidResult();
        Render<MudPopoverProvider>();
    }

    private static ChartPoint P(double x, double y) => new(x, ChartValue.Of(y));

    private static ChartDefinition Curve() => new(ChartAxis.Numbers("Years"),
    [
        ChartSeries.Of("a", "A", SeriesKind.Level, [P(0, 1), P(1, 5), P(2, 9), P(3, 4)]),
        ChartSeries.Of("b", "B", SeriesKind.Level, [P(0, 2), P(1, 4), P(2, 8), P(3, 3)]),
    ]);

    private IRenderedComponent<ChartComponent> Render(IChartChrome? chrome, List<ChartRange?>? ranges = null) =>
        Render<ChartComponent>(ps => ps
            .Add(c => c.Definition, Curve())
            .Add(c => c.Chrome, chrome)
            .Add(c => c.OnRangeChanged, (ChartRange? r) => ranges?.Add(r)));

    private static string[] MenuCommands(IRenderedComponent<ChartComponent> cut) =>
        cut.FindAll(".ex-chart-menu [role=menuitem]").Select(e => e.GetAttribute("data-ex-command")!).ToArray();

    public static TheoryData<bool> BothChromes => new() { false, true };

    [Theory] // ADR-0281: the context menu holds ExChart's commands, in its order, under either Chrome
    [MemberData(nameof(BothChromes))]
    public void The_context_menu_holds_the_same_commands(bool mud)
    {
        var cut = Render(mud ? MudChartChrome.Default : null);
        Assert.Empty(cut.FindAll(".ex-chart-menu"));
        cut.Find(".ex-chart-stage").ContextMenu(new MouseEventArgs { ClientX = 120, ClientY = 80 });
        Assert.Equal([ChartCommandIds.ResetRange, ChartCommandIds.Table, ChartCommandIds.DownloadCsv, ChartCommandIds.DownloadSvg, ChartCommandIds.DownloadPng], MenuCommands(cut));
        Assert.Contains("left:120px;top:80px", cut.Find(".ex-chart-menu").GetAttribute("style"), StringComparison.Ordinal);
        Assert.Equal(mud, cut.FindAll(".mud-ex-chart-menu").Count == 1);
        // Reset range is not available while nothing is narrowed.
        Assert.True(cut.Find($".ex-chart-menu [data-ex-command='{ChartCommandIds.ResetRange}']").HasAttribute("disabled"));
    }

    [Theory] // ADR-0281: a command chosen from the menu runs and closes it; Escape closes it too
    [MemberData(nameof(BothChromes))]
    public void A_command_from_the_menu_runs_and_closes_it(bool mud)
    {
        var cut = Render(mud ? MudChartChrome.Default : null);
        cut.Find(".ex-chart-stage").ContextMenu(new MouseEventArgs());
        // Read once the menu is in the markup: CI's coverage run once found it missing at the first
        // read, after the click had opened it (main at 8fc6cc08).
        // Each outcome is waited for, never read at once: under MudBlazor, a dispatch can wait behind
        // a render the Wrapper's parts started on their own, and return before its handler has run
        // (a reading taken too early; it failed so on CI for #205 and in 1 of 12 local runs).
        cut.WaitForElement($".ex-chart-menu [data-ex-command='{ChartCommandIds.Table}']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".ex-chart-menu"));
            Assert.Single(cut.FindAll("table.ex-chart-table"));
        });

        cut.Find(".ex-chart-stage").ContextMenu(new MouseEventArgs());
        cut.WaitForElement(".ex-chart-menu").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ex-chart-menu")));

        cut.Find(".ex-chart-stage").ContextMenu(new MouseEventArgs());
        cut.WaitForElement(".ex-chart-menu-backdrop").Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ex-chart-menu")));
    }

    [Fact] // ADR-0281, ADR-0273: the Wrapper's toolbar applies a typed range through ExChart, which refuses what it cannot read
    public async Task The_mud_toolbar_applies_the_range_through_the_chart()
    {
        var ranges = new List<ChartRange?>();
        var cut = Render(MudChartChrome.Default, ranges);
        Assert.Empty(cut.FindAll(".ex-chart-toolbar"));
        cut.Find($".mud-ex-chart-toolbar [data-ex-command='{ChartCommandIds.Filters}']").Click();

        await cut.Find(".mud-ex-chart-from input").ChangeAsync(new ChangeEventArgs { Value = "one" });
        await cut.Find(".mud-ex-chart-apply").ClickAsync(new MouseEventArgs());
        Assert.Contains("is not a number", cut.Find(".mud-ex-chart-filter-error").TextContent, StringComparison.Ordinal);
        Assert.Empty(ranges);

        await cut.Find(".mud-ex-chart-from input").ChangeAsync(new ChangeEventArgs { Value = "1" });
        await cut.Find(".mud-ex-chart-to input").ChangeAsync(new ChangeEventArgs { Value = "2" });
        await cut.Find(".mud-ex-chart-apply").ClickAsync(new MouseEventArgs());
        Assert.Equal(new ChartRange(1, 2), Assert.Single(ranges));
        Assert.Empty(cut.FindAll(".mud-ex-chart-filter-error"));
        Assert.False(cut.Find($".mud-ex-chart-toolbar [data-ex-command='{ChartCommandIds.ResetRange}']").HasAttribute("disabled"));

        // The press is read once its handler has run: MudButton runs it after the event's own dispatch returns.
        await cut.Find($".mud-ex-chart-toolbar [data-ex-command='{ChartCommandIds.ResetRange}']").ClickAsync(new MouseEventArgs());
        Assert.Null(ranges[^1]);
    }

    [Fact] // ADR-0252, ADR-0281: a pointer move renders the tooltip and the guide, not the Wrapper's toolbar
    public async Task A_pointer_move_does_not_render_the_mud_toolbar()
    {
        var cut = Render(MudChartChrome.Default);
        var toolbar = cut.FindComponent<MudChartToolbar>();
        var before = toolbar.RenderCount;
        var chart = cut.Instance;
        await cut.InvokeAsync(() => chart.OnPointer(chart.Version, 1, 0, 50));
        await cut.InvokeAsync(() => chart.OnPointer(chart.Version, 2, 0, 50));
        Assert.Single(cut.FindAll(".ex-chart-tooltip"));
        Assert.Equal(before, toolbar.RenderCount);
    }

    [Fact] // ADR-0281: slots 1 to 3 take Primary, Secondary and Tertiary; no slot takes a semantic colour
    public void The_stylesheet_gives_the_theme_three_slots_and_no_semantic_colour()
    {
        var css = File.ReadAllText(Path.Combine(Root(), "src", "ExChart.MudBlazor", "Assets", "mud-ex-chart.css"));
        css = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
        var slots = Regex.Matches(css, @"--ex-chart-colour-(\d+):\s*([^;]+);").ToDictionary(m => int.Parse(m.Groups[1].Value), m => m.Groups[2].Value.Trim());
        Assert.Equal("var(--mud-palette-primary)", slots[0]);
        Assert.Equal("var(--mud-palette-secondary)", slots[1]);
        Assert.Equal("var(--mud-palette-tertiary)", slots[2]);
        Assert.Equal(3, slots.Count);
        Assert.DoesNotMatch(@"--ex-chart-colour-\d+:[^;]*(error|warning|success|info)", css);
        // Every rule is scoped to the paper, the dock or one of the Wrapper's own classes, and none
        // names a class of ExChart's own.
        var selectors = Regex.Matches(css, @"([^{}]+)\{").SelectMany(m => m.Groups[1].Value.Split(',')).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        Assert.NotEmpty(selectors);
        Assert.All(selectors, s => Assert.Matches(@"^\.(mud-ex-grid|mud-ex-chart)(-[a-z0-9-]+)?\b", s));
        Assert.DoesNotContain(selectors, s => Regex.IsMatch(s, @"\.ex-"));
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ExGrid.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
