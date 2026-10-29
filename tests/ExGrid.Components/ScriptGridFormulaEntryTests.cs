using System.Text.Json;
using Bunit;
using ExGrid.Cells;
using ExGrid.Components.Tests.Support;
using Microsoft.JSInterop;
using Xunit;
using ScriptFunction = global::ExGrid.Data.ScriptFunction;

namespace ExGrid.Components.Tests;

/// <summary>
/// ADR-0431: Blazor's ExGrid over the Script Core takes the four functions of Formula entry the browser asks as it
/// decides a key as the Consumer's script, loaded before the grid is mounted, and refuses one first given later. The
/// colours the core hands out cross as places in the palette, both ways. What each function does in the browser is
/// the bridge's tests' and the Script Core's.
/// </summary>
public class ScriptGridFormulaEntryTests : BunitContext
{
    private static readonly ScriptFunction PointAt = ScriptFunction.Of("./js/formula.js", "pointAt");
    private static readonly ScriptFunction ReferenceText = ScriptFunction.Of("./js/formula.js", "referenceText");
    private static readonly ScriptFunction ReferencesIn = ScriptFunction.Of("./js/formula.js", "referencesIn");

    private readonly BunitJSModuleInterop _module;
    private readonly JSRuntimeInvocationHandler<string?> _stamp;
    private readonly List<string> _calls = [];

    public ScriptGridFormulaEntryTests()
    {
        _module = JSInterop.SetupModule(invocation => invocation.Identifier == "import");
        _stamp = _module.Setup<string?>("stamp");
        var grid = _module.SetupModule("attach", _ => { _calls.Add("attach"); return true; });
        grid.SetupVoid("update", _ => true).SetVoidResult();
        grid.SetupVoid("state", _ => true).SetVoidResult();
    }

