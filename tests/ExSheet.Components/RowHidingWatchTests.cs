using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// ADR-0047's note of 2026-10-10 admitted <c>SUBTOTAL</c> and <c>AGGREGATE</c> while a Sheet cannot hide
/// a row (docs/specs/exsheet-functions ticket 16, SH-68). The engine's own test watches the engine;
/// this one watches the component, since a row the component hid without the engine knowing would
/// leave <c>SUBTOTAL(109)</c> counting it.
/// </summary>
public class RowHidingWatchTests
{
    /// <summary>
    /// Names a public member would carry if a row could be hidden, filtered out or folded away: a row's
    /// own height (<c>SetRowHeight(row, 0)</c>), its visibility, an unhide. Narrower than the engine's,
    /// since the component's own <c>RowHeight</c> is every row's and its <c>Hide…</c> parameters hide
    /// parts of the grid's Chrome.
    /// </summary>
    private static readonly Regex Hiding = new(
        "hiddenrow|hiderow|rowhidden|rowshidden|rowheights|setrowheight|rowheight(of|at|for)|rowvisib|visiblerow|rowsvisib|unhide|filter(ed|s)?row|autofilter|collapse|outline(level|row)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [Fact] // ADR-0047 (2026-10-10), SH-68: fails on the day the Sheet's component can hide a row, and says to revisit SUBTOTAL and AGGREGATE
    public void ADR0047_the_sheet_cannot_hide_a_row()
    {
        var hiding = typeof(Components.ExSheet).Assembly.GetExportedTypes()
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(member => $"{type.Name}.{member.Name}")
                .Prepend(type.Name))
            .Where(name => Hiding.IsMatch(name))
            .ToList();

        Assert.True(hiding.Count == 0,
            $"A row can now be hidden: the component has {string.Join(", ", hiding)}. SUBTOTAL's 101 to 111 and AGGREGATE's options 1, 3, 5 and 7 "
            + "were admitted only while no row could be, and give what 1 to 11 and options 0, 2, 4 and 6 give. Open ADR-0047's note of 2026-10-10, "
            + "\"The catalogue's last Decide rows\", and decide with the user how they pass over a hidden or filtered row. "
            + "Do not make this test pass by changing its expectation.");
    }
}
