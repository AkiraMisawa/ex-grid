using Bunit;
using Bunit.Rendering;
using ExGrid.Chrome;
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
    private int _next;
    private int _previous;

    private FindContext Context(string text = "", FindOutcome outcome = FindOutcome.None) => new(
        text, MatchCase: false, WholeCell: false, outcome,
        _texts.Add, _matchCase.Add, _ => { },
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
}
