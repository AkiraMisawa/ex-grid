using Bunit;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Finding;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The Match Mark (ADR-0332 §4): while the find panel holds a search, every painted cell that
/// matches is marked, the current match apart; the grid judges the painted cells with a match
/// function — the reference rule where a Source answers Find, the Consumer's own or none where
/// OnFind does — and a row renders only when its marks move (ADR-0003).
/// 20px rows in a 120px Viewport, three 100px columns: Book ("Row 000000" …), Amount (its index),
/// AsOf.
/// </summary>
public class MatchMarkTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
        new("AsOf", ColumnType.Date, r => r.AsOf, width: Fixed100),
    ];

    private IRenderedComponent<ExGrid<TestRow>> RenderWithSource(int rows = 200, GridColumn<TestRow>[]? columns = null)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, GridSource.From(TestRows.Many(rows)))
            .Add(g => g.Columns, columns ?? Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350));

    private GridSelection? _selection;

    private IRenderedComponent<ExGrid<TestRow>> RenderWithOwnFind(FindMatchOf<TestRow>? match, int rows = 200)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.SelectionChanged, s => _selection = s);
            var window = TestRows.Many(rows);
            ps.Add(g => g.Window, window)
              .Add(g => g.TotalCount, rows)
              .Add(g => g.Columns, Columns())
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 120)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.OnFind, (request, _) => Task.FromResult(GridFind.Step(window, request, Columns().ToDictionary(c => c.Name, c => (Func<TestRow, string>)c.Info.TextOf).GetValueOrDefault)));
            if (match is not null)
                ps.Add(g => g.FindMatch, match);
        });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, bool ctrl = false)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, ctrl, false, false, false, false));

    private static Task TypeAsync(IRenderedComponent<ExGrid<TestRow>> cut, string text)
        => cut.Find("input.ex-find-field").InputAsync(new ChangeEventArgs { Value = text });

    private static Task NextAsync(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.Find("button.ex-find-next").ClickAsync(new MouseEventArgs());

    /// <summary>Each painted row's Book, with the columns of its marked cells, "*" after the current one.</summary>
    private static Dictionary<string, string> Marks(IRenderedComponent<ExGrid<TestRow>> cut)
    {
        var marks = new Dictionary<string, string>();
        foreach (var row in cut.FindAll(".ex-row"))
        {
            var cells = row.QuerySelectorAll(".ex-cell");
            var marked = new List<string>();
            for (var i = 0; i < cells.Length; i++)
            {
                if (cells[i].ClassList.Contains("ex-match-mark"))
                    marked.Add(cells[i].ClassList.Contains("ex-match-mark-current") ? $"{i}*" : $"{i}");
            }
            if (marked.Count > 0)
                marks[cells[0].TextContent] = string.Join(",", marked);
        }
        return marks;
    }

    private static Dictionary<string, int> RenderCounts(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.FindComponents<ExGridRow<TestRow>>().ToDictionary(row => row.Instance.Row.Book, row => row.RenderCount);

    private static string[] Changed(Dictionary<string, int> before, Dictionary<string, int> after)
        => [.. after.Where(kv => !before.TryGetValue(kv.Key, out var was) || was != kv.Value).Select(kv => kv.Key).Order(StringComparer.Ordinal)];

    [Fact] // ADR-0332 §4: with a Grid Source, every painted cell matching what the panel holds is marked by the reference rule, as it is typed
    public async Task ADR0332_a_source_marks_every_painted_match_as_it_is_typed()
    {
        var cut = RenderWithSource();
        await PressAsync(cut, "f", ctrl: true);

        await TypeAsync(cut, "row 00000");

        // Ignoring case, as the reference rule does: every painted row's Book, and nothing else.
        var painted = cut.FindAll(".ex-row").Select(r => r.QuerySelector(".ex-cell")!.TextContent).ToArray();
        Assert.NotEmpty(painted);
        Assert.Equal(painted.ToDictionary(b => b, _ => "0"), Marks(cut));

        await TypeAsync(cut, "Row 000003");
        Assert.Equal(new Dictionary<string, string> { ["Row 000003"] = "0" }, Marks(cut));

        // The displayed text, across columns: Amount 3 shows "3", and Row 000003's Book holds it too.
        await TypeAsync(cut, "3");
        Assert.Equal("0,1", Marks(cut)["Row 000003"]);
    }

    [Fact] // ADR-0332 §4: the options are the reference rule's — Match case and Whole cell change what is marked
    public async Task ADR0332_the_options_change_what_is_marked()
    {
        var cut = RenderWithSource();
        await PressAsync(cut, "f", ctrl: true);
        await TypeAsync(cut, "row 000003");
        Assert.Equal(new Dictionary<string, string> { ["Row 000003"] = "0" }, Marks(cut));

        await cut.Find("input.ex-find-match-case").ChangeAsync(new ChangeEventArgs { Value = true });
        Assert.Empty(Marks(cut));

        await cut.Find("input.ex-find-match-case").ChangeAsync(new ChangeEventArgs { Value = false });
        await TypeAsync(cut, "3");
        await cut.Find("input.ex-find-whole-cell").ChangeAsync(new ChangeEventArgs { Value = true });
        Assert.Equal(new Dictionary<string, string> { ["Row 000003"] = "1" }, Marks(cut));
    }

    [Fact] // ADR-0332 §4: the current match — the Focus a found step moved to — is marked apart, and moves with the next step
    public async Task ADR0332_the_current_match_is_marked_apart()
    {
        var cut = RenderWithSource();
        await PressAsync(cut, "f", ctrl: true);
        await TypeAsync(cut, "Row 00000");

        await NextAsync(cut);
        var marks = Marks(cut);
        Assert.Equal("0*", marks["Row 000000"]);
        Assert.Equal("0", marks["Row 000001"]);
        Assert.Single(cut.FindAll(".ex-match-mark-current"));

        await NextAsync(cut);
        marks = Marks(cut);
        Assert.Equal("0", marks["Row 000000"]);
        Assert.Equal("0*", marks["Row 000001"]);
        Assert.Single(cut.FindAll(".ex-match-mark-current"));
    }

    [Fact] // ADR-0332 §4: the marks clear when the panel closes, and when its text is emptied
    public async Task ADR0332_the_marks_clear_when_the_panel_closes_or_the_text_empties()
    {
        var cut = RenderWithSource();
        await PressAsync(cut, "f", ctrl: true);
        await TypeAsync(cut, "Row 00000");
        await NextAsync(cut);
        Assert.NotEmpty(cut.FindAll(".ex-match-mark"));

        await TypeAsync(cut, "");
        Assert.Empty(cut.FindAll(".ex-match-mark"));

        await TypeAsync(cut, "Row 00000");
        Assert.NotEmpty(cut.FindAll(".ex-match-mark"));
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("Escape", false, false, false, false, false, fromDescendant: true));
        Assert.Empty(cut.FindAll(".ex-popover-find"));
        Assert.Empty(cut.FindAll(".ex-match-mark"));

        // Reopened, the panel holds its text again (ADR-0055), and so marks again.
        await PressAsync(cut, "f", ctrl: true);
        Assert.NotEmpty(cut.FindAll(".ex-match-mark"));
        Assert.Empty(cut.FindAll(".ex-match-mark-current"));
    }

    [Fact] // ADR-0332 §4: a row scrolled into view is marked as it is painted, with no step asked
    public async Task ADR0332_rows_scrolled_into_view_are_marked_as_painted()
    {
        var cut = RenderWithSource();
        await PressAsync(cut, "f", ctrl: true);
        await TypeAsync(cut, "Row 0000");
        var before = cut.FindAll(".ex-row").Select(r => r.QuerySelector(".ex-cell")!.TextContent).ToHashSet();

        await ScrollToAsync(cut.Find(".ex-scroller"), top: 100);

        var painted = cut.FindAll(".ex-row").Select(r => r.QuerySelector(".ex-cell")!.TextContent).ToArray();
        Assert.Contains(painted, book => !before.Contains(book));
        Assert.Equal(painted.ToDictionary(b => b, _ => "0"), Marks(cut));
    }

    [Fact] // ADR-0332 §4: a Consumer that answers Find itself and passes no match function gets no marks — its rule may not be the reference one
    public async Task ADR0332_an_own_onfind_without_a_match_function_marks_nothing()
    {
        var cut = RenderWithOwnFind(match: null);
        await PressAsync(cut, "f", ctrl: true);
        await TypeAsync(cut, "Row 00000");
        await NextAsync(cut);

        Assert.Equal(new CellPosition(0, 0), _selection!.Focus);
        Assert.Empty(cut.FindAll(".ex-match-mark"));
    }

    [Fact] // ADR-0332 §4: a Consumer's match function judges the painted cells, handed each value cell and what the panel holds
    public async Task ADR0332_an_own_match_function_judges_the_painted_cells()
    {
        var asked = new List<(string Column, GridFindPattern Pattern)>();
        // Its own rule: an Amount cell matches when the text is its parity.
        FindMatchOf<TestRow> parity = (row, column, pattern) =>
        {
            asked.Add((column.Name, pattern));
            return column.Name == "Amount" && pattern.Text == (row.Amount % 2 == 0 ? "even" : "odd");
        };
        var cut = RenderWithOwnFind(parity);
        await PressAsync(cut, "f", ctrl: true);
        await cut.Find("input.ex-find-whole-cell").ChangeAsync(new ChangeEventArgs { Value = true });

        await TypeAsync(cut, "odd");

        var marks = Marks(cut);
        Assert.Equal("1", marks["Row 000001"]);
        Assert.Equal("1", marks["Row 000003"]);
        Assert.False(marks.ContainsKey("Row 000000"));
        Assert.Contains(asked, a => a.Pattern == new GridFindPattern("odd", false, true));
        Assert.Equal(["Amount", "AsOf", "Book"], asked.Select(a => a.Column).Distinct().Order(StringComparer.Ordinal));
    }

    [Fact] // ADR-0332 §4: an Action or Template cell paints no value, so is never judged or marked
    public async Task ADR0332_cells_that_paint_no_value_are_never_judged()
    {
        var asked = new List<string>();
        FindMatchOf<TestRow> any = (_, column, _) =>
        {
            asked.Add(column.Name);
            return true;
        };
        var window = TestRows.Many(20);
        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, window)
            .Add(g => g.TotalCount, 20)
            .Add(g => g.Columns, (GridColumn<TestRow>[])[
                new("Book", ColumnType.Text, r => r.Book, width: Fixed100),
                GridColumn<TestRow>.ActionColumn("Open", [new GridAction("open", "Open")], width: Fixed100),
            ])
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnFind, (_, _) => Task.FromResult(GridFindResult.NotFound))
            .Add(g => g.FindMatch, any));
        await PressAsync(cut, "f", ctrl: true);
        await TypeAsync(cut, "x");

        Assert.DoesNotContain("Open", asked);
        Assert.All(cut.FindAll(".ex-row"), row =>
        {
            var cells = row.QuerySelectorAll(".ex-cell");
            Assert.Contains("ex-match-mark", cells[0].ClassList);
            Assert.DoesNotContain("ex-match-mark", cells[1].ClassList);
        });
    }

    [Fact] // ADR-0332 §4 / ADR-0003: opening the panel and typing re-render only the rows whose marks move
    public async Task ADR0332_only_rows_whose_marks_move_render()
    {
        var cut = RenderWithSource();
        var counts = RenderCounts(cut);

        await PressAsync(cut, "f", ctrl: true);
        Assert.Empty(Changed(counts, RenderCounts(cut)));

        // Nothing painted matches: no row renders.
        await TypeAsync(cut, "nowhere");
        Assert.Empty(Changed(counts, RenderCounts(cut)));

        await TypeAsync(cut, "Row 000001");
        Assert.Equal(["Row 000001"], Changed(counts, RenderCounts(cut)));
        counts = RenderCounts(cut);

        await TypeAsync(cut, "Row 000002");
        Assert.Equal(["Row 000001", "Row 000002"], Changed(counts, RenderCounts(cut)));
        counts = RenderCounts(cut);

        // An option that leaves every mark where it was renders nothing.
        await cut.Find("input.ex-find-whole-cell").ChangeAsync(new ChangeEventArgs { Value = true });
        Assert.Empty(Changed(counts, RenderCounts(cut)));
    }

    [Fact] // ADR-0332 §4 / ADR-0003: a step re-renders only the rows the current match leaves and reaches
    public async Task ADR0332_a_step_renders_only_the_rows_the_current_match_moves_between()
    {
        var cut = RenderWithSource();
        await PressAsync(cut, "f", ctrl: true);
        await TypeAsync(cut, "Row 00000");
        await NextAsync(cut);
        var counts = RenderCounts(cut);

        await NextAsync(cut);

        Assert.Equal(["Row 000000", "Row 000001"], Changed(counts, RenderCounts(cut)));
    }

    [Fact] // ADR-0332 §4 / ADR-0003: closing the panel re-renders only the rows that wore a mark
    public async Task ADR0332_closing_renders_only_the_marked_rows()
    {
        var cut = RenderWithSource();
        await PressAsync(cut, "f", ctrl: true);
        await TypeAsync(cut, "Row 000004");
        var counts = RenderCounts(cut);

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("Escape", false, false, false, false, false, fromDescendant: true));

        Assert.Equal(["Row 000004"], Changed(counts, RenderCounts(cut)));
        Assert.Empty(cut.FindAll(".ex-match-mark"));
    }

    [Fact] // ADR-0332 §4 / ADR-0027: the mark is a class on cells already painted — the DOM does not grow and nothing per cell reaches JavaScript
    public async Task ADR0332_marks_add_no_elements_and_no_script_calls()
    {
        var cut = RenderWithSource();
        await PressAsync(cut, "f", ctrl: true);
        var elements = cut.FindAll(".ex-viewport *").Count;
        var calls = JSInterop.Invocations.Count;

        await TypeAsync(cut, "Row 0000");

        Assert.NotEmpty(cut.FindAll(".ex-match-mark"));
        Assert.Equal(elements, cut.FindAll(".ex-viewport *").Count);
        Assert.Equal(calls, JSInterop.Invocations.Count);
    }
}