    private IRenderedComponent<global::ExGrid.Script.ExGrid<TestRow>> Grid(
        Action<ComponentParameterCollectionBuilder<global::ExGrid.Script.ExGrid<TestRow>>>? extra = null, bool stamped = true)
    {
        if (stamped)
            Stamp();
        return Render<global::ExGrid.Script.ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Rows, TestRows.Many(3)).Add(g => g.Columns, TestRows.Columns());
            extra?.Invoke(ps);
        });
    }

    // The script's stamp, answered: until it is, the grid has not asked the browser to load the Consumer's functions.
    private void Stamp() => _stamp.SetResult(global::ExGrid.Script.ScriptCoreAsset.Stamp);

    private void Loads(string? refusal = null)
        => _module.Setup<string?>("loadScripts", _ => { _calls.Add("loadScripts"); return true; }).SetResult(refusal);

    private JsonElement AttachedOptions()
    {
        using var document = JsonDocument.Parse(Assert.IsType<string>(Assert.Single(_module.Invocations["attach"]).Arguments[2]));
        return document.RootElement.GetProperty("options").Clone();
    }

    [Fact] // ADR-0431 section 1: the modules load before the grid is mounted, and the options name each function
    public void ADR0431_the_Consumers_functions_load_before_the_grid_is_mounted_and_are_named_in_its_options()
    {
        Loads();

        Grid(ps => ps.Add(g => g.PointAt, PointAt).Add(g => g.ReferenceText, ReferenceText));

        Assert.Equal(["loadScripts", "attach"], _calls);
        using var loaded = JsonDocument.Parse(Assert.IsType<string>(Assert.Single(_module.Invocations["loadScripts"]).Arguments[0]));
        Assert.Equal(["pointAt", "referenceText"], loaded.RootElement.EnumerateArray().Select(script => script.GetProperty("export").GetString()));
        var scripts = AttachedOptions().GetProperty("scripts");
        Assert.Equal("./js/formula.js", scripts.GetProperty("pointAt").GetProperty("module").GetString());
        Assert.Equal("referenceText", scripts.GetProperty("referenceText").GetProperty("export").GetString());
        Assert.False(scripts.TryGetProperty("cycleReference", out _));
    }

    [Fact] // ADR-0431 section 1: a grid given none loads nothing and names none
    public void ADR0431_a_grid_given_no_function_loads_nothing()
    {
        Grid();

        Assert.Equal(["attach"], _calls);
        Assert.False(AttachedOptions().TryGetProperty("scripts", out _));
    }

    [Fact] // ADR-0431 section 1: a module that does not load is said in the grid's place, and no grid is mounted
    public void ADR0431_a_function_that_cannot_be_had_is_said_in_the_grids_place_and_no_grid_is_mounted()
    {
        Loads("The module \"./js/formula.js\" did not load: 404.");

        var cut = Grid(ps => ps.Add(g => g.ReferencesIn, ReferencesIn));

        Assert.Equal(["loadScripts"], _calls);
        cut.WaitForAssertion(() => Assert.Equal(
            "The module \"./js/formula.js\" did not load: 404.", cut.Find("[data-ex-notice=script-function]").TextContent));
    }

    [Fact] // ADR-0431 section 1: a function first given after the grid was made is refused by name
    public void ADR0431_a_function_first_given_later_is_refused()
    {
        Loads();
        var cut = Grid(ps => ps.Add(g => g.ReferencesIn, ReferencesIn));

        var thrown = Assert.Throws<NotSupportedException>(() => cut.Render(ps => ps.Add(g => g.PointAt, PointAt).Add(g => g.ReferenceText, ReferenceText)));

        Assert.Contains("\"pointAt\" of \"./js/formula.js\" is given after the grid was made", thrown.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0431 section 1: the grid is made when the browser is asked to load them, so one given before is loaded with the rest
    public void ADR0431_a_function_given_before_the_browser_is_asked_to_load_them_is_loaded()
    {
        Loads();
        var cut = Grid(ps => ps.Add(g => g.ReferencesIn, ReferencesIn), stamped: false);

        cut.Render(ps => ps.Add(g => g.PointAt, PointAt).Add(g => g.ReferenceText, ReferenceText));
        cut.InvokeAsync(Stamp);

        cut.WaitForAssertion(() => Assert.Equal(["loadScripts", "attach"], _calls));
        using var loaded = JsonDocument.Parse(Assert.IsType<string>(Assert.Single(_module.Invocations["loadScripts"]).Arguments[0]));
        Assert.Equal(["pointAt", "referenceText", "referencesIn"],
            loaded.RootElement.EnumerateArray().Select(script => script.GetProperty("export").GetString()).Order(StringComparer.Ordinal));
    }

    [Fact] // ADR-0431 section 1: one the grid was made with is loaded already, so it may be taken away and given again
    public void ADR0431_a_function_the_grid_was_made_with_may_be_taken_away_and_given_again()
    {
        Loads();
        var cut = Grid(ps => ps.Add(g => g.ReferencesIn, ReferencesIn));

        cut.Render(ps => ps.Add(g => g.ReferencesIn, (ScriptFunction?)null));
        cut.Render(ps => ps.Add(g => g.ReferencesIn, ReferencesIn));

        Assert.Single(_module.Invocations["loadScripts"]);
    }

    [Fact] // ADR-0051: pointing writes the Consumer's Reference text, so PointAt without it is refused by name
    public void ADR0431_PointAt_without_ReferenceText_is_refused()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() => Grid(ps => ps.Add(g => g.PointAt, PointAt)));

        Assert.Contains("PointAt needs ReferenceText", thrown.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0431 section 2: the colours handed out cross as places in the palette, both ways
    public async Task ADR0431_colours_cross_as_places_in_the_palette()
    {
        IReadOnlyList<ReferenceKeyColour>? told = null;
        var cut = Grid(ps => ps
            .Add(g => g.OutlinedColumns, [new OutlinedColumn("Book", ReferenceColour.Reported(3))])
            .Add(g => g.OnReferenceKeyColoursChanged, (IReadOnlyList<ReferenceKeyColour> keys) => told = keys));

        var options = AttachedOptions();
        var outlined = Assert.Single(options.GetProperty("outlinedColumns").EnumerateArray());
        Assert.Equal("Book", outlined.GetProperty("column").GetString());
        Assert.Equal(3, outlined.GetProperty("colour").GetInt32());
        Assert.Contains("referenceKeyColoursChanged", options.GetProperty("hears").EnumerateArray().Select(kind => kind.GetString()));

        var bridge = Assert.IsType<DotNetObjectReference<global::ExGrid.Script.ScriptBridge<TestRow>>>(
            Assert.Single(_module.Invocations["attach"]).Arguments[1]).Value;
        using var keys = JsonDocument.Parse("""[{"key":"Positions[PV]","colour":2}]""");
        await cut.InvokeAsync(() => bridge.Heard("referenceKeyColoursChanged", keys.RootElement));

        Assert.Equal([new ReferenceKeyColour("Positions[PV]", ReferenceColour.Reported(2))], told);
    }

    [Fact] // ADR-0057: a place outside the palette is no colour the core hands out
    public void ADR0431_a_place_outside_the_palette_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ReferenceColour.Reported(8));
    }
}
