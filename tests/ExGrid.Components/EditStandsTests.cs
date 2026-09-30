using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// An edit stands when the keyboard leaves the grid (ADR-0018 section 6; ED-26's layer 2 half).
/// Losing DOM focus is heard by nothing that could end an edit; keys pressed in another grid are
/// that grid's; and a press back on the rows means what it would have meant — it points where a
/// Reference can go, and commits and asks for the keyboard back at the root where none can.
/// Where the keyboard goes before that press is the listener's (ADR-0021's note of 2026-09-29)
/// and is layer 3's. A Sheet-shaped grid — pointing declared and a Formula Bar — beside a plain
/// one: 50 rows of 20px in a 350 × 200 Viewport; Book (A) and Note (B) edit, Amount (C) does not.
/// </summary>
public class EditStandsTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Note", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    /// <summary>A Reference can go after <c>=</c>, an operator, <c>(</c> or <c>,</c>: the shape
    /// of ExSheet's predicate (ADR-0051).</summary>
    private static bool PointAt(string text, int caret)
        => text.StartsWith('=') && caret > 0 && caret <= text.Length && "=+-*/(,".Contains(text[caret - 1]);

    private static string ReferenceText(SelectionRange range)
        => FormattableString.Invariant($"{(char)('A' + range.LeftColumn)}{range.TopRow + 1}");

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        List<GridEditIntent<TestRow>> intents, List<GridSelection>? selections = null, bool sheet = true)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.OnEdit, (GridEditIntent<TestRow> intent) => intents.Add(intent))
              .Add(g => g.SelectionChanged, (GridSelection s) => selections?.Add(s));
            if (sheet)
            {
                ps.Add(g => g.ShowFormulaBar, true)
                  .Add(g => g.PointAt, PointAt)
                  .Add(g => g.ReferenceText, ReferenceText);
            }
        });

    private static Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static Task PressAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, string key, string? text = null, int caret = -1, bool fromBar = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
            key, false, false, false, false, false, fromDescendant: fromBar, editorText: text, editorCaret: caret));

    private static string? EditorText(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindAll(".ex-viewport .ex-editor").SingleOrDefault()?.GetAttribute("value");

    private static AngleSharp.Dom.IElement Bar(IRenderedComponent<ExGrid<TestRow>> cut) => cut.Find(".ex-formula-bar-text");

    // What each hand-back asked of the handle: whether it takes the keyboard from the Formula Bar
    // and the Name Box too (ADR-0021, widened 2026-09-28).
    private List<bool> HandBacks()
        => [.. Js.FocusReclaimed.Invocations.Select(i => (bool)i.Arguments[0]!)];

    /// <summary><c>=</c> typed onto A1 of the Sheet, then the other grid pressed: the keyboard is
    /// the other grid's, and the Sheet's edit stands.</summary>
    private async Task<(IRenderedComponent<ExGrid<TestRow>> Sheet, IRenderedComponent<ExGrid<TestRow>> Other)> LeaveAnEditAsync(
        List<GridEditIntent<TestRow>> intents, string typed, List<GridSelection>? selections = null)
    {
        var sheet = RenderGrid(intents, selections);
        var other = RenderGrid([], sheet: false);
        await ClickAsync(sheet, 50, 10);
        await PressAsync(sheet, typed);
        await ClickAsync(other, 50, 10);
        return (sheet, other);
    }

    [Fact] // ADR-0018 section 6 / ED-26: nothing an edit is drawn with hears DOM focus leave, so nothing commits or discards it then
    public async Task Nothing_an_edit_is_drawn_with_hears_dom_focus_leave()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents);
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, "=");

        // The root, its scroller, the rows, the Cell Editor and the Formula Bar's text: a blur or
        // a focusout on any of them reaches no handler of the grid's. (The Name Box's blur drops
        // only the address typed into it, and a press into it commits the edit first.)
        foreach (var element in new[]
                 {
                     cut.Find(".ex-grid"), cut.Find(".ex-scroller"), cut.Find(".ex-viewport"),
                     cut.Find(".ex-viewport .ex-editor"), Bar(cut),
                 })
        {
            Assert.False(element.HasAttribute("blazor:onblur"), $"{element.ClassName} hears a blur");
            Assert.False(element.HasAttribute("blazor:onfocusout"), $"{element.ClassName} hears a focusout");
        }
        Assert.Equal("=", EditorText(cut));
        Assert.Equal("=", Bar(cut).GetAttribute("value"));
        Assert.Empty(intents);
    }

    [Fact] // ADR-0018 sections 1 and 6 / ED-26 / KB-1: Escape and typing in another grid are that grid's, and this grid's edit stands
    public async Task Keys_in_another_grid_leave_this_grids_edit_standing()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var (sheet, other) = await LeaveAnEditAsync(intents, "=");

        // The other grid opens an edit of its own and cancels it; then Escape releases it.
        await PressAsync(other, "x");
        Assert.Equal("x", EditorText(other));
        await PressAsync(other, "Escape", text: "x", caret: 1);
        Assert.Null(EditorText(other));
        var blurs = Js.BlurCount;
        await PressAsync(other, "Escape");

        Assert.Equal(blurs + 1, Js.BlurCount);
        // Two edits could stand at once, and the Sheet's still does, untouched.
        Assert.Equal("=", EditorText(sheet));
        Assert.Equal("=", Bar(sheet).GetAttribute("value"));
        Assert.Empty(intents);
    }

    [Fact] // ADR-0018 section 6 / ED-26 / ADR-0051: back on the rows, a press points as if the keyboard had never left, and Escape then cancels
    public async Task A_press_back_on_the_rows_points_and_escape_then_cancels()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var (sheet, other) = await LeaveAnEditAsync(intents, "=");
        await PressAsync(other, "Escape");
        var focusCalls = Js.FocusCalls;

        await ClickAsync(sheet, 150, 30); // B2

        Assert.Equal("=B2", EditorText(sheet));
        Assert.Single(sheet.FindAll(".ex-selection .ex-point"));
        Assert.Empty(intents);
        // The core moved no focus: the keyboard stays where the listener put it, in the edit.
        Assert.Equal(focusCalls, Js.FocusCalls);

        await PressAsync(sheet, "Escape", text: "=B2", caret: 3);

        Assert.Null(EditorText(sheet));
        Assert.Empty(intents);
    }

    [Fact] // ADR-0018 section 6 / ED-26 / ADR-0010: back on the rows where no Reference can go, a press commits, moves and asks for the keyboard at the root
    public async Task A_press_back_on_the_rows_that_cannot_point_commits_and_hands_the_keyboard_back()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var selections = new List<GridSelection>();
        var (sheet, _) = await LeaveAnEditAsync(intents, "9", selections);
        var before = HandBacks().Count;

        await ClickAsync(sheet, 150, 70); // B4

        Assert.Equal("9", Assert.Single(intents).Value);
        Assert.Null(EditorText(sheet));
        Assert.Equal(new CellPosition(3, 1), selections[^1].Focus);
        // Asked of the handle, which grants it only while DOM focus is inside the root or on
        // nothing (ADR-0021): after the listener has put the keyboard back into the edit, it is.
        // A press never asks it on the bar's behalf; a bar the press left standing is the
        // listener's to know about.
        Assert.Equal([false], HandBacks().Skip(before));
    }

    [Fact] // ADR-0018 section 6 / ED-26 / ADR-0051: an edit last typed in the Formula Bar stays the bar's through a press back that points
    public async Task An_edit_typed_in_the_bar_is_not_pulled_into_the_cell_by_a_press_back_that_points()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var sheet = RenderGrid(intents);
        var other = RenderGrid([], sheet: false);
        await ClickAsync(sheet, 150, 70); // B4
        await Bar(sheet).FocusAsync(new FocusEventArgs());
        await sheet.InvokeAsync(() => sheet.Instance.OnEditorCaretAsync("=", 1));
        await Bar(sheet).InputAsync(new ChangeEventArgs { Value = "=" });
        await ClickAsync(other, 50, 10);
        // The listener puts the keyboard back into the bar before the press: its focus reaches
        // the core first, as the bar's own press would.
        await Bar(sheet).FocusAsync(new FocusEventArgs());
        var focusCalls = Js.FocusCalls;

        await ClickAsync(sheet, 50, 30); // A2

        Assert.Equal("=A2", Bar(sheet).GetAttribute("value"));
        Assert.Equal("=A2", EditorText(sheet));
        Assert.Empty(intents);
        Assert.Equal(focusCalls, Js.FocusCalls);

        await PressAsync(sheet, "Enter", text: "=A2", caret: 3, fromBar: true);

        Assert.Equal("=A2", Assert.Single(intents).Value);
        // An edit ended by a key typed in the bar takes the keyboard out of it.
        Assert.True(HandBacks()[^1]);
    }

    // The hold behind a press (ADR-0010, widened 2026-09-29; ED-22): the listener holds the keys
    // typed after a press on the rows while an edit is open until the core has answered the press,
    // and hands them on against the mode the answer leaves. The holding is the browser's half and
    // layer 3's (edit-stands.spec.mjs); this is the core's half — what has happened by the time the
    // answer comes.

    [Fact] // ADR-0010 (widened 2026-09-29) / ED-22: a press that commits asks for the keyboard back while the editor still stands, and is answered behind the gate's new mode
    public async Task A_press_that_commits_is_answered_after_the_gate_and_the_hand_back()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var sheet = RenderGrid(intents);
        await ClickAsync(sheet, 50, 10);
        await PressAsync(sheet, "9");
        var told = Js.UnansweredGateMode();
        var before = HandBacks().Count;
        bool? editorStoodAtHandBack = null;
        Js.OnFocusReclaimed(() => editorStoodAtHandBack ??= sheet.FindAll(".ex-viewport .ex-editor").Count > 0);

        var press = ClickAsync(sheet, 150, 70); // B4: no Reference can go after 9
        var answered = sheet.InvokeAsync(() => sheet.Instance.PressAnsweredAsync());

        // The keyboard was asked back before the render that removes the editor: after that
        // render alone, DOM focus would be on body until the hand-back landed, and a key typed in
        // between reached no listener at all.
        Assert.Equal([false], HandBacks().Skip(before));
        Assert.True(editorStoodAtHandBack);
        // And the press is answered behind the gate's new mode: the browser runs the requests in
        // the order they were sent, so a key handed on after the answer is gated against "none".
        // Its reply is not waited for (ADR-0050 section 6, 2026-09-30): the Consumer has rendered
        // the committed value by now, and a key typed on seeing it must not wait a round trip
        // behind an answer that orders nothing more.
        Assert.Equal("none", (string)told.Invocations["setEditing"][^1].Arguments[0]!);
        Assert.Equal("9", Assert.Single(intents).Value);
        told.SetVoidResult();
        await press;
        await answered;
    }

    [Fact] // ADR-0010 (widened 2026-09-29) / ED-22 / ADR-0051: a press that points is answered with the edit open and the keyboard left where it is
    public async Task A_press_that_points_is_answered_with_the_edit_open()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var sheet = RenderGrid(intents);
        await ClickAsync(sheet, 50, 10);
        await PressAsync(sheet, "=");
        var before = HandBacks().Count;
        var focusCalls = Js.FocusCalls;

        await ClickAsync(sheet, 150, 70); // B4
        await sheet.InvokeAsync(() => sheet.Instance.PressAnsweredAsync());

        Assert.Equal("=B4", EditorText(sheet));
        Assert.Empty(intents);
        Assert.Equal(before, HandBacks().Count);
        Assert.Equal(focusCalls, Js.FocusCalls);
    }
}
