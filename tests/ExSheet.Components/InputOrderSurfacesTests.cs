using System.Globalization;
using Bunit;
using ExGrid.Selection;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using SheetComponent = ExSheet.Components.ExSheet;

namespace ExSheet.Components.Tests;

/// <summary>
/// ExSheet's own surfaces in the Input Order (ADR-0240 sections 6 and 7, input-order ticket 04): the
/// Sheet's outermost element is the Input Root, and the Sheet Toolbar's band and built-in buttons, the
/// Sheet Tabs and Format Cells' OK and Cancel put their input in the grid's order through its seam
/// (<c>InOrderAsync</c>), each claiming its events by <c>data-ex-claims</c>. The script numbers such an
/// event as it happens and tells the core (<c>InputTakenAt</c>); here the telling is made by hand, and
/// the input before it is held by a Consumer that has not yet answered the change it made (the Document
/// or Workbook raised and awaited). What the script hears and holds is layer 3's.
/// </summary>
public class InputOrderSurfacesTests : SheetTestContext
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    private static Task TellAsync(IRenderedComponent<SheetComponent> cut, string kind, int input)
    {
        var grid = Grid(cut);
        return grid.InvokeAsync(() => grid.Instance.InputTakenAt(kind, input));
    }

    private static Task KeyAsync(IRenderedComponent<SheetComponent> cut, string key, int input)
    {
        var grid = Grid(cut);
        return grid.InvokeAsync(() => grid.Instance.OnKeyAsync(key, false, false, false, false, false, input: input));
    }

    private static CellPosition Focus(IRenderedComponent<SheetComponent> cut) => Grid(cut).Instance.ReadSelection().Selection.Focus;

    private static bool Bold(IRenderedComponent<SheetComponent> cut, string address) =>
        cut.Instance.CellFormatAt(CellAddress.Parse(address)).Font.Bold;

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<SheetComponent> cut, string name) =>
        cut.FindAll(".ex-sheet-toolbar button.ex-sheet-toolbar-item").Single(b => b.GetAttribute("aria-label") == name);

    // A Consumer that holds the Sheet Document raised by the next change until `answer` completes: the
    // change, and the input that made it, are still being handled meanwhile.
    private IRenderedComponent<SheetComponent> RenderHeldSheet(TaskCompletionSource answer, bool toolbar = true) =>
        RenderSheet(ps =>
        {
            ps.Add(s => s.ShowToolbar, toolbar);
            ps.Add(s => s.DocumentChanged, async (SheetDocument _) => await answer.Task);
        });

    private static WorkbookDocument TwoSheets()
    {
        var workbook = new Workbook(Culture, ["One", "Two"]);
        workbook.Do("One", SheetEdit.Enter(CellAddress.Parse("A1"), "21"));
        workbook.Do("Two", SheetEdit.Enter(CellAddress.Parse("A1"), "22"));
        return workbook.ToDocument();
    }

    private static string SelectedTab(IRenderedComponent<SheetComponent> cut) =>
        cut.FindAll(".ex-sheet-tab").Single(tab => tab.GetAttribute("aria-selected") == "true").TextContent;

    [Fact] // ADR-0240 §6: the Sheet's box is its Input Root, and its own surfaces say which events their handlers put in the order
    public void ADR0240_the_sheet_is_the_input_root_and_its_surfaces_say_what_they_claim()
    {
        var cut = RenderSheet(ps =>
        {
            ps.Add(s => s.ShowToolbar, true);
            ps.Add(s => s.Workbook, TwoSheets());
        });

        Assert.True(cut.Find(".ex-sheet").HasAttribute("data-ex-input-root"));
        // In no Pointing Scope it claims nothing itself: the keyboard crossing its edge is told only to a Sheet that listens.
        Assert.False(cut.Find(".ex-sheet").HasAttribute("data-ex-claims"));
        Assert.Equal("keydown", cut.Find(".ex-sheet-toolbar").GetAttribute("data-ex-claims"));
        Assert.All(cut.FindAll(".ex-sheet-toolbar button"), button => Assert.Equal("click", button.GetAttribute("data-ex-claims")));
        Assert.All(cut.FindAll(".ex-sheet-tab"), tab => Assert.Equal("click dblclick contextmenu keydown", tab.GetAttribute("data-ex-claims")));
        Assert.Equal("click", cut.Find(".ex-sheet-tab-add").GetAttribute("data-ex-claims"));
    }

    [Fact] // ADR-0240 §6: a toolbar command clicked, then ↓ at once — the ↓ is taken after the command, which sets bold where the Focus was
    public async Task ADR0240_a_toolbar_click_then_a_key_are_taken_in_the_order_they_were_made()
    {
        var answer = new TaskCompletionSource();
        var cut = RenderHeldSheet(answer);
        await GoToAsync(cut, "B2");

        await TellAsync(cut, "click", 1);
        Task click = Task.CompletedTask;
        await cut.InvokeAsync(() => { click = Button(cut, "Bold").ClickAsync(new MouseEventArgs { Detail = 1 }); });
        Task down = Task.CompletedTask;
        await cut.InvokeAsync(() => { down = KeyAsync(cut, "ArrowDown", 2); });

        // The command is still being answered: the ↓ behind it has not moved the Focus.
        Assert.True(Bold(cut, "B2"));
        Assert.Equal(new CellPosition(1, 1), Focus(cut));

        answer.SetResult();
        await click;
        await down;
        Assert.Equal(new CellPosition(2, 1), Focus(cut));
        Assert.False(Bold(cut, "B3"));
    }

    [Fact] // ADR-0240 §6: a key on the toolbar's band waits for the input numbered before it
    public async Task ADR0240_a_key_on_the_band_waits_for_the_input_before_it()
    {
        var answer = new TaskCompletionSource();
        var cut = RenderHeldSheet(answer);
        await GoToAsync(cut, "B2");
        var band = cut.Find(".ex-sheet-toolbar");
        await band.FocusAsync(new FocusEventArgs());
        var active = cut.Find(".ex-sheet-toolbar").GetAttribute("aria-activedescendant");

        await TellAsync(cut, "click", 1);
        Task click = Task.CompletedTask;
        await cut.InvokeAsync(() => { click = Button(cut, "Bold").ClickAsync(new MouseEventArgs { Detail = 1 }); });
        await TellAsync(cut, "keydown", 2);
        Task key = Task.CompletedTask;
        await cut.InvokeAsync(() => { key = cut.Find(".ex-sheet-toolbar").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" }); });

        Assert.Equal(active, cut.Find(".ex-sheet-toolbar").GetAttribute("aria-activedescendant"));

        answer.SetResult();
        await click;
        await key;
        Assert.NotEqual(active, cut.Find(".ex-sheet-toolbar").GetAttribute("aria-activedescendant"));
    }

    [Fact] // ADR-0240 §6, ADR-0331 §4: a press on a tab, then a key on the tab list at once — the key acts on the Sheet the press showed
    public async Task ADR0240_a_press_on_a_tab_then_a_key_are_taken_in_order()
    {
        var answer = new TaskCompletionSource();
        var raised = 0;
        var cut = RenderSheet(ps =>
        {
            ps.Add(s => s.Workbook, TwoSheets());
            ps.Add(s => s.WorkbookChanged, async (WorkbookDocument _) =>
            {
                if (raised++ == 0) await answer.Task;
            });
        });

        await TellAsync(cut, "click", 1);
        Task press = Task.CompletedTask;
        await cut.InvokeAsync(() => { press = cut.FindAll(".ex-sheet-tab")[1].ClickAsync(new MouseEventArgs { Detail = 1 }); });
        await TellAsync(cut, "keydown", 2);
        Task key = Task.CompletedTask;
        await cut.InvokeAsync(() => { key = cut.FindAll(".ex-sheet-tab")[1].KeyDownAsync(new KeyboardEventArgs { Key = "ArrowLeft" }); });

        // The press is still being answered: the ← behind it has not stepped back yet.
        Assert.Equal("Two", SelectedTab(cut));
        Assert.Equal(1, raised);

        answer.SetResult();
        await press;
        await key;
        Assert.Equal("One", SelectedTab(cut));
        Assert.Equal(2, raised);
    }

    [Fact] // ADR-0240 §6, ADR-0331 §4: → on the tab list hands the keyboard to the tab it selects, through the grid, without scrolling
    public async Task ADR0240_an_arrow_on_the_tab_list_hands_the_keyboard_to_the_tab_without_scrolling()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Workbook, TwoSheets()));
        var handed = HandedToCount;

        await cut.FindAll(".ex-sheet-tab")[0].KeyDownAsync(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal("Two", SelectedTab(cut));
        Assert.Equal(handed + 1, HandedToCount);
        var (id, preventScroll) = LastHandedTo;
        Assert.False(string.IsNullOrEmpty(id));
        Assert.True(preventScroll, "the tab takes the keyboard where it stands, as its own focus asked before the Input Order");
    }

    [Fact] // ADR-0240 §1, §6: Format Cells' OK is in the order — ↓ typed straight after Enter in it moves the Focus only once the format is set
    public async Task ADR0240_format_cells_ok_then_a_key_are_taken_in_order()
    {
        var answer = new TaskCompletionSource();
        var cut = RenderHeldSheet(answer, toolbar: false);
        await GoToAsync(cut, "B2");
        Assert.True(await cut.Instance.OpenFormatCellsAsync());
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("form.ex-format-cells")));
        Assert.Equal("submit", cut.Find("form.ex-format-cells").GetAttribute("data-ex-claims"));
        Assert.Equal("click", cut.Find(".ex-format-cells-cancel").GetAttribute("data-ex-claims"));
        await cut.FindAll(".ex-format-cells-tab").Single(t => t.TextContent == "Font").ClickAsync(new MouseEventArgs());
        var bold = cut.FindAll(".ex-format-cells-font-styles label").Single(l => l.TextContent.Trim() == "Bold").QuerySelector("input")!;
        await bold.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "on" });

        await TellAsync(cut, "submit", 1);
        Task ok = Task.CompletedTask;
        await cut.InvokeAsync(() => { ok = cut.Find("form.ex-format-cells").SubmitAsync(); });
        Task down = Task.CompletedTask;
        await cut.InvokeAsync(() => { down = KeyAsync(cut, "ArrowDown", 2); });

        Assert.True(Bold(cut, "B2"));
        Assert.Equal(new CellPosition(1, 1), Focus(cut));

        answer.SetResult();
        await ok;
        await down;
        Assert.Equal(new CellPosition(2, 1), Focus(cut));
        Assert.False(Bold(cut, "B3"));
    }
}
