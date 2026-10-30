using ExGrid.Chrome;
using ExGrid.Clipboard;
using Xunit;

namespace ExGrid.MudBlazor.Tests;

/// <summary>
/// Under this Chrome a refusal has words (ADR-0203, WR-3): MudBlazor has no localised text for a refused paste or
/// copy, so <see cref="MudGridChrome.Label"/> words each, the English beneath it.
/// </summary>
public class MudRefusalWordsTests
{
    [Fact] // ADR-0203 / WR-3: every refusal reason has words under this Chrome
    public void Every_refusal_reason_has_words_under_this_chrome()
    {
        IGridChrome chrome = MudGridChrome.Default;

        foreach (var reason in Enum.GetValues<PasteRefusalReason>())
        {
            Assert.Equal(BuiltInCommandLabels.For(RefusalLabelIds.For(reason)), chrome.Words(RefusalLabelIds.For(reason)));
            var spilling = RefusalLabelIds.For(reason, pasteMaySpill: true);
            Assert.Equal(BuiltInCommandLabels.For(spilling), chrome.Words(spilling));
        }
        foreach (var reason in Enum.GetValues<CopyRefusalReason>())
            Assert.Equal(BuiltInCommandLabels.For(RefusalLabelIds.For(reason)), chrome.Words(RefusalLabelIds.For(reason)));
    }
}
