using Bunit;
using Bunit.Rendering;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Finding;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Xunit;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// The find panel under MudBlazor (ADR-0055, WR-1/3): MudBlazor's controls over the whole
/// FindContext — the steps, the options, the outcome worded into a live region — deciding
/// nothing of its own.
/// </summary>
public class MudFindPanelTests : MudTestContext
{
    private readonly List<string> _texts = [];
    private readonly List<bool> _matchCase = [];
    private readonly List<bool> _wholeCell = [];
    private int _next;
    private int _previous;

    private FindContext Context(string text = "", FindOutcome outcome = FindOutcome.None) => new(
        text, MatchCase: false, WholeCell: false, outcome,
        _texts.Add,
        value => { _matchCase.Add(value); return Task.CompletedTask; },
        value => { _wholeCell.Add(value); return Task.CompletedTask; },
        () => { _next++; return Task.CompletedTask; },
        () => { _previous++; return Task.CompletedTask; },
        () => { }, FocusRequest: 1);

    private IRenderedComponent<ContainerFragment> RenderPanel(FindContext context, MudGridChrome? chrome = null)
        => Render((chrome ?? MudGridChrome.Default).FindPanel(context)!);

    [Fact] // ADR-0055: the field reports its text, and the two steps are the context's
    public async Task The_field_and_the_steps_call_back()
    {
        var cut = RenderPanel(Context());

        await cut.Find(".mud-ex-grid-find-field input").InputAsync(new ChangeEventArgs { Value = "Novak" });
        await cut.Find(".mud-ex-grid-find-next").ClickAsync(new MouseEventArgs());
        await cut.Find(".mud-ex-grid-find-previous").ClickAsync(new MouseEventArgs());

        Assert.Equal("Novak", _texts[^1]);
        Assert.Equal(1, _next);
        Assert.Equal(1, _previous);
    }

    [Fact] // ADR-0240 §2: the two steps' clicks take their place in the grid's Input Order, as the built-in panel's do
    public void ADR0240_the_steps_claim_their_clicks()
    {
        var cut = RenderPanel(Context());

        Assert.Equal("click", cut.Find(".mud-ex-grid-find-next").GetAttribute("data-ex-claims"));
        Assert.Equal("click", cut.Find(".mud-ex-grid-find-previous").GetAttribute("data-ex-claims"));
    }

    [Fact] // ADR-0240 §2, find ticket 06: the two options' ticks are clicks in the grid's Input Order, claimed by the checkboxes' inputs alone, and handed to the context as they come
    public async Task ADR0240_the_options_claim_their_clicks_on_their_inputs()
    {
        var cut = RenderPanel(Context());

        Assert.Equal("click", cut.Find(".mud-ex-grid-find-match-case input").GetAttribute("data-ex-claims"));
        Assert.Equal("click", cut.Find(".mud-ex-grid-find-whole-cell input").GetAttribute("data-ex-claims"));
        // A click on the label reaches the input as a second click; only the input's is numbered.
        Assert.Empty(cut.FindAll(".mud-ex-grid-find-match-case label[data-ex-claims], .mud-ex-grid-find-whole-cell label[data-ex-claims]"));

        await cut.Find(".mud-ex-grid-find-match-case input").ChangeAsync(new ChangeEventArgs { Value = true });
        await cut.Find(".mud-ex-grid-find-whole-cell input").ChangeAsync(new ChangeEventArgs { Value = true });

        Assert.Equal([true], _matchCase);
        Assert.Equal([true], _wholeCell);
    }

    [Fact] // ADR-0055: Enter is the form's submission — next, and previous with Shift held
    public async Task Enter_steps_forward_and_shift_enter_back()
    {
        var cut = RenderPanel(Context("Novak"));

        await cut.Find("form.mud-ex-grid-find").SubmitAsync();
        await cut.Find(".mud-ex-grid-find-field input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter", ShiftKey = true });
        await cut.Find("form.mud-ex-grid-find").SubmitAsync();

