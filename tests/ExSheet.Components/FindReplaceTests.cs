using System.Globalization;
using Bunit;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;
using SheetComponent = ExSheet.Components.ExSheet;

namespace ExSheet.Components.Tests;

/// <summary>
/// Find and Replace on a Sheet (ADR-0332 §2, §4, §5; find-and-replace ticket 04): ExSheet draws the panel
/// itself, with Within — the Sheet shown, the default, or the Workbook. A step over the Workbook goes on to
/// the next Sheet, shows it and selects the match there; Replace matches each cell's Entry and is one undo
/// step; Replace All is all or nothing, uncapped, one undo step and says the count, over the Sheet or the
/// whole Workbook; and the Match Mark judges the painted cells by ExSheet's own rule.
/// </summary>
public class FindReplaceTests : SheetTestContext
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    private WorkbookDocument? _saved;

    private static WorkbookDocument Workbook(params (string Sheet, string Address, string Typed)[] cells)
    {
        var workbook = new Workbook(Culture, ["One", "Two", "Three"]);
        foreach (var (sheet, address, typed) in cells)
            workbook.Do(sheet, SheetEdit.Enter(CellAddress.Parse(address), typed));
        return workbook.ToDocument();
    }

    private IRenderedComponent<SheetComponent> RenderWorkbook(WorkbookDocument document)
    {
        _saved = document;
        return RenderSheet(ps => ps
            .Add(s => s.Workbook, document)
            .Add(s => s.WorkbookChanged, changed => _saved = changed));
    }

    /// <summary>The Entry of a cell as the Workbook last raised it.</summary>
    private string? EntryOn(string sheet, string address)
    {
        var held = global::ExSheet.Engine.Workbook.Open(_saved!).Find(sheet)!;
        var at = CellAddress.Parse(address);
        return held.GetEntry(at) is null ? null : held.GetEntryText(at);
    }

    private static string SelectedTab(IRenderedComponent<SheetComponent> cut) =>
        cut.FindAll(".ex-sheet-tab").Single(tab => tab.GetAttribute("aria-selected") == "true").TextContent;

    private static string NameBox(IRenderedComponent<SheetComponent> cut) => cut.Find(".ex-name-box").GetAttribute("value") ?? "";

    private static string Outcome(IRenderedComponent<SheetComponent> cut) => cut.Find(".ex-popover-find .ex-find-outcome").TextContent;

    private static async Task OpenAsync(IRenderedComponent<SheetComponent> cut, bool replace)
    {
        if (cut.FindAll("input.ex-find-field").Count == 0)
            await PressAsync(cut, replace ? "h" : "f", ctrl: true);
    }

    private static Task WithinWorkbookAsync(IRenderedComponent<SheetComponent> cut)
        => cut.Find("input.ex-sheet-find-within-workbook").ClickAsync(new MouseEventArgs());

    private static async Task FindAsync(IRenderedComponent<SheetComponent> cut, string text, bool backward = false)
    {
        await OpenAsync(cut, replace: false);
        await cut.Find("input.ex-find-field").InputAsync(new ChangeEventArgs { Value = text });
        await cut.Find(backward ? "button.ex-find-previous" : "button.ex-find-next").ClickAsync(new MouseEventArgs());
    }

    private static async Task TypeReplaceAsync(IRenderedComponent<SheetComponent> cut, string find, string replacement)
    {
        await OpenAsync(cut, replace: true);
        await cut.Find("input.ex-find-field").InputAsync(new ChangeEventArgs { Value = find });
        await cut.Find("input.ex-find-replace-field").InputAsync(new ChangeEventArgs { Value = replacement });
    }

    private static Task ReplaceAsync(IRenderedComponent<SheetComponent> cut)
        => cut.Find("button.ex-find-replace").ClickAsync(new MouseEventArgs());

    private static Task ReplaceAllAsync(IRenderedComponent<SheetComponent> cut)
        => cut.Find("button.ex-find-replace-all").ClickAsync(new MouseEventArgs());

    [Fact] // ADR-0332 §2, §5: Ctrl+H opens ExSheet's panel with Replace, and Within offers the Sheet, chosen, or the Workbook
    public async Task ADR0332_ctrl_h_opens_the_panel_with_replace_and_within_the_sheet()
    {
        var cut = RenderWorkbook(Workbook(("One", "A1", "apple")));

        await PressAsync(cut, "h", ctrl: true);

        var panel = cut.Find(".ex-popover-find");
        Assert.NotNull(panel.QuerySelector("form.ex-sheet-find"));
        Assert.NotNull(panel.QuerySelector("input.ex-find-replace-field"));
        Assert.True(panel.QuerySelector("input.ex-sheet-find-within-sheet")!.HasAttribute("checked"));
        Assert.False(panel.QuerySelector("input.ex-sheet-find-within-workbook")!.HasAttribute("checked"));
        Assert.Equal("Within", panel.QuerySelector(".ex-sheet-find-within legend")!.TextContent);
    }

    [Fact] // ADR-0332 §2, FD-6: what is typed in either field stands through the panel's own render, though the grid does not render for the Replace field
    public async Task ADR0332_typed_text_stands_in_both_fields()
    {
        var cut = RenderWorkbook(Workbook(("One", "A1", "apple")));
        await PressAsync(cut, "h", ctrl: true);

        await cut.Find("input.ex-find-replace-field").InputAsync(new ChangeEventArgs { Value = "p" });
        await cut.Find("input.ex-find-replace-field").InputAsync(new ChangeEventArgs { Value = "pe" });
        await cut.Find("input.ex-find-field").InputAsync(new ChangeEventArgs { Value = "a" });

        Assert.Equal("pe", cut.Find("input.ex-find-replace-field").GetAttribute("value"));
        Assert.Equal("a", cut.Find("input.ex-find-field").GetAttribute("value"));
    }

    [Fact] // ADR-0332 §5: Within: Sheet, a step stays on the Sheet shown
    public async Task ADR0332_within_the_sheet_a_step_stays_on_the_sheet_shown()
    {
        var cut = RenderWorkbook(Workbook(("One", "A1", "apple"), ("Two", "C3", "apple pie")));
        await GoToAsync(cut, "A1");

        await FindAsync(cut, "apple");

        Assert.Equal("One", SelectedTab(cut));
        Assert.Equal("A1", NameBox(cut));
    }

    [Fact] // ADR-0332 §5, ADR-0331 §8: Within: Workbook, a step goes on to the next Sheet, shows it with the match selected, and wraps through every Sheet both ways
    public async Task ADR0332_within_the_workbook_a_step_shows_the_sheet_of_the_match()
    {
        var cut = RenderWorkbook(Workbook(("One", "A1", "apple"), ("Two", "C3", "apple pie"), ("Three", "B1", "Apple")));
        await GoToAsync(cut, "A1");
        await OpenAsync(cut, replace: false);
        await WithinWorkbookAsync(cut);

        await FindAsync(cut, "apple");
        Assert.Equal(("Two", "C3"), (SelectedTab(cut), NameBox(cut)));
        Assert.Contains("ex-match-mark-current", Cell(cut, "C3").ClassName);

        await FindAsync(cut, "apple");
        Assert.Equal(("Three", "B1"), (SelectedTab(cut), NameBox(cut)));

        await FindAsync(cut, "apple");
        Assert.Equal(("One", "A1"), (SelectedTab(cut), NameBox(cut)));

        await FindAsync(cut, "apple", backward: true);
        Assert.Equal(("Three", "B1"), (SelectedTab(cut), NameBox(cut)));
        Assert.Equal("", Outcome(cut));
    }

    [Fact] // ADR-0332 §5: Within is kept across a close and reopen, as the find text is
    public async Task ADR0332_within_is_kept_across_a_close_and_reopen()
    {
        var cut = RenderWorkbook(Workbook(("One", "A1", "apple")));
        await OpenAsync(cut, replace: false);
        await WithinWorkbookAsync(cut);

        await PressAsync(cut, "Escape", fromDescendant: true);
        Assert.Empty(cut.FindAll("input.ex-find-field"));
        await PressAsync(cut, "f", ctrl: true);

        Assert.True(cut.Find("input.ex-sheet-find-within-workbook").HasAttribute("checked"));
    }

    [Fact] // ADR-0332 §5: nothing anywhere in the Workbook says no match, and moves nothing
    public async Task ADR0332_no_match_in_the_workbook_says_so()
    {
        var cut = RenderWorkbook(Workbook(("One", "A1", "apple"), ("Two", "C3", "pear")));
        await GoToAsync(cut, "A1");
        await OpenAsync(cut, replace: false);
        await WithinWorkbookAsync(cut);

        await FindAsync(cut, "plum");

        Assert.Equal(("One", "A1"), (SelectedTab(cut), NameBox(cut)));
        Assert.Contains("No match", Outcome(cut));
    }

    [Fact] // ADR-0332 §4: the marks are ExSheet's rule — a cell's displayed text, a Formula's Value as shown, and a Blank never
    public async Task ADR0332_the_marks_follow_the_sheets_own_rule()
    {
        var cut = RenderWorkbook(Workbook(("One", "A1", "=40+2"), ("One", "B1", "42"), ("One", "C1", "x")));
        await OpenAsync(cut, replace: false);

        await cut.Find("input.ex-find-field").InputAsync(new ChangeEventArgs { Value = "42" });

        Assert.Contains("ex-match-mark", Cell(cut, "A1").ClassName);
        Assert.Contains("ex-match-mark", Cell(cut, "B1").ClassName);
        Assert.DoesNotContain("ex-match-mark", Cell(cut, "C1").ClassName);
        Assert.DoesNotContain("ex-match-mark", Cell(cut, "D1").ClassName);
    }

    [Fact] // ADR-0332 §2: Replace writes the current match's Entry — a Formula's text — one undo step each, and goes on to the next
    public async Task ADR0332_replace_writes_the_entry_one_step_each()
    {
        var cut = RenderWorkbook(Workbook(("One", "A1", "cat"), ("One", "A2", "=\"cat\"&\"s\"")));
        await GoToAsync(cut, "B5");
        await TypeReplaceAsync(cut, "cat", "dog");

        await ReplaceAsync(cut);
        Assert.Equal("A1", NameBox(cut));
        Assert.Equal("cat", EntryOn("One", "A1"));

        await ReplaceAsync(cut);
        Assert.Equal("dog", EntryOn("One", "A1"));
        Assert.Equal("A2", NameBox(cut));

        await ReplaceAsync(cut);
        Assert.Equal("=\"dog\"&\"s\"", EntryOn("One", "A2"));
        Assert.Equal("dogs", CellText(cut, "A2"));

        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal("=\"cat\"&\"s\"", EntryOn("One", "A2"));
        Assert.Equal("dog", EntryOn("One", "A1"));
    }

    [Fact] // ADR-0332 §2, §3: Replace All over the Sheet shown writes every matching Entry there and no other Sheet, as one undo step, and says the count
    public async Task ADR0332_replace_all_over_the_sheet_is_one_step_and_says_the_count()
    {
        var cut = RenderWorkbook(Workbook(("One", "A1", "apple"), ("One", "A3", "=\"apple\"&\" tart\""), ("Two", "C3", "apple pie")));
        await TypeReplaceAsync(cut, "apple", "pear");

        await ReplaceAllAsync(cut);

        Assert.Equal("pear", EntryOn("One", "A1"));
        Assert.Equal("=\"pear\"&\" tart\"", EntryOn("One", "A3"));
        Assert.Equal("apple pie", EntryOn("Two", "C3"));
        Assert.Contains("Replaced 2 cells.", Outcome(cut));

        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal("apple", EntryOn("One", "A1"));
        Assert.Equal("=\"apple\"&\" tart\"", EntryOn("One", "A3"));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0332 §5: Replace All over the Workbook writes every Sheet as one undo step, and says the count
    public async Task ADR0332_replace_all_over_the_workbook_is_one_step_and_says_the_count()
    {
        var cut = RenderWorkbook(Workbook(("One", "A1", "apple"), ("Two", "C3", "apple pie"), ("Three", "B1", "=\"Apple\""), ("Three", "B2", "=One!A1")));
        await TypeReplaceAsync(cut, "apple", "pear");
        await WithinWorkbookAsync(cut);

        await ReplaceAllAsync(cut);

        Assert.Equal("pear", EntryOn("One", "A1"));
        Assert.Equal("pear pie", EntryOn("Two", "C3"));
        Assert.Equal("=\"pear\"", EntryOn("Three", "B1"));
        Assert.Equal("One", SelectedTab(cut));
        Assert.Contains("Replaced 3 cells.", Outcome(cut));

        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal("apple", EntryOn("One", "A1"));
        Assert.Equal("apple pie", EntryOn("Two", "C3"));
        Assert.Equal("=\"Apple\"", EntryOn("Three", "B1"));
        Assert.False(cut.Instance.CanUndo);

        Assert.True(await cut.Instance.RedoAsync());
        Assert.Equal("pear pie", EntryOn("Two", "C3"));
    }

    [Fact] // ADR-0332 §2, §5: over the Workbook, a replaced Formula that cannot be read refuses it all, naming the cell by its Sheet, and nothing is written
    public async Task ADR0332_replace_all_over_the_workbook_is_all_or_nothing()
    {
        var cut = RenderWorkbook(Workbook(("One", "A1", "SUM(x"), ("Three", "B2", "=SUM(1)")));
        await TypeReplaceAsync(cut, "SUM(", "SUM((");
        await WithinWorkbookAsync(cut);

        await ReplaceAllAsync(cut);

        Assert.Equal("SUM(x", EntryOn("One", "A1"));
        Assert.Equal("=SUM(1)", EntryOn("Three", "B2"));
        Assert.False(cut.Instance.CanUndo);
        Assert.Contains("Nothing was replaced.", Outcome(cut));
        Assert.Contains("Row 2, Three!B", Outcome(cut));
    }

    [Fact] // ADR-0332 §5: over the Workbook, a text matched nowhere says no match and adds no step
    public async Task ADR0332_replace_all_over_the_workbook_with_no_match_says_so()
    {
        var cut = RenderWorkbook(Workbook(("One", "A1", "apple")));
        await TypeReplaceAsync(cut, "plum", "pear");
        await WithinWorkbookAsync(cut);

        await ReplaceAllAsync(cut);

        Assert.Contains("No match", Outcome(cut));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0332 §4, §5: after a Replace All over the Workbook no match is current, so the next Replace finds one and writes nothing
    public async Task ADR0332_replace_after_a_workbook_replace_all_finds_before_it_writes()
    {
        var cut = RenderWorkbook(Workbook(("One", "A1", "hotel"), ("Two", "A1", "travel")));
        await GoToAsync(cut, "B5");
        await TypeReplaceAsync(cut, "el", "EL");
        await WithinWorkbookAsync(cut);
        await ReplaceAsync(cut);
        Assert.Equal(("Two", "A1"), (SelectedTab(cut), NameBox(cut)));
        Assert.Contains("ex-match-mark-current", Cell(cut, "A1").ClassName);

        await ReplaceAllAsync(cut);
        Assert.Equal("travEL", EntryOn("Two", "A1"));
        Assert.Empty(cut.FindAll(".ex-match-mark-current"));

        // What was written still matches: the press finds, back on One, and writes nothing.
        await ReplaceAsync(cut);
        Assert.Equal(("One", "A1"), (SelectedTab(cut), NameBox(cut)));

        Assert.True(await cut.Instance.UndoAsync());
        Assert.Equal("hotel", EntryOn("One", "A1"));
        Assert.Equal("travel", EntryOn("Two", "A1"));
        Assert.False(cut.Instance.CanUndo);
    }

    [Fact] // ADR-0332 §5: within the Workbook, Replace goes on to a match on another Sheet, and writes it there
    public async Task ADR0332_replace_within_the_workbook_goes_on_to_another_sheet()
    {
        var cut = RenderWorkbook(Workbook(("One", "A1", "apple"), ("Two", "C3", "apple pie")));
        await GoToAsync(cut, "A1");
        await TypeReplaceAsync(cut, "apple", "pear");
        await WithinWorkbookAsync(cut);

        await ReplaceAsync(cut);
        Assert.Equal(("Two", "C3"), (SelectedTab(cut), NameBox(cut)));

        await ReplaceAsync(cut);
        Assert.Equal("pear pie", EntryOn("Two", "C3"));
        Assert.Equal(("One", "A1"), (SelectedTab(cut), NameBox(cut)));
    }
}
