using System.Reflection;
using System.Text.RegularExpressions;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Engine.Tests;

/// <summary>
/// <c>SUBTOTAL</c> and <c>AGGREGATE</c> are admitted while a Sheet cannot hide a row (ADR-0047's note of 2026-10-10,
/// docs/specs/exsheet-functions ticket 16, SH-68). What stays here is .NET's own: the test that fails on the day the
/// engine's surface can hide a row. What they compute is in the shared cases (<c>tests/cases/sheet/subtotal.json</c>),
/// and their answers to Excel's cases in <c>ExcelCases/subtotal.json</c> and <c>ExcelCases/aggregate.json</c>.
/// </summary>
public class SubtotalTests
{
    /// <summary>Names a public member of the engine would carry if a row could be hidden, filtered out or folded away.</summary>
    private static readonly Regex Hiding = new("hid(e|den|ing)|rowheight|visib|filter(ed|s)?row|autofilter|collapse|outline(level|row)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string RowsCanBeHidden(string what) =>
        $"A row can now be hidden: {what}. SUBTOTAL's 101 to 111 and AGGREGATE's options 1, 3, 5 and 7 were admitted only while no row could be, "
        + "and give what 1 to 11 and options 0, 2, 4 and 6 give. Open ADR-0047's note of 2026-10-10, \"The catalogue's last Decide rows\", "
        + "and decide with the user how they pass over a hidden or filtered row. Do not make this test pass by changing its expectation.";

    [Fact] // ADR-0047 (2026-10-10), SH-68: fails on the day a row can be hidden, and says to revisit SUBTOTAL and AGGREGATE
    public void ADR0047_subtotal_and_aggregate_skip_nothing_while_no_row_can_be_hidden()
    {
        var hiding = typeof(Sheet).Assembly.GetExportedTypes()
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(member => $"{type.Name}.{member.Name}")
                .Prepend(type.Name))
            .Where(name => Hiding.IsMatch(name))
            .ToList();
        Assert.True(hiding.Count == 0, RowsCanBeHidden($"the engine has {string.Join(", ", hiding)}"));
    }
}
