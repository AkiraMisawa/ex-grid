using Bunit;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Components.Tests.Support;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// The Input Order's public seam (ADR-0240 section 6; input-order ticket 04): a surface outside the
/// grid's own markup — a Chrome's, a Consumer's, a product's own such as ExSheet's toolbar — puts its
/// input in the instance's order through <see cref="ExGrid{TRow}.InOrderAsync"/>, or through
/// <see cref="GridPopoverContext.InOrder"/> from a popover's contents. A told input is handled in its
/// number; one that was not told runs after every input received so far; the keyboard crossing the
/// Input Root's edge runs only when it was told. The input before is held by a paste whose reading has
/// not ended, as in <see cref="InputOrderClicksTests"/>.
/// </summary>
public class InOrderSeamTests : GridTestContext
{
    private static readonly ColumnWidthSpec Fixed100 = new(ColumnWidth.Fixed(100));

    private IRenderedComponent<ExGrid<TestRow>> RenderGrid()
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
            .Add(g => g.ViewportWidth, 350));
    }

    private static Task TellAsync(IRenderedComponent<ExGrid<TestRow>> cut, string kind, int input)
        => cut.InvokeAsync(() => cut.Instance.InputTakenAt(kind, input));

    private static async Task<Task> HoldTheOrderAsync(IRenderedComponent<ExGrid<TestRow>> cut, TaskCompletionSource opens)
    {
        Task paste = Task.CompletedTask;
        await cut.InvokeAsync(() => { paste = cut.Instance.OnPasteStreamsAsync(FakeJSStream.Held(opens.Task), null, input: 1); });
        return paste;
    }

    private Task<string?> AnsweredAsync(IRenderedComponent<ExGrid<TestRow>> cut)
        => ReadWhenIdleAsync(() => cut.Find(".ex-grid > [data-ex-answered]").GetAttribute("data-ex-answered"));

    // Starts InOrderAsync on the renderer's context, as a Blazor handler calls it, and notes when its handling runs.
    private static async Task<Task> InOrderAsync(IRenderedComponent<ExGrid<TestRow>> cut, GridInputKind kind, List<string> ran, string name)
    {
        Task handled = Task.CompletedTask;
        await cut.InvokeAsync(() =>
        {
            handled = cut.Instance.InOrderAsync(kind, () =>
            {
                ran.Add(name);
                return Task.CompletedTask;
            });
        });
        return handled;
    }

    [Fact] // ADR-0240 §6: an input a surface outside the grid claims is handled in the number told for it, after the input before it
    public async Task ADR0240_a_claimed_input_through_the_seam_waits_for_the_input_before_it()
    {
        var cut = RenderGrid();
        var opens = new TaskCompletionSource();
        var paste = await HoldTheOrderAsync(cut, opens);
        var ran = new List<string>();

        await TellAsync(cut, "keydown", 2);
        var key = await InOrderAsync(cut, GridInputKind.KeyDown, ran, "band key");
        await WhenIdleAsync();
        Assert.Empty(ran);
        Assert.Equal("0", await AnsweredAsync(cut));

        opens.SetResult();
        await paste;
        await key;
        Assert.Equal(["band key"], ran);
        Assert.Equal("2", await AnsweredAsync(cut));
    }

    [Fact] // ADR-0240 §6: an input no number was told for — a control that claims nothing, the Consumer's own code — runs after every input received so far
    public async Task ADR0240_an_untold_input_runs_after_every_input_received()
    {
        var cut = RenderGrid();
        var opens = new TaskCompletionSource();
        var paste = await HoldTheOrderAsync(cut, opens);
        var ran = new List<string>();

        var click = await InOrderAsync(cut, GridInputKind.Click, ran, "click");
        await WhenIdleAsync();
        Assert.Empty(ran);

        opens.SetResult();
        await paste;
        await click;
        Assert.Equal(["click"], ran);
    }

    [Fact] // ADR-0240 §6: the keyboard crossing the Input Root's edge runs only when it was told; a move inside it is no input
    public async Task ADR0240_a_move_of_focus_inside_the_input_root_is_no_input()
    {
        var cut = RenderGrid();
        var ran = new List<string>();

        await InOrderAsync(cut, GridInputKind.KeyboardOut, ran, "out");
        await InOrderAsync(cut, GridInputKind.KeyboardIn, ran, "in");
        Assert.Empty(ran);

        await TellAsync(cut, "focusout", 1);
        await InOrderAsync(cut, GridInputKind.KeyboardIn, ran, "in");
        Assert.Empty(ran);
        await InOrderAsync(cut, GridInputKind.KeyboardOut, ran, "out");
        Assert.Equal(["out"], ran);
        Assert.Equal("1", await AnsweredAsync(cut));
    }

    [Fact] // ADR-0240 §6: the keyboard leaving, told behind an input still being handled, is taken in its place after it
    public async Task ADR0240_the_keyboard_leaving_is_taken_in_its_place()
    {
        var cut = RenderGrid();
        var opens = new TaskCompletionSource();
        var paste = await HoldTheOrderAsync(cut, opens);
        var ran = new List<string>();

        await TellAsync(cut, "focusout", 2);
        var leaving = await InOrderAsync(cut, GridInputKind.KeyboardOut, ran, "out");
        await WhenIdleAsync();
        Assert.Empty(ran);

        opens.SetResult();
        await paste;
        await leaving;
        Assert.Equal(["out"], ran);
        Assert.Equal("2", await AnsweredAsync(cut));
    }

    [Fact] // ADR-0240 §6, §9: a popover's contents are handed the seam, and a click they claim is handled in its number
    public async Task ADR0240_a_popovers_contents_are_handed_the_seam()
    {
        var cut = RenderGrid();
        GridPopoverContext? context = null;
        RenderFragment<GridPopoverContext> content = popover => builder =>
        {
            context = popover;
            builder.AddContent(0, "Contents");
        };
        Assert.True(await cut.InvokeAsync(() => cut.Instance.OpenPopoverAsync(content, "Probe")));
        Assert.NotNull(context?.InOrder);

        var ran = new List<string>();
        var answered = int.Parse(await AnsweredAsync(cut) ?? "0", System.Globalization.CultureInfo.InvariantCulture);
        await TellAsync(cut, "click", answered + 1);
        await cut.InvokeAsync(() => context!.InOrder!(GridInputKind.Click, () =>
        {
            ran.Add("ok");
            return Task.CompletedTask;
        }));

        Assert.Equal(["ok"], ran);
        Assert.Equal((answered + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), await AnsweredAsync(cut));
    }
}
