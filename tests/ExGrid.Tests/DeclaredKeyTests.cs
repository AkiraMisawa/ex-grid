using ExGrid.Keys;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// A Consumer's declared keys (ADR-0050, item 14; DC-57), checked by the key table: a declared key
/// is one the core does not answer itself, in any state, and in the canonical form a key press is
/// matched in. The claiming and the raising are layer 2's; the browser's half is layer 3's.
/// </summary>
public class DeclaredKeyTests
{
    [Fact] // ADR-0050 item 14 / DC-57 / DC-1: no declaration declares nothing
    public void No_declaration_declares_nothing()
    {
        Assert.Empty(GridKeys.Declare(null));
        Assert.Empty(GridKeys.Declare([]));
    }

    [Fact] // ADR-0050 item 14 / DC-57: keys the core leaves to the browser are declared as given, each exactly
    public void Keys_the_core_leaves_alone_are_declared()
    {
        string[] keys = ["Control+b", "Control+B", "Control+#", "Control+Shift+#", "Control+Shift++", "Control+2", "Control+Shift+_", "F5", "Control+PageDown"];

        var declared = GridKeys.Declare(keys);

        Assert.Equal(keys.Order(StringComparer.Ordinal), declared.Order(StringComparer.Ordinal));
        // Exactly: a letter claims the case it names, and Shift is part of the form.
        Assert.DoesNotContain("Control+Shift+B", declared);
        Assert.DoesNotContain("Control+i", declared);
    }

    [Theory] // ADR-0050 item 14 / ADR-0010 / DC-57: a declaration naming a key the core answers itself is refused by name
    [InlineData("Control+a", "SelectAll")]                 // claimed on every grid
    [InlineData("Escape", "Leave")]
    [InlineData("Shift+ArrowDown", "Extend")]
    [InlineData("Alt+ArrowDown", "OpenColumnMenu")]
    [InlineData("Control+f", "Find")]
    [InlineData("Control+z", "Undo")]                      // claimed only while someone listens
    [InlineData("Control+Shift+Z", "Redo")]
    [InlineData("Control+d", "FillDown")]                  // claimed only on a grid that edits
    [InlineData("Delete", "Clear")]
    [InlineData("Control+Enter", "fills the selection")]   // the editor's, while an edit is open
    [InlineData("F4", "cycles the Reference")]
    [InlineData("F2", "opens the Cell Editor")]
    [InlineData("Shift+F2", "opens the Cell Editor")]
    [InlineData("x", "opens the Cell Editor")]             // typing
    [InlineData("Shift+X", "opens the Cell Editor")]
    [InlineData("Control+Alt+q", "AltGr")]
    [InlineData("Control+Shift+Alt+q", "AltGr")]
    [InlineData("Control+c", "copy event")]                // the clipboard rides the browser's events
    [InlineData("Control+V", "paste event")]
    [InlineData("Shift+Insert", "paste event")]
    public void A_key_the_core_answers_is_refused_by_name(string key, string meaning)
    {
        var refused = Assert.Throws<ArgumentException>(() => GridKeys.Declare(["Control+b", key]));

        Assert.Contains($"'{key}'", refused.Message, StringComparison.Ordinal);
        Assert.Contains(meaning, refused.Message, StringComparison.Ordinal);
        Assert.Contains("ADR-0050", refused.Message, StringComparison.Ordinal);
    }

    [Theory] // ADR-0050 item 14 / DC-57: a key no press would ever match is refused by name, not claimed for nothing
    [InlineData("Ctrl+B")]
    [InlineData("Shift+Control+b")]
    [InlineData("Control+")]
    [InlineData("Control+Control+b")]
    [InlineData("Control+Shift")]
    [InlineData("++")]
    [InlineData("")]
    public void A_key_not_in_the_canonical_form_is_refused_by_name(string key)
    {
        var refused = Assert.Throws<ArgumentException>(() => GridKeys.Declare([key]));

        Assert.Contains($"'{key}'", refused.Message, StringComparison.Ordinal);
        Assert.Contains("canonical form", refused.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0050 item 14 / ADR-0012: a declared key is matched in the form a press canonicalises to, Command folded where it is primary
    public void A_press_canonicalises_to_the_declared_form()
    {
        var declared = GridKeys.Declare(["Control+b", "Control+Shift+$", "Control+#"]);

        Assert.Contains(GridKeys.Canonical("b", ctrl: true, shift: false, alt: false, meta: false, metaIsPrimary: false), declared);
        Assert.Contains(GridKeys.Canonical("b", ctrl: false, shift: false, alt: false, meta: true, metaIsPrimary: true), declared);
        Assert.Contains(GridKeys.Canonical("$", ctrl: true, shift: true, alt: false, meta: false, metaIsPrimary: false), declared);
        // On a UK layout # comes without Shift (the eleventh Windows run, Part B case 29).
        Assert.Contains(GridKeys.Canonical("#", ctrl: true, shift: false, alt: false, meta: false, metaIsPrimary: false), declared);
        // Where Meta is the OS's, it stays in the form and matches nothing declared.
        Assert.DoesNotContain(GridKeys.Canonical("b", ctrl: false, shift: false, alt: false, meta: true, metaIsPrimary: false), declared);
    }

    [Fact] // ADR-0050 item 14: a declared key resolves to nothing of the core's, so the core's table and the declaration never overlap
    public void A_declared_key_resolves_to_nothing_of_the_cores()
    {
        foreach (var key in GridKeys.Declare(["Control+b", "Control+Shift+~", "Control+5"]))
        {
            Assert.DoesNotContain(key, GridKeys.TakenFor(new GridKeyClaims(CanEdit: true, CanUndo: true, CanRedo: true, CanFind: true)));
        }
        Assert.Equal(GridKeyKind.None, GridKeys.Resolve("b", ctrl: true, shift: false, alt: false, meta: false, metaIsPrimary: false).Kind);
    }
}
