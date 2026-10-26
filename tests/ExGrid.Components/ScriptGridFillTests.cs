using System.Text.Json;
using Bunit;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.JSInterop;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ADR-0050 item 5, ADR-0350 section 7: Blazor's ExGrid over the Script Core asks the core for the fill handle and
/// hears the fill it raises as the C# core's <see cref="GridFillIntent"/>. The browser has placed the Selection by the
/// time .NET hears it, so a refusal cannot be taken back and fails the component, as an edit's and a paste's do.
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
        { "source": { "topRow": 2, "leftColumn": 0, "rowCount": 2, "columnCount": 1 },
          "target": { "topRow": 0, "leftColumn": 0, "rowCount": 2, "columnCount": 1 },
          "direction": "up", "rowSequenceVersion": 3 }
        """);

    [Fact] // ADR-0050 item 5: the handle is asked for, and a fill the core raises reaches OnFill as the C# core's intent
    public async Task ADR0350_a_fill_the_core_raises_reaches_OnFill()
    {
        var heard = new List<GridFillIntent>();
        var cut = Grid(heard.Add);
        var options = Json(Assert.IsType<string>(Assert.Single(_module.Invocations["attach"]).Arguments[2])).GetProperty("options");
        Assert.True(options.GetProperty("showFillHandle").GetBoolean());
        Assert.Contains("fill", options.GetProperty("hears").EnumerateArray().Select(kind => kind.GetString()));

        await cut.InvokeAsync(() => Bridge().Heard("fill", Fill));

        var intent = Assert.Single(heard);
        Assert.Equal(new SelectionRange(2, 0, 2, 1), intent.Source);
        Assert.Equal(new SelectionRange(0, 0, 2, 1), intent.Target);
        Assert.Equal(GridDirection.Up, intent.Direction);
        Assert.Equal(3, intent.RowSequenceVersion);
    }

    [Fact] // ADR-0350 section 7: a fill the browser has already selected over cannot be refused from .NET, and saying so is loud
    public async Task ADR0350_a_fill_refused_in_dotnet_fails_the_component()
    {
        var cut = Grid(intent => intent.Refuse());

        await cut.InvokeAsync(() => Bridge().Heard("fill", Fill));

        // Raised to the renderer as the handler returned, as the component's other failures are.
        Assert.True(Renderer.UnhandledException.IsCompleted);
        var thrown = Assert.IsType<NotSupportedException>(await Renderer.UnhandledException);
        Assert.Contains("refused a fill", thrown.Message, StringComparison.Ordinal);
    }
}
