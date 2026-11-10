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

    [Fact] // FN-22, ADR-0003: the same Columns list changed in place is read as it was handed until it is handed again
    public async Task FN22_a_columns_list_changed_in_place_is_a_new_list_once_handed_again_and_not_before()
    {
        var columns = new List<GridColumn<TestRow>>(TestRows.Columns());
        var cut = Render<global::ExGrid.Components.ExGrid<TestRow>>(ps => ps
            .Add(g => g.Rows, TestRows.Many(10_000))
            .Add(g => g.Columns, columns));
        var bridge = Assert.IsType<DotNetObjectReference<global::ExGrid.Components.ScriptBridge<TestRow>>>(
            Assert.Single(_module.Invocations["attach"]).Arguments[1]);
        var version = Parsed(Assert.Single(_module.Invocations["attach"]).Arguments[2]).GetProperty("options").GetProperty("columnsVersion").GetInt32();

        columns.RemoveAt(columns.Count - 1);
        // A pass of the grid's own writes its rows by the columns as they were handed, four of them.
        await cut.InvokeAsync(() => bridge.Value.NeedRange(1, 5_000, 20));
        var asked = Parsed(_grid.Invocations["state"][^1].Arguments[0]);
        Assert.Equal(version, asked.GetProperty("cv").GetInt32());
        Assert.Equal(4, asked.GetProperty("window")[0].GetProperty("c").GetArrayLength());

        cut.Render(ps => ps.Add(g => g.Columns, columns));
        var options = Parsed(_grid.Invocations["update"][^1].Arguments[0]).GetProperty("options");
        Assert.NotEqual(version, options.GetProperty("columnsVersion").GetInt32());
        Assert.Equal(3, options.GetProperty("columns").GetArrayLength());
    }

    [Fact] // ADR-0500: the header display crosses by its name, the default as standard, and a change crosses as an update of the grid that stands
    public void ADR0500_the_header_display_crosses_by_name_and_a_change_is_an_update()
    {
        var cut = Grid(100);
        var attached = Parsed(Assert.Single(_module.Invocations["attach"]).Arguments[2]).GetProperty("options");
        Assert.Equal("standard", attached.GetProperty("headerDisplay").GetString());

        cut.Render(ps => ps.Add(g => g.HeaderDisplay, GridHeaderDisplay.Compact));

        var updated = Parsed(_grid.Invocations["update"][^1].Arguments[0]).GetProperty("options");
        Assert.Equal("compact", updated.GetProperty("headerDisplay").GetString());
        Assert.Single(_module.Invocations["attach"]);
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
