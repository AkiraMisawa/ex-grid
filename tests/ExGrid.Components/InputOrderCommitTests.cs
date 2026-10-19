using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The commit barrier folded into the Input Order (ADR-0240 section 3). A numbered input's handler starts
/// only once the one before it has finished, a commit the Consumer hears included, so inside an input's
/// flow nothing waits for a commit that input asked for; a commit asked for by a handler outside the order
/// (one not yet numbered) still holds every gesture behind it, numbered or not (ADR-0142; ADR-0010's
/// click-away, ED-12). 20px rows in a 120px Viewport; Book 0–100 and Amount 100–200, both editable.
/// </summary>
public class InputOrderCommitTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private sealed class Heard
    {
        public List<GridEditIntent<TestRow>> Edits { get; } = [];

        public TaskCompletionSource Answer { get; } = new();

        public async Task OnEditAsync(GridEditIntent<TestRow> intent)
        {
            Edits.Add(intent);
            await Answer.Task;
        }
    }

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(Heard heard)
    {
        var source = new TestSource();
        source.Push(TestRows.Many(50), totalCount: 50);
        return Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns,
            [
                new GridColumn<TestRow>("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
                new GridColumn<TestRow>("Amount", ColumnType.Number, r => r.Amount, width: Fixed100, editable: true),
            ])
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 350)
            .Add(g => g.OnEdit, (GridEditIntent<TestRow> i) => heard.OnEditAsync(i)));
    }

    /// <summary>Sends a key numbered <paramref name="input"/>, and hands back the task of its handling
    /// without waiting for it.</summary>
    private static async Task<Task> SendKeyAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key, int input)
    {
        Task handling = Task.CompletedTask;
        await cut.InvokeAsync(() => { handling = cut.Instance.OnKeyAsync(key, false, false, false, false, false, input: input); });
        return handling;
    }

    private static Task PressAsync(IRenderedComponent<ExGrid<TestRow>> cut, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = 50, OffsetY = y, Detail = 1 });

    private static CellPosition Focus(IRenderedComponent<ExGrid<TestRow>> cut) => cut.Instance.ReadSelection().Selection.Focus;

    private Task<string?> AnsweredAsync(IRenderedComponent<ExGrid<TestRow>> cut)
        => ReadWhenIdleAsync(() => cut.Find(".ex-grid > [data-ex-answered]").GetAttribute("data-ex-answered"));

    [Fact] // ADR-0240 §3, ED-12: a numbered key typed while the Consumer hears the commit a key before it asked for waits in the order, and commits nothing a second time
    public async Task ADR0240_a_key_behind_a_commit_the_consumer_hears_waits_in_the_order()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard);
        await PressAsync(cut, 10);
        await await SendKeyAsync(cut, "5", input: 1);

        var commit = await SendKeyAsync(cut, "Enter", input: 2);
        var after = await SendKeyAsync(cut, "Enter", input: 3);
        await WhenIdleAsync();
        Assert.False(commit.IsCompleted);
        Assert.False(after.IsCompleted);
        Assert.Equal("1", await AnsweredAsync(cut));

        await cut.InvokeAsync(heard.Answer.SetResult);
        await cut.InvokeAsync(() => Task.WhenAll(commit, after));

        var edit = Assert.Single(heard.Edits);
        Assert.Equal("5", edit.Value);
        Assert.Equal(new CellPosition(2, 0), Focus(cut));
        Assert.Equal("3", await AnsweredAsync(cut));
    }

    [Fact] // ADR-0240 §3, ADR-0142: a commit asked for by a handler outside the order still holds a numbered key behind it until it lands
    public async Task ADR0240_a_commit_from_outside_the_order_holds_a_numbered_key()
    {
        var heard = new Heard();
        var cut = RenderGrid(heard);
        await PressAsync(cut, 10);
        await await SendKeyAsync(cut, "5", input: 1);

        // A press nobody numbered commits, and the Consumer hears it.
        var press = PressAsync(cut, 70);
        var key = await SendKeyAsync(cut, "ArrowDown", input: 2);
        await WhenIdleAsync();
        Assert.False(key.IsCompleted);
        Assert.Single(heard.Edits);

        await cut.InvokeAsync(heard.Answer.SetResult);
        await press;
        await cut.InvokeAsync(() => key);

        Assert.Single(heard.Edits);
        Assert.Equal(new CellPosition(4, 0), Focus(cut));
        Assert.Equal("2", await AnsweredAsync(cut));
    }
}
