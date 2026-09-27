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
/// The editor's caret, reported and set, never inferred (ADR-0051's second round; DC-31's C#
/// side). The listener reports the caret with each input, as a message of its own beside
/// Blazor's input event; after the core rewrites the text, the listener is told where the caret
/// goes. The gate's sets for pointing and for an open list are the other half, inspected in
/// <see cref="ShippedStylesheetTests"/>; real keys are layer 3's. 50 rows of 20px under a 20px
/// header; Book (A) edits.
/// </summary>
public class CaretTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));
    private static readonly string[] Declared = ["SUM", "SUMIF", "XLOOKUP"];

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    /// <summary>The name being typed at the caret, after <c>=</c>, completed to <c>NAME(</c>:
    /// the span is the letters before the caret, so a wrong caret replaces the wrong span.</summary>
    private static EditorCompletion? Answer(string text, int caret)
    {
        if (!text.StartsWith('='))
            return null;
        var start = caret;
        while (start > 1 && char.IsLetter(text[start - 1]))
            start--;
        var typed = text[start..caret];
        if (typed.Length == 0)
            return null;
        return new EditorCompletion([.. Declared
            .Where(name => name.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            .Select(name => new CompletionCandidate(name, start, typed.Length, name + "("))]);
    }

    private static bool PointAt(string text, int caret)
        => text.StartsWith('=') && caret > 0 && caret <= text.Length && "=+-*/(,".Contains(text[caret - 1]);

    private static string ReferenceText(SelectionRange range)
        => FormattableString.Invariant($"{(char)('A' + range.LeftColumn)}{range.TopRow + 1}");

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        List<(string, int)>? asked = null, bool complete = true, bool point = false,
        List<GridEditIntent<TestRow>>? intents = null, bool bar = false)
        => Render<ExGrid<TestRow>>(ps =>
        {
            if (bar)
                ps.Add(g => g.ShowFormulaBar, true);
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.OnEdit, (GridEditIntent<TestRow> intent) => intents?.Add(intent));
            if (complete)
            {
                ps.Add(g => g.CompleteEditorText, (text, caret) =>
                {
                    asked?.Add((text, caret));
                    return ValueTask.FromResult(Answer(text, caret));
                });
            }
            if (point)
                ps.Add(g => g.PointAt, PointAt).Add(g => g.ReferenceText, ReferenceText);
        });

    private static Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, string? text = null, int caret = -1)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, false, false, false, false, false, editorText: text, editorCaret: caret));

    /// <summary>Blazor's input event alone, as it arrives before the listener's report.</summary>
    private static Task InputAsync(IRenderedComponent<ExGrid<TestRow>> cut, string text)
        => cut.Find(".ex-viewport .ex-editor").InputAsync(new ChangeEventArgs { Value = text });

    /// <summary>The listener's report of the caret, as it comes with an input.</summary>
    private static Task ReportAsync(IRenderedComponent<ExGrid<TestRow>> cut, string text, int caret)
        => cut.InvokeAsync(() => cut.Instance.OnEditorCaretAsync(text, caret));

    private static async Task StartFormulaAsync(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, "=");
    }

    private static string EditorText(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-viewport .ex-editor").GetAttribute("value") ?? "";

    private static List<string> Labels(IRenderedComponent<ExGrid<TestRow>> cut)
        => [.. cut.FindAll(".ex-completion .ex-completion-item").Select(item => item.TextContent)];

    private List<(string Text, int Caret)> CaretsPlaced()
        => [.. Js.CaretPlaced.Invocations.Select(i => ((string)i.Arguments[0]!, (int)i.Arguments[1]!))];

    private List<(string Mode, bool ReportsCaret)> EditingTold()
        => [.. JSInterop.Invocations.Where(i => i.Identifier == "setEditing")
            .Select(i => ((string)i.Arguments[0]!, (bool)i.Arguments[1]!))];

    [Fact] // ADR-0051 second round / DC-31: the Consumer is asked with the reported caret, never one inferred from the change
    public async Task The_consumer_waits_for_the_reported_caret()
    {
        var asked = new List<(string, int)>();
        var cut = RenderGrid(asked);
        await StartFormulaAsync(cut);

        // "=S" with an S typed before the S: the text alone cannot say which S is new.
        await InputAsync(cut, "=SS");
        Assert.Equal([("=", 1)], asked);
        Assert.Empty(cut.FindAll(".ex-completion"));

        await ReportAsync(cut, "=SS", 2);

        Assert.Equal([("=", 1), ("=SS", 2)], asked);
        Assert.Equal(["SUM", "SUMIF"], Labels(cut));
    }

    [Fact] // ADR-0051 second round / DC-31: repeated letters — the span replaced is the one at the caret, and the caret is placed after it
    public async Task Repeated_letters_complete_at_the_caret()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(intents: intents);
        await StartFormulaAsync(cut);
        await InputAsync(cut, "=SS");
        await ReportAsync(cut, "=SS", 2);

        await PressAsync(cut, "Tab", "=SS", 2);

        Assert.Equal("=SUM(S", EditorText(cut));
        Assert.Equal([("=", 1), ("=SUM(S", 5)], CaretsPlaced());
        Assert.Empty(intents);
    }

    [Fact] // ADR-0051 second round: a report that arrives before Blazor's input event is kept for it
    public async Task A_report_ahead_of_its_input_is_kept_for_it()
    {
        var asked = new List<(string, int)>();
        var cut = RenderGrid(asked);
        await StartFormulaAsync(cut);

        await ReportAsync(cut, "=SU+1", 3);
        Assert.Equal([("=", 1)], asked);
        await InputAsync(cut, "=SU+1");

        Assert.Equal([("=", 1), ("=SU+1", 3)], asked);
        Assert.Equal(["SUM", "SUMIF"], Labels(cut));
    }

    [Fact] // ADR-0051 second round / DC-31: an accept in the middle of the text places the caret after what it wrote
    public async Task An_accept_mid_text_places_the_caret_after_it()
    {
        var cut = RenderGrid();
        await StartFormulaAsync(cut);
        await InputAsync(cut, "=SU+1");
        await ReportAsync(cut, "=SU+1", 3);

        await PressAsync(cut, "Tab", "=SU+1", 3);

        Assert.Equal("=SUM(+1", EditorText(cut));
        Assert.Equal(("=SUM(+1", 5), CaretsPlaced()[^1]);
    }

    [Fact] // ADR-0051 second round / DC-31: a Reference written in the middle of the text places the caret after it
    public async Task A_pointed_reference_mid_text_places_the_caret_after_it()
    {
        var cut = RenderGrid(complete: false, point: true);
        await StartFormulaAsync(cut);
        await InputAsync(cut, "=+1");
        await ReportAsync(cut, "=+1", 1);

        await PressAsync(cut, "ArrowDown", "=+1", 1);

        Assert.Equal("=A2+1", EditorText(cut));
        Assert.Equal([("=", 1), ("=A2+1", 3)], CaretsPlaced());

        // The next arrow replaces that Reference, and the caret follows it again.
        await PressAsync(cut, "ArrowDown", "=A2+1", 3);
        Assert.Equal("=A3+1", EditorText(cut));
        Assert.Equal(("=A3+1", 3), CaretsPlaced()[^1]);
    }

    [Fact] // ADR-0051 second round: a click points from the caret the browser reported
    public async Task A_click_points_from_the_reported_caret()
    {
        var cut = RenderGrid(complete: false, point: true);
        await StartFormulaAsync(cut);
        await InputAsync(cut, "=+1");
        await ReportAsync(cut, "=+1", 1);

        await ClickAsync(cut, 50, 70); // A4

        Assert.Equal("=A4+1", EditorText(cut));
        Assert.Equal([("=", 1), ("=A4+1", 3)], CaretsPlaced());
    }

    [Fact] // ADR-0051 second round: with no caret reported yet, a click writes no Reference at a guessed place
    public async Task A_click_before_the_caret_is_reported_does_not_point()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(complete: false, point: true, intents: intents);
        await StartFormulaAsync(cut);
        await InputAsync(cut, "=+1");

        await ClickAsync(cut, 50, 70);

        // An ordinary click, as where no Reference can go: the text is committed as typed.
        Assert.Empty(cut.FindAll(".ex-point"));
        Assert.Equal("=+1", Assert.Single(intents).Value);
        // Only the opening's placement: nothing was written.
        Assert.Equal([("=", 1)], CaretsPlaced());
    }

    [Fact] // ADR-0051 second round / DC-31: while a list is open the gate claims only its keys; closed, Overwrite's again
    public async Task While_a_list_is_open_the_gate_is_told_completion()
    {
        var cut = RenderGrid();
        await StartFormulaAsync(cut);
        await InputAsync(cut, "=SU");
        await ReportAsync(cut, "=SU", 3);
        Assert.Equal("completion", EditingTold()[^1].Mode);

        await PressAsync(cut, "Escape", "=SU", 3);

        Assert.Equal("overwrite", EditingTold()[^1].Mode);
    }

    [Fact] // ADR-0051 second round / DC-31: a caret moved by ← while the list stands is the caret the next key is decided at
    public async Task A_caret_moved_under_an_open_list_is_asked_about_again()
    {
        var asked = new List<(string, int)>();
        var cut = RenderGrid(asked);
        await StartFormulaAsync(cut);
        await InputAsync(cut, "=SU");
        await ReportAsync(cut, "=SU", 3);

        // ← was the editor's: the next key carries the caret it left, between S and U.
        await PressAsync(cut, "Tab", "=SU", 2);

        Assert.Equal(("=SU", 2), asked[^2]);
        Assert.Equal("=SUM(U", EditorText(cut));
        Assert.Equal(("=SUM(U", 5), CaretsPlaced()[^1]);
    }

    [Fact] // ADR-0051 second round / DC-1: the listener reports carets only where completion or pointing is declared
    public async Task Carets_are_reported_only_where_declared()
    {
        var plain = RenderGrid(complete: false);
        await ClickAsync(plain, 50, 10);
        await PressAsync(plain, "F2");
        await PressAsync(plain, "Enter", "Row 000000", 10);

        Assert.All(EditingTold(), told => Assert.False(told.ReportsCaret));
        Assert.Empty(CaretsPlaced());
    }

    [Fact] // ADR-0051 second round: declared, the gate is told to report the caret with each input
    public async Task Declared_the_listener_reports_carets()
    {
        var cut = RenderGrid();
        await StartFormulaAsync(cut);

        Assert.True(EditingTold()[^1].ReportsCaret);
    }

    [Fact] // ADR-0051 second round: a report for no open edit, or outside its text, changes nothing
    public async Task A_report_outside_an_edit_changes_nothing()
    {
        var asked = new List<(string, int)>();
        var cut = RenderGrid(asked);

        await ReportAsync(cut, "=SU", 3);
        await StartFormulaAsync(cut);
        await ReportAsync(cut, "=", 5);

        Assert.Equal([("=", 1)], asked);
    }

    [Fact] // ADR-0003: caret reports render no row
    public async Task Caret_reports_render_no_row()
    {
        var cut = RenderGrid();
        await StartFormulaAsync(cut);
        var before = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList();

        await InputAsync(cut, "=SU");
        await ReportAsync(cut, "=SU", 3);
        await PressAsync(cut, "Tab", "=SU", 3);

        Assert.Equal(before, cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList());
    }

    [Fact] // ADR-0051 second round / DC-31: opening an edit by typing places the caret at the end of the opening text
    public async Task Opening_by_typing_places_the_caret_at_the_end()
    {
        var cut = RenderGrid();

        await StartFormulaAsync(cut);

        Assert.Equal([("=", 1)], CaretsPlaced());
    }

    [Fact] // ADR-0051 second round / DC-31: opening an edit on the cell's text places the caret at its end, never assumed
    public async Task Opening_on_the_cells_text_places_the_caret_at_its_end()
    {
        var cut = RenderGrid(complete: false, point: true);
        await ClickAsync(cut, 50, 10);

        await PressAsync(cut, "F2");

        Assert.Equal([("Row 000000", 10)], CaretsPlaced());
    }

    [Fact] // ADR-0051 second round / DC-31: opening an edit from the Formula Bar places the caret at the end of the opening text
    public async Task Opening_from_the_formula_bar_places_the_caret_at_the_end()
    {
        var cut = RenderGrid(bar: true);
        await ClickAsync(cut, 50, 10);

        await cut.Find(".ex-formula-bar-text").FocusAsync(new FocusEventArgs());

        Assert.Equal([("Row 000000", 10)], CaretsPlaced());
    }

    [Fact] // ADR-0051 second round / DC-1: without completion or pointing, opening an edit places nothing
    public async Task Undeclared_opening_places_nothing()
    {
        var cut = RenderGrid(complete: false, bar: true);
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, "=");
        await PressAsync(cut, "Escape", "=", 1);
        await cut.Find(".ex-formula-bar-text").FocusAsync(new FocusEventArgs());

        Assert.Empty(CaretsPlaced());
    }

    [Fact] // ADR-0051 second round / DC-31: a caret moved with the text unchanged is the caret completion is asked at
    public async Task A_caret_only_move_asks_completion_at_the_new_caret()
    {
        var asked = new List<(string, int)>();
        var cut = RenderGrid(asked);
        await StartFormulaAsync(cut);
        await InputAsync(cut, "=S+XL");
        await ReportAsync(cut, "=S+XL", 5);
        Assert.Equal(["XLOOKUP"], Labels(cut));

        // ← three times, or a click after the S: the text is unchanged, the caret is not.
        await ReportAsync(cut, "=S+XL", 2);

        Assert.Equal(("=S+XL", 2), asked[^1]);
        Assert.Equal(["SUM", "SUMIF"], Labels(cut));
        await PressAsync(cut, "Tab", "=S+XL", 2);
        Assert.Equal("=SUM(+XL", EditorText(cut));
        Assert.Equal(("=SUM(+XL", 5), CaretsPlaced()[^1]);
    }

    [Fact] // ADR-0051 second round / DC-31: a click points at the caret the user moved to, not the one typing left
    public async Task A_click_points_at_the_caret_the_user_moved_to()
    {
        var cut = RenderGrid(complete: false, point: true);
        await StartFormulaAsync(cut);
        await InputAsync(cut, "=+1");
        await ReportAsync(cut, "=+1", 3);

        // Moved back before the +: a Reference can go after the =.
        await ReportAsync(cut, "=+1", 1);
        await ClickAsync(cut, 50, 70); // A4

        Assert.Equal("=A4+1", EditorText(cut));
        Assert.Equal(("=A4+1", 3), CaretsPlaced()[^1]);
    }

    [Fact] // ADR-0051 second round / DC-31: a caret moved away from a pointed Reference ends pointing; the next click writes at the new caret
    public async Task A_caret_moved_while_pointing_ends_the_outline()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(complete: false, point: true, intents: intents);
        await StartFormulaAsync(cut);
        await InputAsync(cut, "=+1");
        await ReportAsync(cut, "=+1", 1);
        await PressAsync(cut, "ArrowDown", "=+1", 1);
        Assert.Equal("=A2+1", EditorText(cut));
        Assert.NotEmpty(cut.FindAll(".ex-point"));
        Assert.Equal("point", EditingTold()[^1].Mode);

        // A click inside the text, after the +.
        await ReportAsync(cut, "=A2+1", 4);

        Assert.Empty(cut.FindAll(".ex-point"));
        Assert.Equal("overwrite", EditingTold()[^1].Mode);
        await ClickAsync(cut, 50, 70); // A4
        Assert.Equal("=A2+A41", EditorText(cut));
        Assert.Equal(("=A2+A41", 6), CaretsPlaced()[^1]);
        Assert.Empty(intents);
    }

    [Fact] // ADR-0051 second round: the browser's caret in text the core wrote, reported before the placement lands, is not taken
    public async Task A_report_ahead_of_the_placement_is_not_taken()
    {
        var cut = RenderGrid(complete: false, point: true);
        await StartFormulaAsync(cut);
        await InputAsync(cut, "=+1");
        await ReportAsync(cut, "=+1", 1);
        var placement = Js.UnansweredCaretPlacement();

        await PressAsync(cut, "ArrowDown", "=+1", 1);
        var placed = placement.Invocations.Last();
        Assert.Equal(("=A2+1", 3), ((string)placed.Arguments[0]!, (int)placed.Arguments[1]!));
        // Setting the value left the browser's caret at the end, and that was reported.
        await ReportAsync(cut, "=A2+1", 5);
        await cut.InvokeAsync(placement.SetVoidResult);

        // The outline stands and the next arrow replaces its Reference, at the placed caret.
        Assert.NotEmpty(cut.FindAll(".ex-point"));
        await PressAsync(cut, "ArrowDown", "=A2+1", 3);
        Assert.Equal("=A3+1", EditorText(cut));
    }

    [Fact] // ADR-0051 second round / ADR-0003: a caret-only report with nothing to answer renders nothing
    public async Task A_caret_only_report_with_nothing_to_answer_renders_nothing()
    {
        var cut = RenderGrid(complete: false, point: true);
        await StartFormulaAsync(cut);
        await InputAsync(cut, "=+1");
        await ReportAsync(cut, "=+1", 3);
        var renders = cut.RenderCount;

        await ReportAsync(cut, "=+1", 2);

        Assert.Equal(renders, cut.RenderCount);
    }
}
