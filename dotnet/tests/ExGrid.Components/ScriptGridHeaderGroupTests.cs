using System.Text.Json;
using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.JSInterop;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ADR-0400 section 1: Blazor's ExGrid over the Script Core carries a Header Group's id, its expanded and its Hidden
/// Mark to the browser as the core's options spell them, and hands the toggle the core notifies to
/// <c>OnHeaderGroupToggled</c>. What the toggle does is the core's, tested there and in layer 3. The C# core's grid,
/// which draws no toggle, refuses one rather than draw the group without it.
/// </summary>
public class ScriptGridHeaderGroupTests : BunitContext
{
    private readonly BunitJSModuleInterop _module;

    public ScriptGridHeaderGroupTests()
    {
        _module = JSInterop.SetupModule(invocation => invocation.Identifier == "import");
        _module.Setup<string?>("stamp").SetResult(global::ExGrid.Components.ScriptCoreAsset.Stamp);
        var grid = _module.SetupModule("attach", _ => true);
        grid.SetupVoid("update", _ => true).SetVoidResult();
        grid.SetupVoid("state", _ => true).SetVoidResult();
    }

    private static readonly GridColumn<TestRow>[] Columns =
    [
        new("Book", ColumnType.Text, r => r.Book),
        new("Amount", ColumnType.Number, r => r.Amount),
    ];

    private static readonly HeaderGroup[] Groups =
    [
        new("Who", ["Book"]),
        new("Money", ["Amount"]) { Id = "money", Expanded = false, HiddenMark = new(CellState.Modified, "1 user-set field in hidden columns") },
    ];

    private global::ExGrid.Components.ScriptBridge<TestRow> Bridge() => Assert.IsType<DotNetObjectReference<global::ExGrid.Components.ScriptBridge<TestRow>>>(
        Assert.Single(_module.Invocations["attach"]).Arguments[1]).Value;

    private JsonElement Attached()
    {
        using var document = JsonDocument.Parse(Assert.IsType<string>(Assert.Single(_module.Invocations["attach"]).Arguments[2]));
        return document.RootElement.GetProperty("options").Clone();
    }

    [Fact] // ADR-0400 / HG-18: the declaration crosses as the core spells it, and the toggle is heard only where handled
    public void ADR0400_a_groups_toggle_and_mark_cross_as_the_core_spells_them()
    {
        Render<global::ExGrid.Components.ExGrid<TestRow>>(ps => ps
            .Add(g => g.Rows, TestRows.Window())
            .Add(g => g.Columns, Columns)
            .Add(g => g.HeaderGroups, Groups)
            .Add(g => g.OnHeaderGroupToggled, (HeaderGroupToggle _) => { }));

        var options = Attached();
        Assert.Equal(
            """[{"label":"Who","columns":["Book"],"tier":1,"tierSpan":1},{"label":"Money","columns":["Amount"],"tier":1,"tierSpan":1,"id":"money","expanded":false,"hiddenMark":{"state":"modified","reason":"1 user-set field in hidden columns"}}]""",
            options.GetProperty("headerGroups").GetRawText());
        Assert.Contains("headerGroupToggled", options.GetProperty("hears").EnumerateArray().Select(kind => kind.GetString()));
    }

    [Fact] // ADR-0400 / HG-18: the core's notification reaches the Consumer's handler, the Focus's column with it or not
    public async Task ADR0400_the_toggle_the_core_notifies_reaches_OnHeaderGroupToggled()
    {
        var heard = new List<HeaderGroupToggle>();
        var cut = Render<global::ExGrid.Components.ExGrid<TestRow>>(ps => ps
            .Add(g => g.Rows, TestRows.Window())
            .Add(g => g.Columns, Columns)
            .Add(g => g.HeaderGroups, Groups)
            .Add(g => g.OnHeaderGroupToggled, (HeaderGroupToggle toggle) => heard.Add(toggle)));

        await cut.InvokeAsync(() => Bridge().Heard("headerGroupToggled", Payload("""{"id":"money","expanded":true,"focusColumn":"Amount"}""")));
        await cut.InvokeAsync(() => Bridge().Heard("headerGroupToggled", Payload("""{"id":"money","expanded":true}""")));

        Assert.Equal([new HeaderGroupToggle("money", true, "Amount"), new HeaderGroupToggle("money", true, null)], heard);
    }

    [Fact] // ADR-0400 section 10: the C# core's grid draws no toggle, so it refuses a group that asks for one, by name
    public void ADR0400_the_csharp_cores_grid_refuses_a_toggle_it_would_not_draw()
    {
        var refused = Assert.Throws<NotSupportedException>(() => HeaderGroupLayout.Resolve(Groups, ["Book", "Amount"], 0));
        Assert.Contains("Header Group 'Money'", refused.Message, StringComparison.Ordinal);
        Assert.Contains("ExGrid.Components.ExGrid", refused.Message, StringComparison.Ordinal);
    }

    private static JsonElement Payload(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
