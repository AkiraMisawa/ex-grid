namespace ExSheet.Engine.Formulas;

/// <summary>
/// The shape of the array a refused Formula would spill, where its text alone settles it, whatever Values
/// it reads (2026-10-13, decided with the user: a refused Anchor whose array shape is known spills refusals
/// over that shape). Excel's array has that shape whatever the refused Values are, so each cell of it reads
/// as refused; where the shape could depend on a Value, nothing is known and nothing spills.
/// </summary>
/// <remarks>
/// Known: a constant (one Value); a Reference the text names to a rectangle of this Sheet, not a Spill
/// Range (<c>A1#</c>) and not one that names another Sheet or a Linked Table; an operator over those, which
/// Excel applies to each Value and pairs by the larger extent, as <see cref="Evaluator"/> does; <c>TRANSPOSE</c>
/// of a known shape; and a call whose every argument is one Value, of a function that cannot give a Reference
/// or an array from one Value (not <c>SEQUENCE</c>, nor <c>INDIRECT</c>, <c>OFFSET</c> and the others that
/// can give a Reference). Not known, among the rest: <c>FILTER</c> and <c>UNIQUE</c>, whose extent depends on
/// the Values; <c>SORT</c> and <c>SORTBY</c>, which may answer one Error Value for an argument they refuse
/// (<c>=SORT(G1:H5,1.5)</c>); <c>LET</c>; and a function given an array where it wants one Value, which Excel
/// applies to each Value and the engine does not yet (ADR-0125).
/// </remarks>
internal static class KnownShape
{
    /// <summary>The functions that can give an array from arguments of one Value each.</summary>
    private static readonly HashSet<string> ArrayFromOneValue = new(StringComparer.OrdinalIgnoreCase) { "SEQUENCE" };

    /// <summary>The shape, or <see langword="null"/> where the text does not settle it.</summary>
    public static (int Rows, int Columns)? Of(Node node, Func<Reference, bool> isLocal)
    {
        (int, int)? one = (1, 1);
        switch (node)
        {
            case NumberNode or TextNode or BooleanNode or ErrorNode or MissingNode:
                return one;
            case ReferenceNode { Reference: var reference }:
                if (reference.Spilled || !isLocal(reference)) return null;
                var area = reference.Area;
                return (long)area.Rows * area.Columns <= ValueArray.MostCells ? (area.Rows, area.Columns) : null;
            case ParenthesesNode p:
                return Of(p.Inner, isLocal);
            case UnaryNode u:
                return Of(u.Operand, isLocal);
            case PercentNode p:
                return Of(p.Operand, isLocal);
            case BinaryNode b when b.Operator is "+" or "-" or "*" or "/" or "^" or "&" or "=" or "<>" or "<" or ">" or "<=" or ">=":
                if (Of(b.Left, isLocal) is not { } left || Of(b.Right, isLocal) is not { } right) return null;
                var rows = left.Rows == 1 ? right.Rows : right.Rows == 1 ? left.Rows : Math.Max(left.Rows, right.Rows);
                var columns = left.Columns == 1 ? right.Columns : right.Columns == 1 ? left.Columns : Math.Max(left.Columns, right.Columns);
                return (long)rows * columns <= ValueArray.MostCells ? (rows, columns) : null;
            case FunctionNode { Function: null }:
                return one; // #NAME?, one Value
            case FunctionNode f when f.Name.Equals("TRANSPOSE", StringComparison.OrdinalIgnoreCase) && f.Arguments.Count == 1:
                return Of(f.Arguments[0], isLocal) is { } inner ? (inner.Columns, inner.Rows) : null;
            case FunctionNode f:
                if (f.Function.ReturnsReference || ArrayFromOneValue.Contains(f.Name)) return null;
                return f.Arguments.All(a => Of(a, isLocal) == one) ? one : null;
            default:
                return null;
        }
    }
}
