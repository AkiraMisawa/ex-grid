using ExGrid.Clipboard;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// The paste rules' shared cases are in <c>tests/cases/clipboard/paste.json</c> and <c>spill.json</c>;
/// what stays here is the value C# can make without its constructor.
/// </summary>
public class PasteRuleTests
{
    private static readonly GridExtent Grid = new(1_000_000, 50);

    [Fact] // ADR-0014: default(PasteShape) sidesteps the ctor — PlanPaste refuses it by name
    public void A_default_paste_shape_is_refused_by_name_not_by_crash()
    {
        var target = GridSelection.Empty.Click(new(0, 0), Grid).ExtendTo(new(1, 1), Grid);

        Assert.Throws<ArgumentOutOfRangeException>(() => ClipboardRules.PlanPaste(target, default, _ => true));
    }
}
