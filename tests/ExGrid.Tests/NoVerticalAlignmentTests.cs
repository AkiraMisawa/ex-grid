using System.Reflection;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// HG-13's second half (ADR-0032): a Header Group's label is centred, and **there is no
/// vertical alignment option anywhere in the grid** — not on a group, not on a column,
/// not on a cell. The horizontal half is a painted fact and layer 3 asserts it; this is
/// the half about the surface, and it is asserted against the assembly rather than by
/// reading files, so it keeps holding when things move.
///
/// <para>The rule is not fussiness. A vertical alignment option only means anything once
/// a row can be taller than its text, and the row height is fixed on purpose
/// (ADR-0013): the option would be a knob that does nothing, or the first argument for
/// unfixing the height.</para>
/// </summary>
public class NoVerticalAlignmentTests
{
    [Fact] // ADR-0032 / HG-13: no vertical alignment anywhere on the public surface
    public void Nothing_public_offers_a_vertical_alignment()
    {
        var offenders = typeof(GridColumn<>).Assembly
            .GetExportedTypes()
            .SelectMany(type => type
                .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(member => $"{type.Name}.{member.Name}")
                .Prepend(type.Name))
            // Vertical *alignment*, not the word "vertical": CopyOrientation.Vertical is a
            // direction a copy runs in and has nothing to do with where text sits in a box.
            .Where(name => (name.Contains("Vertical", StringComparison.OrdinalIgnoreCase)
                            && name.Contains("Align", StringComparison.OrdinalIgnoreCase))
                        || name.Contains("VAlign", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }
}
