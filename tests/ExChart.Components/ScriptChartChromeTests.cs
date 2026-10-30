using System.Text.Json;
using Bunit;
using ExChart.Chrome;
using ExChart.Engine;
using ExChart.MudBlazor;
using ExChart.Components;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Xunit;
using ScriptChart = ExChart.Components.ExChart;

namespace ExChart.Components.Tests;

/// <summary>
/// ADR-0410 sections 1 and 3: Blazor's ExChart over the Script Core names the slots its Chrome fills, renders each
/// slot the script mounts beside its root in a hidden holder, drawn by the Chrome's view overload from the data the
/// script sent, hands the script the element once drawn, and removes the holder when the script says the slot is gone.
/// Moving the element and wiring its controls are the script's, tested there and in layer 3.
/// </summary>
public class ScriptChartChromeTests : BunitContext
{
    private readonly BunitJSModuleInterop _module;
    private readonly BunitJSModuleInterop _chart;

    public ScriptChartChromeTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        _module = JSInterop.SetupModule(invocation => invocation.Identifier == "import"
            && invocation.Arguments[0] is string address && address.Contains("ex-chart-core.min.js", StringComparison.Ordinal));
        _module.Setup<string?>("stamp").SetResult(ScriptCoreAsset.Stamp);
        _chart = _module.SetupModule("attachChart", _ => true);
        _chart.SetupVoid("update", _ => true).SetVoidResult();
        _chart.SetupVoid("slotRendered", _ => true).SetVoidResult();
        _chart.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    private static ChartDefinition Bonds() =>
        new(ChartAxis.Numbers("Years"), [ChartSeries.Of("bonds", "Bonds", SeriesKind.Level, [new(0, ChartValue.Of(1.0)), new(1, ChartValue.Of(2.0))])]);

    private IRenderedComponent<ScriptChart> Chart(IChartChrome? chrome) =>
        Render<ScriptChart>(ps => ps.Add(c => c.Definition, Bonds()).Add(c => c.Chrome, chrome));

    private ScriptBridge Bridge() =>
        Assert.IsType<DotNetObjectReference<ScriptBridge>>(Assert.Single(_module.Invocations["attachChart"]).Arguments[1]).Value;

    private JsonElement Options()
    {
        using var document = JsonDocument.Parse(Assert.IsType<string>(Assert.Single(_module.Invocations["attachChart"]).Arguments[2]));
        return document.RootElement.GetProperty("options").Clone();
    }

    private const string Commands =
        """[{"id":"exchart.filters","label":"Chart Filters","enabled":true,"expanded":true,"title":null},"""
        + """{"id":"exchart.reset-range","label":"Reset range","enabled":false,"expanded":null,"title":null},"""
        + """{"id":"exchart.download.csv","label":"CSV","enabled":false,"expanded":null,"title":"Thinned by its source"}]""";

    private const string Words =
        """{"from":"Von","to":"Bis","apply":"Anwenden","axis":"Achse","proportional":"Proportional","categories":"Kategorien","downloads":"Download","filters":"Filter"}""";

    private static string Toolbar(string filters) => $$"""{"name":"Bonds","commands":{{Commands}},"filters":{{filters}},"words":{{Words}}}""";

    [Fact] // ADR-0410 section 1: the script is told which slots the Chrome fills, and none where no Chrome fills one
    public void ADR0410_the_slots_the_Chrome_fills_cross_with_the_options()
    {
        Chart(MudChartChrome.Default);
        Assert.Equal("""["toolbar","menu"]""", Options().GetProperty("razorSlots").GetRawText());
        // Outside the browser, .NET is across a circuit: the script holds a call too large for its messages.
        Assert.True(JsonDocument.Parse(Assert.IsType<string>(Assert.Single(_module.Invocations["attachChart"]).Arguments[2])).RootElement.GetProperty("circuit").GetBoolean());
    }

