using Bunit;
using ExGrid.Cells;
using Microsoft.AspNetCore.Components;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The Cell Editor (ADR-0007/0010): one floating input, uncommitted text the grid's
/// own, a commit that leaves as an intent. The real-keys half — capture-phase claims,
/// IME — is layer 3's. 20px rows, editable Book column of 100px.
/// </summary>
public class CellEditorTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Action<GridEditIntent<TestRow>>? onEdit = null,
        Action<GridPasteIntent>? onPaste = null,
        TestRow[]? rows = null,
        int? totalCount = null,
        Action<PasteRefusalReason>? onPasteRefused = null,
        Action<EditDiscardReason>? onEditDiscarded = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, rows ?? TestRows.Many(50))
              .Add(g => g.TotalCount, totalCount ?? (rows?.Length ?? 50))
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350);
            if (onEdit is not null)
                ps.Add(g => g.OnEdit, onEdit);
            if (onPaste is not null)
                ps.Add(g => g.OnPaste, onPaste);
            if (onPasteRefused is not null)
                ps.Add(g => g.OnPasteRefused, onPasteRefused);
            if (onEditDiscarded is not null)
                ps.Add(g => g.OnEditDiscarded, onEditDiscarded);
        });

    private static Task PressAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, shift, false, false, false));

    private static Task ClickCellAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static Task TypeAsync(IRenderedComponent<ExGrid<TestRow>> cut, string text)
        => cut.Find(".ex-editor").InputAsync(new ChangeEventArgs { Value = text });

    [Fact] // ADR-0010 / ED-1: exactly one editor element, floating — never an input inside a row
    public async Task Typing_opens_one_floating_editor()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 10);
        var rowCounts = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList();

        await PressAsync(cut, "5");

        Assert.Single(cut.FindAll("input.ex-editor"));
        Assert.Empty(cut.FindAll(".ex-row input"));
        // ED-1's other half: editing renders no row.
        Assert.Equal(rowCounts, cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList());
        // And the root says so, for the reserved class's consumers (ADR-0029).
        Assert.Contains("ex-editing", cut.Find(".ex-grid").ClassName);
    }

    [Fact] // ADR-0010 / ED-4: the character that started Overwrite mode is not lost
    public async Task The_first_character_opens_the_editor_containing_it()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 10);

        await PressAsync(cut, "5");

        Assert.Equal("5", cut.Find(".ex-editor").GetAttribute("value"));
    }

    [Fact] // ADR-0010: F2 enters Caret with the current value kept
    public async Task F2_opens_caret_with_the_current_value()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 10);

        await PressAsync(cut, "F2");

        Assert.Equal("Row 000000", cut.Find(".ex-editor").GetAttribute("value"));
    }

    [Fact] // ADR-0007 / ED-5: a commit leaves as an intent; the grid holds no committed value
    public async Task A_commit_is_an_intent_and_paints_nothing()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(onEdit: intents.Add);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "5");
        await TypeAsync(cut, "5x");

        await PressAsync(cut, "Enter");

        var intent = Assert.Single(intents);
        Assert.Equal("Book", intent.Column);
        Assert.Equal("5x", intent.Value);
        Assert.Equal("Row 000000", intent.Row.Book);
        // The painted value is unchanged until a new row instance arrives.
        Assert.Contains("Row 000000", cut.FindAll(".ex-row")[0].TextContent);
        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0010: Enter commits and moves down; Tab commits and moves right
    public async Task Enter_and_tab_commit_and_move()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(onEdit: _ => { });
        cut.Render(ps => ps.Add(g => g.SelectionChanged, (GridSelection s) => selection = s));
        await ClickCellAsync(cut, 50, 10);

        await PressAsync(cut, "5");
        await PressAsync(cut, "Enter");
        Assert.Equal(new CellPosition(1, 0), selection!.Focus);

        await PressAsync(cut, "7");
        await PressAsync(cut, "Tab");
        Assert.Equal(new CellPosition(1, 1), selection.Focus);
    }

    [Fact] // ADR-0010: in Overwrite an arrow commits and moves to the neighbouring cell
    public async Task An_arrow_in_overwrite_commits_and_moves()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        GridSelection? selection = null;
        var cut = RenderGrid(onEdit: intents.Add);
        cut.Render(ps => ps.Add(g => g.SelectionChanged, (GridSelection s) => selection = s));
        await ClickCellAsync(cut, 50, 10);

        await PressAsync(cut, "5");
        await PressAsync(cut, "ArrowDown");

        Assert.Single(intents);
        Assert.Equal(new CellPosition(1, 0), selection!.Focus);
        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0007: Escape cancels — the uncommitted text dies with the editor
    public async Task Escape_cancels_without_an_intent()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(onEdit: intents.Add);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "5");

        await PressAsync(cut, "Escape");

        Assert.Empty(intents);
        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0007 / ED-6: uncommitted text survives a re-render of the cell underneath
    public async Task The_text_survives_a_window_pushed_mid_edit()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "5");
        await TypeAsync(cut, "5 edited");

        cut.Render(ps => ps.Add(g => g.Window, TestRows.Many(50)));

        Assert.Equal("5 edited", cut.Find(".ex-editor").GetAttribute("value"));
    }

    [Fact] // ADR-0028 / ED-7: a density change mid-edit moves the editor with its cell
    public async Task A_geometry_change_moves_the_editor_and_keeps_the_text()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 30); // row 1
        await PressAsync(cut, "5");
        Assert.Contains("top: 20px", cut.Find(".ex-editor").GetAttribute("style"));

        cut.Render(ps => ps.Add(g => g.RowHeight, 30d));

        Assert.Contains("top: 30px", cut.Find(".ex-editor").GetAttribute("style"));
        Assert.Contains("height: 30px", cut.Find(".ex-editor").GetAttribute("style"));
        Assert.Equal("5", cut.Find(".ex-editor").GetAttribute("value"));
    }

    [Fact] // ADR-0012 / ED-8: editing does not start on a Placeholder
    public async Task Editing_waits_for_the_row()
    {
        // The Window holds nothing; every painted position is a Placeholder.
        var cut = RenderGrid(rows: [], totalCount: 50);
        await PressAsync(cut, "ArrowDown"); // places the Focus

        await PressAsync(cut, "5");

        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0020: a column that did not opt in does not edit
    public async Task A_non_editable_column_does_not_open_the_editor()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 150, 10); // Amount, not editable

        await PressAsync(cut, "5");
        await PressAsync(cut, "F2");

        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0007/0014 / ED-10: Ctrl+Enter fills the selection as one intent
    public async Task Ctrl_enter_fills_the_selection_as_one_intent()
    {
        var pastes = new List<GridPasteIntent>();
        var cut = RenderGrid(onPaste: pastes.Add);
        await ClickCellAsync(cut, 50, 10);
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, true, false, false, false));
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, true, false, false, false));
        // A 3×1 selection; typing opens the editor on its Focus... typing collapses?
        await PressAsync(cut, "9");

        await PressAsync(cut, "Enter", ctrl: true);

        var fill = Assert.Single(pastes);
        Assert.Equal(3, fill.CellCount);
        Assert.Equal("9", fill.ValueFor(new CellPosition(2, 0)));
    }

    [Fact] // ADR-0010: F2 moves between Overwrite and Caret
    public async Task F2_toggles_the_mode_and_keeps_the_text()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        GridSelection? selection = null;
        var cut = RenderGrid(onEdit: intents.Add);
        cut.Render(ps => ps.Add(g => g.SelectionChanged, (GridSelection s) => selection = s));
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "5");

        await PressAsync(cut, "F2"); // now Caret: an arrow would belong to the editor
        Assert.Equal("5", cut.Find(".ex-editor").GetAttribute("value"));

        await PressAsync(cut, "F2"); // back to Overwrite: the arrow commits and moves
        await PressAsync(cut, "ArrowRight");
        Assert.Single(intents);
        Assert.Equal(new CellPosition(0, 1), selection!.Focus);
    }


    [Fact] // ADR-0010: Excel's click-away — a press on another cell commits, then selects
    public async Task A_click_on_another_cell_commits_the_edit()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(onEdit: intents.Add);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "5");
        await TypeAsync(cut, "5x");

        await ClickCellAsync(cut, 50, 50);

        var intent = Assert.Single(intents);
        Assert.Equal("Row 000000", intent.Row.Book);
        Assert.Equal("5x", intent.Value);
        Assert.Empty(cut.FindAll(".ex-editor"));
        // And the press kept its own meaning: the Focus stands on the clicked cell.
        Assert.EndsWith("r2c0", cut.Find(".ex-grid").GetAttribute("aria-activedescendant"));
    }

    [Fact] // ADR-0010: a header press mid-edit commits before it sorts or grabs a column
    public async Task A_header_press_commits_the_edit()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(onEdit: intents.Add);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "5");

        await cut.Find(".ex-header").MouseDownAsync(
            new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 30, OffsetY = 10 });

        Assert.Single(intents);
        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    [Fact] // A caret-placing press inside the editor stays the editor's: it must never
           // bubble to the delegated viewport, whose arithmetic would read the input's
           // own offsets as viewport coordinates and re-select a wrong cell
    public async Task A_press_inside_the_editor_never_reaches_the_viewport()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "5");
        var before = cut.Find(".ex-grid").GetAttribute("aria-activedescendant");

        // stopPropagation leaves the press with no handler anywhere on its path — the
        // exception is the assertion.
        await Assert.ThrowsAsync<MissingEventHandlerException>(() =>
            cut.Find("input.ex-editor").MouseDownAsync(
                new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 3, OffsetY = 3 }));

        Assert.Single(cut.FindAll("input.ex-editor"));
        Assert.Equal(before, cut.Find(".ex-grid").GetAttribute("aria-activedescendant"));
    }

    [Fact] // An AltGr character — Control+Alt held together on Windows — is typing, not a chord
    public async Task An_altgr_character_opens_the_editor()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 10);

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
            "@", ctrl: true, shift: false, alt: true, meta: false, metaIsPrimary: false));

        Assert.Equal("@", cut.Find(".ex-editor").GetAttribute("value"));
    }

    [Fact] // Either modifier alone stays a shortcut and opens nothing
    public async Task A_lone_modifier_chord_does_not_open_the_editor()
    {
        var cut = RenderGrid();
        await ClickCellAsync(cut, 50, 10);

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
            "q", ctrl: true, shift: false, alt: false, meta: false, metaIsPrimary: false));
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
            "q", ctrl: false, shift: false, alt: true, meta: false, metaIsPrimary: false));

        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0035 / CP-16: a fill whose selection crosses a non-editable column is refused whole
    public async Task Ctrl_enter_fill_across_a_non_editable_column_is_refused()
    {
        var pastes = new List<GridPasteIntent>();
        PasteRefusalReason? refused = null;
        var cut = RenderGrid(onPaste: pastes.Add, onPasteRefused: r => refused = r);
        await ClickCellAsync(cut, 150, 10);                      // Amount, not editable
        // Extend left so the Focus lands on Book: the editor opens on an editable cell,
        // and the selection still covers Amount — the case that used to write anyway.
        await PressAsync(cut, "ArrowLeft", shift: true);
        await PressAsync(cut, "9");

        await PressAsync(cut, "Enter", ctrl: true);

        Assert.Empty(pastes);
        // And it is no longer silent: the refusal reaches the Consumer (ADR-0035).
        Assert.Equal(PasteRefusalReason.TargetNotEditable, refused);
    }

    [Fact] // ADR-0011 / ED-21: a sort landing under an open editor takes the typing, and says so
    public async Task Text_discarded_because_the_order_changed_is_announced()
    {
        EditDiscardReason? discarded = null;
        var cut = RenderGrid(onEditDiscarded: r => discarded = r);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "9");
        Assert.NotEmpty(cut.FindAll(".ex-editor"));

        cut.Render(ps => ps.Add(g => g.RowSequenceVersion, 1));

        Assert.Empty(cut.FindAll(".ex-editor"));
        Assert.Equal(EditDiscardReason.OrderChanged, discarded);
    }

    [Fact] // ADR-0011 / ED-21: the row left the Window before the commit landed, so there is no identity
    public async Task Text_discarded_because_the_row_left_the_window_is_announced()
    {
        EditDiscardReason? discarded = null;
        var edits = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(onEdit: edits.Add, totalCount: 500, onEditDiscarded: r => discarded = r);
        await ClickCellAsync(cut, 50, 10);
        await PressAsync(cut, "9");

        // The Window moves on beneath the open editor, with the order unchanged — so the
        // selection is kept (ADR-0011) and only the row instance is gone.
        cut.Render(ps => ps.Add(g => g.WindowStart, 100).Add(g => g.Window, TestRows.Many(50)));
        await PressAsync(cut, "Enter");

        Assert.Empty(edits);
        Assert.Equal(EditDiscardReason.RowLeftTheWindow, discarded);
    }

    [Fact] // ADR-0035 / ED-19: the refusal judged the operation, so it does not take the text with it
    public async Task A_refused_fill_holds_the_editor_and_enter_still_commits_the_one_cell()
    {
        var edits = new List<GridEditIntent<TestRow>>();
        var pastes = new List<GridPasteIntent>();
        var cut = RenderGrid(onEdit: edits.Add, onPaste: pastes.Add);
        await ClickCellAsync(cut, 150, 10);                      // Amount, not editable
        await PressAsync(cut, "ArrowLeft", shift: true);         // Focus on Book, selection still covers Amount
        await PressAsync(cut, "9");

        await PressAsync(cut, "Enter", ctrl: true);

        Assert.Empty(pastes);
        Assert.NotEmpty(cut.FindAll(".ex-editor"));

        // Enter is a gesture the refusal never named, and the cell the editor opened on
        // is editable by construction — so it commits, carrying the text that survived.
        await PressAsync(cut, "Enter");

        Assert.Empty(cut.FindAll(".ex-editor"));
        Assert.Equal("9", Assert.Single(edits).Value);
    }
}
