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
/// Undo and Redo show the Sheet their step was taken on (ADR-0331 §3; exsheet-workbook ticket 08):
/// Ctrl+Z and Ctrl+Y, and <c>UndoAsync</c> and <c>RedoAsync</c>, show the step's Sheet with the range it
/// changed selected, as Excel does; a Sheet operation's undo shows the Sheet it brings back, or the one it
/// leaves.
/// </summary>
public class UndoShowsTheSheetTests : SheetTestContext
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    private static WorkbookDocument Sheets(params string[] names) => new Workbook(Culture, names).ToDocument();

    private IRenderedComponent<SheetComponent> RenderWorkbook(WorkbookDocument document) =>
        RenderSheet(ps => ps.Add(s => s.Workbook, document));

    private static IReadOnlyList<string> TabNames(IRenderedComponent<SheetComponent> cut) =>
        [.. cut.FindAll(".ex-sheet-tabs [role=tab]").Select(tab => tab.TextContent)];

    private static string SelectedTab(IRenderedComponent<SheetComponent> cut) =>
        cut.FindAll(".ex-sheet-tab").Single(tab => tab.GetAttribute("aria-selected") == "true").TextContent;

    private static Task ShowAsync(IRenderedComponent<SheetComponent> cut, string name) =>
        cut.FindAll("button.ex-sheet-tab").Single(tab => tab.TextContent == name).ClickAsync(new MouseEventArgs());

    private static GridSelection Selection(IRenderedComponent<SheetComponent> cut) => Grid(cut).Instance.ReadSelection().Selection;

    private static SelectionRange Range(string text)
    {
        var range = CellRange.Parse(text);
        return new SelectionRange(range.First.Row, range.First.Column, range.RowCount, range.ColumnCount);
    }

    private static void AssertSelected(IRenderedComponent<SheetComponent> cut, string range)
    {
        var selection = Selection(cut);
        Assert.Equal([Range(range)], selection.Ranges);
        Assert.Equal(new CellPosition(Range(range).TopRow, Range(range).LeftColumn), selection.Focus);
    }

    [Fact] // ADR-0331 §3: Ctrl+Z shows the Sheet the step was taken on and selects the cell it changed; Ctrl+Y does again, there
    public async Task ADR0331_ctrl_z_shows_the_sheet_of_the_step_and_selects_its_cell()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await EnterAsync(cut, "C3", "7");
        await ShowAsync(cut, "Two");
        await GoToAsync(cut, "E5");
        Assert.Equal("Two", SelectedTab(cut));

        await PressAsync(cut, "z", ctrl: true);

        Assert.Equal("One", SelectedTab(cut));
        Assert.Equal("", CellText(cut, "C3"));
        AssertSelected(cut, "C3");

        await ShowAsync(cut, "Two");
        await PressAsync(cut, "y", ctrl: true);

        Assert.Equal("One", SelectedTab(cut));
        Assert.Equal("7", CellText(cut, "C3"));
        AssertSelected(cut, "C3");

        // The Sheet left keeps its own Selection, as a press on its tab leaves it.
        await ShowAsync(cut, "Two");
        AssertSelected(cut, "E5");
    }

    [Fact] // ADR-0331 §3: on the Sheet shown, an undo selects the range the operation changed, wherever the Selection has gone since
    public async Task ADR0331_an_undo_on_the_sheet_shown_selects_the_range_it_changed()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "B2:C3");
        await PressAsync(cut, "b", ctrl: true);
        await GoToAsync(cut, "F9");

        Assert.True(await cut.Instance.UndoAsync());
        AssertSelected(cut, "B2:C3");

        await GoToAsync(cut, "A1");
        Assert.True(await cut.Instance.RedoAsync());
        AssertSelected(cut, "B2:C3");
    }

    [Fact] // ADR-0331 §3: the undo of a typed entry selects the cell typed in, not where Enter moved
    public async Task ADR0331_the_undo_of_an_entry_selects_the_cell_typed_in()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "B4", "12345678901234");
        AssertSelected(cut, "B5");

        await PressAsync(cut, "z", ctrl: true);

        // The entry and the width it widened its column to are one step; the cell is selected.
        AssertSelected(cut, "B4");
    }

    [Fact] // ADR-0331 §3, spine 6: two undos at once under a DocumentChanged handler that awaits end with the second's range selected, over the data both left
    public async Task ADR0331_two_undos_under_an_awaiting_handler_select_the_second_ones_range()
    {
        var delays = new Queue<TaskCompletionSource>();
        var cut = RenderSheet(ps => ps.Add(s => s.DocumentChanged, async (SheetDocument _) =>
        {
            if (delays.TryDequeue(out var wait)) await wait.Task;
        }));
        await EnterAsync(cut, "A1", "1");
        await EnterAsync(cut, "C5", "2");
        var first = new TaskCompletionSource();
        delays.Enqueue(first);

        // The first undo's handler is still awaiting as the second undo runs and completes.
        var firstUndo = cut.InvokeAsync(() => cut.Instance.UndoAsync());
        await cut.InvokeAsync(() => cut.Instance.UndoAsync());
        first.SetResult();
        await firstUndo;

        Assert.Equal("", CellText(cut, "C5"));
        Assert.Equal("", CellText(cut, "A1"));
        AssertSelected(cut, "A1");
    }

    [Fact] // ADR-0331 §3: an undo placed on the Sheet shown is not placed once the Consumer's handler opens another document
    public async Task ADR0331_an_undos_range_is_not_placed_among_another_documents_rows()
    {
        SheetDocument? replacement = null;
        IRenderedComponent<SheetComponent>? rendered = null;
        var cut = RenderSheet(ps => ps.Add(s => s.DocumentChanged, (SheetDocument _) =>
        {
            if (replacement is { } next) rendered!.Render(p => p.Add(s => s.Document, next));
        }));
        rendered = cut;
        await EnterAsync(cut, "C5", "2");
        await GoToAsync(cut, "A1");
        replacement = new Sheet(Culture).ToDocument();

        await cut.InvokeAsync(() => cut.Instance.UndoAsync());

        // The range was placed before the handler ran; the document it opened starts with its own Selection.
        Assert.DoesNotContain(Range("C5"), Selection(cut).Ranges);
    }

    [Fact] // ADR-0331 §3, ADR-0054: Delete's undo selects what Delete was pressed over, not only the cells it cleared
    public async Task ADR0331_deletes_undo_selects_what_it_was_pressed_over()
    {
        var cut = RenderSheet();
        await EnterAsync(cut, "C3", "5");
        await GoToAsync(cut, "B2:D4");
        await PressAsync(cut, "Delete");
        Assert.Equal("", CellText(cut, "C3"));
        await GoToAsync(cut, "A1");

        await PressAsync(cut, "z", ctrl: true);

        Assert.Equal("5", CellText(cut, "C3"));
        AssertSelected(cut, "B2:D4");
    }

    [Fact] // ADR-0331 §3: the undo and redo of an insertion select the whole rows inserted, on the Sheet the command named
    public async Task ADR0331_an_insertions_undo_selects_its_whole_rows()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await cut.InvokeAsync(() => cut.Instance.DoAsync("Two", SheetEdit.InsertRows(2, 3)));
        Assert.Equal("One", SelectedTab(cut));

        await cut.InvokeAsync(() => cut.Instance.UndoAsync());

        Assert.Equal("Two", SelectedTab(cut));
        Assert.Equal([new SelectionRange(2, 0, 3, Sheet.ColumnCount)], Selection(cut).Ranges);
        Assert.Equal(new CellPosition(2, 0), Selection(cut).Focus);
    }

    [Fact] // ADR-0331 §3: the whole columns of one resize, one step, are selected together
    public async Task ADR0331_a_resize_of_several_columns_is_selected_whole()
    {
        var cut = RenderSheet();
        await cut.InvokeAsync(() => cut.Instance.DoAsync(SheetEdit.SetColumnWidth(CellRange.WholeColumns(1, 3), 20)));

        Assert.True(await cut.Instance.UndoAsync());

        Assert.Equal([new SelectionRange(0, 1, Sheet.RowCount, 3)], Selection(cut).Ranges);
    }

    [Fact] // ADR-0331 §3: an operation over several ranges selects the one it acted from, the grid placing one range
    public async Task ADR0331_an_operation_over_several_ranges_selects_the_one_it_acted_from()
    {
        var cut = RenderSheet();
        await cut.InvokeAsync(() => cut.Instance.DoAsync(SheetEdit.EnterInto([CellRange.Parse("A1:A2"), CellRange.Parse("D5:E6")], CellAddress.Parse("D5"), "1")));

        Assert.True(await cut.Instance.UndoAsync());

        AssertSelected(cut, "D5:E6");
    }

    [Fact] // ADR-0331 §3: a deletion's undo shows the Sheet it brings back; its redo the one shown in its place
    public async Task ADR0331_a_deletions_undo_shows_the_sheet_it_brings_back()
    {
        var cut = RenderWorkbook(Sheets("One", "Two", "Three"));
        await ShowAsync(cut, "Two");
        await GoToAsync(cut, "D4");
        await cut.InvokeAsync(() => cut.Instance.DoAsync("Two", SheetEdit.DeleteSheet()));
        Assert.Equal("Three", SelectedTab(cut));
        await ShowAsync(cut, "One");

        await cut.InvokeAsync(() => cut.Instance.UndoAsync());

        Assert.Equal(["One", "Two", "Three"], TabNames(cut));
        Assert.Equal("Two", SelectedTab(cut));
        // A Sheet operation selects nothing of its own: the Sheet brought back shows as a press on its tab shows it.
        Assert.Empty(Selection(cut).Ranges);

        await cut.InvokeAsync(() => cut.Instance.RedoAsync());

        Assert.Equal(["One", "Three"], TabNames(cut));
        Assert.Equal("Three", SelectedTab(cut));
    }

    [Fact] // ADR-0331 §3: an insertion's undo shows the Sheet it was added beside; its redo shows the Sheet added again
    public async Task ADR0331_an_insertions_undo_shows_the_sheet_it_was_added_beside()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await cut.Find(".ex-sheet-tab-add").ClickAsync(new MouseEventArgs());
        Assert.Equal("Sheet1", SelectedTab(cut));
        await ShowAsync(cut, "Two");

        await PressAsync(cut, "z", ctrl: true);

        Assert.Equal(["One", "Two"], TabNames(cut));
        Assert.Equal("One", SelectedTab(cut));

        await PressAsync(cut, "y", ctrl: true);

        Assert.Equal(["One", "Sheet1", "Two"], TabNames(cut));
        Assert.Equal("Sheet1", SelectedTab(cut));
    }

    [Fact] // ADR-0331 §3: a copy's undo shows the Sheet it was copied from
    public async Task ADR0331_a_copys_undo_shows_the_sheet_it_was_copied_from()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await cut.InvokeAsync(() => cut.Instance.DoAsync("Two", SheetEdit.CopySheet()));

        await cut.InvokeAsync(() => cut.Instance.UndoAsync());

        Assert.Equal(["One", "Two"], TabNames(cut));
        Assert.Equal("Two", SelectedTab(cut));
    }

    [Fact] // ADR-0331 §3: a rename's and a move's undo and redo show the Sheet renamed or moved, keeping its own Selection
    public async Task ADR0331_a_renames_and_a_moves_undo_show_their_sheet()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await ShowAsync(cut, "Two");
        await GoToAsync(cut, "C2");
        await ShowAsync(cut, "One");
        await cut.InvokeAsync(() => cut.Instance.DoAsync("Two", SheetEdit.Rename("Second")));
        await cut.InvokeAsync(() => cut.Instance.DoAsync("Second", SheetEdit.MoveSheet(0)));
        await ShowAsync(cut, "One");

        await cut.InvokeAsync(() => cut.Instance.UndoAsync());

        Assert.Equal(["One", "Second"], TabNames(cut));
        Assert.Equal("Second", SelectedTab(cut));
        AssertSelected(cut, "C2");

        await ShowAsync(cut, "One");
        await cut.InvokeAsync(() => cut.Instance.UndoAsync());

        Assert.Equal(["One", "Two"], TabNames(cut));
        Assert.Equal("Two", SelectedTab(cut));
        AssertSelected(cut, "C2");

        await ShowAsync(cut, "One");
        await cut.InvokeAsync(() => cut.Instance.RedoAsync());
        Assert.Equal("Second", SelectedTab(cut));
    }

    [Fact] // ADR-0331 §3, §2: the Sheet an undo shows is the one the Workbook Document records as shown
    public async Task ADR0331_the_sheet_an_undo_shows_is_recorded_as_shown()
    {
        WorkbookDocument? raised = null;
        var cut = RenderSheet(ps => ps
            .Add(s => s.Workbook, Sheets("One", "Two"))
            .Add(s => s.WorkbookChanged, d => raised = d));
        await cut.InvokeAsync(() => cut.Instance.DoAsync("Two", SheetEdit.Enter(CellAddress.Parse("A1"), "5")));
        Assert.Equal("One", raised?.Shown);

        await cut.InvokeAsync(() => cut.Instance.UndoAsync());

        Assert.Equal("Two", raised?.Shown);
        Assert.Empty(raised!.Sheets.Single(s => s.Name == "Two").Cells);
    }
}
