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
/// Completion as the tenth Windows run saw it (ADR-0058, "What the tenth Windows run settled";
/// ADR-0051's correction of 2026-09-30; SH-36), on the grid's side: Tab closes the list, and the
/// text it wrote is asked about for its hint alone; a list takes only ↑, ↓, Tab and Escape, so
/// where a Reference can go at the caret ← and → point past it and close it, and the gate is told
/// so from the render that paints the list. As Part B of the ninth Windows run saw it (Q51), Home,
/// End and the Shift+arrows do the same there, and stay the editor's in a list of names and in
/// Caret. The Consumer here stands in for ExSheet: a name after
/// <c>=</c>, completed to itself and listed again once written whole, as a table's name is; an
/// argument's values after <c>,,</c>; a hint inside <c>F(</c>. 50 rows of 20px under a 20px
/// header; A, B and C edit.
/// </summary>
public class CompletionOverPointTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("A", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("B", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("C", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
    ];

    /// <summary>
    /// <c>Positions</c> for any letters that begin it, the name itself included, replacing them
    /// with the whole name; <c>0 - Exact</c> and <c>1 - Next</c> for an argument after <c>,,</c>
    /// holding nothing yet; and inside <c>F(</c>, its hint.
    /// </summary>
    private static EditorCompletion? Answer(string text, int caret)
    {
        if (!text.StartsWith('='))
            return null;
        var hint = text.StartsWith("=F(", StringComparison.Ordinal) ? new EditorHint("F(value, [mode])") : null;
        if (text[..caret].EndsWith(",,", StringComparison.Ordinal))
            return new EditorCompletion([new("0 - Exact", caret, 0, "0"), new("1 - Next", caret, 0, "1")], hint);
        var start = caret;
        while (start > 1 && char.IsLetter(text[start - 1]))
            start--;
        var typed = text[start..caret];
        if (typed.Length > 0 && "Positions".StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            return new EditorCompletion([new("Positions", start, typed.Length, "Positions")], hint);
        return hint is null ? null : new EditorCompletion([], hint);
    }

    private static bool PointAt(string text, int caret)
        => text.StartsWith('=') && caret > 0 && caret <= text.Length && "=+-*/(,".Contains(text[caret - 1]);

    private static string ReferenceText(SelectionRange range)
    {
        static string Cell(int row, int column) => FormattableString.Invariant($"{(char)('A' + column)}{row + 1}");
        var first = Cell(range.TopRow, range.LeftColumn);
        return range.CellCount == 1 ? first : first + ":" + Cell(range.BottomRow, range.RightColumn);
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        List<(string, int)>? asked = null, List<GridEditIntent<TestRow>>? intents = null, bool bar = false)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(50))
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.ShowFormulaBar, bar)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> intent) => intents?.Add(intent))
            .Add(g => g.CompleteEditorText, (text, caret) =>
            {
                asked?.Add((text, caret));
                return ValueTask.FromResult(Answer(text, caret));
            })
            .Add(g => g.PointAt, PointAt)
            .Add(g => g.ReferenceText, ReferenceText));

    /// <summary>Opens Overwrite on B1 (on B<paramref name="row"/> + 1) with <c>=</c>, then types the
    /// rest into the Cell Editor.</summary>
    private static async Task TypeFormulaAsync(IRenderedComponent<ExGrid<TestRow>> cut, string text, int row = 0)
    {
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 150, OffsetY = 10 + 20 * row });
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("=", false, false, false, false, false));
        await TypeAsync(cut, text);
    }

    /// <summary>Blazor's input event, then the listener's report of the caret at the end.</summary>
    private static async Task TypeAsync(IRenderedComponent<ExGrid<TestRow>> cut, string text)
    {
        await cut.Find(".ex-viewport .ex-editor").InputAsync(new ChangeEventArgs { Value = text });
        await cut.InvokeAsync(() => cut.Instance.OnEditorCaretAsync(text, text.Length));
    }

    /// <summary>A key as the gate forwards it while editing: with the editor's text and caret.
    /// <c>Shift+</c> before the key presses Shift with it.</summary>
    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, string text, int? caret = null)
    {
        var shift = key.StartsWith("Shift+", StringComparison.Ordinal);
        return cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
            shift ? key["Shift+".Length..] : key, false, shift, false, false, false, editorText: text, editorCaret: caret ?? text.Length));
    }

    private static string EditorText(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-viewport .ex-editor").GetAttribute("value") ?? "";

    private static List<string> Labels(IRenderedComponent<ExGrid<TestRow>> cut)
        => [.. cut.FindAll(".ex-completion .ex-completion-item").Select(item => item.TextContent)];

    private List<string> GateModesTold()
        => [.. JSInterop.Invocations.Where(i => i.Identifier == "setEditing").Select(i => (string)i.Arguments[0]!)];

    // ---- Tab closes the list -----------------------------------------------------------------

    [Fact] // ADR-0058 (the tenth Windows run, case 5) / SH-36: Tab on a name closes the list, and the grid does not open it again on the name it wrote, which the Consumer still lists
    public async Task ADR0058_tab_closes_the_list_and_it_is_not_opened_again_on_what_tab_wrote()
    {
        var asked = new List<(string, int)>();
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(asked, intents);
        await TypeFormulaAsync(cut, "=Posit");
        Assert.Equal(["Positions"], Labels(cut));

        await PressAsync(cut, "Tab", "=Posit");

        Assert.Equal("=Positions", EditorText(cut));
        // Asked about what Tab wrote, whose answer lists the name again: no list is shown for it.
        Assert.Equal(("=Positions", 10), asked[^1]);
        Assert.NotNull(Answer("=Positions", 10));
        Assert.Empty(cut.FindAll(".ex-completion"));
        Assert.Equal("overwrite", GateModesTold()[^1]);
        Assert.Empty(intents);

        // So Tab moves on: with no list open, it commits (ADR-0012).
        await PressAsync(cut, "Tab", "=Positions");
        Assert.Equal("=Positions", Assert.Single(intents).Value);
    }

    [Fact] // ADR-0058 (the tenth Windows run, cases 3 and 4) / ADR-0051 / SH-36: Tab closes the list, and the hint for what it wrote still follows
    public async Task ADR0058_after_tab_the_hint_stays_and_the_list_does_not()
    {
        var cut = RenderGrid();
        await TypeFormulaAsync(cut, "=F(1,Posi");
        Assert.Equal(["Positions"], Labels(cut));

        await PressAsync(cut, "Tab", "=F(1,Posi");

        Assert.Equal("=F(1,Positions", EditorText(cut));
        Assert.Empty(Labels(cut));
        Assert.Equal("F(value, [mode])", cut.Find(".ex-completion .ex-completion-hint").TextContent);
        Assert.False(cut.Find(".ex-completion").HasAttribute("data-ex-list"));
    }

    [Fact] // ADR-0058 / ADR-0051 / SH-36: only the text Tab wrote goes unlisted — the text typed next is listed again, as Backspace back into a name is
    public async Task ADR0058_the_text_typed_after_tab_is_listed_again()
    {
        var cut = RenderGrid();
        await TypeFormulaAsync(cut, "=Posit");
        await PressAsync(cut, "Tab", "=Posit");
        Assert.Empty(Labels(cut));

        await TypeAsync(cut, "=Position");

        Assert.Equal(["Positions"], Labels(cut));
    }

    [Fact] // ADR-0058 / SH-36: a press on a candidate accepts it as Tab does, and closes the list
    public async Task ADR0058_a_press_on_a_candidate_closes_the_list()
    {
        var cut = RenderGrid();
        await TypeFormulaAsync(cut, "=Posit");

        await cut.Find(".ex-completion-item").MouseDownAsync(new MouseEventArgs { Button = 0 });

        Assert.Equal("=Positions", EditorText(cut));
        Assert.Empty(cut.FindAll(".ex-completion"));
    }

    [Fact] // ADR-0058 / DC-18: an answer that arrives later for the text Tab wrote shows its hint alone
    public async Task ADR0058_a_late_answer_for_what_tab_wrote_shows_no_list()
    {
        var late = new TaskCompletionSource<EditorCompletion?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(50))
            .Add(g => g.TotalCount, 50)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.CompleteEditorText, (text, caret) => text == "=F(Positions"
                ? new ValueTask<EditorCompletion?>(late.Task)
                : ValueTask.FromResult(Answer(text, caret))));
        await TypeFormulaAsync(cut, "=F(Pos");
        Assert.Equal(["Positions"], Labels(cut));

        await PressAsync(cut, "Tab", "=F(Pos");
        late.SetResult(Answer("=F(Positions", 12));

        cut.WaitForAssertion(() => Assert.Equal("F(value, [mode])", cut.Find(".ex-completion .ex-completion-hint").TextContent));
        Assert.Empty(Labels(cut));
    }

    // ---- ← and → at a list open over Point ---------------------------------------------------

    [Fact] // ADR-0058 (the tenth Windows run, case 6) / SH-36: at a list open where a Reference can go, the box is marked for the gate in the render that paints it, and the gate is told so
    public async Task ADR0058_a_list_over_point_is_marked_and_told_to_the_gate()
    {
        var cut = RenderGrid();

        await TypeFormulaAsync(cut, "=F(1,,");

        Assert.Equal(["0 - Exact", "1 - Next"], Labels(cut));
        var box = cut.Find(".ex-completion");
        Assert.True(box.HasAttribute("data-ex-list"));
        Assert.True(box.HasAttribute("data-ex-over-point"));
        Assert.Equal("completionOverPoint", GateModesTold()[^1]);
    }

    [Fact] // ADR-0058 / ADR-0051 second round / SH-36: a list of names, where no Reference can go, is not marked, and ← and → are left to the editor
    public async Task ADR0058_a_list_of_names_is_not_over_point()
    {
        var cut = RenderGrid();

        await TypeFormulaAsync(cut, "=Pos");

        var box = cut.Find(".ex-completion");
        Assert.True(box.HasAttribute("data-ex-list"));
        Assert.False(box.HasAttribute("data-ex-over-point"));
        Assert.Equal("completion", GateModesTold()[^1]);
    }

    [Theory] // ADR-0058 (the tenth Windows run, case 6) / SH-36: → at a list open over Point points and closes the list — so does ← — and the Reference is the one being written
    [InlineData("ArrowRight", "C1")]
    [InlineData("ArrowLeft", "A1")]
    public async Task ADR0058_an_arrow_at_a_list_over_point_points_and_closes_it(string key, string pointed)
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents: intents);
        await TypeFormulaAsync(cut, "=F(1,,");

        await PressAsync(cut, key, "=F(1,,");

        Assert.Equal("=F(1,," + pointed, EditorText(cut));
        Assert.Empty(Labels(cut));
        Assert.NotEmpty(cut.FindAll(".ex-point"));
        Assert.Equal("point", GateModesTold()[^1]);
        Assert.Empty(intents);
    }

    [Theory] // ADR-0058 (Part B of the ninth Windows run, x6; Q51) / SH-36: a Shift+arrow at a list open over Point closes it and does what Point does with it — the outline starts on the edited cell and reaches the next one
    [InlineData("Shift+ArrowRight", "B2:C2")]
    [InlineData("Shift+ArrowLeft", "A2:B2")]
    [InlineData("Shift+ArrowDown", "B2:B3")]
    [InlineData("Shift+ArrowUp", "B1:B2")]
    public async Task ADR0058_a_shift_arrow_at_a_list_over_point_points_at_a_range_and_closes_it(string key, string pointed)
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents: intents);
        await TypeFormulaAsync(cut, "=F(1,,", row: 1);
        Assert.Equal("completionOverPoint", GateModesTold()[^1]);

        await PressAsync(cut, key, "=F(1,,");

        Assert.Equal("=F(1,," + pointed, EditorText(cut));
        Assert.Empty(Labels(cut));
        Assert.NotEmpty(cut.FindAll(".ex-point"));
        Assert.Equal("point", GateModesTold()[^1]);
        Assert.Empty(intents);
    }

    [Fact] // ADR-0058 (Part B of the ninth Windows run, x4; Q51, Q53) / SH-36: Home at a list open over Point closes it and points at the row's first column
    public async Task ADR0058_home_at_a_list_over_point_points_at_the_rows_first_column_and_closes_it()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents: intents);
        await TypeFormulaAsync(cut, "=F(1,,", row: 1);

        await PressAsync(cut, "Home", "=F(1,,");

        Assert.Equal("=F(1,,A2", EditorText(cut));
        Assert.Empty(Labels(cut));
        Assert.NotEmpty(cut.FindAll(".ex-point"));
        Assert.Equal("point", GateModesTold()[^1]);
        Assert.Empty(intents);
    }

    [Fact] // ADR-0058 (x5; Q51, Q53) / SH-36: End at a list open over Point closes it and does nothing more — it writes nothing and asks for no commit
    public async Task ADR0058_end_at_a_list_over_point_closes_it_and_writes_nothing()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents: intents);
        await TypeFormulaAsync(cut, "=F(1,,", row: 1);

        await PressAsync(cut, "End", "=F(1,,");

        Assert.Equal("=F(1,,", EditorText(cut));
        Assert.Empty(cut.FindAll(".ex-completion"));
        Assert.Empty(cut.FindAll(".ex-point"));
        Assert.Equal("overwrite", GateModesTold()[^1]);
        Assert.Empty(intents);
        Assert.NotEmpty(cut.FindAll(".ex-viewport .ex-editor"));
    }

    [Theory] // ADR-0058 (Q51) / ADR-0051 second round / SH-36: in a list of names, where no Reference can go, Home, End and the Shift+arrows are not claimed; one that arrives all the same, claimed by a gate not yet told, neither points nor commits
    [InlineData("Home")]
    [InlineData("End")]
    [InlineData("Shift+ArrowRight")]
    [InlineData("Shift+ArrowDown")]
    public async Task ADR0058_in_a_list_of_names_home_end_and_the_shift_arrows_stay_the_editors(string key)
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents: intents);
        await TypeFormulaAsync(cut, "=Pos");
        Assert.Equal("completion", GateModesTold()[^1]);

        await PressAsync(cut, key, "=Pos");

        Assert.Equal("=Pos", EditorText(cut));
        Assert.Empty(cut.FindAll(".ex-point"));
        Assert.Empty(intents);
    }

    [Theory] // ADR-0058 (Q51) / ADR-0051 third round / SH-36: in Caret, a list open where a Reference can go is not over Point, and Home, End and the Shift+arrows stay the editor's there too
    [InlineData("Home")]
    [InlineData("End")]
    [InlineData("Shift+ArrowRight")]
    public async Task ADR0058_in_caret_home_end_and_the_shift_arrows_stay_the_editors(string key)
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents: intents, bar: true);
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 150, OffsetY = 30 });
        await cut.Find(".ex-formula-bar-text").FocusAsync(new FocusEventArgs());
        await cut.Find(".ex-formula-bar-text").InputAsync(new ChangeEventArgs { Value = "=F(1,," });
        await cut.InvokeAsync(() => cut.Instance.OnEditorCaretAsync("=F(1,,", 6));
        Assert.Equal("completion", GateModesTold()[^1]);

        await PressAsync(cut, key, "=F(1,,");

        Assert.Equal("=F(1,,", EditorText(cut));
        Assert.Empty(cut.FindAll(".ex-point"));
        Assert.Empty(intents);
    }

    [Fact] // ADR-0058 / ADR-0051 / SH-36: ↑, ↓, Tab and Escape at a list open over Point are unchanged — they choose, write the value, and close the list, and ↓ points once it is closed
    public async Task ADR0058_up_down_tab_and_escape_at_a_list_over_point_are_unchanged()
    {
        var cut = RenderGrid();
        await TypeFormulaAsync(cut, "=F(1,,");

        await PressAsync(cut, "ArrowDown", "=F(1,,");
        Assert.Equal("1 - Next", cut.Find(".ex-completion-selected").TextContent);
        await PressAsync(cut, "ArrowUp", "=F(1,,");
        Assert.Equal("0 - Exact", cut.Find(".ex-completion-selected").TextContent);
        await PressAsync(cut, "Escape", "=F(1,,");
        Assert.Empty(Labels(cut));
        Assert.Equal("=F(1,,", EditorText(cut));
        await PressAsync(cut, "ArrowDown", "=F(1,,");
        Assert.Equal("=F(1,,B2", EditorText(cut));

        await TypeAsync(cut, "=F(1,,");
        Assert.Equal(["0 - Exact", "1 - Next"], Labels(cut));
        await PressAsync(cut, "ArrowDown", "=F(1,,");
        await PressAsync(cut, "Tab", "=F(1,,");
        Assert.Equal("=F(1,,1", EditorText(cut));
        Assert.Empty(Labels(cut));
    }

    [Fact] // ADR-0058 / ADR-0051 third round / SH-36: in Caret the arrows are the editor's, so a list open where a Reference can go is not over Point there
    public async Task ADR0058_in_caret_a_list_is_not_over_point()
    {
        var cut = RenderGrid(bar: true);
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 150, OffsetY = 30 });
        await cut.Find(".ex-formula-bar-text").FocusAsync(new FocusEventArgs());
        Assert.Equal("caret", GateModesTold()[^1]);

        await cut.Find(".ex-formula-bar-text").InputAsync(new ChangeEventArgs { Value = "=F(1,," });
        await cut.InvokeAsync(() => cut.Instance.OnEditorCaretAsync("=F(1,,", 6));

        Assert.Equal(["0 - Exact", "1 - Next"], Labels(cut));
        Assert.False(cut.Find(".ex-completion").HasAttribute("data-ex-over-point"));
        Assert.Equal("completion", GateModesTold()[^1]);
    }

    [Fact] // ADR-0058 / ADR-0051: every decision at a keystroke is made from the text it carries — a → claimed over Point that carries a name typed since only closes that name's list, and neither points nor commits
    public async Task ADR0058_an_arrow_carrying_text_where_no_reference_can_go_only_closes_the_list()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents: intents);
        await TypeFormulaAsync(cut, "=F(1,,");
        Assert.Equal("completionOverPoint", GateModesTold()[^1]);

        // Claimed while the list stood over Point; it carries "Pos", typed before the core heard it.
        await PressAsync(cut, "ArrowRight", "=F(1,,Pos");

        Assert.Equal("=F(1,,Pos", EditorText(cut));
        Assert.Empty(Labels(cut));
        Assert.Empty(cut.FindAll(".ex-point"));
        Assert.Empty(intents);
    }

    // ---- What Point writes is shown --------------------------------------------------------------

    private (string Text, int Caret, int End)? CaretPlaced()
        => JSInterop.Invocations.LastOrDefault(i => i.Identifier == "setCaret") is { } call
            ? ((string)call.Arguments[0]!, (int)call.Arguments[1]!, (int)call.Arguments[2]!)
            : null;

    [Theory] // ADR-0058 (the thirteenth Windows run, seen and not asked) / ticket 75: every key Point writes with — at a list open over Point (b3, b5) or with none — tells the listener the caret after what it wrote, which is where the listener brings the field's view (ShippedStylesheetTests)
    [InlineData("=F(1,,", "Home", "=F(1,,A2")]
    [InlineData("=F(1,,", "Shift+ArrowRight", "=F(1,,B2:C2")]
    [InlineData("=F(1,,", "Shift+ArrowDown", "=F(1,,B2:B3")]
    [InlineData("=F(1,,", "ArrowLeft", "=F(1,,A2")]
    [InlineData("=F(1,,", "ArrowRight", "=F(1,,C2")]
    [InlineData("=F(1,", "ArrowDown", "=F(1,B3")]
    [InlineData("=F(1,", "ArrowUp", "=F(1,B1")]
    [InlineData("=F(1,", "Home", "=F(1,A2")]
    [InlineData("=F(1,", "Shift+ArrowLeft", "=F(1,A2:B2")]
    public async Task Ticket75_every_key_point_writes_with_tells_the_listener_its_caret(string typed, string key, string written)
    {
        var cut = RenderGrid();
        await TypeFormulaAsync(cut, typed, row: 1);

        await PressAsync(cut, key, typed);

        Assert.Equal(written, EditorText(cut));
        Assert.Equal((written, written.Length, written.Length), CaretPlaced());

        // Pointing on, the next key rewrites the Reference, and the caret follows it again.
        await PressAsync(cut, "Shift+ArrowDown", written);
        var extended = EditorText(cut);
        Assert.NotEqual(written, extended);
        Assert.Equal((extended, extended.Length, extended.Length), CaretPlaced());
    }

    [Fact] // ADR-0058 (the thirteenth Windows run) / ticket 75: a press on the Sheet writes as a key does, and tells the listener the caret after what it wrote — in the middle of the text too
    public async Task Ticket75_a_press_tells_the_listener_the_caret_after_what_it_wrote()
    {
        var cut = RenderGrid();
        await TypeFormulaAsync(cut, "=F(1,", row: 1);

        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 250, OffsetY = 70 });

        Assert.Equal("=F(1,C4", EditorText(cut));
        Assert.Equal(("=F(1,C4", 7, 7), CaretPlaced());

        // With text after the caret: the caret placed is after the Reference, short of the end.
        await PressAsync(cut, "Escape", "=F(1,C4");
        await TypeFormulaAsync(cut, "=F(1,)", row: 1);
        await cut.InvokeAsync(() => cut.Instance.OnEditorCaretAsync("=F(1,)", 5));
        await cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 250, OffsetY = 70 });

        Assert.Equal("=F(1,C4)", EditorText(cut));
        Assert.Equal(("=F(1,C4)", 7, 7), CaretPlaced());
    }
}
