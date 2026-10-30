using System.Text.Json;
using Bunit;
using ExGrid.Components.Tests.Support;
using Microsoft.JSInterop;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ADR-0350 section 7, ADR-0004: Blazor's ExGrid over the Script Core sends the browser the part of its source's
/// Window around the rows the grid asked for, so a source that holds every row crosses the rows in view and a few
/// screens either side, never the whole result on each change.
/// </summary>
public class ScriptGridWindowTests : BunitContext
{
    private readonly BunitJSModuleInterop _grid;
    private readonly BunitJSModuleInterop _module;

    public ScriptGridWindowTests()
    {
        _module = JSInterop.SetupModule(invocation => invocation.Identifier == "import");
        _module.Setup<string?>("stamp").SetResult(global::ExGrid.Components.ScriptCoreAsset.Stamp);
        _grid = _module.SetupModule("attach", _ => true);
        _grid.SetupVoid("update", _ => true).SetVoidResult();
        _grid.SetupVoid("state", _ => true).SetVoidResult();
    }

    private IRenderedComponent<global::ExGrid.Components.ExGrid<TestRow>> Grid(int rows) => Render<global::ExGrid.Components.ExGrid<TestRow>>(ps => ps
        .Add(g => g.Rows, TestRows.Many(rows))
        .Add(g => g.Columns, TestRows.Columns()));

    private static JsonElement Parsed(object? json)
    {
        using var document = JsonDocument.Parse(Assert.IsType<string>(json));
        return document.RootElement.Clone();
    }

    [Fact] // ADR-0004: before the grid asks, the first screens cross, and the count says how many rows there are
    public void ADR0350_the_attach_sends_the_first_rows_and_the_whole_count()
    {
        Grid(10_000);

        var state = Parsed(Assert.Single(_module.Invocations["attach"]).Arguments[2]).GetProperty("state");
        Assert.Equal(0, state.GetProperty("windowStart").GetInt32());
        Assert.Equal(256, state.GetProperty("window").GetArrayLength());
        Assert.Equal(10_000, state.GetProperty("totalCount").GetInt32());
    }

    [Fact] // ADR-0004: rows asked for past what crossed are sent with two times as many either side, and no more
    public async Task ADR0350_rows_the_grid_asks_for_cross_with_a_margin_either_side()
    {
        var cut = Grid(10_000);
        var bridge = Assert.IsType<DotNetObjectReference<global::ExGrid.Components.ScriptBridge<TestRow>>>(
            Assert.Single(_module.Invocations["attach"]).Arguments[1]);

        await cut.InvokeAsync(() => bridge.Value.NeedRange(1, 5_000, 20));

        var state = Parsed(_grid.Invocations["state"][^1].Arguments[0]);
        Assert.Equal(5_000 - 128, state.GetProperty("windowStart").GetInt32());
        Assert.Equal(20 + 2 * 128, state.GetProperty("window").GetArrayLength());
        Assert.Equal("Row 004872", state.GetProperty("window")[0].GetProperty("c")[0].GetString());
    }
}
