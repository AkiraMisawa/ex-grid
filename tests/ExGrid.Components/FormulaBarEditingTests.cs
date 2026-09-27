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
/// The Formula Bar as the Cell Editor's second surface, and the Name Box placing the
/// Selection (ADR-0051, ADR-0050 item 4; DC-11, DC-22). One uncommitted text shown in two
/// places; a commit from either raises one Edit Intent; Escape from either cancels once.
/// 50 rows of 20px in a 350 × 200 Viewport; Book is editable, Amount is not.
/// </summary>
public class FormulaBarEditingTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    private static string? Entry(TestRow row, GridColumn<TestRow> column)
        => column.Name == "Book" && row.Book == "Row 000002" ? "=A1*2" : null;

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        List<GridEditIntent<TestRow>>? intents = null,
        Action<GridSelection>? onSelection = null,
        Action<string>? onNameBox = null,
        Action<Bunit.ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? extra = null,
        GridColumn<TestRow>[]? columns = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, columns ?? Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.ShowFormulaBar, true)
              .Add(g => g.EditorTextOf, Entry)
              .Add(g => g.NameBoxLabel, cell => FormattableString.Invariant($"R{cell.Row + 1}C{cell.Column + 1}"))
              .Add(g => g.OnEdit, (GridEditIntent<TestRow> intent) => intents?.Add(intent))
              .Add(g => g.SelectionChanged, onSelection ?? (_ => { }));
            if (onNameBox is not null)
                ps.Add(g => g.OnNameBoxEntered, onNameBox);
            extra?.Invoke(ps);
        });

    private static Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    // Keys typed in the bar reach the listener as a descendant's (not the root's).
    private static Task PressInBarAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, false, false, false, false, false, fromDescendant: true));

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, false, false, false, false, false));

    private static AngleSharp.Dom.IElement Bar(IRenderedComponent<ExGrid<TestRow>> cut) => cut.Find(".ex-formula-bar-text");

    private static string CellEditorText(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-viewport .ex-editor").GetAttribute("value") ?? "";

    private List<string> EditingModesTold()
        => [.. JSInterop.Invocations.Where(i => i.Identifier == "setEditing").Select(i => (string)i.Arguments[0]!)];

    [Fact] // ADR-0051/0018 / DC-22: the bar's field is an editor surface inside the root, where the listener gates it
    public void The_bar_field_is_marked_as_an_editor_surface_inside_the_root()
    {
        var cut = RenderGrid();

        var field = cut.Find(".ex-grid > .ex-formula-bar > input.ex-editor.ex-formula-bar-text");
        Assert.Equal("-1", field.GetAttribute("tabindex"));
        // Written after the scroller: the Cell Editor stays the first editor surface in the markup.
        Assert.Empty(cut.FindAll(".ex-scroller ~ .ex-scroller"));
        Assert.NotNull(cut.Find(".ex-scroller ~ .ex-formula-bar"));
    }

    [Fact] // ADR-0051 / DC-22: a press into the bar opens Caret on the opening text, and both surfaces show it
    public async Task Focusing_the_bar_opens_caret_on_the_opening_text()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 45);

        await Bar(cut).FocusAsync(new FocusEventArgs());

        Assert.Equal("=A1*2", Bar(cut).GetAttribute("value"));
        Assert.Equal("=A1*2", CellEditorText(cut));
        Assert.Equal("caret", EditingModesTold()[^1]);
    }

    [Fact] // ADR-0051 / DC-22: typing in either surface updates the other
    public async Task Both_surfaces_agree_after_every_keystroke()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 45);
        await Bar(cut).FocusAsync(new FocusEventArgs());

        await Bar(cut).InputAsync(new ChangeEventArgs { Value = "=A1*3" });
        Assert.Equal("=A1*3", CellEditorText(cut));

        await cut.Find(".ex-viewport .ex-editor").InputAsync(new ChangeEventArgs { Value = "=A1*30" });
        Assert.Equal("=A1*30", Bar(cut).GetAttribute("value"));
    }

    [Fact] // ADR-0051/0007 / DC-22: Enter in the bar commits once — one Edit Intent — and moves as Enter does
    public async Task Enter_in_the_bar_raises_one_intent()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        GridSelection? selection = null;
        var cut = RenderGrid(intents, s => selection = s);
        await ClickAsync(cut, 50, 45);
        await Bar(cut).FocusAsync(new FocusEventArgs());
        await Bar(cut).InputAsync(new ChangeEventArgs { Value = "=A1*3" });

        await PressInBarAsync(cut, "Enter");

        var intent = Assert.Single(intents);
        Assert.Equal("=A1*3", intent.Value);
        Assert.Equal("Row 000002", intent.Row.Book);
        Assert.Empty(cut.FindAll(".ex-viewport .ex-editor"));
        Assert.Equal(new CellPosition(3, 0), selection!.Focus);
    }

    [Fact] // ADR-0051/0007 / DC-22: a commit from the cell's surface is the same one commit, with the bar's text
    public async Task Enter_in_the_cell_after_typing_in_the_bar_raises_one_intent()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents);
        await ClickAsync(cut, 50, 45);
        await PressAsync(cut, "F2");
        await Bar(cut).FocusAsync(new FocusEventArgs());
        await Bar(cut).InputAsync(new ChangeEventArgs { Value = "=B1" });

        await PressAsync(cut, "Enter");

        Assert.Equal("=B1", Assert.Single(intents).Value);
    }

    [Fact] // ADR-0051 / DC-22: Escape from the bar cancels once — no intent, and the next Escape only returns the keyboard
    public async Task Escape_from_the_bar_cancels_once()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var discarded = new List<EditDiscardReason>();
        var cut = RenderGrid(intents, extra: ps => ps.Add(g => g.OnEditDiscarded, discarded.Add));
        await ClickAsync(cut, 50, 45);
        await Bar(cut).FocusAsync(new FocusEventArgs());
        await Bar(cut).InputAsync(new ChangeEventArgs { Value = "typed" });

        await PressInBarAsync(cut, "Escape");

        Assert.Empty(intents);
        Assert.Empty(cut.FindAll(".ex-viewport .ex-editor"));
        Assert.Equal("=A1*2", Bar(cut).GetAttribute("value"));
        Assert.Equal("none", EditingModesTold()[^1]);

        await PressInBarAsync(cut, "Escape");

        Assert.Empty(intents);
        Assert.Empty(discarded);
        Assert.Equal(0, Js.BlurCount);
    }

    [Fact] // ADR-0051/0035: a Focus cell that does not edit leaves the bar read-only and opens nothing
    public async Task A_cell_that_does_not_edit_leaves_the_bar_read_only()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 150, 45);

        Assert.True(Bar(cut).HasAttribute("readonly"));
        await Bar(cut).FocusAsync(new FocusEventArgs());

        Assert.Empty(cut.FindAll(".ex-viewport .ex-editor"));
        Assert.DoesNotContain("caret", EditingModesTold());
    }

    [Fact] // ADR-0051/0010: a press into the Name Box commits an open edit first
    public async Task The_name_box_commits_an_open_edit()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents);
        await ClickAsync(cut, 50, 45);
        await PressAsync(cut, "7");

        await cut.Find(".ex-name-box").FocusAsync(new FocusEventArgs());

        Assert.Equal("7", Assert.Single(intents).Value);
        Assert.Empty(cut.FindAll(".ex-viewport .ex-editor"));
    }

    private int RootFocusCalls()
        => JSInterop.Invocations.Count(i => i.Identifier == "Blazor._internal.domWrapper.focus");

    [Fact] // ADR-0010/0051: the press into the Name Box keeps its own meaning — the commit does not take the keyboard back to the root
    public async Task Committing_by_a_press_into_the_name_box_leaves_the_keyboard_there()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents);
        await ClickAsync(cut, 50, 45);
        await PressAsync(cut, "7");
        var before = RootFocusCalls();

        await cut.Find(".ex-name-box").FocusAsync(new FocusEventArgs());

        // Were the root focused here, the address typed next would land on the grid, open
        // Overwrite on the Focus cell and be committed into it instead of navigating.
        Assert.Single(intents);
        Assert.Equal(before, RootFocusCalls());
    }

    [Fact] // ADR-0051 / ADR-0050 item 4: with an edit open, an address typed into the Name Box navigates, and the edit commits once
    public async Task An_address_entered_with_an_edit_open_navigates()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        GridSelection? selection = null;
        IRenderedComponent<ExGrid<TestRow>>? cut = null;
        cut = RenderGrid(intents, onSelection: s => selection = s, onNameBox: text =>
        {
            Assert.Equal("R41C1", text);
            _ = cut!.Instance.PlaceSelectionAsync(new SelectionRange(40, 0, 1, 1), new CellPosition(40, 0), 0);
        });
        await ClickAsync(cut, 50, 45);
        await PressAsync(cut, "7");

        await cut.Find(".ex-name-box").FocusAsync(new FocusEventArgs());
        await cut.Find(".ex-name-box").InputAsync(new ChangeEventArgs { Value = "R41C1" });
        await cut.Find(".ex-name-box-form").SubmitAsync();

        Assert.Equal(["7"], intents.Select(i => i.Value));
        Assert.Equal(new CellPosition(40, 0), selection!.Focus);
        Assert.Empty(cut.FindAll(".ex-viewport .ex-editor"));
    }

    [Fact] // ADR-0034/0010: a Reject holds the editor, and the press into the Name Box does not keep its meaning
    public async Task A_rejected_commit_takes_the_keyboard_back_from_the_name_box()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents, columns:
        [
            new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true, validate: (_, _) => EditVerdict.Reject("no")),
            new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
        ]);
        await ClickAsync(cut, 50, 45);
        await PressAsync(cut, "7");

        await cut.Find(".ex-name-box").FocusAsync(new FocusEventArgs());

        Assert.Empty(intents);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".ex-viewport .ex-editor")));
    }

    [Fact] // ADR-0051 / ADR-0050 item 4 / DC-11: the Name Box hands its text over, and the placement lands and scrolls
    public async Task The_name_box_moves_the_selection_and_the_focus_and_scrolls()
    {
        GridSelection? selection = null;
        IRenderedComponent<ExGrid<TestRow>>? cut = null;
        var entered = new List<string>();
        cut = RenderGrid(onSelection: s => selection = s, onNameBox: text =>
        {
            entered.Add(text);
            // The Consumer resolves "R41C2" to a position and asks the grid to place it.
            _ = cut!.Instance.PlaceSelectionAsync(new SelectionRange(40, 1, 1, 1), new CellPosition(40, 1), 0);
        });
        await ClickAsync(cut, 50, 5);

        await cut.Find(".ex-name-box").InputAsync(new ChangeEventArgs { Value = "R41C2" });
        Assert.Equal("R41C2", cut.Find(".ex-name-box").GetAttribute("value"));
        await cut.Find(".ex-name-box-form").SubmitAsync();

        Assert.Equal(["R41C2"], entered);
        Assert.Equal(new CellPosition(40, 1), selection!.Focus);
        Assert.Equal("R41C2", cut.Find(".ex-name-box").GetAttribute("value"));
        // Row 40 is past the 160px of rows: the Focus is scrolled into view.
        Assert.NotEmpty(Js.ScrolledTo);
        Assert.True(Js.ScrolledTo[^1].Top > 0);
    }

    [Fact] // ADR-0050 item 4/0033 / DC-11: a placed range is the Selection, and its extent is announced once it settles
    public async Task A_placed_range_is_selected_and_announced()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(onSelection: s => selection = s);

        var placed = await cut.Instance.PlaceSelectionAsync(new SelectionRange(2, 0, 3, 2), new CellPosition(2, 0), 0);
        await cut.InvokeAsync(() => Clock.Advance(TimeSpan.FromMilliseconds(200)));

        Assert.True(placed);
        Assert.Equal([new SelectionRange(2, 0, 3, 2)], selection!.Ranges);
        Assert.Equal(new CellPosition(2, 0), selection.Anchor);
        Assert.NotEqual("", cut.Find(".ex-announce").TextContent);
    }

    [Fact] // ADR-0011 / DC-11: a placement made under an older Row Sequence Version is dropped and changes nothing
    public async Task A_stale_placement_is_dropped()
    {
        GridSelection? selection = null;
        var cut = RenderGrid(onSelection: s => selection = s, extra: ps => ps.Add(g => g.RowSequenceVersion, 3));
        await ClickAsync(cut, 50, 25);
        var before = selection;
        var scrolls = Js.ScrolledTo.Count;

        var placed = await cut.Instance.PlaceSelectionAsync(new SelectionRange(10, 0, 1, 1), new CellPosition(10, 0), 2);

        Assert.False(placed);
        Assert.Same(before, selection);
        Assert.Equal(new CellPosition(1, 0), selection!.Focus);
        Assert.Equal(scrolls, Js.ScrolledTo.Count);
    }

    [Fact] // ADR-0050 item 4: a placement outside the grid is refused by name, not clamped
    public async Task A_placement_outside_the_grid_is_refused()
    {
        var cut = RenderGrid();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => cut.Instance.PlaceSelectionAsync(new SelectionRange(49, 1, 2, 1), new CellPosition(49, 1), 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => cut.Instance.PlaceSelectionAsync(new SelectionRange(4, 0, 2, 1), new CellPosition(9, 0), 0));
    }

    [Fact] // ADR-0003: editing through the bar renders no row
    public async Task Editing_in_the_bar_renders_no_row()
    {
        var cut = RenderGrid(new List<GridEditIntent<TestRow>>());
        await ClickAsync(cut, 50, 45);
        var before = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList();

        await Bar(cut).FocusAsync(new FocusEventArgs());
        await Bar(cut).InputAsync(new ChangeEventArgs { Value = "=A1*3" });
        await PressInBarAsync(cut, "Escape");

        Assert.Equal(before, cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList());
    }
}
