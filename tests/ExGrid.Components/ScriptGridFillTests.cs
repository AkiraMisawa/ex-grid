using System.Text.Json;
using Bunit;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.JSInterop;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ADR-0050 item 5, ADR-0350 section 7, ADR-0380 section 5: Blazor's ExGrid over the Script Core asks the core for the
/// fill handle and hears the fill it raises as the C# core's <see cref="GridFillIntent"/>. The fill is pending in the
/// browser until .NET answers, and the answer says whether the Consumer refused it.
/// </summary>
public class ScriptGridFillTests : BunitContext
{
    private readonly BunitJSModuleInterop _module;

    public ScriptGridFillTests()
    {
        _module = JSInterop.SetupModule(invocation => invocation.Identifier == "import");
        _module.Setup<string?>("stamp").SetResult(global::ExGrid.Script.ScriptCoreAsset.Stamp);
        var grid = _module.SetupModule("attach", _ => true);
        grid.SetupVoid("update", _ => true).SetVoidResult();
        grid.SetupVoid("state", _ => true).SetVoidResult();
    }

    private IRenderedComponent<global::ExGrid.Script.ExGrid<TestRow>> Grid(Action<GridFillIntent> onFill) =>
        Render<global::ExGrid.Script.ExGrid<TestRow>>(ps => ps
            .Add(g => g.Rows, TestRows.Many(10))
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.ShowFillHandle, true)
            .Add(g => g.OnFill, onFill));

    private global::ExGrid.Script.ScriptBridge<TestRow> Bridge() =>
        Assert.IsType<DotNetObjectReference<global::ExGrid.Script.ScriptBridge<TestRow>>>(
            Assert.Single(_module.Invocations["attach"]).Arguments[1]).Value;

    private static JsonElement Json(string text)
    {
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static readonly JsonElement Fill = Json("""
        { "answer": 1,
          "source": { "topRow": 2, "leftColumn": 0, "rowCount": 2, "columnCount": 1 },
          "target": { "topRow": 0, "leftColumn": 0, "rowCount": 2, "columnCount": 1 },
          "direction": "up", "rowSequenceVersion": 3 }
        """);

    [Fact] // ADR-0050 item 5: the handle is asked for, and a fill the core raises reaches OnFill as the C# core's intent
    public async Task ADR0350_a_fill_the_core_raises_reaches_OnFill_and_is_answered_as_taken()
    {
        var heard = new List<GridFillIntent>();
        var cut = Grid(heard.Add);
        var options = Json(Assert.IsType<string>(Assert.Single(_module.Invocations["attach"]).Arguments[2])).GetProperty("options");
        Assert.True(options.GetProperty("showFillHandle").GetBoolean());
        Assert.Contains("fill", options.GetProperty("hears").EnumerateArray().Select(kind => kind.GetString()));

        var answer = await cut.InvokeAsync(() => Bridge().Heard("fill", Fill));

        Assert.Null(answer);
        var intent = Assert.Single(heard);
        Assert.Equal(new SelectionRange(2, 0, 2, 1), intent.Source);
        Assert.Equal(new SelectionRange(0, 0, 2, 1), intent.Target);
        Assert.Equal(GridDirection.Up, intent.Direction);
        Assert.Equal(3, intent.RowSequenceVersion);
    }

    [Fact] // ADR-0380 section 5: a fill the Consumer refuses is answered as refused, with no sentence, and the component stands
    public async Task ADR0380_a_fill_refused_in_dotnet_is_answered_as_refused()
    {
        var cut = Grid(intent => intent.Refuse());

        var answer = await cut.InvokeAsync(() => Bridge().Heard("fill", Fill));

        Assert.Equal("", answer);
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }
}
