using Bunit;
using ExGrid.Cells;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using ExGrid.Selection;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// A press on the rows keeps its place among held keys (ADR-0021/0010, ED-22). The listener
/// holds the press behind the keys typed before it and replays it in its place; it then asks
/// the core whether the press has been answered before it hands on the keys typed after it.
/// The holding and replaying are the browser's half and layer 3's (circuit.spec.mjs); this is
/// the core's half: the answer to that question comes only once the press has done everything
/// it does, and a press replayed in its place between keys lands where it was aimed. 20px
/// rows, an editable Book column of 100px.
/// </summary>
public class PressOrderingTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid(
        Func<GridEditIntent<TestRow>, Task> onEdit, Action<GridSelection>? onSelection = null)
        => Render<ExGrid<TestRow>>(ps =>
        {
            ps.Add(g => g.Window, TestRows.Many(50))
              .Add(g => g.TotalCount, 50)
              .Add(g => g.Columns, [
                  new GridColumn<TestRow>("Book", ColumnType.Text, r => r.Book, width: Fixed100, editable: true),
                  new GridColumn<TestRow>("Amount", ColumnType.Number, r => r.Amount, width: Fixed100),
              ])
              .Add(g => g.RowHeight, 20d)
              .Add(g => g.ViewportHeight, 200)
              .Add(g => g.ViewportWidth, 350)
              .Add(g => g.OnEdit, onEdit);
            if (onSelection is not null)
                ps.Add(g => g.SelectionChanged, onSelection);
        });

    private static Task PressKeyAsync(IRenderedComponent<ExGrid<TestRow>> cut, string key)
        => cut.InvokeAsync(() => cut.Instance.OnKeyAsync(key, false, false, false, false, false));

    private static Task PressRowAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseDownAsync(new MouseEventArgs { Button = 0, Buttons = 1, OffsetX = x, OffsetY = y, Detail = 1 });

    private static Task ReleaseRowAsync(IRenderedComponent<ExGrid<TestRow>> cut, double x, double y)
        => cut.Find(".ex-viewport").MouseUpAsync(new MouseEventArgs { Button = 0, OffsetX = x, OffsetY = y, Detail = 1 });

    private static Task AnsweredAsync(IRenderedComponent<ExGrid<TestRow>> cut)
        => cut.InvokeAsync(() => cut.Instance.PressAnsweredAsync());

    private IReadOnlyList<string> GateModes()
        => [.. JSInterop.Invocations.Where(i => i.Identifier == "setEditing").Select(i => (string)i.Arguments[0]!)];

    [Fact] // ADR-0021/0010, ED-22: a press is answered only once its commit, and the Consumer hearing it, is done
    public async Task A_press_that_commits_is_answered_only_after_the_commit_has_landed()
    {
        var heard = new TaskCompletionSource();
        var intents = new List<GridEditIntent<TestRow>>();
        GridSelection? selection = null;
        var cut = RenderGrid(async intent =>
        {
            intents.Add(intent);
            await heard.Task;
        }, s => selection = s);
        await PressRowAsync(cut, 50, 10);
        await ReleaseRowAsync(cut, 50, 10);
        await PressKeyAsync(cut, "5");

        var press = PressRowAsync(cut, 50, 50);                  // a press on row 2, over the open editor
        var answered = AnsweredAsync(cut);

        Assert.False(answered.IsCompleted);
        Assert.False(press.IsCompleted);
        heard.SetResult();
        await press;
        await answered;

        Assert.Equal("5", Assert.Single(intents).Value);
        Assert.Equal(new CellPosition(2, 0), selection!.Focus);
        // The gate was told the edit ended before the press was answered: the keys the listener
        // hands on after it are gated as the grid's, not the editor's.
        Assert.Equal("none", GateModes()[^1]);
    }

    [Fact] // ADR-0021/0010: with no press in flight the question is answered at once
    public async Task With_no_press_in_flight_the_answer_is_immediate()
    {
        var cut = RenderGrid(_ => Task.CompletedTask);

        var answered = AnsweredAsync(cut);

        Assert.True(answered.IsCompletedSuccessfully);
        await answered;
    }

    [Fact] // ADR-0021/0010: a press answered long ago leaves nothing to wait for
    public async Task A_press_already_answered_is_not_waited_for_again()
    {
        var cut = RenderGrid(_ => Task.CompletedTask);
        await PressRowAsync(cut, 50, 30);

        var answered = AnsweredAsync(cut);

        Assert.True(answered.IsCompletedSuccessfully);
        await answered;
    }

    [Fact] // ADR-0021/0010, ED-22: click, type, Enter, click, type, Enter — each value lands in the cell clicked for it
    public async Task Presses_replayed_in_their_place_between_keys_land_each_value_where_it_was_aimed()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        GridSelection? selection = null;
        var cut = RenderGrid(intent =>
        {
            intents.Add(intent);
            return Task.CompletedTask;
        }, s => selection = s);

        // The order the listener hands them on in: each press after the keys typed before it,
        // and answered before the keys typed after it.
        foreach (var (y, value) in new[] { (10d, "1"), (30d, "2"), (50d, "3"), (130d, "7") })
        {
            await PressRowAsync(cut, 50, y);
            await AnsweredAsync(cut);
            await ReleaseRowAsync(cut, 50, y);
            await AnsweredAsync(cut);
            await PressKeyAsync(cut, value);
            await PressKeyAsync(cut, "Enter");
        }

        Assert.Equal(
            ["Row 000000=1", "Row 000001=2", "Row 000002=3", "Row 000006=7"],
            intents.Select(i => $"{i.Row.Book}={i.Value}"));
        Assert.Equal(new CellPosition(7, 0), selection!.Focus);
    }

    [Fact] // ADR-0021/0010, ED-22: a press replayed behind an Enter that has not been answered would land a row too low — replayed after it, it lands where it was aimed
    public async Task A_press_replayed_after_the_enter_it_followed_is_not_carried_past_by_it()
    {
        var intents = new List<GridEditIntent<TestRow>>();
        GridSelection? selection = null;
        var cut = RenderGrid(intent =>
        {
            intents.Add(intent);
            return Task.CompletedTask;
        }, s => selection = s);
        await PressRowAsync(cut, 50, 10);
        await ReleaseRowAsync(cut, 50, 10);
        await PressKeyAsync(cut, "1");

        await PressKeyAsync(cut, "Enter");                       // held, then handed on first
        await PressRowAsync(cut, 50, 30);                         // the press on row 1, in its place
        await AnsweredAsync(cut);
        await ReleaseRowAsync(cut, 50, 30);
        await PressKeyAsync(cut, "2");
        await PressKeyAsync(cut, "Enter");

        Assert.Equal(["Row 000000=1", "Row 000001=2"], intents.Select(i => $"{i.Row.Book}={i.Value}"));
        Assert.Equal(new CellPosition(2, 0), selection!.Focus);
    }
}
