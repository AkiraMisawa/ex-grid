using ExGrid.Columns;
using Xunit;

namespace ExGrid.Tests;

/// <summary>
/// Header Groups are declared rectangles over the leaf columns (ADR-0032). What resolution answers and
/// refuses is in the shared cases, <c>tests/cases/columns/header-group.json</c> and
/// <c>header-group-layout.json</c>; what stays here is a reference's identity, which a case's JSON cannot
/// see.
/// </summary>
public class HeaderGroupTests
{
    private static readonly string[] Names = ["TradeId", "CvaBefore", "CvaAfter", "CvaDiff", "FvaBefore", "FvaAfter"];

    [Fact] // ADR-0032 / HG-15: the declaration survives resolution unchanged — membership is gesture-proof
    public void The_declaration_is_not_rewritten_by_resolution()
    {
        var declaration = new HeaderGroup("CVA", ["CvaDiff", "CvaBefore", "CvaAfter"]);

        var layout = HeaderGroupLayout.Resolve([declaration], Names, 0);

        Assert.Same(declaration, layout.Groups[0].Group);
        Assert.Equal(["CvaDiff", "CvaBefore", "CvaAfter"], declaration.Columns);
    }

    [Fact] // ADR-0032: no declarations resolve to the one layout with no tiers
    public void No_declarations_resolve_to_Empty()
    {
        Assert.Same(HeaderGroupLayout.Empty, HeaderGroupLayout.Resolve([], Names, 0));
    }
}
