using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A press made while the Consumer hears a commit (ADR-0142; ADR-0010's click-away, ED-12). The Edit
/// Intent is heard while the editor still stands, so that a refusal can hold it as a Reject does
/// (ADR-0034's note of 2026-10-07, LV-19), and a commit takes as long as the Consumer's handler does. A
/// press that arrives meanwhile — on another row, the second press of a double click, on a header or in a
/// column menu, and the click, double click or context menu that follows a press — waits for that commit
/// to land, and is then answered against what it leaves: one commit raises one Edit Intent, however many
/// presses arrive while it is heard, and the press does what it does with no editor open. The outcome is
/// the one a Consumer that answers at once gets, whichever order slow answers come back in (principle 6),
/// and nothing the commit was heard under outlives it: a Source replaced, or columns changed, under a
/// later edit discard that edit and say so (LV-32, ED-21).
///
/// 20px rows in a 120px Viewport, 350px wide: Book 0–100 and Amount 100–200, both editable. The bound
/// source takes a sort, and the headers carry a column menu.
/// </summary>
public class PressesWhileACommitIsHeardTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private static GridColumn<TestRow>[] Columns() =>
    [
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: true),
    ];

    private static GridColumn<TestRow>[] OtherColumns() =>
    [
        new("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: true),
        new("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
    ];

    /// <summary>How the Consumer answers the Edit Intents it hears: at once, or once the test gives the
    /// answers — in the order the intents were raised, or the last raised first.</summary>
    public enum Answered
    {
        AtOnce,
        InOrder,
        LastFirst,
    }

    /// <summary>What the Consumer heard, and the answers it still owes.</summary>
    private sealed class Heard
    {
        public List<GridEditIntent<TestRow>> Edits { get; } = [];

        public List<EditDiscardReason> Discards { get; } = [];

        /// <summary>The answers to the Edit Intents heard and not yet answered, in the order raised.</summary>
        public List<TaskCompletionSource> Pending { get; } = [];

        /// <summary>Whether an Edit Intent is answered only once the test gives the answer.</summary>
        public bool Slow { get; set; }

        /// <summary>The refusal the Consumer answers every Edit Intent with, if it refuses them.</summary>
        public string? Refusal { get; set; }

        public async Task OnEditAsync(GridEditIntent<TestRow> intent)
        {
            Edits.Add(intent);
            if (Slow)
            {
                var answer = new TaskCompletionSource();
                Pending.Add(answer);
                await answer.Task;
            }
            if (Refusal is { } refusal)
                intent.Refuse(refusal);
        }
    }

    private static TestSource SourceOf(TestRow[] rows)
    {
        var source = new TestSource();
        source.Push(rows, totalCount: rows.Length);
        return source;
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(TestSource source, Heard heard)
        => Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> i) => heard.OnEditAsync(i))
            .Add(g => g.OnEditDiscarded, (EditDiscardReason r) => heard.Discards.Add(r)));

    private static Task KeyAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, false, false, false, false, false));

    private static Task DownAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y, long button = 0)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs
        {
            Button = button, Buttons = button == 0 ? 1 : 2, OffsetX = x, OffsetY = y, Detail = 1,
        });

    private static Task UpAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, OffsetX = x, OffsetY = y, Detail = 1 });

    private static async Task ClickAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
    {
        await DownAsync(cut, x, y);
        await UpAsync(cut, x, y);
    }

    /// <summary>A press on another row, the third: its press and its release.</summary>
    private static Task[] PressRow(IRenderedComponent<ExGrid<TestRow>> cut, double y)
        => [DownAsync(cut, 50, y), UpAsync(cut, 50, y)];

    /// <summary>A press on Book's header and the click that ends it, which sorts.</summary>
    private static Task[] PressHeader(IRenderedComponent<ExGrid<TestRow>> cut)
        =>
        [
            cut.Find(".ex-header").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 30, OffsetY = 10, ClientX = 30 }),
            cut.Find(".ex-header").ClickAsync(new MouseEventArgs { Button = 0, OffsetX = 30, OffsetY = 10, ClientX = 30 }),
        ];

    /// <summary>A press on Amount's column menu button, which opens its menu.</summary>
    private static Task[] PressMenu(IRenderedComponent<ExGrid<TestRow>> cut)
        => [cut.FindAll(".ex-menu-button")[1].ClickAsync(new MouseEventArgs())];

    private static Task[] Press(IRenderedComponent<ExGrid<TestRow>> cut, string press) => press switch
    {
        "row" => PressRow(cut, 70),
        "header" => PressHeader(cut),
        "menu" => PressMenu(cut),
        _ => throw new ArgumentOutOfRangeException(nameof(press), press, null),
    };

    /// <summary>The Consumer gives its answers to the Edit Intents it is hearing, in the order asked, and
    /// answers any it hears from then on at once. Asked while nothing is being handled: every gesture made
    /// is waiting on an answer, so the answers owed are all in <see cref="Heard.Pending"/>.</summary>
    private static async Task AnswerAsync(IRenderedComponent<ExGrid<TestRow>> cut, Heard heard, Answered answered)
    {
        heard.Slow = false;
        TaskCompletionSource[] answers = answered == Answered.LastFirst ? [.. Enumerable.Reverse(heard.Pending)] : [.. heard.Pending];
        heard.Pending.Clear();
        foreach (var answer in answers)
            await cut.InvokeAsync(answer.SetResult);
    }

    private static bool EditorOpen(IRenderedComponent<ExGrid<TestRow>> cut) => cut.FindAll("input.ex-editor").Count > 0;

    private static CellPosition Focus(IRenderedComponent<ExGrid<TestRow>> cut) => cut.Instance.ReadSelection().Selection.Focus;

    /// <summary>Types <c>5</c> into Book of the first row, presses the third row — a commit the Consumer
    /// hears — and, while it is heard, makes <paramref name="press"/>; then the Consumer answers.</summary>
    private static async Task PressWhileACommitIsHeardAsync(
        IRenderedComponent<ExGrid<TestRow>> cut, Heard heard, string press, Answered answered)
    {
        heard.Slow = answered != Answered.AtOnce;
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");
        Assert.True(EditorOpen(cut));

        Task[] made = [.. PressRow(cut, 50), .. Press(cut, press)];
        await AnswerAsync(cut, heard, answered);
        await Task.WhenAll(made);
    }

    [Theory] // ADR-0142 / ADR-0010, ED-12, principle 6: a press made while the Consumer hears a commit waits for it to land — one Edit Intent for the one commit — and then does what it does with no editor open, as it does when the Consumer answers at once
    [InlineData("row", Answered.AtOnce)]
    [InlineData("row", Answered.InOrder)]
    [InlineData("row", Answered.LastFirst)]
    [InlineData("header", Answered.AtOnce)]
    [InlineData("header", Answered.InOrder)]
    [InlineData("header", Answered.LastFirst)]
    [InlineData("menu", Answered.AtOnce)]
    [InlineData("menu", Answered.InOrder)]
    [InlineData("menu", Answered.LastFirst)]
    public async Task A_press_made_while_a_commit_is_heard_raises_no_second_edit_intent(string press, Answered answered)
    {
        var heard = new Heard();
        var source = SourceOf(TestRows.Many(50));
        var cut = RenderGrid(source, heard);

        await PressWhileACommitIsHeardAsync(cut, heard, press, answered);

        var edit = Assert.Single(heard.Edits);
        Assert.Equal(("Row 000000", "5"), (edit.Row.Book, edit.Value));
        Assert.False(EditorOpen(cut));
        Assert.Empty(heard.Discards);
        switch (press)
        {
            case "row":
                // The later press was answered after the earlier one.
                Assert.Equal(new CellPosition(3, 0), Focus(cut));
                break;
            case "header":
                Assert.Equal([new SortSpec("Book", SortDirection.Ascending)], Assert.Single(source.SortChanges));
                break;
            case "menu":
                Assert.Single(cut.FindAll(".ex-popover"));
                break;
        }
    }

    [Theory] // ADR-0142 / LV-32, principle 6: after a press made while a commit was heard, a Source replaced under a later edit discards that edit, said once as SourceChanged, and Enter commits nothing onto the new Source's row — whichever order the answers came back in
    [InlineData("row", Answered.InOrder)]
    [InlineData("row", Answered.LastFirst)]
    [InlineData("header", Answered.InOrder)]
    [InlineData("header", Answered.LastFirst)]
    [InlineData("menu", Answered.InOrder)]
    [InlineData("menu", Answered.LastFirst)]
    public async Task After_a_press_made_while_a_commit_was_heard_a_replaced_source_still_discards_an_edit(string press, Answered answered)
    {
        var rows = TestRows.Many(50);
        var heard = new Heard();
        var cut = RenderGrid(SourceOf(rows), heard);
        await PressWhileACommitIsHeardAsync(cut, heard, press, answered);
        heard.Slow = false;
        heard.Edits.Clear();

        // The press on the second row closes the column menu a press may have opened.
        await ClickAsync(cut, 50, 30);
        await KeyAsync(cut, "7");
        var copies = rows.Select(r => new TestRow { Book = "Other " + r.Book, Amount = r.Amount, AsOf = r.AsOf, Active = r.Active }).ToArray();
        cut.Render(ps => ps.Add(g => g.Source, SourceOf(copies)));
        await KeyAsync(cut, "Enter");

        Assert.Equal([EditDiscardReason.SourceChanged], heard.Discards);
        Assert.Empty(heard.Edits);
        Assert.False(EditorOpen(cut));
    }

    [Theory] // ADR-0011 / ED-21, principle 6: after a press made while a commit was heard, columns changed under a later edit take it down and say so, once, as ColumnsChanged — whichever order the answers came back in
    [InlineData("row", Answered.InOrder)]
    [InlineData("row", Answered.LastFirst)]
    [InlineData("header", Answered.InOrder)]
    [InlineData("header", Answered.LastFirst)]
    [InlineData("menu", Answered.InOrder)]
    [InlineData("menu", Answered.LastFirst)]
    public async Task After_a_press_made_while_a_commit_was_heard_changed_columns_still_discard_an_edit(string press, Answered answered)
    {
        var heard = new Heard();
        var cut = RenderGrid(SourceOf(TestRows.Many(50)), heard);
        await PressWhileACommitIsHeardAsync(cut, heard, press, answered);
        heard.Slow = false;
        heard.Edits.Clear();

        // The press on the second row closes the column menu a press may have opened.
        await ClickAsync(cut, 50, 30);
        await KeyAsync(cut, "7");
        cut.Render(ps => ps.Add(g => g.Columns, OtherColumns()));

        Assert.False(EditorOpen(cut));
        Assert.Equal([EditDiscardReason.ColumnsChanged], heard.Discards);
        Assert.Empty(heard.Edits);
    }

    [Theory] // ADR-0012 / ADR-0010, principle 6: a header pressed past an editor sorts once the Consumer has heard the commit — the click that ends the press never reads the editor still standing as one a Reject held
    [InlineData(Answered.AtOnce)]
    [InlineData(Answered.InOrder)]
    public async Task A_header_press_that_commits_sorts_once_the_commit_has_landed(Answered answered)
    {
        var heard = new Heard { Slow = answered != Answered.AtOnce };
        var source = SourceOf(TestRows.Many(50));
        var cut = RenderGrid(source, heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");

        var made = PressHeader(cut);
        await AnswerAsync(cut, heard, answered);
        await Task.WhenAll(made);

        Assert.Equal("5", Assert.Single(heard.Edits).Value);
        Assert.Equal([new SortSpec("Book", SortDirection.Ascending)], Assert.Single(source.SortChanges));
        Assert.False(EditorOpen(cut));
    }

    [Theory] // ADR-0010 / ADR-0063, principle 6: a double click on another cell, made while the commit its first press asked for is heard, commits once and opens the editor on the cell double-clicked
    [InlineData(Answered.AtOnce)]
    [InlineData(Answered.InOrder)]
    public async Task A_double_click_past_an_editor_opens_an_edit_once_the_commit_has_landed(Answered answered)
    {
        var heard = new Heard { Slow = answered != Answered.AtOnce };
        var cut = RenderGrid(SourceOf(TestRows.Many(50)), heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");

        Task[] made =
        [
            .. PressRow(cut, 50),
            .. PressRow(cut, 50),
            cut.Find(".ex-viewport").DoubleClickAsync(new MouseEventArgs { Button = 0, OffsetX = 50, OffsetY = 50, Detail = 2 }),
        ];
        await AnswerAsync(cut, heard, answered);
        await Task.WhenAll(made);

        var edit = Assert.Single(heard.Edits);
        Assert.Equal(("Row 000000", "5"), (edit.Row.Book, edit.Value));
        Assert.True(EditorOpen(cut));
        Assert.Equal("Row 000002", cut.Find("input.ex-editor").GetAttribute("value"));
        Assert.Equal(new CellPosition(2, 0), Focus(cut));
    }

    [Theory] // ADR-0036 / ADR-0010, principle 6: a secondary press past an editor commits, and the context menu it opens opens once the commit has landed, on the cell pressed
    [InlineData(Answered.AtOnce)]
    [InlineData(Answered.InOrder)]
    public async Task A_context_menu_past_an_editor_opens_once_the_commit_has_landed(Answered answered)
    {
        var heard = new Heard { Slow = answered != Answered.AtOnce };
        var cut = RenderGrid(SourceOf(TestRows.Many(50)), heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");

        Task[] made =
        [
            DownAsync(cut, 50, 50, button: 2),
            cut.Find(".ex-viewport").ContextMenuAsync(new MouseEventArgs { Button = 2, OffsetX = 50, OffsetY = 50 }),
        ];
        await AnswerAsync(cut, heard, answered);
        await Task.WhenAll(made);

        Assert.Equal("5", Assert.Single(heard.Edits).Value);
        Assert.False(EditorOpen(cut));
        Assert.Single(cut.FindAll(".ex-popover"));
        Assert.Equal(new CellPosition(2, 0), Focus(cut));
    }

    [Theory] // ADR-0142 / LV-19, ADR-0034, principle 6: a press made while a commit the Consumer refuses is heard is answered once the refusal is in, as when the Consumer answers at once: the editor stands, so the press commits it again, and is refused again
    [InlineData(Answered.AtOnce)]
    [InlineData(Answered.InOrder)]
    [InlineData(Answered.LastFirst)]
    public async Task A_press_made_while_a_refused_commit_is_heard_is_answered_as_after_the_refusal(Answered answered)
    {
        var heard = new Heard { Refusal = "Not above 150" };
        var cut = RenderGrid(SourceOf(TestRows.Many(50)), heard);

        await PressWhileACommitIsHeardAsync(cut, heard, "row", answered);

        Assert.Equal(["5", "5"], heard.Edits.Select(e => e.Value));
        Assert.All(heard.Edits, e => Assert.Equal("Row 000000", e.Row.Book));
        Assert.True(EditorOpen(cut));
        Assert.Equal(new CellPosition(0, 0), Focus(cut));
        Assert.Empty(heard.Discards);
    }

    [Theory] // ADR-0050 item 4 / ADR-0142, principle 6: a placement the Consumer asks for while the grid hears a commit a press asked for waits for it to land, as a click does: one Edit Intent, and the placement lands after the press, with no editor open
    [InlineData(Answered.AtOnce)]
    [InlineData(Answered.InOrder)]
    public async Task A_placement_asked_while_a_commit_is_heard_lands_once_it_has_landed(Answered answered)
    {
        var heard = new Heard { Slow = answered != Answered.AtOnce };
        var cut = RenderGrid(SourceOf(TestRows.Many(50)), heard);
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");

        var pressed = PressRow(cut, 50);
        var placement = cut.InvokeAsync(() => cut.Instance.PlaceSelectionAsync(
            new SelectionRange(4, 1, 1, 1), new CellPosition(4, 1), cut.Instance.ReadSelection().RowSequenceVersion));
        await AnswerAsync(cut, heard, answered);
        await Task.WhenAll(pressed);
        var placed = await placement;

        Assert.Equal("5", Assert.Single(heard.Edits).Value);
        Assert.True(placed);
        Assert.Equal(new CellPosition(4, 1), Focus(cut));
        Assert.False(EditorOpen(cut));
    }

    [Fact] // ADR-0050 item 4 / ADR-0142: a placement the Consumer awaits inside its own handler of the commit does not wait for that commit, which waits for the handler: it places, and the commit lands once, with nothing left standing
    public async Task A_placement_awaited_inside_the_commits_own_handler_does_not_wait_for_it()
    {
        var edits = new List<GridEditIntent<TestRow>>();
        var placed = new List<bool>();
        IRenderedComponent<ExGrid<TestRow>>? cut = null;
        cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, SourceOf(TestRows.Many(50)))
            .Add(g => g.Columns, Columns())
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnEdit, async (GridEditIntent<TestRow> intent) =>
            {
                edits.Add(intent);
                await Task.Yield();
                placed.Add(await cut!.Instance.PlaceSelectionAsync(
                    new SelectionRange(4, 1, 1, 1), new CellPosition(4, 1), cut.Instance.ReadSelection().RowSequenceVersion));
            }));
        await ClickAsync(cut, 50, 10);
        await KeyAsync(cut, "5");

        await Task.WhenAll(PressRow(cut, 50)).WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);

        Assert.Equal("5", Assert.Single(edits).Value);
        Assert.Equal([true], placed);
        Assert.False(EditorOpen(cut));
    }
}
