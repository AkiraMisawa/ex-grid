using System.Globalization;
using Bunit;
using ExGrid;
using ExGrid.Selection;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using SheetComponent = ExSheet.Components.ExSheet;

namespace ExSheet.Components.Tests;

/// <summary>
/// The Workbook in one ExSheet (ADR-0331 §1, §2, §4; exsheet-workbook ticket 05): a tab for each Sheet
/// under the grid, the Sheet shown switched by a press or Ctrl+PageUp and Ctrl+PageDown, each Sheet
/// keeping its own view, and the Workbook Document raised beside the Sheet Document while one Sheet
/// is held.
/// </summary>
public class SheetTabsTests : SheetTestContext
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    private static WorkbookDocument TwoSheets()
    {
        var workbook = new Workbook(Culture, ["One", "Two"]);
        workbook.Do("One", SheetEdit.Enter(CellAddress.Parse("A1"), "21"));
        workbook.Do("Two", SheetEdit.Enter(CellAddress.Parse("A1"), "=One!A1*2"));
        return workbook.ToDocument();
    }

    private IRenderedComponent<SheetComponent> RenderWorkbook(WorkbookDocument document, Action<WorkbookDocument>? changed = null) =>
        RenderSheet(ps =>
        {
            ps.Add(s => s.Workbook, document);
            if (changed is not null) ps.Add(s => s.WorkbookChanged, changed);
        });

    private static IReadOnlyList<string> TabNames(IRenderedComponent<SheetComponent> cut) =>
        [.. cut.FindAll(".ex-sheet-tab").Select(tab => tab.TextContent)];

    private static string SelectedTab(IRenderedComponent<SheetComponent> cut) =>
        cut.FindAll(".ex-sheet-tab").Single(tab => tab.GetAttribute("aria-selected") == "true").TextContent;

    private static int Paint(IRenderedComponent<SheetComponent> cut)
        => int.Parse(Grid(cut).Find(".ex-viewport").GetAttribute("data-ex-paint")!, CultureInfo.InvariantCulture);

    private static HeldSelection Held(IRenderedComponent<SheetComponent> cut) => Grid(cut).Instance.ReadSelection();

    [Fact] // ADR-0331 §1: a tab for each Sheet, in order, under the grid and outside its root, the shown Sheet's selected
    public void A_tab_stands_for_each_sheet_under_the_grid()
    {
        var cut = RenderWorkbook(TwoSheets());

        Assert.Equal(["One", "Two"], TabNames(cut));
        Assert.Equal("One", SelectedTab(cut));
        var tabs = cut.Find(".ex-sheet-tabs");
        Assert.Equal("tablist", tabs.GetAttribute("role"));
        Assert.Null(tabs.Closest(".ex-grid"));
        Assert.Equal("0", cut.FindAll(".ex-sheet-tab")[0].GetAttribute("tabindex"));
        Assert.Equal("-1", cut.FindAll(".ex-sheet-tab")[1].GetAttribute("tabindex"));
    }

    [Fact] // ADR-0331 §4: a press on a tab shows its Sheet in the one grid, its Values read across Sheets
    public async Task A_press_on_a_tab_shows_its_sheet()
    {
        var cut = RenderWorkbook(TwoSheets());

        await cut.FindAll(".ex-sheet-tab")[1].ClickAsync(new MouseEventArgs());

        Assert.Equal("Two", SelectedTab(cut));
        Assert.Equal("42", CellText(cut, "A1"));
    }

    [Fact] // ADR-0331 §4: Ctrl+PageDown and Ctrl+PageUp show the Sheet after and before, and stop at the last and the first
    public async Task Ctrl_page_keys_step_through_the_sheets()
    {
        var cut = RenderWorkbook(TwoSheets());

        await PressAsync(cut, "PageUp", ctrl: true);
        Assert.Equal("One", SelectedTab(cut));
        await PressAsync(cut, "PageDown", ctrl: true);
        Assert.Equal("Two", SelectedTab(cut));
        await PressAsync(cut, "PageDown", ctrl: true);
        Assert.Equal("Two", SelectedTab(cut));
        await PressAsync(cut, "PageUp", ctrl: true);
        Assert.Equal("One", SelectedTab(cut));
        Assert.Equal("21", CellText(cut, "A1"));
    }

    [Fact] // ADR-0331 §4, ADR-0011: each Sheet keeps its Selection, taken back with the version the Sheet is shown under; a Sheet not shown before starts with none
    public async Task Each_sheet_keeps_its_own_selection()
    {
        var cut = RenderWorkbook(TwoSheets());
        await GoToAsync(cut, "C5");
        var onOne = Held(cut);

        await PressAsync(cut, "PageDown", ctrl: true);
        var onTwo = Held(cut);
        Assert.NotEqual(onOne.RowSequenceVersion, onTwo.RowSequenceVersion);
        Assert.Empty(onTwo.Selection.Ranges);

        await GoToAsync(cut, "B2");
        await PressAsync(cut, "PageUp", ctrl: true);
        var back = Held(cut);
        Assert.Equal(4, back.Selection.Ranges.Single().TopRow);
        Assert.Equal(2, back.Selection.Ranges.Single().LeftColumn);
        Assert.NotEqual(onTwo.RowSequenceVersion, back.RowSequenceVersion);

        await PressAsync(cut, "PageDown", ctrl: true);
        Assert.Equal(1, Held(cut).Selection.Ranges.Single().TopRow);
    }

    [Fact] // ADR-0331 §4, ADR-0142: Delete pressed on the Sheet left and reaching the component after the switch clears nothing on the Sheet shown, and the notice says why
    public async Task A_write_aimed_at_the_sheet_left_is_refused()
    {
        var cut = RenderWorkbook(TwoSheets());
        await GoToAsync(cut, "A1");
        var pressedOn = Paint(cut);

        await PressAsync(cut, "PageDown", ctrl: true);
        var grid = Grid(cut);
        await grid.InvokeAsync(() => grid.Instance.OnKeyAsync("Delete", false, false, false, false, false, paint: pressedOn));

        Assert.Equal("Two", SelectedTab(cut));
        Assert.Equal("42", CellText(cut, "A1"));
        Assert.Equal(SheetWords.PasteRefused(global::ExGrid.Clipboard.PasteRefusalReason.OrderMoved), cut.Find(".ex-sheet-notice").TextContent);
    }

    [Fact] // ADR-0331 §4, ADR-0240: keys typed after Ctrl+PageDown, numbered after it, land on the Sheet shown though they told the paint before the switch, as they would typed slowly
    public async Task Keys_numbered_after_the_switch_land_on_the_sheet_shown()
    {
        var cut = RenderWorkbook(TwoSheets());
        await GoToAsync(cut, "A1");
        var before = Paint(cut);
        var grid = Grid(cut);

        // On a circuit every key leaves the browser before the switch's render reaches it.
        await grid.InvokeAsync(() => grid.Instance.OnKeyAsync("PageDown", true, false, false, false, false, paint: before, input: 1));
        foreach (var (key, input) in new[] { ("7", 2), ("Enter", 3) })
            await grid.InvokeAsync(() => grid.Instance.OnKeyAsync(key, false, false, false, false, false, paint: before, input: input));

        Assert.Equal("Two", SelectedTab(cut));
        Assert.Equal("7", CellText(cut, "A1"));
        Assert.Equal("", cut.Find(".ex-sheet-notice").TextContent);
    }

    [Fact] // ADR-0331 §4: another Sheet is not shown while an edit is open, and the notice says why
    public async Task No_sheet_is_shown_while_an_edit_is_open()
    {
        var cut = RenderWorkbook(TwoSheets());
        await GoToAsync(cut, "B1");
        await PressAsync(cut, "7");

        await cut.FindAll(".ex-sheet-tab")[1].ClickAsync(new MouseEventArgs());

        Assert.Equal("One", SelectedTab(cut));
        Assert.Contains("a cell is being edited", cut.Find(".ex-sheet-notice").TextContent);
    }

    [Fact] // ADR-0331 §2: the Sheet shown is recorded in the Workbook Document raised as it changes
    public async Task Showing_another_sheet_raises_the_workbook_document()
    {
        WorkbookDocument? raised = null;
        var cut = RenderWorkbook(TwoSheets(), d => raised = d);

        await PressAsync(cut, "PageDown", ctrl: true);

        Assert.NotNull(raised);
        Assert.Equal("Two", raised.Shown);
        Assert.Equal(2, raised.Sheets.Count);
    }

    [Fact] // ADR-0331 §2: DocumentChanged is raised while one Sheet is held, and stops once there are several
    public async Task The_sheet_document_is_raised_only_while_one_sheet_is_held()
    {
        var documents = 0;
        var workbooks = 0;
        var cut = RenderSheet(ps => ps
            .Add(s => s.DocumentChanged, (SheetDocument _) => documents++)
            .Add(s => s.WorkbookChanged, (WorkbookDocument _) => workbooks++));

        await EnterAsync(cut, "A1", "5");
        Assert.Equal((1, 1), (documents, workbooks));

        await cut.InvokeAsync(() => cut.Instance.DoAsync(SheetEdit.InsertSheet()));
        Assert.Equal((1, 2), (documents, workbooks));
        Assert.Equal(["Sheet1", "Sheet2"], TabNames(cut));
        Assert.Throws<InvalidOperationException>(() => cut.Instance.ToDocument());
        Assert.Equal(2, cut.Instance.ToWorkbookDocument().Sheets.Count);
    }

    [Fact] // ADR-0331 §3, ADR-0048: deleting the Sheet shown shows the one that takes its place, and the undo shows it again
    public async Task Deleting_the_sheet_shown_shows_the_next()
    {
        var cut = RenderWorkbook(TwoSheets());

        await cut.InvokeAsync(() => cut.Instance.DoAsync(SheetEdit.DeleteSheet()));
        Assert.Equal(["Two"], TabNames(cut));
        Assert.Equal("#REF!", CellText(cut, "A1"));

        await cut.InvokeAsync(() => cut.Instance.UndoAsync());
        Assert.Equal(["One", "Two"], TabNames(cut));
        Assert.Equal("One", SelectedTab(cut));
        Assert.Equal("21", CellText(cut, "A1"));
    }

    [Fact] // ADR-0331 §2: Document and Workbook are not set together
    public void Document_and_workbook_are_not_set_together()
    {
        Assert.Throws<InvalidOperationException>(() => RenderSheet(ps => ps
            .Add(s => s.Document, new Sheet(Culture).ToDocument())
            .Add(s => s.Workbook, TwoSheets())));
    }

    [Fact] // ADR-0331 §1, ADR-0028: ShowSheetTabs false hides the strip, and the grid takes the height it left
    public void Sheet_tabs_can_be_hidden()
    {
        var shown = RenderWorkbook(TwoSheets());
        var withTabs = Grid(shown).Instance.ViewportHeight.Px;
        var cut = RenderSheet(ps => ps.Add(s => s.Workbook, TwoSheets()).Add(s => s.ShowSheetTabs, false));

        Assert.Empty(cut.FindAll(".ex-sheet-tabs"));
        Assert.Equal((ViewportSize)400, Grid(cut).Instance.ViewportHeight);
        Assert.True(withTabs < 400);
        Assert.Contains("display: contents", cut.Find(".ex-sheet").GetAttribute("style"));
    }
}
