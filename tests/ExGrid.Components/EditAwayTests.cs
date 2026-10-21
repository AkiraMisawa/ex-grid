using Bunit;
using ExGrid.Cells;
using ExGrid.Clipboard;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// An edit stays open with the rows it was opened among while a view of other rows is shown
/// (ADR-0331 §5): away, its editor holds the keyboard unseen, the Formula Bar shows its text, Point
/// points at the rows shown from their Focus, and the commit raises the Edit Intent with the row it was
/// opened on. A view the grid read of its rows, handed back, brings the edit home. A Sheet-shaped grid:
/// 50 rows of 20px in a 350 × 200 Viewport, Book (A) editable.
/// </summary>
public class EditAwayTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns(Func<TestRow, string, EditVerdict>? validate = null) =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true, validate: validate),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
    ];

    private static bool PointAt(string text, int caret)
        => text.StartsWith('=') && caret > 0 && caret <= text.Length && "=+-*/(,".Contains(text[caret - 1]);

    private static string ReferenceText(SelectionRange range)
        => FormattableString.Invariant($"{(char)('A' + range.LeftColumn)}{range.TopRow + 1}");

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        TestRow[] rows, List<GridEditIntent<TestRow>> intents,
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? more = null,
        Func<TestRow, string, EditVerdict>? validate = null)
        => Render<ExGrid<TestRow>>(ps => (more ?? (_ => { }))(ps
            .Add(g => g.Window, rows)
            .Add(g => g.TotalCount, rows.Length)
            .Add(g => g.Columns, Columns(validate))
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 200)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.RowSequenceVersion, 0)
            .Add(g => g.ShowFormulaBar, true)
            .Add(g => g.PointAt, PointAt)
            .Add(g => g.ReferenceText, ReferenceText)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> intent) => intents.Add(intent))));

    private static Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y });

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, string? text = null, int caret = -1)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, false, false, false, false, false, editorText: text, editorCaret: caret));

    private static string Bar(IRenderedComponent<ExGrid<TestRow>> cut) => cut.Find(".ex-formula-bar-text").GetAttribute("value") ?? "";

    /// <summary><c>=</c> typed onto A3 of the first rows, then the other rows shown with no view kept for them.</summary>
    private async Task<(IRenderedComponent<ExGrid<TestRow>> Grid, TestRow[] Home, GridView Kept)> TypeThenShowOtherRowsAsync(
        List<GridEditIntent<TestRow>> intents,
        Action<ComponentParameterCollectionBuilder<ExGrid<TestRow>>>? more = null,
        Func<TestRow, string, EditVerdict>? validate = null)
    {
        var home = TestRows.Many(50);
        var cut = RenderGrid(home, intents, more, validate);
        await ClickAsync(cut, 50, 50);
        await PressAsync(cut, "=");
        var kept = cut.Instance.ReadView();
        await cut.InvokeAsync(() => cut.Instance.ShowViewWith(new GridView(1, GridSelection.Empty, 0, 0)));
        cut.Render(ps => ps.Add(g => g.Window, TestRows.Many(50)).Add(g => g.RowSequenceVersion, 1));
        return (cut, home, kept);
    }

    [Fact] // ADR-0331 §5: away, the edit stays open, its editor unseen and its text in the Formula Bar
    public async Task An_edit_stays_open_away_from_its_rows()
    {
        var (cut, _, _) = await TypeThenShowOtherRowsAsync([]);

        Assert.Equal("=", cut.Instance.ReadEditText());
        Assert.Contains("ex-editor-away", cut.Find(".ex-viewport .ex-editor").ClassName);
        Assert.Equal("=", Bar(cut));
    }

    [Fact] // ADR-0331 §5: away, Point points at the rows shown, from their Focus, and the commit lands on the row the edit was opened on
    public async Task Away_point_writes_among_the_rows_shown_and_the_commit_lands_on_its_own_row()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var (cut, home, _) = await TypeThenShowOtherRowsAsync(intents);

        await PressAsync(cut, "ArrowDown", "=", 1);
        Assert.Equal("=A2", cut.Instance.ReadEditText());
        await PressAsync(cut, "Enter", "=A2", 3);

        var intent = Assert.Single(intents);
        Assert.Same(home[2], intent.Row);
        Assert.Equal("Book", intent.Column);
        Assert.Equal("=A2", intent.Value);
        Assert.Null(cut.Instance.ReadEditText());
    }

    [Fact] // ADR-0331 §5: away, a press on a cell points at it
    public async Task Away_a_press_points_at_the_cell_pressed()
    {
        var (cut, _, _) = await TypeThenShowOtherRowsAsync([]);

        await ClickAsync(cut, 50, 90);

        Assert.Equal("=A5", cut.Instance.ReadEditText());
    }

    [Fact] // ADR-0331 §5: the view read of the edit's rows, handed back, brings the edit home over its cell, and the commit lands there
    public async Task The_view_handed_back_brings_the_edit_home()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var (cut, home, kept) = await TypeThenShowOtherRowsAsync(intents);
        await PressAsync(cut, "ArrowDown", "=", 1);

        await cut.InvokeAsync(() => cut.Instance.ShowViewWith(kept with { RowSequenceVersion = 2 }));
        cut.Render(ps => ps.Add(g => g.Window, home).Add(g => g.RowSequenceVersion, 2));

        Assert.DoesNotContain("ex-editor-away", cut.Find(".ex-viewport .ex-editor").ClassName);
        Assert.Equal("=A2", cut.Instance.ReadEditText());
        await PressAsync(cut, "ArrowDown", "=A2", 3);
        Assert.Equal("=A4", cut.Instance.ReadEditText());
        await PressAsync(cut, "Enter", "=A4", 3);
        Assert.Same(home[2], Assert.Single(intents).Row);
        Assert.Equal(new CellPosition(3, 0), cut.Instance.ReadSelection().Selection.Focus);
    }

    [Fact] // ADR-0331 §5: away, Ctrl+Enter's fill has no rows of its own to land on, so it is refused and nothing is written
    public async Task Away_ctrl_enter_is_refused()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var refusals = new List<PasteRefusalReason>();
        var (cut, _, _) = await TypeThenShowOtherRowsAsync(intents,
            ps => ps.Add(g => g.OnPasteRefused, (PasteRefusalReason reason) => refusals.Add(reason)));

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("Enter", true, false, false, false, false, editorText: "=1", editorCaret: 2));

        Assert.Equal([PasteRefusalReason.OrderMoved], refusals);
        Assert.Empty(intents);
    }

    [Fact] // ADR-0331 §5: away, a rejected value keeps the edit open but shows no message over a cell of the rows shown
    public async Task Away_a_rejected_value_shows_no_message_over_the_rows_shown()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var (cut, _, _) = await TypeThenShowOtherRowsAsync(intents, validate: (_, _) => EditVerdict.Reject("no"));

        await PressAsync(cut, "Enter", "=1", 2);

        Assert.Empty(intents);
        Assert.Equal("=1", cut.Instance.ReadEditText());
        Assert.Empty(cut.FindAll(".ex-message"));
    }

    [Fact] // ADR-0331 §5, ADR-0160: away, the Overwrite Notice reads the edit's own row, so a value written there meanwhile is told
    public async Task Away_a_value_written_under_the_edit_meanwhile_raises_the_overwrite_notice()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        var notices = new List<GridOverwriteNotice>();
        var (cut, home, _) = await TypeThenShowOtherRowsAsync(intents,
            ps => ps.Add(g => g.OnOverwriteNotice, (GridOverwriteNotice notice) => notices.Add(notice)));
        var seen = home[2].Book;
        home[2].Book = "Written meanwhile";

        await PressAsync(cut, "Enter", "=1", 2);

        var notice = Assert.Single(notices);
        Assert.Equal(seen, notice.SeenText);
        Assert.Equal("Written meanwhile", notice.ReplacedText);
        Assert.Same(home[2], Assert.Single(intents).Row);
    }

    [Fact] // ADR-0331 §5: the row set a view carries is the grid's own note, not part of what the view says
    public void A_view_read_equals_the_same_view_built()
    {
        var cut = RenderGrid(TestRows.Many(50), []);

        var read = cut.Instance.ReadView();

        Assert.Equal(new GridView(read.RowSequenceVersion, read.Selection, read.ScrollTopPx, read.ScrollLeftPx), read);
        Assert.Equal(new GridView(read.RowSequenceVersion, read.Selection, read.ScrollTopPx, read.ScrollLeftPx).GetHashCode(), read.GetHashCode());
    }
}
