using System.Globalization;
using Bunit;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using SheetComponent = ExSheet.Components.ExSheet;

namespace ExSheet.Components.Tests;

/// <summary>
/// Commands and addresses name their Sheet (ADR-0331 §6; exsheet-workbook ticket 07): one naming none
/// is refused by name while the Workbook holds several Sheets, one naming a Sheet acts on it whether it
/// is shown or not, and the application adds, renames, moves, copies and deletes Sheets on the one undo
/// stack, hearing each change.
/// </summary>
public class WorkbookCommandsTests : SheetTestContext
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    private static WorkbookDocument TwoSheets()
    {
        var workbook = new Workbook(Culture, ["One", "Two"]);
        workbook.Do("One", SheetEdit.Enter(CellAddress.Parse("A1"), "21"));
        workbook.Do("Two", SheetEdit.Enter(CellAddress.Parse("A1"), "=One!A1*2"));
        return workbook.ToDocument();
    }

    private IRenderedComponent<SheetComponent> RenderWorkbook(WorkbookDocument document, Action<SheetsChange>? sheetsChanged = null) =>
        RenderSheet(ps =>
        {
            ps.Add(s => s.Workbook, document);
            if (sheetsChanged is not null) ps.Add(s => s.SheetsChanged, sheetsChanged);
        });

    private static IReadOnlyList<string> TabNames(IRenderedComponent<SheetComponent> cut) =>
        [.. cut.FindAll(".ex-sheet-tab").Select(tab => tab.TextContent)];

    private static string SelectedTab(IRenderedComponent<SheetComponent> cut) =>
        cut.FindAll(".ex-sheet-tab").Single(tab => tab.GetAttribute("aria-selected") == "true").TextContent;

    [Fact] // ADR-0331 §6: a command naming no Sheet is refused by name while the Workbook holds several, and changes nothing
    public async Task A_command_naming_no_sheet_is_refused_while_there_are_several()
    {
        var cut = RenderWorkbook(TwoSheets());

        var refused = await Assert.ThrowsAsync<SheetRefusedException>(() => cut.Instance.DoAsync(SheetEdit.Enter(CellAddress.Parse("A1"), "5")));

        Assert.Equal(SheetRefusalReason.SheetNotNamed, refused.Refusal.Reason);
        Assert.Equal("21", CellText(cut, "A1"));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0331 §6: a command naming no Sheet is taken while the Workbook holds one, as it always was
    public async Task A_command_naming_no_sheet_is_taken_while_there_is_one()
    {
        var cut = RenderSheet();

        await cut.InvokeAsync(() => cut.Instance.DoAsync(SheetEdit.Enter(CellAddress.Parse("A1"), "5")));

        Assert.Equal("5", CellText(cut, "A1"));
    }

    [Fact] // ADR-0331 §6, ADR-0048: a command naming a Sheet not shown acts on it, the Sheet shown shows what it reads of it, and one undo takes it back
    public async Task A_command_naming_a_sheet_not_shown_acts_on_it()
    {
        var cut = RenderWorkbook(TwoSheets());

        await cut.InvokeAsync(() => cut.Instance.DoAsync("One", SheetEdit.Enter(CellAddress.Parse("A2"), "3")));
        await cut.InvokeAsync(() => cut.Instance.DoAsync("Two", SheetEdit.Enter(CellAddress.Parse("A2"), "=A1+One!A2")));

        Assert.Equal("One", SelectedTab(cut));
        Assert.Equal("3", CellText(cut, "A2"));

        await cut.InvokeAsync(() => cut.Instance.DoAsync("one", SheetEdit.Enter(CellAddress.Parse("A2"), "4")));
        Assert.Equal("4", CellText(cut, "A2"));
        await cut.InvokeAsync(() => cut.Instance.UndoAsync());
        Assert.Equal("3", CellText(cut, "A2"));

        await cut.FindAll(".ex-sheet-tab")[1].ClickAsync(new MouseEventArgs());
        Assert.Equal("45", CellText(cut, "A2"));
    }

    [Fact] // ADR-0331 §6: a command naming a Sheet the Workbook does not have is refused, and changes nothing
    public async Task A_command_naming_no_such_sheet_is_refused()
    {
        var cut = RenderWorkbook(TwoSheets());

        await Assert.ThrowsAsync<ArgumentException>(() => cut.Instance.DoAsync("Three", SheetEdit.Enter(CellAddress.Parse("A1"), "5")));

        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0331 §6: an address naming no Sheet is refused by name while the Workbook holds several; one naming a Sheet reads it, shown or not
    public async Task An_address_names_its_sheet()
    {
        var cut = RenderWorkbook(TwoSheets());
        await cut.InvokeAsync(() => cut.Instance.DoAsync("Two", SheetEdit.SetNumberFormat(CellRange.Parse("B2"), NumberFormat.Parse("0.00"))));

        var refused = Assert.Throws<SheetRefusedException>(() => cut.Instance.CellFormatAt(CellAddress.Parse("B2")));
        Assert.Equal(SheetRefusalReason.SheetNotNamed, refused.Refusal.Reason);
        Assert.Equal("0.00", cut.Instance.CellFormatAt("Two", CellAddress.Parse("B2")).NumberFormat.Code);
        Assert.Equal(NumberFormat.General, cut.Instance.CellFormatAt("One", CellAddress.Parse("B2")).NumberFormat);
    }

    [Fact] // ADR-0331 §6, ADR-0048: the application adds, renames, moves, copies and deletes Sheets, each heard with the names before and after, each one step on the one undo stack
    public async Task The_application_manages_sheets_and_hears_each_change()
    {
        var heard = new List<SheetsChange>();
        var cut = RenderWorkbook(TwoSheets(), heard.Add);

        await cut.InvokeAsync(() => cut.Instance.DoAsync("Two", SheetEdit.InsertSheet("Three")));
        await cut.InvokeAsync(() => cut.Instance.DoAsync("Two", SheetEdit.Rename("Second")));
        await cut.InvokeAsync(() => cut.Instance.DoAsync("Three", SheetEdit.MoveSheet(0)));
        await cut.InvokeAsync(() => cut.Instance.DoAsync("One", SheetEdit.CopySheet()));
        await cut.InvokeAsync(() => cut.Instance.DoAsync("Second", SheetEdit.DeleteSheet()));

        Assert.Equal(
            [
                ("One,Two", "One,Two,Three"),
                ("One,Two,Three", "One,Second,Three"),
                ("One,Second,Three", "Three,One,Second"),
                ("Three,One,Second", "Three,One,One (2),Second"),
                ("Three,One,One (2),Second", "Three,One,One (2)"),
            ],
            heard.Select(h => (string.Join(",", h.Before), string.Join(",", h.After))));
        Assert.All(heard, h => Assert.Equal("One", h.Shown));
        Assert.Equal(["Three", "One", "One (2)"], TabNames(cut));
        Assert.Equal("One", SelectedTab(cut));

        for (var i = 0; i < 5; i++) await cut.InvokeAsync(() => cut.Instance.UndoAsync());
        Assert.Equal(["One", "Two"], TabNames(cut));
        Assert.Equal(10, heard.Count);
        Assert.Equal("One,Two", string.Join(",", heard[^1].After));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0331 §6: the Sheet shown deleted by a command shows the one that takes its place, and the change says so
    public async Task Deleting_the_sheet_shown_by_name_shows_the_next()
    {
        var heard = new List<SheetsChange>();
        var cut = RenderWorkbook(TwoSheets(), heard.Add);

        await cut.InvokeAsync(() => cut.Instance.DoAsync("One", SheetEdit.DeleteSheet()));

        Assert.Equal("Two", SelectedTab(cut));
        Assert.Equal("#REF!", CellText(cut, "A1"));
        Assert.Equal("Two", heard.Single().Shown);
    }

    [Fact] // ADR-0331 §6: showing another Sheet changes no Sheet, and is not heard as a change to them
    public async Task Showing_another_sheet_is_not_a_change_to_the_sheets()
    {
        var heard = new List<SheetsChange>();
        var cut = RenderWorkbook(TwoSheets(), heard.Add);

        await PressAsync(cut, "PageDown", ctrl: true);

        Assert.Empty(heard);
    }

    [Fact] // ADR-0331 §6: a command that acts on the Selection acts on the Sheet shown, where the Selection is
    public async Task A_selection_command_acts_on_the_sheet_shown()
    {
        var cut = RenderWorkbook(TwoSheets());
        await PressAsync(cut, "PageDown", ctrl: true);
        await GoToAsync(cut, "B2");

        await cut.InvokeAsync(() => cut.Instance.SetNumberFormatAsync(NumberFormat.Parse("0.00")));

        Assert.Equal("0.00", cut.Instance.CellFormatAt("Two", CellAddress.Parse("B2")).NumberFormat.Code);
        Assert.Equal(NumberFormat.General, cut.Instance.CellFormatAt("One", CellAddress.Parse("B2")).NumberFormat);
    }
}
