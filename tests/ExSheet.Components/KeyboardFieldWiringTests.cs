using Bunit;
using ExSheet.Components.Tests.Support;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// The Keyboard Field on the Sheet (ADR-0080): the Sheet's grid edits, so the keyboard with no edit
/// open is held by its field, the one tab stop, which carries <c>aria-activedescendant</c>; and a
/// composition's text on a selected cell opens the Cell Editor holding it, as on any grid that
/// edits. The fifteenth Windows run's case i1: <c>kana</c> composed onto D10.
/// </summary>
public class KeyboardFieldWiringTests : SheetTestContext
{
    [Fact] // ADR-0080 / ADR-0033 / A11Y-4 / A11Y-5: the Sheet's grid has one field, the tab stop, naming the Focus cell; the root is -1 and names nothing
    public async Task ADR0080_the_sheets_keyboard_field_is_the_tab_stop_and_names_the_focus_cell()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "D10");

        var field = Assert.Single(cut.FindAll("input.ex-key-field"));
        var root = cut.Find(".ex-grid");
        Assert.Equal("0", field.GetAttribute("tabindex"));
        Assert.Equal("-1", root.GetAttribute("tabindex"));
        Assert.False(root.HasAttribute("aria-activedescendant"));
        Assert.Equal(Cell(cut, "D10").Id, field.GetAttribute("aria-activedescendant"));
        Assert.False(field.HasAttribute("readonly"));
    }

    [Fact] // ADR-0080 / ED-30 / the fifteenth Windows run, i1: a composition's text on D10 opens the Cell Editor holding it; Enter commits it and moves to D11
    public async Task ADR0080_a_compositions_text_on_a_selected_cell_opens_the_cell_editor_holding_it()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "D10");
        var grid = Grid(cut);

        var opened = await grid.InvokeAsync(() => grid.Instance.OnKeyFieldTextAsync("かな"));

        Assert.True(opened);
        Assert.Equal("かな", EditorText(cut));
        Assert.Equal("D10", cut.Find(".ex-name-box").GetAttribute("value"));
        await PressInEditorAsync(cut, "Enter", "かな", 2);
        Assert.Empty(cut.FindAll(".ex-viewport .ex-editor"));
        Assert.Equal("かな", CellText(cut, "D10"));
        Assert.Equal("D11", cut.Find(".ex-name-box").GetAttribute("value"));
    }

    [Fact] // ADR-0080 / ED-30 / the fifteenth Windows run, i2: a cancelled composition leaves an empty edit on D10, and Escape then cancels it, the cell as it was
    public async Task ADR0080_a_cancelled_composition_leaves_an_empty_edit_that_escape_cancels()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "D10", "7");
        await GoToAsync(cut, "D10");
        var grid = Grid(cut);

        var opened = await grid.InvokeAsync(() => grid.Instance.OnKeyFieldTextAsync(""));

        Assert.True(opened);
        Assert.Equal("", EditorText(cut));
        await PressInEditorAsync(cut, "Escape", "", 0);
        Assert.Empty(cut.FindAll(".ex-viewport .ex-editor"));
        Assert.Equal("7", CellText(cut, "D10"));
    }
}
