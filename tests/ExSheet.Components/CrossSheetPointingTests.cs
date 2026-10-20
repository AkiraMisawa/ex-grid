using System.Globalization;
using Bunit;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Xunit;
using SheetComponent = ExSheet.Components.ExSheet;

namespace ExSheet.Components.Tests;

/// <summary>
/// Pointing reaches across Sheets (ADR-0331 §5; exsheet-workbook ticket 09): while a Formula is typed,
/// another Sheet is shown with the edit kept open, pointing there writes a Reference qualified with that
/// Sheet's name, and the commit lands on the Sheet the Formula was typed on, which is shown again.
/// </summary>
public class CrossSheetPointingTests : SheetTestContext
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    private static WorkbookDocument TwoSheets()
    {
        var workbook = new Workbook(Culture, ["One", "Two"]);
        workbook.Do("One", SheetEdit.Enter(CellAddress.Parse("A1"), "21"));
        workbook.Do("Two", SheetEdit.Enter(CellAddress.Parse("A1"), "=One!A1*2"));
        workbook.Do("Two", SheetEdit.Enter(CellAddress.Parse("A2"), "5"));
        return workbook.ToDocument();
    }

    private IRenderedComponent<SheetComponent> RenderWorkbook(Action<WorkbookDocument>? changed = null) =>
        RenderSheet(ps =>
        {
            ps.Add(s => s.Workbook, TwoSheets());
            if (changed is not null) ps.Add(s => s.WorkbookChanged, changed);
        });

    private static string SelectedTab(IRenderedComponent<SheetComponent> cut) =>
        cut.FindAll(".ex-sheet-tab").Single(tab => tab.GetAttribute("aria-selected") == "true").TextContent;

    private static string FormulaBar(IRenderedComponent<SheetComponent> cut) => cut.Find(".ex-formula-bar-text").GetAttribute("value") ?? "";

    private static string NameBox(IRenderedComponent<SheetComponent> cut) => cut.Find(".ex-name-box").GetAttribute("value") ?? "";

    private static async Task StartTypingAsync(IRenderedComponent<SheetComponent> cut, string address, string typed)
    {
        await GoToAsync(cut, address);
        await PressAsync(cut, typed[..1]);
        if (typed.Length > 1) await TypeAsync(cut, typed);
    }

    [Fact] // ADR-0331 §5: Ctrl+PageDown while a Formula is typed shows the next Sheet and keeps the edit, away, its text in the Formula Bar
    public async Task Another_sheet_is_shown_while_a_formula_is_typed()
    {
        var cut = RenderWorkbook();
        await StartTypingAsync(cut, "B1", "=");

        await PressInEditorAsync(cut, "PageDown", "=", 1, ctrl: true);

        Assert.Equal("Two", SelectedTab(cut));
        Assert.True(cut.Instance.IsEditing);
        Assert.Equal("=", FormulaBar(cut));
        Assert.Contains("ex-editor-away", cut.Find(".ex-viewport input.ex-editor").ClassName);
        Assert.Equal("42", CellText(cut, "A1"));
    }

    [Fact] // ADR-0331 §5: pointing on another Sheet writes its name; Enter commits on the Sheet typed on, shows it again and moves down there
    public async Task Pointing_on_another_sheet_writes_its_name_and_enter_commits_at_home()
    {
        WorkbookDocument? raised = null;
        var cut = RenderWorkbook(d => raised = d);
        await StartTypingAsync(cut, "B1", "=");
        await PressInEditorAsync(cut, "PageDown", "=", 1, ctrl: true);

        await PressInEditorAsync(cut, "ArrowDown", "=", 1);
        Assert.Equal("=Two!A2", FormulaBar(cut));

        await PressInEditorAsync(cut, "Enter", "=Two!A2", 7);

        Assert.Equal("One", SelectedTab(cut));
        Assert.False(cut.Instance.IsEditing);
        Assert.Equal("5", CellText(cut, "B1"));
        Assert.Equal("B2", NameBox(cut));
        Assert.Equal("One", raised?.Shown);
        var two = Workbook.Open(raised!);
        Assert.Equal("=Two!A2", two.Sheets[0].GetEntryText(CellAddress.Parse("B1")));
        Assert.Equal("5", two.Sheets[1].GetEntryText(CellAddress.Parse("A2")));
    }

    [Fact] // ADR-0331 §5: a quoted name is written quoted
    public async Task Pointing_on_a_sheet_with_a_space_quotes_its_name()
    {
        var workbook = new Workbook(Culture, ["One", "Q1 Sales"]);
        var cut = RenderSheet(ps => ps.Add(s => s.Workbook, workbook.ToDocument()));
        await StartTypingAsync(cut, "A1", "=");
        await PressInEditorAsync(cut, "PageDown", "=", 1, ctrl: true);

        await PressInEditorAsync(cut, "ArrowRight", "=", 1);

        Assert.Equal("='Q1 Sales'!B1", FormulaBar(cut));
    }

    [Fact] // ADR-0331 §5: what was pointed on the Sheet left is replaced by the next point on the Sheet shown, from its own Focus
    public async Task A_point_on_the_next_sheet_replaces_what_was_pointed()
    {
        var cut = RenderWorkbook();
        await StartTypingAsync(cut, "B1", "=");
        await PressInEditorAsync(cut, "ArrowDown", "=", 1);
        Assert.Equal("=B2", EditorText(cut));

        await PressInEditorAsync(cut, "PageDown", "=B2", 3, ctrl: true);
        Assert.Equal("=B2", FormulaBar(cut));
        await PressInEditorAsync(cut, "ArrowDown", "=B2", 3);
        Assert.Equal("=Two!A2", FormulaBar(cut));

        await PressInEditorAsync(cut, "PageUp", "=Two!A2", 7, ctrl: true);
        Assert.Equal("One", SelectedTab(cut));
        await PressInEditorAsync(cut, "ArrowDown", "=Two!A2", 7);
        Assert.Equal("=B2", EditorText(cut));
    }

    [Fact] // ADR-0331 §5: Escape cancels the edit and shows the Sheet it was typed on, with nothing written
    public async Task Escape_shows_the_sheet_typed_on_and_writes_nothing()
    {
        var cut = RenderWorkbook();
        await StartTypingAsync(cut, "B1", "=");
        await PressInEditorAsync(cut, "PageDown", "=", 1, ctrl: true);
        await PressInEditorAsync(cut, "ArrowDown", "=", 1);

        await PressInEditorAsync(cut, "Escape", "=Two!A2", 7);

        Assert.Equal("One", SelectedTab(cut));
        Assert.False(cut.Instance.IsEditing);
        Assert.Equal("", CellText(cut, "B1"));
        Assert.Equal("B1", NameBox(cut));
    }

    [Fact] // ADR-0331 §5, ticket 12: while a constant is typed another Sheet is not shown, and the notice says why
    public async Task Another_sheet_is_not_shown_while_a_constant_is_typed()
    {
        var cut = RenderWorkbook();
        await StartTypingAsync(cut, "B1", "7");

        await PressInEditorAsync(cut, "PageDown", "7", 1, ctrl: true);

        Assert.Equal("One", SelectedTab(cut));
        Assert.True(cut.Instance.IsEditing);
        Assert.Contains("a cell is being edited", cut.Find(".ex-sheet-notice").TextContent);
    }

    [Fact] // ADR-0331 §5, ADR-0057: on another Sheet only the References naming it outline its cells; one naming none is the home Sheet's
    public void References_on_another_sheet_are_the_ones_naming_it()
    {
        var workbook = new Workbook(Culture, ["One", "Two"]);
        var (one, two) = (workbook.Sheets[0], workbook.Sheets[1]);

        var references = SheetFormulaAids.References(two, one, "=A1+Two!B2+one!C3");

        var only = Assert.Single(references);
        Assert.Equal((4, 6), (only.Start, only.Length));
        Assert.Equal(2, SheetFormulaAids.References(one, one, "=A1+Two!B2+one!C3").Count);
        Assert.Equal(2, SheetFormulaAids.References(one, null, "=A1+Two!B2+one!C3").Count);
    }
}
