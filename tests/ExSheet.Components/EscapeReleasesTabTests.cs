using Bunit;
using ExSheet.Components.Tests.Support;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// Escape with nothing left to dismiss releases Tab, not DOM focus (ADR-0012, rewritten
/// 2026-10-01; KB-8; ticket 77), on the Sheet as on any grid. The fifteenth Windows run's case i2:
/// an edit opened by a character on D10, Escape, Escape. The second sent the keyboard to
/// <c>body</c>; now it tells the gate to release Tab, and the next character opens an edit in D10.
/// </summary>
public class EscapeReleasesTabTests : SheetTestContext
{
    [Fact] // ADR-0012 (rewritten 2026-10-01) / KB-8 / ticket 77: a character, Escape, Escape, a character opens an edit in the selected cell
    public async Task Escape_escape_then_a_character_opens_an_edit_in_the_selected_cell()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "D10");
        await PressAsync(cut, "k");
        await TypeAsync(cut, "kana");

        await PressInEditorAsync(cut, "Escape", "kana", 4);
        Assert.Empty(cut.FindAll(".ex-viewport .ex-editor"));
        Assert.Equal(0, TabReleases);

        await PressAsync(cut, "Escape");
        Assert.Equal(1, TabReleases);
        Assert.Equal("D10", cut.Find(".ex-name-box").GetAttribute("value"));

        await PressAsync(cut, "x");
        Assert.Equal("x", EditorText(cut));
        Assert.Equal("D10", cut.Find(".ex-name-box").GetAttribute("value"));
    }

    [Fact] // ADR-0012 (rewritten 2026-10-01) / ADR-0051 / KB-8: the Escape a completion list takes closes the list and releases nothing
    public async Task The_escape_a_completion_list_takes_releases_nothing()
    {
        var cut = RenderSheet();
        await GoToAsync(cut, "D10");
        await PressAsync(cut, "=");
        await TypeAsync(cut, "=SU");
        Assert.NotEmpty(cut.FindAll(".ex-completion"));

        await PressInEditorAsync(cut, "Escape", "=SU", 3);

        Assert.Empty(cut.FindAll(".ex-completion"));
        Assert.Equal("=SU", EditorText(cut));
        Assert.Equal(0, TabReleases);
    }
}