        Assert.Equal(1, _next);
        Assert.Equal(1, _previous);
    }

    [Fact] // ADR-0055 / ADR-0033: the outcome is the Chrome's words, in a live region
    public void Not_found_is_worded_into_a_live_region()
    {
        var cut = RenderPanel(Context(outcome: FindOutcome.NotFound));

        var outcome = cut.Find(".mud-ex-grid-find-outcome");
        Assert.Equal("status", outcome.GetAttribute("role"));
        Assert.Equal("No match", outcome.TextContent.Trim());
    }

    [Fact] // ADR-0333 §1 / ADR-0033: a match found in a header is said, naming the header, in the live region, as the Focus does not move to it, and not in the colour of a fault
    public void ADR0333_a_match_in_a_header_is_said_and_is_no_fault()
    {
        var cut = RenderPanel(Context(outcome: FindOutcome.FoundInHeader) with { FoundHeader = "As of" });

        var outcome = cut.Find(".mud-ex-grid-find-outcome");
        Assert.Equal("status", outcome.GetAttribute("role"));
        Assert.Equal("Found in the header As of", outcome.TextContent.Trim());
        Assert.DoesNotContain("mud-error-text", outcome.ClassList);
        Assert.Contains("mud-error-text", RenderPanel(Context(outcome: FindOutcome.NotFound)).Find(".mud-ex-grid-find-outcome").ClassList);
    }

    [Fact] // ADR-0142 / ADR-0055: an answer from a Source since replaced is worded as such, apart from an order move, into the live region
    public void A_replaced_source_is_worded_apart_from_an_order_move()
    {
        var replaced = RenderPanel(Context(outcome: FindOutcome.SourceChanged)).Find(".mud-ex-grid-find-outcome").TextContent.Trim();
        var moved = RenderPanel(Context(outcome: FindOutcome.OrderChanged)).Find(".mud-ex-grid-find-outcome").TextContent.Trim();

        Assert.Equal("The data was replaced; find again", replaced);
        Assert.NotEqual(moved, replaced);
    }

    [Fact] // ADR-0055 / ADR-0030 / WR-3: the Chrome's Label words the panel
    public void The_chrome_words_the_panel()
    {
        var chrome = new MudGridChrome { Label = id => id == FindPanelLabelIds.Next ? "Weiter" : null };

        var cut = RenderPanel(Context(), chrome);

        Assert.Contains("Weiter", cut.Find(".mud-ex-grid-find-next").TextContent);
    }

    private sealed record Book(string Name, int Lots);

    private IRenderedComponent<global::ExGrid.Components.ExGrid<Book>> RenderGridWith(IGridChrome? chrome)
        => Render<global::ExGrid.Components.ExGrid<Book>>(ps => ps
            .Add(g => g.Source, GridSource.From(Enumerable.Range(0, 100).Select(i => new Book($"Book {i:D3}", i)).ToArray()))
            .Add(g => g.Columns, (GridColumn<Book>[])[
                new("Name", ColumnType.Text, r => r.Name, width: new ColumnWidthSpec(ColumnWidth.Fixed(100))),
                new("Lots", ColumnType.Number, r => r.Lots, width: new ColumnWidthSpec(ColumnWidth.Fixed(100)))])
            .Add(g => g.RowHeight, 20d)
            .Add(g => g.ViewportHeight, 120)
            .Add(g => g.ViewportWidth, 300)
            .Add(g => g.Chrome, chrome));

    private static string[] MarkedNames(IRenderedComponent<global::ExGrid.Components.ExGrid<Book>> cut, string mark = "ex-match-mark")
        => [.. cut.FindAll($".ex-cell.{mark}").Select(c => c.TextContent)];

    [Fact] // ADR-0332 §4 / WR-1: the Match Mark is the same under both Chromes — typed into the Wrapper's field, the painted matches are marked, the current apart, and cleared on close
    public async Task ADR0332_the_match_mark_is_the_same_under_both_chromes()
    {
        var seen = new List<(string[] Typed, string[] Current, string[] Closed)>();
        foreach (var chrome in new IGridChrome?[] { null, MudGridChrome.Default })
        {
            var cut = RenderGridWith(chrome);
            await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("f", true, false, false, false, false));

            if (chrome is null)
                await cut.Find("input.ex-find-field").InputAsync(new ChangeEventArgs { Value = "book 00" });
            else
                await cut.Find(".mud-ex-grid-find-field input").InputAsync(new ChangeEventArgs { Value = "book 00" });
            var typed = MarkedNames(cut);

            if (chrome is null)
                await cut.Find("button.ex-find-next").ClickAsync(new MouseEventArgs());
            else
                await cut.Find(".mud-ex-grid-find-next").ClickAsync(new MouseEventArgs());
            var current = MarkedNames(cut, "ex-match-mark-current");

            await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("Escape", false, false, false, false, false, fromDescendant: true));
            seen.Add((typed, current, MarkedNames(cut)));
        }

        Assert.NotEmpty(seen[0].Typed);
        Assert.All(seen[0].Typed, name => Assert.StartsWith("Book 00", name, StringComparison.Ordinal));
        Assert.Equal(["Book 000"], seen[0].Current);
        Assert.Empty(seen[0].Closed);
        Assert.Equal(seen[0].Typed, seen[1].Typed);
        Assert.Equal(seen[0].Current, seen[1].Current);
        Assert.Equal(seen[0].Closed, seen[1].Closed);
    }

    [Fact] // ADR-0332 §4: the Wrapper's options reach the marks as the built-in panel's do
    public async Task ADR0332_the_wrappers_options_change_the_marks()
    {
        var cut = RenderGridWith(MudGridChrome.Default);
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("f", true, false, false, false, false));
        await cut.Find(".mud-ex-grid-find-field input").InputAsync(new ChangeEventArgs { Value = "book 001" });
        Assert.Equal(["Book 001"], MarkedNames(cut));

        await cut.Find(".mud-ex-grid-find-match-case input").ChangeAsync(new ChangeEventArgs { Value = true });

        Assert.Empty(MarkedNames(cut));
    }

    [Fact] // ADR-0332 §2: the Replace part is drawn only while the context shows it
    public void ADR0332_replace_is_drawn_only_while_shown()
    {
        var cut = RenderPanel(Context());

        Assert.Empty(cut.FindAll(".mud-ex-grid-find-replace-field"));
        Assert.Empty(cut.FindAll(".mud-ex-grid-find-replace"));
    }

    [Fact] // ADR-0332 §2 / ADR-0240 §2: the Replace field reports its text, and the two writes are the context's, claiming their clicks
    public async Task ADR0332_the_replace_field_and_writes_call_back()
    {
        var replaced = new List<string>();
        int one = 0, all = 0;
        var cut = RenderPanel(Context("Novak") with
        {
            ReplaceShown = true,
            ReplaceText = "",
            ReplaceTextChanged = replaced.Add,
            Replace = () => { one++; return Task.CompletedTask; },
            ReplaceAll = () => { all++; return Task.CompletedTask; },
        });

        await cut.Find(".mud-ex-grid-find-replace-field input").InputAsync(new ChangeEventArgs { Value = "Rossi" });
        await cut.Find(".mud-ex-grid-find-replace").ClickAsync(new MouseEventArgs());
        await cut.Find(".mud-ex-grid-find-replace-all").ClickAsync(new MouseEventArgs());

        Assert.Equal("Rossi", replaced[^1]);
        Assert.Equal(1, one);
        Assert.Equal(1, all);
        Assert.Equal("click", cut.Find(".mud-ex-grid-find-replace").GetAttribute("data-ex-claims"));
        Assert.Equal("click", cut.Find(".mud-ex-grid-find-replace-all").GetAttribute("data-ex-claims"));
    }

    [Fact] // ADR-0332 §2 / ADR-0033: a Replace's count and its refusal are worded into the live region, as the built-in panel words them
    public void ADR0332_replace_outcomes_are_worded()
    {
        var count = RenderPanel(Context(outcome: FindOutcome.Replaced) with { ReplacedCount = 3 });
        Assert.Equal("Replaced 3 cells.", count.Find(".mud-ex-grid-find-outcome").TextContent.Trim());

        var refused = RenderPanel(Context(outcome: FindOutcome.ReplaceRefused) with
        {
            ReplaceRefusedCells = [new FindPanelRefusedCell(2, "Lots", null), new FindPanelRefusedCell(4, "Name", "too long")],
        });
        var outcome = refused.Find(".mud-ex-grid-find-outcome");
        Assert.Equal("status", outcome.GetAttribute("role"));
        Assert.Equal("Nothing was replaced. Row 2, Lots: this column cannot be edited. Row 4, Name: too long.", outcome.TextContent.Trim());
    }

    [Fact] // ADR-0332 §2 / WR-1: Replace All gives the same outcome under both Chromes, through GridSource.From
    public async Task ADR0332_replace_all_is_the_same_under_both_chromes()
    {
        var seen = new List<(string Outcome, string Names)>();
        foreach (var chrome in new IGridChrome?[] { null, MudGridChrome.Default })
        {
            var source = GridSource.From(Enumerable.Range(0, 100).Select(i => new Book($"Book {i:D3}", i)).ToArray());
            var cut = Render<global::ExGrid.Components.ExGrid<Book>>(ps => ps
                .Add(g => g.Source, source)
                .Add(g => g.Columns, (GridColumn<Book>[])[
                    new("Name", ColumnType.Text, r => r.Name, width: new ColumnWidthSpec(ColumnWidth.Fixed(100)), editable: true),
                    new("Lots", ColumnType.Number, r => r.Lots, width: new ColumnWidthSpec(ColumnWidth.Fixed(100)))])
                .Add(g => g.RowHeight, 20d)
                .Add(g => g.ViewportHeight, 120)
                .Add(g => g.ViewportWidth, 300)
                .Add(g => g.OnReplace, request => Task.FromResult(source.Replace(request,
                    (row, _, text) => GridReplaceWrite<Book>.To(row with { Name = text })).Result))
                .Add(g => g.Chrome, chrome));
            await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("h", true, false, false, false, false));

            if (chrome is null)
            {
                await cut.Find("input.ex-find-field").InputAsync(new ChangeEventArgs { Value = "book 00" });
                await cut.Find("input.ex-find-replace-field").InputAsync(new ChangeEventArgs { Value = "Lot 00" });
                await cut.Find("button.ex-find-replace-all").ClickAsync(new MouseEventArgs());
            }
            else
            {
                await cut.Find(".mud-ex-grid-find-field input").InputAsync(new ChangeEventArgs { Value = "book 00" });
                await cut.Find(".mud-ex-grid-find-replace-field input").InputAsync(new ChangeEventArgs { Value = "Lot 00" });
                await cut.Find(".mud-ex-grid-find-replace-all").ClickAsync(new MouseEventArgs());
            }
            var outcome = cut.Find(chrome is null ? ".ex-find-outcome" : ".mud-ex-grid-find-outcome").TextContent.Trim();
            seen.Add((outcome, string.Join(",", source.Window.Take(12).Select(b => b.Name))));
        }

        Assert.Equal("Replaced 10 cells.", seen[0].Outcome);
        Assert.StartsWith("Lot 000,", seen[0].Names, StringComparison.Ordinal);
        Assert.EndsWith("Lot 009,Book 010,Book 011", seen[0].Names, StringComparison.Ordinal);
        Assert.Equal(seen[0], seen[1]);
    }
}
