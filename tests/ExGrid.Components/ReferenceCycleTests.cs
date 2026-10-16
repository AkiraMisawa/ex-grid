using System.Text.RegularExpressions;
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
/// F4 cycles the Reference at the caret (ADR-0051, 2026-09-29; DC-45's layer 2 half, DC-1): with
/// the Consumer's cycling function declared, F4 while an edit is open hands it the text and the
/// selection the key carries, writes back the text it answers — in both editor surfaces — and has
/// the listener place the selection it answers: as a selection where the key carried a selection or
/// a caret the user moved, and as a caret at its end where the caret stood where typing left it, in
/// any editing state (observed in Excel: the seventeenth Windows run, Part C; the readings of
/// 2026-10-12, R1 (iv) and (v)). While pointing, F4 rewrites the whole Reference the outline wrote
/// and pointing goes on, the caret at its end (the Reference wears Point's look, never the field's
/// selection, ADR-0057). Without
/// the function, or with no edit open, F4 is not the grid's. The gate's half is inspected in
/// <see cref="ShippedStylesheetTests"/>; real keys are layer 3's. 50 rows of 20px in a 350 × 200
/// Viewport; Book (A) edits.
/// </summary>
public partial class ReferenceCycleTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    [GeneratedRegex(@"(?<c>\$?[A-Z])(?<r>\$?[0-9]+)")]
    private static partial Regex CellReference();

    /// <summary>
    /// The shape of ExSheet's answer, for one-letter columns: the Reference the caret is inside or
    /// touching goes to its next form, A1 → $A$1 → A$1 → $A1 → A1, answered with its span.
    /// </summary>
    private static EditorRewrite? Cycle(string text, int start, int end)
    {
        if (!text.StartsWith('='))
            return null;
        foreach (Match m in CellReference().Matches(text))
        {
            if (start < m.Index || start > m.Index + m.Length)
                continue;
            var (column, row) = (m.Groups["c"].Value.TrimStart('$'), m.Groups["r"].Value.TrimStart('$'));
            var next = (m.Groups["c"].Value[0] == '$', m.Groups["r"].Value[0] == '$') switch
            {
                (false, false) => (true, true),
                (true, true) => (false, true),
                (false, true) => (true, false),
                _ => (false, false),
            };
            var written = (next.Item1 ? "$" : "") + column + (next.Item2 ? "$" : "") + row;
            var at = m.Index + written.Length;
            return new EditorRewrite(text[..m.Index] + written + text[(m.Index + m.Length)..], m.Index, at);
        }
        return null;
    }

    private static bool PointAt(string text, int caret)
        => text.StartsWith('=') && caret > 0 && caret <= text.Length && "=+-*/(,".Contains(text[caret - 1]);

    private static string ReferenceText(SelectionRange range)
        => FormattableString.Invariant($"{(char)('A' + range.LeftColumn)}{range.TopRow + 1}");

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Func<string, int, int, EditorRewrite?>? cycle = null,
        bool declare = true,
        bool point = false,
        List<(string Text, int Start, int End)>? asked = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.ShowFormulaBar, true);
            if (declare)
            {
                var answer = cycle ?? Cycle;
                ps.Add(g => g.CycleReference, (text, start, end) =>
                {
                    asked?.Add((text, start, end));
                    return answer(text, start, end);
                });
            }
            if (point)
                ps.Add(g => g.PointAt, PointAt).Add(g => g.ReferenceText, ReferenceText);
        });

    private static Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    /// <summary>A key as the capture listener forwards it while editing: with the editor's text,
    /// its selection, and whether the user moved the caret in that very text.</summary>
    private static Task PressAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, string key, string? text = null, int start = -1, int end = -1,
        bool moved = false, bool fromBar = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(
            key, false, false, false, false, false, fromDescendant: fromBar,
            editorText: text, editorCaret: start, editorSelectionEnd: end < 0 ? start : end, editorCaretMoved: moved));

    /// <summary>Selects A1 and types <paramref name="typed"/> onto it: Overwrite, the caret at the end.</summary>
    private static async Task TypeFormulaAsync(IRenderedComponent<ExGrid<TestRow>> cut, string typed)
    {
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, typed[..1]);
        if (typed.Length > 1)
        {
            await cut.Find(".ex-viewport .ex-editor").InputAsync(new ChangeEventArgs { Value = typed });
            await cut.InvokeAsync(() => cut.Instance.OnEditorCaretAsync(typed, typed.Length));
        }
    }

    private static string CellEditorText(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-viewport .ex-editor").GetAttribute("value") ?? "";

    private static string BarText(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-formula-bar-text").GetAttribute("value") ?? "";

    private List<(string Text, int Start, int End)> SelectionsPlaced()
        => [.. JSInterop.Invocations.Where(i => i.Identifier == "setCaret")
            .Select(i => ((string)i.Arguments[0]!, (int)i.Arguments[1]!, (int)i.Arguments[2]!))];

    private List<(string Mode, bool CyclesReferences)> EditingTold()
        => [.. JSInterop.Invocations.Where(i => i.Identifier == "setEditing")
            .Select(i => ((string)i.Arguments[0]!, (bool)i.Arguments[2]!))];

    [Fact] // ADR-0051 / DC-45 / DC-1: without the function the gate is never told to claim F4, and an F4 that arrives all the same changes nothing
    public async Task DC45_without_the_function_F4_is_not_claimed_and_changes_nothing()
    {
        var cut = RenderGrid(declare: false);
        await TypeFormulaAsync(cut, "=B2");
        var placed = SelectionsPlaced().Count;

        await PressAsync(cut, "F4", "=B2", 3);

        Assert.NotEmpty(EditingTold());
        Assert.All(EditingTold(), told => Assert.False(told.CyclesReferences));
        Assert.Equal("=B2", CellEditorText(cut));
        Assert.Equal(placed, SelectionsPlaced().Count);
    }

    [Fact] // ADR-0051 / DC-45: declared, the gate is told to claim F4 with the editing set — which it consults only while an edit is open
    public async Task DC45_the_gate_is_told_to_claim_F4_while_an_edit_is_open()
    {
        var cut = RenderGrid();

        await TypeFormulaAsync(cut, "=B2");

        Assert.Equal(("overwrite", true), EditingTold()[^1]);
        // Outside an edit the gate claims only what C# lists for it, and F4 is never listed.
        Assert.DoesNotContain("F4", Js.TakenAtAttach);
    }

    [Fact] // ADR-0051 / DC-45: with no edit open, F4 is not the grid's — the function is not asked and nothing moves
    public async Task DC45_with_no_edit_open_F4_does_nothing()
    {
        var asked = new List<(string, int, int)>();
        var cut = RenderGrid(asked: asked);
        await ClickAsync(cut, 50, 10);

        await PressAsync(cut, "F4");

        Assert.Empty(asked);
        Assert.Empty(cut.FindAll(".ex-viewport .ex-editor"));
        Assert.DoesNotContain(EditingTold(), told => told.Mode != "none");
    }

    [Fact] // ADR-0051 / DC-45: the function is handed the text and the selection the F4 key message carries
    public async Task DC45_F4_hands_the_function_the_text_and_selection_its_key_carries()
    {
        var asked = new List<(string, int, int)>();
        var cut = RenderGrid(cycle: (_, _, _) => null, asked: asked);
        await TypeFormulaAsync(cut, "=A1");

        // Typed on since the core last heard, and a selection over "A1+B": the key's own text and selection.
        await PressAsync(cut, "F4", "=A1+B2", 1, 5);

        Assert.Equal([("=A1+B2", 1, 5)], asked);
    }

    [Fact] // ADR-0051 / DC-45: the answer is written to both editor surfaces, and the listener places its selection — a caret before the text's end selects what was rewritten (run 18a)
    public async Task DC45_the_answer_is_written_to_both_surfaces_and_its_caret_placed()
    {
        var cut = RenderGrid();
        await TypeFormulaAsync(cut, "=A1+B2");

        await PressAsync(cut, "F4", "=A1+B2", 3);

        Assert.Equal("=$A$1+B2", CellEditorText(cut));
        Assert.Equal("=$A$1+B2", BarText(cut));
        Assert.Equal(("=$A$1+B2", 1, 5), SelectionsPlaced()[^1]);
    }

    [Fact] // ADR-0051 / DC-45 / SH-28: a caret before the text's end — ← after F2 in the cell — is placed as a selection: the rewritten Reference selected (observed, the seventeenth Windows run, Part C, cases 2 to 4 with F2: $A$1 selected, 1 to 5)
    public async Task DC45_a_caret_the_user_moved_is_placed_as_a_selection()
    {
        var cut = RenderGrid();
        await TypeFormulaAsync(cut, "=A1+B2");
        await PressAsync(cut, "F2", "=A1+B2", 6);
        Assert.Equal("caret", EditingTold()[^1].Mode);

        await PressAsync(cut, "F4", "=A1+B2", 3, moved: true);

        Assert.Equal("=$A$1+B2", CellEditorText(cut));
        Assert.Equal(("=$A$1+B2", 1, 5), SelectionsPlaced()[^1]);
    }

    [Fact] // ADR-0051 / DC-45 / SH-28: a caret where typing left it is placed as a caret at the end of what the answer spans, in Overwrite as in Caret (observed, the seventeenth Windows run, Part C, cases 1, 10, 21 and 22, typed into a cell; R1 (iv), typed into the Formula Bar)
    public async Task DC45_a_caret_where_typing_left_it_is_placed_as_a_caret_at_the_end()
    {
        var cut = RenderGrid(cycle: (_, _, _) => new EditorRewrite("=$B$2", 1, 5));
        await TypeFormulaAsync(cut, "=B2");

        await PressAsync(cut, "F4", "=B2", 3);

        Assert.Equal("=$B$2", CellEditorText(cut));
        Assert.Equal(("=$B$2", 5, 5), SelectionsPlaced()[^1]);
        Assert.Equal("overwrite", EditingTold()[^1].Mode);
    }

    [Fact] // ADR-0051 / DC-45 / SH-28: a selection the key carried is placed as the selection answered, whatever the editing state (observed in Edit mode, the seventeenth Windows run, Part C, cases 7 and 18 to 20 with F2; in Overwrite by generalisation)
    public async Task DC45_a_selection_carried_is_placed_as_the_selection_answered()
    {
        var cut = RenderGrid(cycle: (_, _, _) => new EditorRewrite("=$A$1+$B$2", 1, 10));
        await TypeFormulaAsync(cut, "=A1+B2");

        await PressAsync(cut, "F4", "=A1+B2", 1, 6);

        Assert.Equal("=$A$1+$B$2", CellEditorText(cut));
        Assert.Equal(("=$A$1+$B$2", 1, 10), SelectionsPlaced()[^1]);
        Assert.Equal("overwrite", EditingTold()[^1].Mode);
    }

    [Fact] // ADR-0051 / DC-45: after F2 in a cell, a caret at the text's end — where the edit's opening or typing left it — is placed as a caret at the end (by generalisation of R1 (iv) and run 18a answer 1: Excel was asked in the Formula Bar)
    public async Task DC45_in_caret_a_caret_not_moved_is_placed_as_a_caret()
    {
        var cut = RenderGrid();
        await TypeFormulaAsync(cut, "=A1+B2");
        await PressAsync(cut, "F2", "=A1+B2", 6);
        Assert.Equal("caret", EditingTold()[^1].Mode);

        await PressAsync(cut, "F4", "=A1+B2", 6);

        Assert.Equal("=A1+$B$2", CellEditorText(cut));
        Assert.Equal(("=A1+$B$2", 8, 8), SelectionsPlaced()[^1]);
    }

    [Fact] // ADR-0051 / DC-45: a second press before the selection is placed cycles the same Reference — a placement in flight carries a selection as it carries a caret, and the second rewrite is selected too (observed, Part C, case 5 with F2: each press selected)
    public async Task DC45_a_second_press_before_the_selection_lands_cycles_the_same_reference()
    {
        var asked = new List<(string, int, int)>();
        var cut = RenderGrid(asked: asked);
        await TypeFormulaAsync(cut, "=A1+B2");
        await PressAsync(cut, "F2", "=A1+B2", 6);
        Js.UnansweredCaretPlacement();

        await PressAsync(cut, "F4", "=A1+B2", 3, moved: true);
        // Setting the value left the browser's caret at the end, after B2: not the user's.
        await PressAsync(cut, "F4", "=$A$1+B2", 8);

        Assert.Equal(("=$A$1+B2", 1, 5), asked[^1]);
        Assert.Equal("=A$1+B2", CellEditorText(cut));
        Assert.Equal(("=A$1+B2", 1, 4), SelectionsPlaced()[^1]);
    }

    [Fact] // ADR-0051 / DC-45: F4 in the Formula Bar writes the bar and the Cell Editor alike, and the edit stays in the bar; typed there, the caret stands at the end, nothing selected (observed, R1 (iv): =B2, F4 gave =$B$2 with a caret at its end)
    public async Task DC45_F4_in_the_formula_bar_writes_both_surfaces()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 10);
        var bar = cut.Find(".ex-formula-bar-text");
        await bar.FocusAsync(new FocusEventArgs());
        await bar.InputAsync(new ChangeEventArgs { Value = "=B2" });
        await cut.InvokeAsync(() => cut.Instance.OnEditorCaretAsync("=B2", 3));

        await PressAsync(cut, "F4", "=B2", 3, fromBar: true);

        Assert.Equal("=$B$2", BarText(cut));
        Assert.Equal("=$B$2", CellEditorText(cut));
        Assert.Equal(("=$B$2", 5, 5), SelectionsPlaced()[^1]);
        Assert.Equal("caret", EditingTold()[^1].Mode);
    }

    [Fact] // ADR-0051 / DC-45: in the Formula Bar, a caret moved by ← selects the rewritten end of the range (observed, R1 (v): =SUM(A1:B2), ←, F4 gave =SUM(A1:$B$2) with $B$2 selected)
    public async Task DC45_F4_in_the_formula_bar_after_a_caret_move_selects()
    {
        var cut = RenderGrid(cycle: (text, _, _) => text == "=SUM(A1:B2)" ? new EditorRewrite("=SUM(A1:$B$2)", 8, 12) : null);
        await ClickAsync(cut, 50, 10);
        var bar = cut.Find(".ex-formula-bar-text");
        await bar.FocusAsync(new FocusEventArgs());
        await bar.InputAsync(new ChangeEventArgs { Value = "=SUM(A1:B2)" });
        await cut.InvokeAsync(() => cut.Instance.OnEditorCaretAsync("=SUM(A1:B2)", 11));

        await PressAsync(cut, "F4", "=SUM(A1:B2)", 10, moved: true, fromBar: true);

        Assert.Equal("=SUM(A1:$B$2)", BarText(cut));
        Assert.Equal(("=SUM(A1:$B$2)", 8, 12), SelectionsPlaced()[^1]);
        Assert.Equal("caret", EditingTold()[^1].Mode);
    }

    [Fact] // ADR-0051 / DC-45 / ED-29: after F2 takes the Formula Bar to Enter mode, F4 with the caret where typing left it leaves a caret at the rewritten Reference's end, as it does typed into a cell (Part C, case 1; the bar after F2 by generalisation)
    public async Task DC45_F4_in_the_formula_bar_after_F2_leaves_a_caret()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 10);
        var bar = cut.Find(".ex-formula-bar-text");
        await bar.FocusAsync(new FocusEventArgs());
        await bar.InputAsync(new ChangeEventArgs { Value = "=B2" });
        await cut.InvokeAsync(() => cut.Instance.OnEditorCaretAsync("=B2", 3));
        await PressAsync(cut, "F2", "=B2", 3, fromBar: true);
        Assert.Equal("overwrite", EditingTold()[^1].Mode);

        await PressAsync(cut, "F4", "=B2", 3, fromBar: true);

        Assert.Equal("=$B$2", BarText(cut));
        Assert.Equal("=$B$2", CellEditorText(cut));
        Assert.Equal(("=$B$2", 5, 5), SelectionsPlaced()[^1]);
    }

    [Fact] // ADR-0051, run 18a answer 1: =A1+B2 typed into the Formula Bar, F2 (Enter mode), F4 gave =A1+$B$2 with a caret at the end (8, 8), nothing selected
    public async Task ADR0051_F4_after_F2_in_the_formula_bar_leaves_a_caret_at_the_end()
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 10);
        var bar = cut.Find(".ex-formula-bar-text");
        await bar.FocusAsync(new FocusEventArgs());
        await bar.InputAsync(new ChangeEventArgs { Value = "=A1+B2" });
        await cut.InvokeAsync(() => cut.Instance.OnEditorCaretAsync("=A1+B2", 6));
        await PressAsync(cut, "F2", "=A1+B2", 6, fromBar: true);
        Assert.Equal("overwrite", EditingTold()[^1].Mode);

        await PressAsync(cut, "F4", "=A1+B2", 6, fromBar: true);

        Assert.Equal("=A1+$B$2", BarText(cut));
        Assert.Equal(("=A1+$B$2", 8, 8), SelectionsPlaced()[^1]);
    }

    [Theory] // ADR-0051, run 18a answer 2: in the Formula Bar, a caret moved back to the text's end (=A1+B2, ←, →) leaves a caret after F4 (8); a caret inside the text, after a digit typed there (=A10+B2, the caret at 4) or a press after A1 (3), selects what was rewritten (1-6, 1-5)
    [InlineData("=A1+B2", 6, true, "=A1+$B$2", 8, 8)]
    [InlineData("=A10+B2", 4, false, "=$A$10+B2", 1, 6)]
    [InlineData("=A10+B2", 4, true, "=$A$10+B2", 1, 6)]
    [InlineData("=A1+B2", 3, true, "=$A$1+B2", 1, 5)]
    public async Task ADR0051_F4_selects_unless_the_caret_stands_at_the_texts_end(
        string text, int caret, bool moved, string rewritten, int start, int end)
    {
        var cut = RenderGrid();
        await ClickAsync(cut, 50, 10);
        var bar = cut.Find(".ex-formula-bar-text");
        await bar.FocusAsync(new FocusEventArgs());
        await bar.InputAsync(new ChangeEventArgs { Value = text });
        await cut.InvokeAsync(() => cut.Instance.OnEditorCaretAsync(text, caret));

        await PressAsync(cut, "F4", text, caret, moved: moved, fromBar: true);

        Assert.Equal(rewritten, BarText(cut));
        Assert.Equal((rewritten, start, end), SelectionsPlaced()[^1]);
        Assert.Equal("caret", EditingTold()[^1].Mode);
    }

    [Fact] // ADR-0051 / DC-45: no answer — a caret touching no Reference — changes nothing
    public async Task DC45_no_answer_changes_nothing()
    {
        var cut = RenderGrid();
        await TypeFormulaAsync(cut, "=1+2");
        var placed = SelectionsPlaced().Count;

        await PressAsync(cut, "F4", "=1+2", 4);

        Assert.Equal("=1+2", CellEditorText(cut));
        Assert.Equal(placed, SelectionsPlaced().Count);
    }

    [Fact] // ADR-0051 / DC-45: four presses in a burst, each decided from the text its own key message carries, give the four forms in order
    public async Task DC45_a_burst_of_presses_gives_the_four_forms_in_order()
    {
        var asked = new List<(string, int, int)>();
        var cut = RenderGrid(asked: asked);
        await TypeFormulaAsync(cut, "=B2");
        // On a circuit the placements land a round trip later: none of them has, while the
        // presses after the first carry the text each answer left, the caret where setting
        // the value left it — at the end.
        Js.UnansweredCaretPlacement();
        var seen = new List<string>();

        foreach (var text in new[] { "=B2", "=$B$2", "=B$2", "=$B2" })
        {
            await PressAsync(cut, "F4", text, text.Length);
            seen.Add(CellEditorText(cut));
        }

        Assert.Equal(["=$B$2", "=B$2", "=$B2", "=B2"], seen);
        Assert.Equal([("=B2", 3, 3), ("=$B$2", 5, 5), ("=B$2", 4, 4), ("=$B2", 4, 4)], asked);
    }

    [Fact] // ADR-0051 / DC-45: a second press before the first placement has landed cycles the same Reference, not the one at the browser's own caret
    public async Task DC45_a_second_press_before_the_placement_lands_cycles_the_same_reference()
    {
        var cut = RenderGrid();
        await TypeFormulaAsync(cut, "=A1+B2");
        Js.UnansweredCaretPlacement();

        await PressAsync(cut, "F4", "=A1+B2", 3);
        Assert.Equal("=$A$1+B2", CellEditorText(cut));
        // Setting the value left the browser's caret at the end, after B2: not the user's.
        await PressAsync(cut, "F4", "=$A$1+B2", 8);

        Assert.Equal("=A$1+B2", CellEditorText(cut));
        // The placement in flight was a selection over $A$1 (the caret stood before the text's end,
        // run 18a), and the second rewrite is selected as it was.
        Assert.Equal(("=A$1+B2", 1, 4), SelectionsPlaced()[^1]);
    }

    [Fact] // ADR-0051 third round / DC-45: the user's own move in that text, before the placement lands, is newer — F4 cycles at it
    public async Task DC45_a_users_move_before_the_placement_lands_is_taken()
    {
        var cut = RenderGrid();
        await TypeFormulaAsync(cut, "=A1+B2");
        Js.UnansweredCaretPlacement();

        await PressAsync(cut, "F4", "=A1+B2", 3);
        // A press after B2, in the text the first F4 left, before its placement came.
        await PressAsync(cut, "F4", "=$A$1+B2", 8, moved: true);

        Assert.Equal("=$A$1+$B$2", CellEditorText(cut));
    }

    [Fact] // ADR-0051 / DC-45: while pointing, F4 rewrites the Reference the outline wrote and pointing goes on, the caret at its end so that a key typed next follows it (observed, Part C, case 16); a further move writes the next Reference in the relative form (observed, Part C, case 15b)
    public async Task DC45_while_pointing_F4_rewrites_the_pointed_reference_and_pointing_goes_on()
    {
        var cut = RenderGrid(point: true);
        await TypeFormulaAsync(cut, "=");
        await PressAsync(cut, "ArrowDown", "=", 1);
        Assert.Equal("=A2", CellEditorText(cut));

        await PressAsync(cut, "F4", "=A2", 3);

        Assert.Equal("=$A$2", CellEditorText(cut));
        Assert.NotEmpty(cut.FindAll(".ex-selection .ex-point"));
        Assert.Equal("point", EditingTold()[^1].Mode);
        // The caret at the end of the rewritten Reference, which wears Point's look and is not the
        // field's selection (ADR-0057), where Excel reported it as the bar's selection (R1 (vi)).
        Assert.Equal(("=$A$2", 5, 5), SelectionsPlaced()[^1]);
        await PressAsync(cut, "ArrowDown", "=$A$2", 5);
        Assert.Equal("=A3", CellEditorText(cut));
    }

    [Fact] // ADR-0051 / DC-45: while pointing, F4 is asked over the whole of the outline's Reference, so both ends of a pointed range cycle together (observed, R1 (vi)), wherever a placement still in flight has left the browser's caret
    public async Task DC45_while_pointing_F4_cycles_the_outlines_reference()
    {
        var asked = new List<(string, int, int)>();
        var cut = RenderGrid(point: true, asked: asked);
        await TypeFormulaAsync(cut, "=SUM()");
        // The caret between the parentheses, where a Reference can go.
        await cut.InvokeAsync(() => cut.Instance.OnEditorCaretAsync("=SUM()", 5));
        Js.UnansweredCaretPlacement();
        await PressAsync(cut, "ArrowDown", "=SUM()", 5);
        Assert.Equal("=SUM(A2)", CellEditorText(cut));

        // The browser's caret went to the end when the value was set.
        await PressAsync(cut, "F4", "=SUM(A2)", 8);

        Assert.Equal(("=SUM(A2)", 5, 7), asked[^1]);
        Assert.Equal("=SUM($A$2)", CellEditorText(cut));
        Assert.NotEmpty(cut.FindAll(".ex-selection .ex-point"));
        Assert.Equal(("=SUM($A$2)", 9, 9), SelectionsPlaced()[^1]);
    }

    [Fact] // ADR-0051 / DC-45: an answer whose selection does not lie in its text is refused by name, and the text is left as it was
    public async Task DC45_an_answer_outside_its_text_is_refused()
    {
        var cut = RenderGrid(cycle: (_, _, _) => new EditorRewrite("=$B$2", 3, 9));
        await TypeFormulaAsync(cut, "=B2");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => PressAsync(cut, "F4", "=B2", 3));

        Assert.Equal("=B2", CellEditorText(cut));
    }
}
