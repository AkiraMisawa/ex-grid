using System.Text.Json;
using Bunit;
using ExGrid.Chrome;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ADR-0421: Blazor's ExGrid over the Script Core asks the Consumer's Context Menu commands of .NET as the browser's
/// menu opens, by the number the browser gave the menu, and runs one where the browser pressed it. That the menu
/// stands once they come, and that the input after a command waits for it, is the core's, tested there.
/// </summary>
public class ScriptGridContextCommandTests : BunitContext
{
    private readonly BunitJSModuleInterop _module;
    private readonly TestRow[] _rows = TestRows.Window();

    public ScriptGridContextCommandTests()
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

    private IRenderedComponent<global::ExGrid.Components.ExGrid<TestRow>> Grid(Func<ContextMenuContext<TestRow>, IEnumerable<GridCommand>>? commands)
        => Render<global::ExGrid.Components.ExGrid<TestRow>>(ps => ps
            .Add(g => g.Rows, _rows).Add(g => g.Columns, Columns)
            .Add(g => g.ContextCommands, commands)
            .Add(g => g.ContextCommandLabel, id => id == "open-trade" ? "Open trade" : null));

    private global::ExGrid.Components.ScriptBridge<TestRow> Bridge() => Assert.IsType<DotNetObjectReference<global::ExGrid.Components.ScriptBridge<TestRow>>>(
        Assert.Single(_module.Invocations["attach"]).Arguments[1]).Value;

    private JsonElement Attached()
    {
        using var document = JsonDocument.Parse(Assert.IsType<string>(Assert.Single(_module.Invocations["attach"]).Arguments[2]));
        return document.RootElement.Clone();
    }

    private long IdOf(int index) => Attached().GetProperty("state").GetProperty("window")[index].GetProperty("i").GetInt64();

    private static JsonElement Selection(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [Fact] // ADR-0421 / ADR-0036: the menu's commands are .NET's, asked with the row, the column and the Selection the browser opened it on
    public async Task ADR0421_the_consumers_commands_are_asked_with_what_the_browser_opened_the_menu_on_and_cross_with_their_words()
    {
        ContextMenuContext<TestRow>? asked = null;
        var cut = Grid(context =>
        {
            asked = context;
            return [new GridCommand("open-trade", true, () => Task.CompletedTask), new GridCommand("audit", false, () => Task.CompletedTask)];
        });
        Assert.True(Attached().GetProperty("options").GetProperty("contextCommands").GetBoolean());

        var answer = await cut.InvokeAsync(() => Bridge().ContextCommands(1, 1, 1, IdOf(2), "Amount",
            Selection("""[{"topRow":1,"leftColumn":0,"rowCount":2,"columnCount":2}]"""), 0));

        // A command the Consumer gives no words crosses without them, and the core shows its id.
        Assert.Equal("""[{"id":"open-trade","enabled":true,"label":"Open trade"},{"id":"audit","enabled":false}]""", answer);
        Assert.NotNull(asked);
        Assert.Same(_rows[2], asked.Row);
        Assert.Equal(("Amount", ColumnType.Number, 0), (asked.Column, asked.Type, asked.RowSequenceVersion));
        Assert.Equal([new SelectionRange(1, 0, 2, 2)], asked.Selection);
        // The menu closes itself in the browser.
        Assert.Throws<NotSupportedException>(asked.Close);
    }

    [Fact] // ADR-0421: a press runs the command of the menu it stood in, in its turn, and only the last menu asked runs one
    public async Task ADR0421_a_press_runs_the_command_of_the_last_menu_asked()
    {
        var ran = new List<string>();
        var asked = 0;
        var cut = Grid(_ =>
        {
            asked += 1;
            var menu = asked;
            return [new GridCommand("open-trade", true, () => { ran.Add($"open-trade of menu {menu}"); return Task.CompletedTask; })];
        });
        var selection = Selection("""[{"topRow":0,"leftColumn":0,"rowCount":1,"columnCount":1}]""");
        await cut.InvokeAsync(() => Bridge().ContextCommands(1, 1, 1, IdOf(0), "Book", selection, 0));
        await cut.InvokeAsync(() => Bridge().ContextCommands(2, 1, 1, IdOf(0), "Book", selection, 0));

        await cut.InvokeAsync(() => Bridge().RunContextCommand(2, 0));
        await Assert.ThrowsAsync<InvalidOperationException>(() => cut.InvokeAsync(() => Bridge().RunContextCommand(1, 0)));

        Assert.Equal(["open-trade of menu 2"], ran);
        // Let go once it ran: a second press on the same menu is not the browser's.
        await Assert.ThrowsAsync<InvalidOperationException>(() => cut.InvokeAsync(() => Bridge().RunContextCommand(2, 0)));
    }

    [Fact] // ADR-0421 / ADR-0011: a menu opened under columns or an order .NET no longer holds is not handed to the Consumer
    public async Task ADR0421_a_menu_opened_under_columns_or_an_order_dotnet_left_is_answered_as_moved()
    {
        var asked = 0;
        var cut = Grid(_ => { asked += 1; return []; });
        var selection = Selection("""[{"topRow":0,"leftColumn":0,"rowCount":1,"columnCount":1}]""");

        var columns = await cut.InvokeAsync(() => Bridge().ContextCommands(1, 1, 0, IdOf(0), "Book", selection, 0));
        var order = await cut.InvokeAsync(() => Bridge().ContextCommands(2, 1, 1, IdOf(0), "Book", selection, 5));
        var source = await cut.InvokeAsync(() => Bridge().ContextCommands(3, 2, 1, IdOf(0), "Book", selection, 0));

        Assert.All(new[] { columns, order, source }, answer => Assert.Equal("""{"moved":true}""", answer));
        Assert.Equal(0, asked);
    }

    [Fact] // ADR-0421: a grid without ContextCommands does not ask .NET as its menu opens
    public void ADR0421_a_grid_without_commands_of_the_consumers_does_not_ask_dotnet()
    {
        Grid(null);

        Assert.False(Attached().GetProperty("options").TryGetProperty("contextCommands", out _));
    }
}
