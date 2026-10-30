using System.Text.Json;
using Bunit;
using ExGrid.Cells;
using ExGrid.Components.Tests.Support;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ADR-0430: Blazor's ExGrid over the Script Core takes the declaration a Pointing Scope made for it, names the Scope
/// in the page, and sends each row with what the Scope's function reads of it as the table's row. It refuses another
/// declaration later, by name.
/// </summary>
public class ScriptGridPointedAtTests : BunitContext
{
    private readonly BunitJSModuleInterop _module;

    public ScriptGridPointedAtTests()
    {
        _module = JSInterop.SetupModule(invocation => invocation.Identifier == "import");
        _module.Setup<string?>("stamp").SetResult(global::ExGrid.Components.ScriptCoreAsset.Stamp);
        var grid = _module.SetupModule("attach", _ => true);
        grid.SetupVoid("update", _ => true).SetVoidResult();
        grid.SetupVoid("state", _ => true).SetVoidResult();
    }

    private static GridPointedAt<TestRow> Declared(string declaration = "scope-1") => new(new GridPointedInBrowser<TestRow>(
        "scope", declaration, "Trades", new Dictionary<string, string> { ["Book"] = "Trade" }, row => $"[\"{row.Book}\"]"));

    private IRenderedComponent<global::ExGrid.Components.ExGrid<TestRow>> Grid(GridPointedAt<TestRow>? pointedAt) =>
        Render<global::ExGrid.Components.ExGrid<TestRow>>(ps => ps
            .Add(g => g.Rows, TestRows.Many(3))
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.PointedAt, pointedAt));

    private static JsonElement Attached(BunitJSModuleInterop module)
    {
        using var document = JsonDocument.Parse(Assert.IsType<string>(Assert.Single(module.Invocations["attach"]).Arguments[2]));
        return document.RootElement.Clone();
    }

    [Fact] // ADR-0430 sections 1 and 2: the Scope is named in the page, and each row carries the table's row
    public void ADR0430_a_Scopes_declaration_names_its_Scope_and_sends_each_row_as_the_tables_row()
    {
        Grid(Declared());

        var options = Attached(_module);
        var pointedAt = options.GetProperty("options").GetProperty("pointedAt");
        Assert.Equal("scope", pointedAt.GetProperty("scope").GetString());
        Assert.Equal("scope-1", pointedAt.GetProperty("declaration").GetString());
        Assert.Equal("Trades", pointedAt.GetProperty("table").GetString());
        Assert.Equal("Trade", pointedAt.GetProperty("tableColumns").GetProperty("Book").GetString());
        var first = options.GetProperty("state").GetProperty("window")[0];
        Assert.Equal(first.GetProperty("c")[0].GetString(), first.GetProperty("p")[0].GetString());
    }

    [Fact] // ADR-0430 section 2: a grid in no Scope sends no table rows
    public void ADR0430_a_grid_in_no_Scope_sends_no_table_rows()
    {
        Grid(pointedAt: null);

        var options = Attached(_module);
        Assert.False(options.GetProperty("options").TryGetProperty("pointedAt", out _));
        Assert.False(options.GetProperty("state").GetProperty("window")[0].TryGetProperty("p", out _));
    }

    [Fact] // ADR-0430 section 3: the declaration is set as the grid is made, and another later is refused
    public void ADR0430_another_declaration_later_is_refused()
    {
        var cut = Grid(Declared());

        var thrown = Assert.Throws<NotSupportedException>(() => cut.Render(ps => ps.Add(g => g.PointedAt, Declared("scope-2"))));

        Assert.Contains("set as the grid is made", thrown.Message, StringComparison.Ordinal);
    }
}
