using Bunit;
using ExGrid.Cells;
using ExGrid.Chrome;
using ExGrid.Clipboard;
using Microsoft.AspNetCore.Components;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The Consumer's verdict at the commit (ADR-0034): the grid enforces the verb and never
/// judges a value. The convention these tests use is the ADR's intended one — an
/// unparseable text Rejects, a parseable value a rule dislikes Flags.
/// </summary>
public class ValidationTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private int _validateCalls;

    private GridColumn<TestRow>[] Columns(Func<TestRow, string, EditVerdict>? validate = null) =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true,
            validate: validate is null ? null : (row, text) => { _validateCalls++; return validate(row, text); }),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: true),
    ];

    private readonly TestRow[] _rows = TestRows.Many(50);

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Func<TestRow, string, EditVerdict>? validate = null,
        Action<GridEditIntent<TestRow>>? onEdit = null,
        Action<GridPasteIntent>? onPaste = null,
        Func<TestRow, GridColumn<TestRow>, string?>? cellMessageOf = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, _rows)
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, Columns(validate))
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350);
            if (onEdit is not null)
                ps.Add(g => g.OnEdit, onEdit);
            if (onPaste is not null)
                ps.Add(g => g.OnPaste, onPaste);
            if (cellMessageOf is not null)
                ps.Add(g => g.CellMessageOf, cellMessageOf);
        });

    private static Task PressAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, false, false));

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(
            new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static async Task TypeAndCommitAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, string text, string commitKey = "Enter", bool ctrl = false)
    {
        await ClickCellAsync(cut, 50, 10);
        // A printable key opens the editor; the rest arrives as the control's input,
        // which is the only way text reaches the core (ADR-0010).
        await PressAsync(cut, text[..1]);
        if (text.Length > 1)
            await cut.Find(".ex-editor").InputAsync(new ChangeEventArgs { Value = text });
        await PressAsync(cut, commitKey, ctrl: ctrl);
    }

    [Fact] // ADR-0034 / ED-14: no function means Accept, and Accept raises the intent unchanged
    public async Task A_column_without_a_verdict_commits_exactly_as_before()
    {
        var edits = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(onEdit: edits.Add);

        await TypeAndCommitAsync(cut, "9");

        Assert.Equal("9", Assert.Single(edits).Value);
        Assert.Equal(0, _validateCalls);
    }

    [Fact] // ADR-0034 / ED-14: the verdict is asked once, with the row and the committed text
    public async Task The_verdict_is_asked_with_the_row_and_the_text()
    {
        TestRow? seenRow = null;
        string? seenText = null;
        var cut = RenderGrid(validate: (row, text) => { seenRow = row; seenText = text; return EditVerdict.Accept; });

        await TypeAndCommitAsync(cut, "9");

        Assert.Same(_rows[0], seenRow);
        Assert.Equal("9", seenText);
        Assert.Equal(1, _validateCalls);
    }

    [Fact] // ADR-0034 / ED-15: a Reject holds the editor and raises nothing
    public async Task A_reject_holds_the_editor_and_raises_no_intent()
    {
        var edits = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(validate: (_, _) => EditVerdict.Reject("not a date"), onEdit: edits.Add);

        await TypeAndCommitAsync(cut, "abc");

        Assert.Empty(edits);
        Assert.NotEmpty(cut.FindAll(".ex-editor"));
        Assert.Equal("true", cut.Find(".ex-editor").GetAttribute("aria-invalid"));
    }

    [Theory] // ADR-0034 / ED-15: every commit gesture stops, not only Enter
    [InlineData("Enter")]
    [InlineData("Tab")]
    [InlineData("ArrowDown")]
    [InlineData("ArrowRight")]
    public async Task A_reject_stops_every_commit_gesture(string key)
    {
        var edits = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(validate: (_, _) => EditVerdict.Reject("no"), onEdit: edits.Add);

        await TypeAndCommitAsync(cut, "abc", commitKey: key);

        Assert.Empty(edits);
        Assert.NotEmpty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0010 amended by ADR-0034 / ED-12: the rejected press keeps no meaning of its own
    public async Task A_click_away_under_a_reject_selects_nothing()
    {
        var cut = RenderGrid(validate: (_, _) => EditVerdict.Reject("no"));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "a");
        await cut.Find(".ex-editor").InputAsync(new ChangeEventArgs { Value = "abc" });
        var focusBefore = cut.Find(".ex-grid").GetAttribute("aria-activedescendant");

        await ClickCellAsync(cut, 150, 50);

        Assert.NotEmpty(cut.FindAll(".ex-editor"));
        Assert.Equal(focusBefore, cut.Find(".ex-grid").GetAttribute("aria-activedescendant"));
    }

    [Fact] // ADR-0034 / ED-15: Escape remains the only exit without applying
    public async Task Escape_still_leaves_a_rejected_editor()
    {
        var edits = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(validate: (_, _) => EditVerdict.Reject("no"), onEdit: edits.Add);
        await TypeAndCommitAsync(cut, "abc");

        await PressAsync(cut, "Escape");

        Assert.Empty(cut.FindAll(".ex-editor"));
        Assert.Empty(edits);
    }

    [Fact] // ADR-0034 / ED-16: a Flag raises the intent byte-identical to an Accept's
    public async Task A_flag_raises_the_intent_unchanged()
    {
        var flagged = new List<GridEditIntent<TestRow>>();
        var accepted = new List<GridEditIntent<TestRow>>();
        var flagGrid = RenderGrid(validate: (_, _) => EditVerdict.Flag("out of range"), onEdit: flagged.Add);
        await TypeAndCommitAsync(flagGrid, "9");

        var acceptGrid = RenderGrid(onEdit: accepted.Add);
        await TypeAndCommitAsync(acceptGrid, "9");

        Assert.Equal(accepted.Single().Value, flagged.Single().Value);
        Assert.Equal(accepted.Single().Column, flagged.Single().Column);
        Assert.Same(accepted.Single().Row, flagged.Single().Row);
        // The mark does not travel in the intent; it comes back through the display
        // channels when the Consumer answers them.
        Assert.Empty(flagGrid.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0034 / A11Y-15: a rejected commit is otherwise a key that did nothing and said nothing
    public async Task A_reject_is_announced_with_the_consumers_own_sentence()
    {
        var cut = RenderGrid(validate: (_, _) => EditVerdict.Reject("'abc' is not a date"));

        await TypeAndCommitAsync(cut, "abc");

        Assert.Equal("'abc' is not a date", cut.Find(".ex-announce").TextContent);
    }

    [Fact] // ADR-0034 / ED-20: a fill is judged on value, once, against the editor's row
    public async Task A_fill_runs_the_verdict_once_and_a_reject_stops_it()
    {
        var pastes = new List<GridPasteIntent>();
        var cut = RenderGrid(validate: (_, _) => EditVerdict.Reject("no"), onPaste: pastes.Add);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "ArrowDown", shift: true);
        await PressAsync(cut, "a");
        await cut.Find(".ex-editor").InputAsync(new ChangeEventArgs { Value = "abc" });

        await PressAsync(cut, "Enter", ctrl: true);

        Assert.Empty(pastes);
        Assert.Equal(1, _validateCalls);
        Assert.NotEmpty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0034 / ED-18: a clipboard paste is never judged on value, whatever the payload
    public async Task A_clipboard_paste_never_runs_the_verdict()
    {
        var pastes = new List<GridPasteIntent>();
        var cut = RenderGrid(validate: (_, _) => EditVerdict.Reject("no"), onPaste: pastes.Add);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "ArrowDown", shift: true);

        await cut.InvokeAsync(() => cut.Instance.OnPasteAsync("abc\r\nabc", null));

        Assert.Single(pastes);
        Assert.Equal(0, _validateCalls);
    }

    [Fact] // ADR-0034 / ED-17: the message is asked for at the open, never while painting
    public async Task The_message_is_never_asked_for_while_painting()
    {
        var asked = 0;
        var cut = RenderGrid(cellMessageOf: (_, _) => { asked++; return "out of range"; });

        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "ArrowDown");

        // Painted several times by now, and moving the Focus does not open anything.
        Assert.Equal(0, asked);
        Assert.Empty(cut.FindAll(".ex-message"));
    }

    [Fact] // ADR-0034 / ED-17: 300 ms of stillness opens it, and one move closes it again
    public async Task The_popover_opens_once_the_focus_has_stood_still()
    {
        var asked = 0;
        var cut = RenderGrid(cellMessageOf: (_, _) => { asked++; return "out of range"; });
        await ClickCellAsync(cut, 50, 10);

        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(300)));

        Assert.Equal(1, asked);
        Assert.Equal("out of range", cut.Find(".ex-message").TextContent.Trim());

        await PressAsync(cut, "ArrowDown");
        Assert.Empty(cut.FindAll(".ex-message"));
    }

    [Fact] // ADR-0034 / ED-17: a run across the rows opens nothing — the delay is the point
    public async Task Continuous_arrow_movement_stays_quiet()
    {
        var asked = 0;
        var cut = RenderGrid(cellMessageOf: (_, _) => { asked++; return "out of range"; });
        await ClickCellAsync(cut, 50, 10);

        for (var i = 0; i < 6; i++)
        {
            await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(120)));
            await PressAsync(cut, "ArrowDown");
        }

        Assert.Equal(0, asked);
        Assert.Empty(cut.FindAll(".ex-message"));
    }

    [Fact] // ADR-0033/0034: announced once, by the one live region — the popover is a tooltip
    public async Task The_rejects_message_is_not_announced_twice()
    {
        var cut = RenderGrid(validate: (_, _) => EditVerdict.Reject("not a date"));

        await TypeAndCommitAsync(cut, "abc");

        Assert.Equal("not a date", cut.Find(".ex-announce").TextContent);
        var message = cut.Find(".ex-message");
        Assert.Equal("tooltip", message.GetAttribute("role"));
        // And the editor points at it, so it is read when the field is read.
        Assert.Equal(message.GetAttribute("id"), cut.Find(".ex-editor").GetAttribute("aria-describedby"));
    }

    [Fact] // ADR-0034 / ED-17b: the pointer coming to rest opens it, asking once
    public async Task The_pointer_coming_to_rest_opens_the_message()
    {
        var asked = 0;
        var cut = RenderGrid(cellMessageOf: (_, _) => { asked++; return "out of range"; });

        // JS heard the moves and reports only the stillness (ADR-0021's fifth entry).
        await cut.InvokeAsync(() => cut.Instance.OnPointerRestAsync(150, 50));

        Assert.Equal(1, asked);
        Assert.Equal("out of range", cut.Find(".ex-message").TextContent.Trim());

        // Resting again on the same cell asks nothing more.
        await cut.InvokeAsync(() => cut.Instance.OnPointerRestAsync(155, 52));
        Assert.Equal(1, asked);
    }

    [Fact] // ADR-0034 / ED-17b: the pointer leaving takes the message with it
    public async Task The_pointer_leaving_closes_the_message()
    {
        var cut = RenderGrid(cellMessageOf: (_, _) => "out of range");
        await cut.InvokeAsync(() => cut.Instance.OnPointerRestAsync(150, 50));
        Assert.NotEmpty(cut.FindAll(".ex-message"));

        await cut.InvokeAsync(() => cut.Instance.OnPointerAwayAsync());

        Assert.Empty(cut.FindAll(".ex-message"));
    }

    [Fact] // ADR-0034: while an editor holds, the sentence the user needs is the editor's
    public async Task Hovering_says_nothing_while_an_editor_is_open()
    {
        var asked = 0;
        var cut = RenderGrid(
            validate: (_, _) => EditVerdict.Reject("not a date"),
            cellMessageOf: (_, _) => { asked++; return "out of range"; });
        await TypeAndCommitAsync(cut, "abc");

        await cut.InvokeAsync(() => cut.Instance.OnPointerRestAsync(150, 50));

        Assert.Equal(0, asked);
        Assert.Equal("not a date", cut.Find(".ex-message").TextContent.Trim());
    }

    [Fact] // ADR-0034 / ED-15: a header press is a click-away too, and a Reject stops it
    public async Task A_reject_holds_the_editor_against_a_header_press()
    {
        var cut = RenderGrid(validate: (_, _) => EditVerdict.Reject("no"));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "a");
        await cut.Find(".ex-editor").InputAsync(new ChangeEventArgs { Value = "abc" });

        await cut.Find(".ex-header").MouseDownAsync(
            new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 5 });

        // A sort here would bump the sequence version, drop the selection, and take the
        // typing the Reject was holding on to.
        Assert.NotEmpty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0034 / ED-15: the sort is on the click, so the click is where the Reject must hold
    public async Task A_reject_holds_the_editor_against_a_header_click()
    {
        var discarded = new List<EditDiscardReason>();
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, _rows)
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, Columns((_, _) => EditVerdict.Reject("no")))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnSortChanged, _ => { })
            .Add(g => g.OnEditDiscarded, r => discarded.Add(r)));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "a");
        await cut.Find(".ex-editor").InputAsync(new ChangeEventArgs { Value = "abc" });

        var header = cut.Find(".ex-header");
        await header.MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = 5 });
        await header.ClickAsync(new MouseEventArgs { Button = 0, OffsetX = 50, OffsetY = 5 });

        // No sort, so no sequence bump, so nothing dropped the selection and took the text.
        Assert.NotEmpty(cut.FindAll(".ex-editor"));
        Assert.Empty(discarded);
    }

    [Fact] // ADR-0010/0034: the menu button stops the press, so it carries the gate itself
    public async Task A_reject_holds_the_editor_against_the_column_menu_button()
    {
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, _rows)
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, Columns((_, _) => EditVerdict.Reject("no")))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnSortChanged, _ => { }));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "a");
        await cut.Find(".ex-editor").InputAsync(new ChangeEventArgs { Value = "abc" });

        await cut.FindAll(".ex-menu-button")[0].ClickAsync(new MouseEventArgs());

        Assert.NotEmpty(cut.FindAll(".ex-editor"));
        Assert.Empty(cut.FindAll("[role=menu] button[role=menuitem]"));
    }

    [Fact] // ADR-0034: what was open belonged to the cell the pointer left
    public async Task Resting_on_a_cell_with_nothing_to_say_closes_the_last_message()
    {
        var cut = RenderGrid(cellMessageOf: (_, column) => column.Name == "Book" ? "out of range" : null);
        await cut.InvokeAsync(() => cut.Instance.OnPointerRestAsync(50, 10));
        Assert.NotEmpty(cut.FindAll(".ex-message"));

        await cut.InvokeAsync(() => cut.Instance.OnPointerRestAsync(150, 10));

        Assert.Empty(cut.FindAll(".ex-message"));
    }

    [Fact] // ADR-0033: the region holds the sentence again after an identical repeat
    public async Task The_same_reject_twice_leaves_the_sentence_standing()
    {
        var cut = RenderGrid(validate: (_, _) => EditVerdict.Reject("not a date"));
        await TypeAndCommitAsync(cut, "abc");

        await PressAsync(cut, "Enter");

        // The mechanism is empty-then-rewrite across two renders — two mutations inside
        // one element, rather than a replaced element, which a screen reader ignores.
        // What this layer can see is the outcome; whether an assistive technology speaks
        // it is a browser-level fact no suite here asserts.
        Assert.Equal("not a date", cut.Find(".ex-announce").TextContent);
        Assert.Equal("status", cut.Find(".ex-announce").GetAttribute("role"));
    }

    [Fact] // ADR-0034 / ED-15: nor may a secondary press walk the selection away from it
    public async Task A_reject_holds_the_editor_against_a_secondary_press()
    {
        var cut = RenderGrid(validate: (_, _) => EditVerdict.Reject("no"));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "a");
        await cut.Find(".ex-editor").InputAsync(new ChangeEventArgs { Value = "abc" });
        var focusBefore = cut.Find(".ex-grid").GetAttribute("aria-activedescendant");

        await cut.Find(".ex-viewport").ContextMenuAsync(new MouseEventArgs { OffsetX = 150, OffsetY = 50 });

        Assert.NotEmpty(cut.FindAll(".ex-editor"));
        Assert.Equal(focusBefore, cut.Find(".ex-grid").GetAttribute("aria-activedescendant"));
        Assert.Empty(cut.FindAll("[role=menu] button[role=menuitem]"));
    }

    [Fact] // ADR-0034: a standing error belongs to the editor that earned it, not the next one
    public async Task A_rejects_error_does_not_survive_into_the_next_editor()
    {
        var cut = RenderGrid(validate: (row, text) => text == "abc" ? EditVerdict.Reject("no") : EditVerdict.Accept);
        await TypeAndCommitAsync(cut, "abc");
        Assert.NotEmpty(cut.FindAll(".ex-message"));

        // The Consumer pushes a reorder: the selection is dropped and the editor with it.
        cut.Render(ps => ps.Add(g => g.RowSequenceVersion, 1));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "7");

        Assert.Empty(cut.FindAll(".ex-message"));
        Assert.Null(cut.Find(".ex-editor").GetAttribute("aria-invalid"));
    }

    [Fact] // ADR-0033/0034: a live region announces on mutation, so a repeat must be one
    public async Task The_same_reject_twice_is_announced_twice()
    {
        var cut = RenderGrid(validate: (_, _) => EditVerdict.Reject("not a date"));
        await TypeAndCommitAsync(cut, "abc");
        var first = cut.Find(".ex-announce");

        await PressAsync(cut, "Enter");

        // Same sentence, a different node: the second refusal is the one the user needs.
        Assert.Equal("not a date", cut.Find(".ex-announce").TextContent);
        Assert.NotSame(first, cut.Find(".ex-announce"));
    }
}
