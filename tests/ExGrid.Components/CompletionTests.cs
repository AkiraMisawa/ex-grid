using Bunit;
using ExGrid.Cells;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// Completion and the argument hint (ADR-0051; DC-1, DC-17, DC-18): the editor's text and caret
/// reach the Consumer as the user types, from the cell and the Formula Bar alike; the answer is
/// painted as the editor's Inner Popup inside the grid's box; ↑/↓ choose, Tab accepts, Escape
/// closes the list and leaves the edit open; an answer for text that has since changed is never
/// shown. 50 rows of 20px in a 350 × 200 Viewport; Book is editable, Amount is not.
/// </summary>
public class CompletionTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static readonly string[] Declared = ["SUM", "SUMIF", "XLOOKUP"];

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    /// <summary>A Consumer that knows three function names: the name being typed at the caret
    /// after <c>=</c> is completed to <c>NAME(</c>, and a caret just after <c>SUM(</c> gets the
    /// hint. It stands in for ExSheet's <c>FormulaEntry.Complete</c> and <c>HintAt</c>.</summary>
    private static EditorCompletion? Answer(string text, int caret)
    {
        if (!text.StartsWith('='))
            return null;
        var before = text[..caret];
        if (before.EndsWith("SUM(", StringComparison.Ordinal))
            return new EditorCompletion([], new EditorHint("SUM(number1, [number2], …)", 4, 7));
        var start = caret;
        while (start > 1 && char.IsLetter(text[start - 1]))
            start--;
        var typed = text[start..caret];
        if (typed.Length == 0)
            return null;
        var candidates = Declared
            .Where(name => name.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            .Select(name => new CompletionCandidate(name, start, typed.Length, name + "("))
            .ToList();
        return new EditorCompletion(candidates);
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Func<string, int, ValueTask<EditorCompletion?>>? complete,
        List<GridEditIntent<TestRow>>? intents = null,
        IGridChrome? chrome = null,
        bool formulaBar = false,
        int viewportHeight = 200)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, viewportHeight)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.ShowFormulaBar, formulaBar)
              .Add(g => g.OnEdit, (GridEditIntent<TestRow> intent) => intents?.Add(intent));
            if (complete is not null)
                ps.Add(g => g.CompleteEditorText, complete);
            if (chrome is not null)
                ps.Add(g => g.Chrome, chrome);
        });

    private static Func<string, int, ValueTask<EditorCompletion?>> Synchronous(List<(string, int)>? asked = null)
        => (text, caret) =>
        {
            asked?.Add((text, caret));
            return ValueTask.FromResult(Answer(text, caret));
        };

    private static Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool shift = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, false, shift, false, false, false));

    private static Task TypeAsync(IRenderedComponent<ExGrid<TestRow>> cut, string text)
        => cut.Find(".ex-viewport .ex-editor").InputAsync(new ChangeEventArgs { Value = text });

    /// <summary>Opens Overwrite on the first cell with <c>=</c>, then types the rest.</summary>
    private static async Task TypeFormulaAsync(IRenderedComponent<ExGrid<TestRow>> cut, string text)
    {
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, "=");
        await TypeAsync(cut, text);
    }

    private static List<string> Labels(IRenderedComponent<ExGrid<TestRow>> cut)
        => [.. cut.FindAll(".ex-completion .ex-completion-item").Select(item => item.TextContent)];

    private static string Selected(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-completion .ex-completion-selected").TextContent;

    private static string EditorText(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find(".ex-viewport .ex-editor").GetAttribute("value") ?? "";

    private List<string> EditingModesTold()
        => [.. JSInterop.Invocations.Where(i => i.Identifier == "setEditing").Select(i => (string)i.Arguments[0]!)];

    [Fact] // ADR-0051 / DC-1: with nothing declared, nothing is reported and no box or attribute is painted
    public async Task Without_the_declaration_nothing_changes()
    {
        var cut = RenderGrid(complete: null);

        await TypeFormulaAsync(cut, "=SU");

        Assert.Empty(cut.FindAll(".ex-completion"));
        Assert.Null(cut.Find(".ex-viewport .ex-editor").GetAttribute("aria-autocomplete"));
        Assert.Equal("overwrite", EditingModesTold()[^1]);
    }

    [Fact] // ADR-0051 / DC-17: the text and the caret reach the Consumer as the user types
    public async Task The_text_and_the_caret_reach_the_consumer_as_the_user_types()
    {
        var asked = new List<(string, int)>();
        var cut = RenderGrid(Synchronous(asked));

        await TypeFormulaAsync(cut, "=S");
        await TypeAsync(cut, "=SU");
        // Typed in the middle: the caret is where the edit happened, not the end of the text.
        await TypeAsync(cut, "=SU+1");
        await TypeAsync(cut, "=SUM+1");

        Assert.Equal([("=", 1), ("=S", 2), ("=SU", 3), ("=SU+1", 5), ("=SUM+1", 4)], asked);
    }

    [Fact] // ADR-0051 / DC-17: the candidates are painted as the editor's popup, inside the root and outside the scroller
    public async Task The_candidates_are_painted_beneath_the_editor()
    {
        var cut = RenderGrid(Synchronous());

        await TypeFormulaAsync(cut, "=SU");

        Assert.Equal(["SUM", "SUMIF"], Labels(cut));
        Assert.Equal("SUM", Selected(cut));
        var box = cut.Find(".ex-grid > .ex-completion");
        Assert.Empty(cut.FindAll(".ex-scroller .ex-completion"));
        // Beneath the edited cell: the header's 20px (the Grid Metrics' header height at a
        // 20px row), then the cell's own row; bounded by what is left of the 200px box.
        Assert.Contains("top: 40px", box.GetAttribute("style"));
        Assert.Contains("max-height: 160px", box.GetAttribute("style"));
        var editor = cut.Find(".ex-viewport .ex-editor");
        Assert.Equal("list", editor.GetAttribute("aria-autocomplete"));
        Assert.Equal(cut.Find(".ex-completion-list").Id, editor.GetAttribute("aria-controls"));
        Assert.Equal(cut.Find(".ex-completion-selected").Id, editor.GetAttribute("aria-activedescendant"));
    }

    [Fact] // ADR-0051: =X offers XLOOKUP, without regard to case
    public async Task A_different_prefix_offers_its_own_candidates()
    {
        var cut = RenderGrid(Synchronous());

        await TypeFormulaAsync(cut, "=x");

        Assert.Equal(["XLOOKUP"], Labels(cut));
    }

    [Fact] // ADR-0051 / DC-17: ↑/↓ choose among the candidates, and move nothing else
    public async Task Up_and_down_choose()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(Synchronous(), intents);
        await TypeFormulaAsync(cut, "=SU");

        await PressAsync(cut, "ArrowDown");
        Assert.Equal("SUMIF", Selected(cut));
        await PressAsync(cut, "ArrowDown"); // clamped at the last
        Assert.Equal("SUMIF", Selected(cut));
        await PressAsync(cut, "ArrowUp");
        Assert.Equal("SUM", Selected(cut));

        // In Overwrite the arrows would have committed and moved; the list had them instead.
        Assert.Empty(intents);
        Assert.Equal("=SU", EditorText(cut));
    }

    [Fact] // ADR-0051 / DC-17: Tab accepts the chosen candidate — the Consumer's span and text — and commits nothing
    public async Task Tab_accepts_the_chosen_candidate()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(Synchronous(), intents);
        await TypeFormulaAsync(cut, "=SU");
        await PressAsync(cut, "ArrowDown");

        await PressAsync(cut, "Tab");

        Assert.Equal("=SUMIF(", EditorText(cut));
        Assert.Empty(intents);
        Assert.Empty(cut.FindAll(".ex-completion-item"));
        // With no list open, Tab is Tab again: it commits and moves (ADR-0012).
        await PressAsync(cut, "Tab");
        Assert.Equal("=SUMIF(", Assert.Single(intents).Value);
    }

    [Fact] // ADR-0051: the accepted text is reported like any other, so the hint follows
    public async Task The_hint_follows_an_accepted_function()
    {
        var cut = RenderGrid(Synchronous());
        await TypeFormulaAsync(cut, "=SU");

        await PressAsync(cut, "Tab");

        var hint = cut.Find(".ex-completion .ex-completion-hint");
        Assert.Equal("SUM(number1, [number2], …)", hint.TextContent);
        Assert.Equal("number1", hint.QuerySelector("strong")!.TextContent);
        Assert.Empty(cut.FindAll(".ex-completion-item"));
    }

    [Fact] // ADR-0051 / DC-17: Escape closes the list and leaves the edit open; the next Escape cancels
    public async Task Escape_closes_the_list_before_it_cancels()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(Synchronous(), intents);
        await TypeFormulaAsync(cut, "=SU");

        await PressAsync(cut, "Escape");

        Assert.Empty(cut.FindAll(".ex-completion"));
        Assert.Equal("=SU", EditorText(cut));

        await PressAsync(cut, "Escape");
        Assert.Empty(cut.FindAll(".ex-editor"));
        Assert.Empty(intents);
    }

    [Fact] // ADR-0051: with the list closed, the arrows keep Overwrite's meaning
    public async Task After_escape_the_arrows_commit_and_move_again()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(Synchronous(), intents);
        await TypeFormulaAsync(cut, "=SU");
        await PressAsync(cut, "Escape");

        await PressAsync(cut, "ArrowDown");

        Assert.Equal("=SU", Assert.Single(intents).Value);
    }

    [Fact] // ADR-0051: a press on a candidate accepts it
    public async Task A_press_on_a_candidate_accepts_it()
    {
        var cut = RenderGrid(Synchronous());
        await TypeFormulaAsync(cut, "=SU");

        await cut.FindAll(".ex-completion-item")[1].MouseDownAsync(new MouseEventArgs { Button = 0 });

        Assert.Equal("=SUMIF(", EditorText(cut));
    }

    [Fact] // ADR-0051 / DC-18: an answer for text that has since changed is dropped, never shown
    public async Task A_stale_answer_is_never_shown()
    {
        var pending = new Dictionary<string, TaskCompletionSource<EditorCompletion?>>();
        var cut = RenderGrid((text, caret) =>
        {
            var answer = new TaskCompletionSource<EditorCompletion?>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending[text] = answer;
            return new ValueTask<EditorCompletion?>(answer.Task);
        });
        await TypeFormulaAsync(cut, "=S");
        await TypeAsync(cut, "=SU");
        await TypeAsync(cut, "=SUX");

        // The answer for "=S" lands after the user typed on: it answers text that is gone.
        pending["=S"].SetResult(Answer("=S", 2));
        await cut.InvokeAsync(() => { });
        Assert.Empty(cut.FindAll(".ex-completion"));

        // So does the one for "=SU", even though it has candidates to show.
        pending["=SU"].SetResult(Answer("=SU", 3));
        await cut.InvokeAsync(() => { });
        Assert.Empty(cut.FindAll(".ex-completion"));

        // Typed back to "=SU": the question is asked again, and that answer is current.
        await TypeAsync(cut, "=SU");
        pending["=SU"].SetResult(Answer("=SU", 3));
        cut.WaitForAssertion(() => Assert.Equal(["SUM", "SUMIF"], Labels(cut)));
    }

    [Fact] // ADR-0051 / DC-18: a list standing for the old text goes the moment the text changes
    public async Task A_list_goes_as_soon_as_the_text_changes()
    {
        var pending = new TaskCompletionSource<EditorCompletion?>();
        var cut = RenderGrid((text, caret) => text == "=SU"
            ? new ValueTask<EditorCompletion?>(pending.Task)
            : ValueTask.FromResult(Answer(text, caret)));
        await TypeFormulaAsync(cut, "=S");
        Assert.Equal(["SUM", "SUMIF"], Labels(cut));

        // "=SU" is answered on its own time; until then nothing answers what is in the editor.
        await TypeAsync(cut, "=SU");

        Assert.Empty(cut.FindAll(".ex-completion"));
    }

    [Fact] // ADR-0051 / DC-18: an answer arriving after the edit closed is dropped
    public async Task An_answer_after_the_edit_closed_is_dropped()
    {
        var pending = new TaskCompletionSource<EditorCompletion?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cut = RenderGrid((_, _) => new ValueTask<EditorCompletion?>(pending.Task));
        await TypeFormulaAsync(cut, "=SU");
        await PressAsync(cut, "Escape");

        pending.SetResult(Answer("=SU", 3));
        await cut.InvokeAsync(() => { });

        Assert.Empty(cut.FindAll(".ex-completion"));
        Assert.Empty(cut.FindAll(".ex-editor"));
    }

    [Fact] // ADR-0051 / DC-17: from the Formula Bar as from the cell — the list stands beneath the bar
    public async Task The_list_works_from_the_formula_bar()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(Synchronous(), intents, formulaBar: true);
        await ClickAsync(cut, 50, 10);
        var bar = cut.Find(".ex-formula-bar-text");
        await bar.FocusAsync(new FocusEventArgs());
        Assert.Equal("caret", EditingModesTold()[^1]);

        await cut.Find(".ex-formula-bar-text").InputAsync(new ChangeEventArgs { Value = "=SU" });

        Assert.Equal(["SUM", "SUMIF"], Labels(cut));
        var style = cut.Find(".ex-completion").GetAttribute("style")!;
        // Beneath the bar: its 20px band, at a 20px row (ADR-0028).
        Assert.Contains("top: 20px", style);
        // In Caret the arrows are the editor's, but while a list is open ↑/↓ are the list's: the
        // gate is told to claim them, and told back when the list closes (ADR-0010).
        Assert.Equal("overwrite", EditingModesTold()[^1]);

        await PressAsync(cut, "ArrowDown");
        await PressAsync(cut, "Tab");

        Assert.Equal("=SUMIF(", cut.Find(".ex-formula-bar-text").GetAttribute("value"));
        Assert.Equal("=SUMIF(", EditorText(cut));
        Assert.Equal("caret", EditingModesTold()[^1]);
        Assert.Empty(intents);
    }

    [Fact] // ADR-0051: in Caret with a list open, the caret keys the gate claimed only close the list
    public async Task In_caret_a_left_arrow_closes_the_list_and_moves_nothing()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var cut = RenderGrid(Synchronous(), intents, formulaBar: true);
        await ClickAsync(cut, 50, 10);
        await cut.Find(".ex-formula-bar-text").FocusAsync(new FocusEventArgs());
        await cut.Find(".ex-formula-bar-text").InputAsync(new ChangeEventArgs { Value = "=SU" });

        await PressAsync(cut, "ArrowLeft");
        // A second arrow, arriving before the gate heard the list was gone, moves nothing either.
        await PressAsync(cut, "ArrowDown");

        Assert.Empty(cut.FindAll(".ex-completion"));
        Assert.Empty(intents);
        Assert.Equal("=SU", EditorText(cut));
    }

    [Fact] // ADR-0040 / DC-17: near the bottom of the box the list opens above the cell, bounded by the room there
    public async Task Near_the_bottom_the_list_opens_above()
    {
        var cut = RenderGrid(Synchronous(), viewportHeight: 120);
        // The last painted row: 120px less the 20px header leaves five rows.
        await ClickAsync(cut, 50, 90);
        await PressAsync(cut, "=");
        await TypeAsync(cut, "=SU");

        var style = cut.Find(".ex-completion").GetAttribute("style")!;
        Assert.Contains("translateY(-100%)", style);
        Assert.Contains("top: 100px", style);
        Assert.Contains("max-height: 100px", style);
    }

    [Fact] // ADR-0003: typing and the list render no row
    public async Task Completion_renders_no_row()
    {
        var cut = RenderGrid(Synchronous());
        await ClickAsync(cut, 50, 10);
        await PressAsync(cut, "=");
        var before = cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList();

        await TypeAsync(cut, "=SU");
        await PressAsync(cut, "ArrowDown");
        await PressAsync(cut, "Tab");

        Assert.Equal(before, cut.FindComponents<ExGridRow<TestRow>>().Select(r => r.RenderCount).ToList());
    }

    /// <summary>A Chrome that paints only the completion seam, recording what it was handed.</summary>
    private sealed class CompletionChrome : IGridChrome
    {
        public EditorCompletionContext? Handed { get; private set; }

        public RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public RenderFragment? CellEditor(CellEditorContext context) => null;

        public RenderFragment? LoadingIndicator(LoadingContext context) => null;

        public RenderFragment? EditorCompletion(EditorCompletionContext context)
        {
            Handed = context;
            return builder => builder.AddMarkupContent(0, "<span class='stub-completion'></span>");
        }
    }

    [Fact] // ADR-0051/0010 / DC-17: a Chrome paints the contents inside the core's box, and its accept is the core's
    public async Task A_chrome_paints_the_list_and_calls_back()
    {
        var chrome = new CompletionChrome();
        var cut = RenderGrid(Synchronous(), chrome: chrome);
        await TypeFormulaAsync(cut, "=SU");

        Assert.NotNull(cut.Find(".ex-grid > .ex-completion > .stub-completion"));
        Assert.Empty(cut.FindAll(".ex-completion-item"));
        var context = chrome.Handed!;
        Assert.Equal(["SUM", "SUMIF"], context.Candidates.Select(c => c.Label));
        Assert.Equal(0, context.Selected);

        await PressAsync(cut, "ArrowDown");
        Assert.Equal(1, chrome.Handed!.Selected);

        await cut.InvokeAsync(() => chrome.Handed!.Accept(0));
        cut.WaitForAssertion(() => Assert.Equal("=SUM(", EditorText(cut)));
    }
}