    [Fact] // ADR-0410 section 1: a Chrome that names a slot this adapter does not fill is refused by name as it is handed
    public void ADR0410_a_slot_the_adapter_does_not_fill_is_refused()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => Chart(new Unknown()));
        Assert.Contains("does not fill", refused.Message, StringComparison.Ordinal);
    }

    private sealed class Unknown : IChartChrome
    {
        public ChartScriptSlots ScriptSlots => (ChartScriptSlots)4;
    }

    [Fact] // ADR-0410 sections 1 to 3, ADR-0281: MudBlazor's toolbar is drawn beside the root from the data sent, its controls marked for the script, and goes when unmounted
    public async Task ADR0410_MudBlazors_toolbar_is_drawn_from_the_data_and_marked_for_the_script()
    {
        var cut = Chart(MudChartChrome.Default);
        await cut.InvokeAsync(() => Bridge().MountSlot(1, 1, "toolbar", Toolbar("null")));

        var contents = cut.Find("div[hidden] > .ex-razor-slot");
        Assert.Equal("1", contents.GetAttribute("data-ex-version"));
        Assert.Equal("Bonds", contents.QuerySelector("[role=toolbar]")?.GetAttribute("aria-label"));
        var filters = contents.QuerySelector($"button[{ChartScriptSlotAttributes.Command}='exchart.filters']");
        Assert.Equal("true", filters?.GetAttribute("aria-expanded"));
        Assert.True(contents.QuerySelector($"button[{ChartScriptSlotAttributes.Command}='exchart.reset-range']")?.HasAttribute("disabled"));
        // The downloads are a group in the toolbar, not a menu whose list would open outside the chart.
        var csv = contents.QuerySelector($".mud-ex-chart-downloads button[{ChartScriptSlotAttributes.Command}='exchart.download.csv']");
        Assert.Equal("Thinned by its source", csv?.GetAttribute("title"));
        Assert.Equal("Download", contents.QuerySelector(".mud-ex-chart-downloads")?.GetAttribute("aria-label"));
        Assert.Empty(contents.QuerySelectorAll(".mud-menu"));
        Assert.Equal([1L, 1], Assert.Single(_chart.Invocations["slotRendered"]).Arguments.Take(2));

        // Chart Filters open on a category axis: the ends are native selects of the categories, an empty option the
        // axis' own end, with Apply, and no axis switch where the axis cannot be switched; the words are the chart's.
        await cut.InvokeAsync(() => Bridge().UpdateSlot(1, 2, Toolbar(
            """{"values":"labels","labels":["2Y","5Y","10Y"],"hint":"a category","error":"'7Y' is not a category on this axis","scale":null}""")));
        contents = cut.Find(".ex-razor-slot");
        Assert.Equal("Filter", contents.QuerySelector(".mud-ex-chart-filters")?.GetAttribute("aria-label"));
        var from = contents.QuerySelector($"select[{ChartScriptSlotAttributes.From}]");
        Assert.Equal(["", "2Y", "5Y", "10Y"], from!.QuerySelectorAll("option").Select(o => o.GetAttribute("value")));
        Assert.NotNull(contents.QuerySelector($"select[{ChartScriptSlotAttributes.To}]"));
        Assert.Equal("Anwenden", contents.QuerySelector($"[{ChartScriptSlotAttributes.Apply}]")?.TextContent.Trim());
        Assert.Null(contents.QuerySelector($"[{ChartScriptSlotAttributes.Scale}]"));
        Assert.Contains("'7Y' is not a category", contents.QuerySelector(".mud-ex-chart-filter-error")?.TextContent, StringComparison.Ordinal);
        Assert.Equal([1L, 2], _chart.Invocations["slotRendered"][^1].Arguments.Take(2));

        // A date axis that can be switched: the ends are native text fields showing the axis' hint, with no value bound
        // and no handler of .NET's, which would write a copy a round trip behind over what the script writes; a native
        // date input would read a date it cannot make out as empty. The switch offers both scales.
        await cut.InvokeAsync(() => Bridge().UpdateSlot(1, 3, Toolbar(
            """{"values":"dates","labels":[],"hint":"yyyy-MM-dd","error":null,"scale":"categories"}""")));
        contents = cut.Find(".ex-razor-slot");
        var date = contents.QuerySelector($"input[{ChartScriptSlotAttributes.From}]");
        Assert.Equal("text", date?.GetAttribute("type"));
        Assert.Equal("yyyy-MM-dd", date?.GetAttribute("placeholder"));
        Assert.Null(date?.GetAttribute("value"));
        Assert.DoesNotContain(date!.Attributes, a => a.Name.StartsWith("blazor:on", StringComparison.Ordinal));
        Assert.Equal(["proportional", "categories"],
            contents.QuerySelectorAll($"select[{ChartScriptSlotAttributes.Scale}] option").Select(o => o.GetAttribute("value")));

        await cut.InvokeAsync(() => Bridge().UnmountSlot(1));
        Assert.Empty(cut.FindAll(".ex-razor-slot"));
    }

    [Fact] // ADR-0410 sections 1 and 3, ADR-0281: MudBlazor's menu items are drawn from the data, each marked with its command
    public async Task ADR0410_MudBlazors_menu_items_are_marked_with_their_commands()
    {
        var cut = Chart(MudChartChrome.Default);
        await cut.InvokeAsync(() => Bridge().MountSlot(1, 1, "menu", $$"""{"name":"Bonds","commands":{{Commands}}}"""));
        var items = cut.FindAll(".ex-razor-slot [role=menuitem]");
        Assert.Equal(["exchart.filters", "exchart.reset-range", "exchart.download.csv"], items.Select(i => i.GetAttribute(ChartScriptSlotAttributes.Command)));
        Assert.True(items[1].HasAttribute("disabled"));
    }

    [Fact] // ADR-0410 section 4: a Chrome that fills no Script slot leaves every part the Script Core's own, and a slot it no longer fills draws nothing
    public async Task ADR0410_a_Chrome_that_fills_no_slot_draws_nothing()
    {
        var cut = Chart(MudChartChrome.Default);
        await cut.InvokeAsync(() => Bridge().MountSlot(1, 1, "menu", $$"""{"name":"Bonds","commands":{{Commands}}}"""));
        cut.Render(ps => ps.Add(c => c.Definition, Bonds()).Add(c => c.Chrome, new None()));
        Assert.Equal(JsonValueKind.Null, JsonDocument.Parse(Assert.IsType<string>(_chart.Invocations["update"][^1].Arguments[0]))
            .RootElement.GetProperty("options").GetProperty("razorSlots").ValueKind);
        Assert.Empty(cut.FindAll(".ex-razor-slot [role=menuitem]"));
    }

    private sealed class None : IChartChrome;

    /// <summary>A held call's arguments as the browser hands them over: gzip of their JSON.</summary>
    private sealed class Gzipped(byte[] text) : IJSStreamReference
    {
        private readonly byte[] _bytes = Packed(text);
        public long Length => _bytes.LongLength;
        public ValueTask<Stream> OpenReadStreamAsync(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            Length > maxAllowedSize ? throw new ArgumentOutOfRangeException(nameof(maxAllowedSize)) : ValueTask.FromResult<Stream>(new MemoryStream(_bytes, writable: false));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new NotSupportedException();

        private static byte[] Packed(byte[] text)
        {
            using var packed = new MemoryStream();
            using (var gzip = new System.IO.Compression.GZipStream(packed, System.IO.Compression.CompressionLevel.Fastest))
                gzip.Write(text);
            return packed.ToArray();
        }
    }

    [Fact] // ADR-0005 (as amended 2026-10-28): a toolbar too large for a circuit's message is taken as a stream in its turn, ahead of the calls after it
    public async Task ADR0005_a_held_toolbar_is_mounted_in_its_turn()
    {
        var take = _chart.Setup<IJSStreamReference>("take", _ => true);
        var cut = Chart(MudChartChrome.Default);
        var arguments = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new object[] { 1, 1, "toolbar", Toolbar("null") }));
        using var crossed = JsonDocument.Parse($$"""{"held":3,"bytes":{{arguments.Length}}}""");
        Task mounted = Task.CompletedTask, unmounted = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            mounted = Bridge().Held("MountSlot", crossed.RootElement);
            unmounted = Bridge().UnmountSlot(1);
        });
        Assert.Equal([3], Assert.Single(take.Invocations).Arguments);
        Assert.False(unmounted.IsCompleted);

        take.SetResult(new Gzipped(arguments));
        await mounted.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        await unmounted.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        // Mounted, then taken away by the call that came after it: nothing stands.
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ex-razor-slot")));
    }
}
