using System.Globalization;
using AngleSharp.Dom;
using Bunit;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using SheetComponent = ExSheet.Components.ExSheet;

namespace ExSheet.Components.Tests;

/// <summary>
/// What the Sheet Tabs do besides showing a Sheet (ADR-0331 §4; exsheet-workbook ticket 06): <c>+</c> and
/// Shift+F11 add, a double click renames on the tab, the tab's menu deletes, moves left and right, copies
/// and renames, and a drag moves a tab. Each is one step on the undo stack (ADR-0331 §3), said in the
/// Sheet's notice, refused while an edit is open, and reachable from the keyboard alone. Their place in
/// the Input Order is told by hand here, as <see cref="InputOrderSurfacesTests"/> tells it; what the script
/// hears and holds is layer 3's.
/// </summary>
public class SheetTabOperationsTests : SheetTestContext
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    private static WorkbookDocument Sheets(params string[] names)
    {
        var workbook = new Workbook(Culture, names);
        workbook.Do(names[0], SheetEdit.Enter(CellAddress.Parse("A1"), "21"));
        if (names.Length > 1) workbook.Do(names[1], SheetEdit.Enter(CellAddress.Parse("A1"), $"={names[0]}!A1*2"));
        return workbook.ToDocument();
    }

    private IRenderedComponent<SheetComponent> RenderWorkbook(WorkbookDocument document, Action<SheetsChange>? sheetsChanged = null) =>
        RenderSheet(ps =>
        {
            ps.Add(s => s.Workbook, document);
            if (sheetsChanged is not null) ps.Add(s => s.SheetsChanged, sheetsChanged);
        });

    private static IReadOnlyList<string> TabNames(IRenderedComponent<SheetComponent> cut) =>
        [.. cut.FindAll(".ex-sheet-tabs [role=tab]").Select(tab => tab.TextContent)];

    // The selected tab's name: its text, or, while it is being renamed, what its field holds.
    private static string SelectedTab(IRenderedComponent<SheetComponent> cut)
    {
        var tab = cut.FindAll(".ex-sheet-tab").Single(tab => tab.GetAttribute("aria-selected") == "true");
        return tab.QuerySelector("input")?.GetAttribute("value") ?? tab.TextContent;
    }

    private static IElement Tab(IRenderedComponent<SheetComponent> cut, string name) =>
        cut.FindAll("button.ex-sheet-tab").Single(tab => tab.TextContent == name);

    private static string Notice(IRenderedComponent<SheetComponent> cut) => cut.Find(".ex-sheet-notice").TextContent;

    private static IElement NameField(IRenderedComponent<SheetComponent> cut) => cut.Find(".ex-sheet-tab-name");

    private static IReadOnlyList<IElement> Commands(IRenderedComponent<SheetComponent> cut) => cut.FindAll(".ex-sheet-tab-menu [role=menuitem]");

    private static IElement Command(IRenderedComponent<SheetComponent> cut, string label) => Commands(cut).Single(c => c.TextContent == label);

    private static string ActiveCommand(IRenderedComponent<SheetComponent> cut)
    {
        var id = cut.Find(".ex-sheet-tab-menu").GetAttribute("aria-activedescendant");
        return Commands(cut).Single(c => c.Id == id).TextContent;
    }

    private static Task MenuKeyAsync(IRenderedComponent<SheetComponent> cut, string key, bool shift = false) =>
        cut.Find(".ex-sheet-tab-menu").KeyDownAsync(new KeyboardEventArgs { Key = key, ShiftKey = shift });

    private static Task OpenMenuAsync(IRenderedComponent<SheetComponent> cut, string tab) =>
        Tab(cut, tab).TriggerEventAsync("oncontextmenu", new MouseEventArgs { Button = 2 });

    private static async Task RenameAsync(IRenderedComponent<SheetComponent> cut, string tab, string name, string key = "Enter")
    {
        await Tab(cut, tab).DoubleClickAsync(new MouseEventArgs());
        await NameField(cut).InputAsync(new ChangeEventArgs { Value = name });
        await NameField(cut).KeyDownAsync(new KeyboardEventArgs { Key = key });
    }

    private static string Formula(IRenderedComponent<SheetComponent> cut, string sheet, string address) =>
        cut.Instance.ToWorkbookDocument().Sheets.Single(s => s.Name == sheet).Cells.Single(c => c.Address == CellAddress.Parse(address)).Entry!.ToString();

    // ---- Adding --------------------------------------------------------------------------------

    [Fact] // ADR-0331 §4: + adds a Sheet after the one shown, named SheetN, shows it, says so, and is one step
    public async Task Plus_adds_a_sheet_after_the_one_shown()
    {
        SheetsChange? heard = null;
        var cut = RenderWorkbook(Sheets("One", "Two"), change => heard = change);

        await cut.Find(".ex-sheet-tab-add").ClickAsync(new MouseEventArgs());

        Assert.Equal(["One", "Sheet1", "Two"], TabNames(cut));
        Assert.Equal("Sheet1", SelectedTab(cut));
        Assert.Equal("Sheet 'Sheet1' was added.", Notice(cut));
        Assert.Equal(["One", "Sheet1", "Two"], heard?.After);

        await cut.InvokeAsync(() => cut.Instance.UndoAsync());
        Assert.Equal(["One", "Two"], TabNames(cut));
        // The undo shows the Sheet the one taken out was added beside (ADR-0331 §3, ticket 08).
        Assert.Equal("One", SelectedTab(cut));
    }

    [Fact] // ADR-0331 §4: Shift+F11, a key the Sheet declares to its grid, adds a Sheet before the one shown, as Excel's does
    public async Task Shift_F11_adds_a_sheet_before_the_one_shown()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await Tab(cut, "Two").ClickAsync(new MouseEventArgs());

        await PressAsync(cut, "F11", shift: true);

        Assert.Equal(["One", "Sheet1", "Two"], TabNames(cut));
        Assert.Equal("Sheet1", SelectedTab(cut));
        Assert.Contains("Shift+F11", Grid(cut).Instance.DeclaredKeys!);
    }

    [Fact] // ADR-0331 §4: Shift+F11 on the tab list adds a Sheet too: the keyboard alone reaches it there as well
    public async Task Shift_F11_on_the_tab_list_adds_a_sheet()
    {
        var cut = RenderWorkbook(Sheets("One"));

        await Tab(cut, "One").KeyDownAsync(new KeyboardEventArgs { Key = "F11", ShiftKey = true });

        Assert.Equal(["Sheet1", "One"], TabNames(cut));
    }

    [Fact] // ADR-0331 §4: no Sheet is added while a cell is being edited, and the notice says why
    public async Task Adding_is_refused_while_editing()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await GoToAsync(cut, "B1");
        await PressAsync(cut, "7");

        await cut.Find(".ex-sheet-tab-add").ClickAsync(new MouseEventArgs());
        await PressAsync(cut, "F11", shift: true);

        Assert.Equal(["One", "Two"], TabNames(cut));
        Assert.Contains("No Sheet was changed: a cell is being edited", Notice(cut));
    }

    // ---- Renaming ------------------------------------------------------------------------------

    [Fact] // ADR-0331 §4: a double click edits the name on its tab, all of it, the keyboard handed to the field through the grid
    public async Task A_double_click_edits_the_name_on_the_tab()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        var handed = HandedToCount;

        await Tab(cut, "Two").DoubleClickAsync(new MouseEventArgs());

        var field = NameField(cut);
        Assert.Equal("Two", field.GetAttribute("value"));
        Assert.Equal("keydown", field.GetAttribute("data-ex-claims"));
        Assert.Equal("Two", SelectedTab(cut));
        // Straight to the field: the keyboard stays in the grid, which stays drawn, until the field has it.
        Assert.Equal(handed + 1, HandedToCount);
        // The field's box, so the grid selects the whole name as it hands the keyboard over, where it stands.
        Assert.False(string.IsNullOrEmpty(LastHandedTo.Id));
        Assert.True(LastHandedTo.PreventScroll);
    }

    [Fact] // ADR-0331 §4: Enter commits the name, rewrites the Formulas that name the Sheet, gives the keyboard back to the grid, and one undo puts it all back
    public async Task Enter_commits_the_name_and_one_undo_puts_it_back()
    {
        SheetsChange? heard = null;
        var cut = RenderWorkbook(Sheets("One", "Two"), change => heard = change);
        var returns = KeyboardReturns;

        await RenameAsync(cut, "One", "Claims");

        Assert.Equal(["Claims", "Two"], TabNames(cut));
        Assert.Empty(cut.FindAll(".ex-sheet-tab-name"));
        Assert.Equal("=Claims!A1*2", Formula(cut, "Two", "A1"));
        Assert.Equal("Sheet 'One' was renamed 'Claims'.", Notice(cut));
        Assert.Equal(["Claims", "Two"], heard?.After);
        Assert.Equal(returns + 1, KeyboardReturns);

        await cut.InvokeAsync(() => cut.Instance.UndoAsync());
        Assert.Equal(["One", "Two"], TabNames(cut));
        Assert.Equal("=One!A1*2", Formula(cut, "Two", "A1"));
    }

    [Fact] // ADR-0331 §4: Escape keeps the old name, records nothing, and gives the keyboard back to the grid
    public async Task Escape_keeps_the_old_name()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        var returns = KeyboardReturns;

        await RenameAsync(cut, "One", "Claims", key: "Escape");

        Assert.Equal(["One", "Two"], TabNames(cut));
        Assert.Empty(cut.FindAll(".ex-sheet-tab-name"));
        Assert.Equal(returns + 1, KeyboardReturns);
        Assert.False(await cut.InvokeAsync(() => cut.Instance.UndoAsync()));
    }

    [Theory] // ADR-0331 §4: a name Excel's rules refuse is refused by name, and the field stays, holding the keyboard
    [InlineData("", "cannot be empty")]
    [InlineData("Q1/Q2", "cannot contain any of")]
    [InlineData("A name far longer than Excel takes", "at most 31 characters")]
    [InlineData("'Quoted'", "apostrophe")]
    [InlineData("history", "reserves")]
    [InlineData("TWO", "already named 'Two'")]
    public async Task A_name_excel_refuses_is_refused_by_name(string name, string said)
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        var returns = KeyboardReturns;

        await RenameAsync(cut, "One", name);

        Assert.Equal(["Two"], [.. cut.FindAll("button.ex-sheet-tab").Select(t => t.TextContent)]);
        Assert.Equal("true", NameField(cut).GetAttribute("aria-invalid"));
        Assert.Contains("The Sheet was not renamed:", Notice(cut));
        Assert.Contains(said, Notice(cut));
        Assert.Equal(returns, KeyboardReturns);
        Assert.False(await cut.InvokeAsync(() => cut.Instance.UndoAsync()));

        // The change event Chrome raises after the refused Enter's keydown carries the same text: the mark stays.
        await NameField(cut).ChangeAsync(new ChangeEventArgs { Value = name });
        Assert.Equal("true", NameField(cut).GetAttribute("aria-invalid"));

        // Typing again clears the mark, and a name that is allowed is taken.
        await NameField(cut).InputAsync(new ChangeEventArgs { Value = "Claims" });
        Assert.Null(NameField(cut).GetAttribute("aria-invalid"));
        await NameField(cut).KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        Assert.Equal(["Claims", "Two"], TabNames(cut));
    }

    [Fact] // ADR-0331 §4, ADR-0210: text the key gate typed into the field arrives as a change, and Enter commits it
    public async Task Text_typed_by_the_key_gate_arrives_as_a_change()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await Tab(cut, "One").DoubleClickAsync(new MouseEventArgs());

        await NameField(cut).ChangeAsync(new ChangeEventArgs { Value = "Held" });
        await NameField(cut).KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(["Held", "Two"], TabNames(cut));
    }

    [Fact] // ADR-0331 §4: the keyboard leaving the field commits the name, as Excel commits it on a press elsewhere; a focus leaving a field already ended does nothing
    public async Task Leaving_the_field_commits_the_name()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await Tab(cut, "One").DoubleClickAsync(new MouseEventArgs());
        await NameField(cut).InputAsync(new ChangeEventArgs { Value = "Claims" });

        await NameField(cut).TriggerEventAsync("onfocusout", new FocusEventArgs());

        Assert.Equal(["Claims", "Two"], TabNames(cut));
        Assert.True(await cut.InvokeAsync(() => cut.Instance.UndoAsync()));
        Assert.False(await cut.InvokeAsync(() => cut.Instance.UndoAsync()));
    }

    [Fact] // ADR-0331 §4: leaving the field with a name refused keeps the old name, and says why
    public async Task Leaving_the_field_with_a_refused_name_keeps_the_old_one()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await Tab(cut, "One").DoubleClickAsync(new MouseEventArgs());
        await NameField(cut).InputAsync(new ChangeEventArgs { Value = "two" });

        await NameField(cut).TriggerEventAsync("onfocusout", new FocusEventArgs());

        Assert.Equal(["One", "Two"], TabNames(cut));
        Assert.Empty(cut.FindAll(".ex-sheet-tab-name"));
        Assert.Contains("already named 'Two'", Notice(cut));
    }

    [Fact] // ADR-0331 §4: the keys typed in the field do not reach the tab list: ← moves the caret, not to another Sheet
    public async Task Keys_in_the_field_stay_in_the_field()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await Tab(cut, "Two").DoubleClickAsync(new MouseEventArgs());

        await NameField(cut).KeyDownAsync(new KeyboardEventArgs { Key = "ArrowLeft" });

        Assert.Equal("Two", SelectedTab(cut));
        Assert.NotEmpty(cut.FindAll(".ex-sheet-tab-name"));
    }

    [Fact] // ADR-0331 §4: no name is edited while a cell is being edited
    public async Task Renaming_is_refused_while_editing()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await GoToAsync(cut, "B1");
        await PressAsync(cut, "7");

        await Tab(cut, "One").DoubleClickAsync(new MouseEventArgs());

        Assert.Empty(cut.FindAll(".ex-sheet-tab-name"));
        Assert.Contains("No Sheet was changed: a cell is being edited", Notice(cut));
    }

    // ---- The tab's menu ------------------------------------------------------------------------

    [Fact] // ADR-0331 §4: a secondary press on a tab shows its Sheet and opens its menu, the keyboard on its first command, handed through the grid
    public async Task The_context_menu_opens_the_tab_menu()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        var handed = HandedToCount;

        await OpenMenuAsync(cut, "Two");

        Assert.Equal("Two", SelectedTab(cut));
        var menu = cut.Find(".ex-sheet-tab-menu");
        Assert.Equal("menu", menu.GetAttribute("role"));
        Assert.Equal("keydown click", menu.GetAttribute("data-ex-claims"));
        Assert.Equal(["Rename", "Delete", "Move Left", "Move Right", "Copy"], [.. Commands(cut).Select(c => c.TextContent)]);
        Assert.Equal("Rename", ActiveCommand(cut));
        Assert.Equal("true", Command(cut, "Move Right").GetAttribute("aria-disabled"));
        Assert.Equal("The Sheet is already the last.", Command(cut, "Move Right").GetAttribute("title"));
        Assert.Null(Command(cut, "Move Left").GetAttribute("aria-disabled"));
        Assert.Equal(handed + 1, HandedToCount);
        Assert.Equal("menu", Tab(cut, "Two").GetAttribute("aria-haspopup"));
    }

    [Fact] // ADR-0331 §3, §4: the only Sheet's Delete cannot be done, and says why
    public async Task The_only_sheet_cannot_be_deleted()
    {
        var cut = RenderWorkbook(Sheets("One"));

        await OpenMenuAsync(cut, "One");
        await Command(cut, "Delete").ClickAsync(new MouseEventArgs());

        Assert.Equal(["One"], TabNames(cut));
        Assert.Equal("A Workbook holds at least one Sheet.", Command(cut, "Delete").GetAttribute("title"));
        Assert.NotEmpty(cut.FindAll(".ex-sheet-tab-menu"));
    }

    [Theory] // ADR-0331 §4: Shift+F10 and the menu key on a tab open its menu; Escape then gives the keyboard back to the tab
    [InlineData("F10", true)]
    [InlineData("ContextMenu", false)]
    public async Task The_keyboard_opens_the_tab_menu_and_escape_returns_to_the_tab(string key, bool shift)
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));

        await Tab(cut, "One").KeyDownAsync(new KeyboardEventArgs { Key = key, ShiftKey = shift });
        Assert.NotEmpty(cut.FindAll(".ex-sheet-tab-menu"));
        var handed = HandedToCount;

        await MenuKeyAsync(cut, "Escape");

        Assert.Empty(cut.FindAll(".ex-sheet-tab-menu"));
        Assert.Equal(handed + 1, HandedToCount);
        Assert.True(LastHandedTo.PreventScroll);
    }

    [Fact] // ADR-0331 §4: the browser's context menu event that follows the menu key finds the menu open, and leaves it as it is
    public async Task A_second_opening_leaves_the_menu_as_it_is()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await Tab(cut, "One").KeyDownAsync(new KeyboardEventArgs { Key = "ContextMenu" });
        await MenuKeyAsync(cut, "ArrowDown");
        var handed = HandedToCount;

        await OpenMenuAsync(cut, "One");

        Assert.Equal("Delete", ActiveCommand(cut));
        Assert.Equal(handed, HandedToCount);
    }

    [Fact] // ADR-0331 §4: ↓ and ↑ move among the commands that can be done, round the ends; Home and End go to the first and the last
    public async Task The_arrows_move_among_the_commands_that_can_be_done()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await OpenMenuAsync(cut, "One");

        await MenuKeyAsync(cut, "ArrowDown");
        Assert.Equal("Delete", ActiveCommand(cut));
        // Move Left cannot be done on the first Sheet: ↓ passes it.
        await MenuKeyAsync(cut, "ArrowDown");
        Assert.Equal("Move Right", ActiveCommand(cut));
        await MenuKeyAsync(cut, "ArrowDown");
        await MenuKeyAsync(cut, "ArrowDown");
        Assert.Equal("Rename", ActiveCommand(cut));
        await MenuKeyAsync(cut, "ArrowUp");
        Assert.Equal("Copy", ActiveCommand(cut));
        await MenuKeyAsync(cut, "Home");
        Assert.Equal("Rename", ActiveCommand(cut));
        await MenuKeyAsync(cut, "End");
        Assert.Equal("Copy", ActiveCommand(cut));
    }

    [Fact] // ADR-0331 §3, §4: Delete from the keyboard deletes the Sheet, shows the next, says so, and the undo gives it and its Formulas back
    public async Task Delete_from_the_keyboard_deletes_and_one_undo_gives_it_back()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        var returns = KeyboardReturns;
        await OpenMenuAsync(cut, "One");

        await MenuKeyAsync(cut, "ArrowDown");
        await MenuKeyAsync(cut, "Enter");

        Assert.Equal(["Two"], TabNames(cut));
        Assert.Equal("Two", SelectedTab(cut));
        Assert.Equal("#REF!", CellText(cut, "A1"));
        Assert.Equal("Sheet 'One' was deleted. Ctrl+Z gives it back.", Notice(cut));
        Assert.Empty(cut.FindAll(".ex-sheet-tab-menu"));
        Assert.Equal(returns + 1, KeyboardReturns);

        await cut.InvokeAsync(() => cut.Instance.UndoAsync());
        Assert.Equal(["One", "Two"], TabNames(cut));
        Assert.Equal("=One!A1*2", Formula(cut, "Two", "A1"));
    }

    [Fact] // ADR-0331 §4: Move Right and Move Left move the Sheet one place, say where, and each is one step
    public async Task Move_right_and_left_move_one_place()
    {
        var cut = RenderWorkbook(Sheets("One", "Two", "Three"));

        await OpenMenuAsync(cut, "One");
        await Command(cut, "Move Right").ClickAsync(new MouseEventArgs());
        Assert.Equal(["Two", "One", "Three"], TabNames(cut));
        Assert.Equal("Sheet 'One' was moved to position 2 of 3.", Notice(cut));
        Assert.Equal("One", SelectedTab(cut));

        await OpenMenuAsync(cut, "Three");
        await Command(cut, "Move Left").ClickAsync(new MouseEventArgs());
        Assert.Equal(["Two", "Three", "One"], TabNames(cut));

        await cut.InvokeAsync(() => cut.Instance.UndoAsync());
        Assert.Equal(["Two", "One", "Three"], TabNames(cut));
        await cut.InvokeAsync(() => cut.Instance.UndoAsync());
        Assert.Equal(["One", "Two", "Three"], TabNames(cut));
    }

    [Fact] // ADR-0331 §4: Copy places Name (2) after the Sheet with its Entries, shows it, says so, and is one step
    public async Task Copy_places_the_copy_after_the_sheet_and_shows_it()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));

        await OpenMenuAsync(cut, "One");
        await Command(cut, "Copy").ClickAsync(new MouseEventArgs());

        Assert.Equal(["One", "One (2)", "Two"], TabNames(cut));
        Assert.Equal("One (2)", SelectedTab(cut));
        Assert.Equal("21", CellText(cut, "A1"));
        Assert.Equal("Sheet 'One' was copied as 'One (2)'.", Notice(cut));

        await cut.InvokeAsync(() => cut.Instance.UndoAsync());
        Assert.Equal(["One", "Two"], TabNames(cut));
    }

    [Fact] // ADR-0331 §4: Rename on the menu edits the name on the tab, the keyboard handed from the menu to the field
    public async Task Rename_on_the_menu_edits_the_name_on_the_tab()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await OpenMenuAsync(cut, "Two");
        var handed = HandedToCount;

        await MenuKeyAsync(cut, "Enter");

        Assert.Empty(cut.FindAll(".ex-sheet-tab-menu"));
        Assert.Equal("Two", NameField(cut).GetAttribute("value"));
        // Through the tab list, which holds it while the menu and the tab are drawn away, then to the field.
        Assert.Equal(handed + 2, HandedToCount);
        await NameField(cut).InputAsync(new ChangeEventArgs { Value = "Summary" });
        await NameField(cut).KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        Assert.Equal(["One", "Summary"], TabNames(cut));
    }

    [Fact] // ADR-0331 §4: the keyboard leaving the menu for elsewhere closes it, and a focus leaving a menu already closed changes nothing
    public async Task The_menu_closes_when_the_keyboard_leaves_it()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await OpenMenuAsync(cut, "One");

        await cut.Find(".ex-sheet-tab-menu").TriggerEventAsync("onfocusout", new FocusEventArgs());

        Assert.Empty(cut.FindAll(".ex-sheet-tab-menu"));
        Assert.Equal(["One", "Two"], TabNames(cut));
    }

    [Fact] // ADR-0331 §4: the menu does not open while a cell is being edited
    public async Task The_menu_is_refused_while_editing()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await GoToAsync(cut, "B1");
        await PressAsync(cut, "7");

        await OpenMenuAsync(cut, "One");

        Assert.Empty(cut.FindAll(".ex-sheet-tab-menu"));
        Assert.Contains("No Sheet was changed", Notice(cut));
    }

    // ---- Dragging ------------------------------------------------------------------------------

    [Fact] // ADR-0331 §4: a press on a tab and its release on another moves the tab to that place, shows it, and is one step
    public async Task A_drag_moves_the_tab_to_the_tab_it_is_released_on()
    {
        var cut = RenderWorkbook(Sheets("One", "Two", "Three"));

        await Tab(cut, "One").MouseDownAsync(new MouseEventArgs { Button = 0 });
        Assert.Equal("true", cut.Find(".ex-sheet-tab-area").GetAttribute("data-ex-dragging"));
        await Tab(cut, "Three").MouseUpAsync(new MouseEventArgs { Button = 0 });

        Assert.Equal(["Two", "Three", "One"], TabNames(cut));
        Assert.Equal("One", SelectedTab(cut));
        Assert.Null(cut.Find(".ex-sheet-tab-area").GetAttribute("data-ex-dragging"));
        Assert.Equal("Sheet 'One' was moved to position 3 of 3.", Notice(cut));

        await Tab(cut, "One").MouseDownAsync(new MouseEventArgs { Button = 0 });
        await Tab(cut, "Two").MouseUpAsync(new MouseEventArgs { Button = 0 });
        Assert.Equal(["One", "Two", "Three"], TabNames(cut));

        await cut.InvokeAsync(() => cut.Instance.UndoAsync());
        Assert.Equal(["Two", "Three", "One"], TabNames(cut));
    }

    [Fact] // ADR-0331 §4: released past the last tab — on the strip's end or the + button — the tab moves to the last place
    public async Task A_drag_released_past_the_last_tab_moves_it_last()
    {
        var cut = RenderWorkbook(Sheets("One", "Two", "Three"));

        await Tab(cut, "One").MouseDownAsync(new MouseEventArgs { Button = 0 });
        await cut.Find(".ex-sheet-tab-add").MouseUpAsync(new MouseEventArgs { Button = 0 });

        Assert.Equal(["Two", "Three", "One"], TabNames(cut));

        await Tab(cut, "Two").MouseDownAsync(new MouseEventArgs { Button = 0 });
        await cut.Find(".ex-sheet-tab-end").MouseUpAsync(new MouseEventArgs { Button = 0 });

        Assert.Equal(["Three", "One", "Two"], TabNames(cut));
    }

    [Theory] // ADR-0331 §4: released anywhere else in the tabs' area — between tabs, on the list's scrollbar, on the menu or the name's field — a drag ends with nothing moved
    [InlineData(".ex-sheet-tabs")]
    [InlineData(".ex-sheet-tab-area")]
    [InlineData(".ex-sheet-tab-menu")]
    [InlineData(".ex-sheet-tab-name")]
    public async Task A_drag_released_elsewhere_in_the_area_moves_nothing(string where)
    {
        var cut = RenderWorkbook(Sheets("One", "Two", "Three"));
        if (where == ".ex-sheet-tab-menu") await OpenMenuAsync(cut, "Three");
        if (where == ".ex-sheet-tab-name") await Tab(cut, "Three").DoubleClickAsync(new MouseEventArgs());

        await Tab(cut, "One").MouseDownAsync(new MouseEventArgs { Button = 0 });
        await cut.Find(where).MouseUpAsync(new MouseEventArgs { Button = 0 });

        Assert.Equal(["One", "Two", "Three"], [.. cut.Instance.ToWorkbookDocument().Sheets.Select(s => s.Name)]);
        Assert.Null(cut.Find(".ex-sheet-tab-area").GetAttribute("data-ex-dragging"));
        // Ended: a release on a tab after it moves nothing either.
        await Tab(cut, "Two").MouseUpAsync(new MouseEventArgs { Button = 0 });
        Assert.Equal(["One", "Two", "Three"], [.. cut.Instance.ToWorkbookDocument().Sheets.Select(s => s.Name)]);
    }

    [Fact] // ADR-0331 §4: the pointer leaving the tabs' area is heard before any press, so a pointer that leaves before the press's render still ends the drag
    public void Leaving_the_area_is_heard_before_any_drag()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));

        Assert.True(cut.Find(".ex-sheet-tab-area").HasAttribute("blazor:onmouseleave"));
    }

    [Fact] // ADR-0331 §4: a press and release on one tab is a click, which moves nothing; leaving the tabs ends a drag; a secondary press starts none
    public async Task Only_a_drag_onto_another_place_moves_a_tab()
    {
        var cut = RenderWorkbook(Sheets("One", "Two", "Three"));

        await Tab(cut, "One").MouseDownAsync(new MouseEventArgs { Button = 0 });
        await Tab(cut, "One").MouseUpAsync(new MouseEventArgs { Button = 0 });
        Assert.Equal(["One", "Two", "Three"], TabNames(cut));

        await Tab(cut, "One").MouseDownAsync(new MouseEventArgs { Button = 0 });
        await cut.Find(".ex-sheet-tab-area").TriggerEventAsync("onmouseleave", new MouseEventArgs());
        await Tab(cut, "Three").MouseUpAsync(new MouseEventArgs { Button = 0 });
        Assert.Equal(["One", "Two", "Three"], TabNames(cut));

        await Tab(cut, "One").MouseDownAsync(new MouseEventArgs { Button = 2 });
        await Tab(cut, "Three").MouseUpAsync(new MouseEventArgs { Button = 2 });
        Assert.Equal(["One", "Two", "Three"], TabNames(cut));
        Assert.False(await cut.InvokeAsync(() => cut.Instance.UndoAsync()));
    }

    // ---- In the Input Order --------------------------------------------------------------------

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

    [Fact] // ADR-0240 §6, ADR-0331 §4: a press on +, then a key at once — the key is taken once the Sheet added is shown, and types there
    public async Task ADR0240_a_press_on_plus_then_a_key_are_taken_in_order()
    {
        var answer = new TaskCompletionSource();
        var raised = 0;
        var cut = RenderSheet(ps =>
        {
            ps.Add(s => s.Workbook, Sheets("One", "Two"));
            ps.Add(s => s.WorkbookChanged, async (WorkbookDocument _) =>
            {
                if (raised++ == 0) await answer.Task;
            });
        });

        await TellAsync(cut, "click", 1);
        Task press = Task.CompletedTask;
        await cut.InvokeAsync(() => { press = cut.Find(".ex-sheet-tab-add").ClickAsync(new MouseEventArgs { Detail = 1 }); });
        Task key = Task.CompletedTask;
        await cut.InvokeAsync(() => { key = KeyAsync(cut, "7", 2); });

        Assert.False(cut.Instance.IsEditing);

        answer.SetResult();
        await press;
        await key;
        Assert.Equal("Sheet1", SelectedTab(cut));
        Assert.True(cut.Instance.IsEditing);
    }

    [Fact] // ADR-0240 §6, ADR-0331 §4: Enter in the name's field, then a key at once — the key is taken once the name is committed and the keyboard is the grid's
    public async Task ADR0240_enter_in_the_name_then_a_key_are_taken_in_order()
    {
        var answer = new TaskCompletionSource();
        var raised = 0;
        var cut = RenderSheet(ps =>
        {
            ps.Add(s => s.Workbook, Sheets("One", "Two"));
            ps.Add(s => s.SheetsChanged, async (SheetsChange _) =>
            {
                if (raised++ == 0) await answer.Task;
            });
        });
        await Tab(cut, "One").DoubleClickAsync(new MouseEventArgs());
        await NameField(cut).InputAsync(new ChangeEventArgs { Value = "Claims" });

        await TellAsync(cut, "keydown", 1);
        Task enter = Task.CompletedTask;
        await cut.InvokeAsync(() => { enter = NameField(cut).KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }); });
        Task key = Task.CompletedTask;
        await cut.InvokeAsync(() => { key = KeyAsync(cut, "7", 2); });

        Assert.False(cut.Instance.IsEditing);

        answer.SetResult();
        await enter;
        await key;
        Assert.Equal(["Claims", "Two"], TabNames(cut));
        Assert.True(cut.Instance.IsEditing);
    }

    [Fact] // ADR-0240 §6, ADR-0331 §4: a command on the tab's menu, then a key at once — the key is taken once the Sheet is deleted, on the Sheet shown in its place
    public async Task ADR0240_a_menu_command_then_a_key_are_taken_in_order()
    {
        var answer = new TaskCompletionSource();
        var raised = 0;
        var cut = RenderSheet(ps =>
        {
            ps.Add(s => s.Workbook, Sheets("One", "Two"));
            ps.Add(s => s.SheetsChanged, async (SheetsChange _) =>
            {
                if (raised++ == 0) await answer.Task;
            });
        });
        await OpenMenuAsync(cut, "One");

        await TellAsync(cut, "click", 1);
        Task click = Task.CompletedTask;
        await cut.InvokeAsync(() => { click = Command(cut, "Delete").ClickAsync(new MouseEventArgs { Detail = 1 }); });
        Task key = Task.CompletedTask;
        await cut.InvokeAsync(() => { key = KeyAsync(cut, "7", 2); });

        Assert.False(cut.Instance.IsEditing);

        answer.SetResult();
        await click;
        await key;
        // The key opened its edit on Two, the Sheet shown once the deletion was answered: typed on One
        // before it, the edit would have refused the deletion.
        Assert.Equal(["Two"], TabNames(cut));
        Assert.Equal("Two", SelectedTab(cut));
        Assert.True(cut.Instance.IsEditing);
    }

    // ---- The menu's context ------------------------------------------------------------------------

    [Fact] // ADR-0331 §4: a menu's context claims no equality of its own: each one built is its own, as its commands' Invoke are
    public async Task Each_menu_built_is_a_context_of_its_own()
    {
        var contexts = new List<SheetTabMenuContext>();
        var cut = RenderSheet(ps =>
        {
            ps.Add(s => s.Workbook, Sheets("One", "Two"));
            ps.Add(s => s.Chrome, new MenuRecordingChrome(contexts));
        });
        await OpenMenuAsync(cut, "One");
        await MenuKeyAsync(cut, "ArrowDown");
        await MenuKeyAsync(cut, "ArrowUp");

        var first = contexts.First(c => c.Commands.Single(k => k.Active).Kind == SheetTabCommandKind.Rename);
        var again = contexts.Last();
        Assert.Equal(SheetTabCommandKind.Rename, again.Commands.Single(k => k.Active).Kind);
        Assert.NotSame(first, again);
        Assert.NotEqual(first, again);
        Assert.Equal(first with { }, first);
    }

    private sealed class MenuRecordingChrome(List<SheetTabMenuContext> contexts) : ISheetChrome
    {
        public RenderFragment? FilterPanel(global::ExGrid.Chrome.FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(global::ExGrid.Chrome.ColumnMenuContext context) => null;

        public RenderFragment? CellEditor(global::ExGrid.Chrome.CellEditorContext context) => null;

        public RenderFragment? LoadingIndicator(global::ExGrid.Chrome.LoadingContext context) => null;

        public RenderFragment? SheetTabMenu(SheetTabMenuContext context)
        {
            contexts.Add(context);
            return null;
        }
    }

    // ---- A Sheet that leaves while a gesture stands on it -----------------------------------------

    [Fact] // ADR-0331 §4: a Consumer's command that deletes the Sheet whose name is being edited ends the edit, the keyboard back in the grid
    public async Task A_rename_ends_when_the_consumer_deletes_its_sheet()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await Tab(cut, "One").DoubleClickAsync(new MouseEventArgs());
        var returns = KeyboardReturns;

        await cut.Instance.DoAsync("One", SheetEdit.DeleteSheet());

        Assert.Equal(["Two"], TabNames(cut));
        Assert.Empty(cut.FindAll(".ex-sheet-tab-name"));
        Assert.Equal(returns + 1, KeyboardReturns);
    }

    [Fact] // ADR-0331 §4: an undo that takes away the Sheet whose name is being edited ends the edit; nothing is renamed after
    public async Task A_rename_ends_when_an_undo_takes_its_sheet_away()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await cut.Find(".ex-sheet-tab-add").ClickAsync(new MouseEventArgs());
        await Tab(cut, "Sheet1").DoubleClickAsync(new MouseEventArgs());
        await NameField(cut).InputAsync(new ChangeEventArgs { Value = "Costs" });

        Assert.True(await cut.InvokeAsync(() => cut.Instance.UndoAsync()));

        Assert.Equal(["One", "Two"], TabNames(cut));
        Assert.Empty(cut.FindAll(".ex-sheet-tab-name"));
    }

    [Fact] // ADR-0331 §4: a Consumer's command that deletes the menu's Sheet closes the menu, the keyboard back in the grid
    public async Task The_menu_closes_when_the_consumer_deletes_its_sheet()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await OpenMenuAsync(cut, "One");
        var returns = KeyboardReturns;

        await cut.Instance.DoAsync("One", SheetEdit.DeleteSheet());

        Assert.Empty(cut.FindAll(".ex-sheet-tab-menu"));
        Assert.Equal(returns + 1, KeyboardReturns);
    }

    [Fact] // ADR-0331 §4: a menu still standing follows the Sheets as they stand: its Sheet now the only one, it can be neither deleted nor moved
    public async Task The_menu_follows_the_sheets_as_they_stand()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await OpenMenuAsync(cut, "Two");
        Assert.Null(Command(cut, "Move Left").GetAttribute("aria-disabled"));

        await cut.Instance.DoAsync("One", SheetEdit.DeleteSheet());

        Assert.Equal("true", Command(cut, "Delete").GetAttribute("aria-disabled"));
        Assert.Equal("true", Command(cut, "Move Left").GetAttribute("aria-disabled"));
        Assert.Equal("Rename", ActiveCommand(cut));
    }

    [Fact] // ADR-0331 §4: a command is judged on the Workbook as it stands when it runs, after the menu's close has waited on the browser
    public async Task A_command_is_judged_on_the_workbook_as_it_stands_when_it_runs()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await cut.Find(".ex-sheet-tab-add").ClickAsync(new MouseEventArgs());
        await OpenMenuAsync(cut, "Sheet1");
        await MenuKeyAsync(cut, "End");
        await MenuKeyAsync(cut, "ArrowUp");
        await MenuKeyAsync(cut, "ArrowUp");
        Assert.Equal("Move Left", ActiveCommand(cut));
        var answer = HoldKeyboardHandBack();

        Task enter = Task.CompletedTask;
        await cut.InvokeAsync(() => { enter = cut.Find(".ex-sheet-tab-menu").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }); });
        Task<bool> undo = Task.FromResult(false);
        await cut.InvokeAsync(() => { undo = cut.Instance.UndoAsync(); });
        answer();
        await enter;
        Assert.True(await undo);

        Assert.Equal(["One", "Two"], TabNames(cut));
        Assert.Equal("No Sheet was changed: The Sheet is no longer in the Workbook.", Notice(cut));
    }

    private static int Kept(IRenderedComponent<SheetComponent> cut, string field) =>
        ((System.Collections.ICollection)typeof(SheetComponent)
            .GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(cut.Instance)!).Count;

    [Fact] // ADR-0331 §4: a Sheet that leaves the Workbook leaves no tab and no view kept behind, and a Workbook opened afresh none of the one it replaced
    public async Task A_sheet_that_leaves_keeps_no_tab_and_no_view()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await Tab(cut, "Two").ClickAsync(new MouseEventArgs());
        await Tab(cut, "One").ClickAsync(new MouseEventArgs());
        Assert.Equal(2, Kept(cut, "_tabs"));
        Assert.Equal(2, Kept(cut, "_views"));

        await cut.Instance.DoAsync("Two", SheetEdit.DeleteSheet());

        Assert.Equal(1, Kept(cut, "_tabs"));
        Assert.Equal(1, Kept(cut, "_views"));

        cut.Render(ps => ps.Add(s => s.Workbook, Sheets("Alpha", "Beta", "Gamma")));

        Assert.Equal(["Alpha", "Beta", "Gamma"], TabNames(cut));
        Assert.Equal(3, Kept(cut, "_tabs"));
    }

    // ---- An IME in the name's field ----------------------------------------------------------------

    [Theory] // ADR-0010, ADR-0080, ADR-0331 §4: while an IME composes, its Enter and its Escape are its own: the name's edit neither commits nor ends
    [InlineData("Enter", true)]
    [InlineData("Escape", true)]
    [InlineData("Process", false)]
    public async Task A_composing_imes_keys_leave_the_name_alone(string key, bool composing)
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await Tab(cut, "One").DoubleClickAsync(new MouseEventArgs());
        await NameField(cut).InputAsync(new ChangeEventArgs { Value = "Costs" });
        var returns = KeyboardReturns;

        await NameField(cut).KeyDownAsync(new KeyboardEventArgs { Key = key, IsComposing = composing });

        Assert.Equal("Costs", NameField(cut).GetAttribute("value"));
        Assert.Equal(["One", "Two"], [.. cut.Instance.ToWorkbookDocument().Sheets.Select(s => s.Name)]);
        Assert.Equal(returns, KeyboardReturns);

        // The composition over, Enter commits the name.
        await NameField(cut).KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        Assert.Equal(["Costs", "Two"], TabNames(cut));
    }

    [Fact] // ADR-0240 §6, ADR-0331 §4: the keyboard leaving the menu is taken in its turn: an Enter numbered on the menu before it left is still answered there
    public async Task ADR0240_the_menu_closes_in_its_turn_behind_a_key_numbered_before()
    {
        var answer = new TaskCompletionSource();
        var raised = 0;
        var cut = RenderSheet(ps =>
        {
            ps.Add(s => s.Workbook, Sheets("One", "Two"));
            ps.Add(s => s.WorkbookChanged, async (WorkbookDocument _) =>
            {
                if (raised++ == 0) await answer.Task;
            });
        });
        await GoToAsync(cut, "A1");
        await OpenMenuAsync(cut, "One");
        await MenuKeyAsync(cut, "ArrowDown");
        Assert.Equal("Delete", ActiveCommand(cut));
        var menu = cut.Find(".ex-sheet-tab-menu");

        // A key on the grid that waits on the Consumer, then the menu's Enter numbered behind it, then the
        // keyboard leaving the menu before either has been answered.
        Task clear = Task.CompletedTask;
        await cut.InvokeAsync(() => { clear = KeyAsync(cut, "Delete", 1); });
        await TellAsync(cut, "keydown", 2);
        Task enter = Task.CompletedTask;
        await cut.InvokeAsync(() => { enter = menu.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }); });
        Task left = Task.CompletedTask;
        await cut.InvokeAsync(() => { left = menu.TriggerEventAsync("onfocusout", new FocusEventArgs()); });
        Assert.NotEmpty(cut.FindAll(".ex-sheet-tab-menu"));

        answer.SetResult();
        await clear;
        await enter;
        await left;

        Assert.Equal(["Two"], TabNames(cut));
        Assert.Empty(cut.FindAll(".ex-sheet-tab-menu"));
    }

    // ---- Where the keyboard goes when a gesture on the tabs ends --------------------------------------

    [Fact] // ADR-0331 §4: + pressed while a name is edited commits it and gives the keyboard, which the field held, to the grid; both are said
    public async Task Plus_while_renaming_commits_the_name_and_gives_the_keyboard_to_the_grid()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await Tab(cut, "One").DoubleClickAsync(new MouseEventArgs());
        await NameField(cut).InputAsync(new ChangeEventArgs { Value = "Costs" });
        var returns = KeyboardReturns;

        await cut.Find(".ex-sheet-tab-add").ClickAsync(new MouseEventArgs());

        Assert.Equal(["Costs", "Sheet1", "Two"], TabNames(cut));
        Assert.Equal(returns + 1, KeyboardReturns);
        Assert.Equal("Sheet 'One' was renamed 'Costs'. Sheet 'Sheet1' was added.", Notice(cut));
    }

    [Fact] // ADR-0331 §4: + pressed while a refused name stands keeps the old name, gives the keyboard to the grid, and the refusal is still said
    public async Task Plus_while_a_name_is_refused_keeps_the_refusal_said()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await RenameAsync(cut, "One", "Q1/Q2");
        var returns = KeyboardReturns;

        await cut.Find(".ex-sheet-tab-add").ClickAsync(new MouseEventArgs());

        Assert.Equal(["One", "Sheet1", "Two"], TabNames(cut));
        Assert.Equal(returns + 1, KeyboardReturns);
        Assert.StartsWith("The Sheet was not renamed:", Notice(cut));
        Assert.EndsWith("Sheet 'Sheet1' was added.", Notice(cut));
    }

    [Fact] // ADR-0331 §4: a drag while the menu is open closes it and gives the keyboard, which the menu held, to the grid
    public async Task A_drag_while_the_menu_is_open_gives_the_keyboard_to_the_grid()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await OpenMenuAsync(cut, "Two");
        var returns = KeyboardReturns;

        await Tab(cut, "One").MouseDownAsync(new MouseEventArgs { Button = 0 });
        await Tab(cut, "Two").MouseUpAsync(new MouseEventArgs { Button = 0 });

        Assert.Equal(["Two", "One"], TabNames(cut));
        Assert.Empty(cut.FindAll(".ex-sheet-tab-menu"));
        Assert.Equal(returns + 1, KeyboardReturns);
    }

    [Fact] // ADR-0331 §4: a press on another tab commits a name being edited first, then shows its Sheet, the keyboard in the grid
    public async Task A_press_on_another_tab_commits_the_name_first()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await Tab(cut, "One").DoubleClickAsync(new MouseEventArgs());
        await NameField(cut).InputAsync(new ChangeEventArgs { Value = "Costs" });
        var returns = KeyboardReturns;

        await Tab(cut, "Two").ClickAsync(new MouseEventArgs());

        Assert.Equal(["Costs", "Two"], TabNames(cut));
        Assert.Empty(cut.FindAll(".ex-sheet-tab-name"));
        Assert.Equal("Two", SelectedTab(cut));
        Assert.Equal(returns + 1, KeyboardReturns);
        Assert.Equal("Sheet 'One' was renamed 'Costs'.", Notice(cut));
    }

    [Fact] // ADR-0331 §4: a press on another tab closes an open menu first, then shows its Sheet
    public async Task A_press_on_another_tab_closes_the_menu_first()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await OpenMenuAsync(cut, "One");
        var returns = KeyboardReturns;

        await Tab(cut, "Two").ClickAsync(new MouseEventArgs());

        Assert.Empty(cut.FindAll(".ex-sheet-tab-menu"));
        Assert.Equal("Two", SelectedTab(cut));
        Assert.Equal(returns + 1, KeyboardReturns);
    }

    [Theory] // ADR-0331 §4: a rename begun from the keyboard on the tab list — Shift+F10, Rename — gives the keyboard back to the tab on Enter and on Escape
    [InlineData("Enter", "Costs")]
    [InlineData("Escape", "One")]
    public async Task A_rename_from_the_tab_list_gives_the_keyboard_back_to_the_tab(string key, string name)
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await Tab(cut, "One").KeyDownAsync(new KeyboardEventArgs { Key = "F10", ShiftKey = true });
        await MenuKeyAsync(cut, "Enter");
        // While the name is edited the tab list keeps a tab in the Tab order: the one being renamed.
        Assert.Equal("0", cut.Find(".ex-sheet-tab-renaming").GetAttribute("tabindex"));
        await NameField(cut).InputAsync(new ChangeEventArgs { Value = "Costs" });
        var returns = KeyboardReturns;
        var handed = HandedToCount;

        await NameField(cut).KeyDownAsync(new KeyboardEventArgs { Key = key });

        Assert.Equal([name, "Two"], TabNames(cut));
        Assert.Equal(returns, KeyboardReturns);
        // Through the tab list, which holds it while the field is swapped for the tab again, then to the tab.
        Assert.Equal(handed + 2, HandedToCount);
        Assert.Equal(TabReference(cut, name), LastHandedTo.Id);
    }

    [Theory] // ADR-0331 §4, ADR-0240 §6: a rename begun from the keys on a tab and ended there never leaves the keyboard on the page's body: the element that holds it stays drawn until the next one has it, so the keys typed meanwhile are heard and held
    [InlineData("Enter")]
    [InlineData("Escape")]
    public async Task ADR0331_a_rename_from_the_tab_list_never_draws_away_the_element_holding_the_keyboard(string key)
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await Tab(cut, "One").KeyDownAsync(new KeyboardEventArgs { Key = "F10", ShiftKey = true });
        Assert.NotEmpty(cut.FindAll(".ex-sheet-tab-menu"));
        // DOM focus as the browser keeps it: on the element the grid last handed the keyboard to, the menu
        // now. Every render must leave that element drawn, and each element handed it must be able to take it.
        var holder = Holder(cut, LastHandedTo.Id);
        var lost = new List<string>();
        OnHandKeyboardTo(id =>
        {
            holder = Holder(cut, id);
            if (holder.Find() is not { } handed || !TakesFocus(handed)) lost.Add("handed to what cannot take it: " + holder.Name);
        });
        cut.OnMarkupUpdated += (_, _) =>
        {
            if (holder.Find() is null) lost.Add("drawn away while holding the keyboard: " + holder.Name);
        };

        await MenuKeyAsync(cut, "Enter");
        Assert.Equal("One", NameField(cut).GetAttribute("value"));
        Assert.Equal("the name's field", holder.Name);
        await NameField(cut).InputAsync(new ChangeEventArgs { Value = "Costs" });
        await NameField(cut).KeyDownAsync(new KeyboardEventArgs { Key = key });

        Assert.Equal([key == "Enter" ? "Costs" : "One", "Two"], TabNames(cut));
        Assert.Equal("the tab " + (key == "Enter" ? "Costs" : "One"), holder.Name);
        Assert.Empty(lost);
        // The tab list holds the keyboard only across the swap: it is no stop of its own after.
        Assert.Null(cut.Find(".ex-sheet-tabs").GetAttribute("tabindex"));
        Assert.Null(cut.Find(".ex-sheet-tabs").GetAttribute("data-ex-claims"));
    }

    // The element of the Sheet's own an element reference names, found in the markup as it stands each time:
    // bUnit draws no reference's id, so it is matched to the field ExSheet holds it in.
    private static (string Name, Func<IElement?> Find) Holder(IRenderedComponent<SheetComponent> cut, string id)
    {
        string Reference(string field) => ((ElementReference)typeof(SheetComponent)
            .GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(cut.Instance)!).Id;
        if (id == Reference("_menuElement")) return ("the menu", () => cut.FindAll(".ex-sheet-tab-menu").FirstOrDefault());
        if (id == Reference("_renameBox")) return ("the name's field", () => cut.FindAll(".ex-sheet-tab-name-box").FirstOrDefault());
        if (id == Reference("_tabList")) return ("the tab list", () => cut.FindAll(".ex-sheet-tabs").FirstOrDefault());
        foreach (var name in new[] { "One", "Two", "Costs" })
        {
            if (id == TabReference(cut, name)) return ("the tab " + name, () => cut.FindAll("button.ex-sheet-tab").FirstOrDefault(tab => tab.TextContent == name));
        }
        return ("an element unknown", static () => null);
    }

    // Focusable as the grid's hand-off finds it: the element itself, or, for a box that takes none, a field inside it.
    private static bool TakesFocus(IElement element) =>
        element.LocalName is "button" or "input" || element.HasAttribute("tabindex") || element.QuerySelector("input") is not null;

    [Fact] // ADR-0331 §4: a rename begun by a double click gives the keyboard back to the grid, as before
    public async Task A_rename_from_a_double_click_gives_the_keyboard_to_the_grid()
    {
        var cut = RenderWorkbook(Sheets("One", "Two"));
        await Tab(cut, "One").DoubleClickAsync(new MouseEventArgs());
        var returns = KeyboardReturns;

        await NameField(cut).KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal(returns + 1, KeyboardReturns);
    }

    // The element reference ExSheet holds for the tab of the Sheet named `name`, as the grid is handed it.
    private static string TabReference(IRenderedComponent<SheetComponent> cut, string name)
    {
        var tabs = (System.Collections.IDictionary)typeof(SheetComponent)
            .GetField("_tabs", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(cut.Instance)!;
        foreach (System.Collections.DictionaryEntry entry in tabs)
        {
            if (((Sheet)entry.Key).Name == name) return ((ElementReference)entry.Value!).Id;
        }
        return "";
    }
}
