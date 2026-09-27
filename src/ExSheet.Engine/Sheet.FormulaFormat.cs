using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

public sealed partial class Sheet
{
    /// <summary>
    /// The format a Formula gives a General cell when it is entered, as Excel's does (ADR-0047), or
    /// <see langword="null"/> for none. Microsoft does not document the rule; what is applied is
    /// what the case corpus has observed Excel to do, and nothing beyond it:
    /// <list type="bullet">
    /// <item>A Formula made only of References to single cells, constants, <c>+</c>, <c>-</c>
    /// (binary and unary) and parentheses takes the format of the first Reference, in the order
    /// written, whose cell shows numbers in a format other than General (a text format such as
    /// <c>@</c> is passed over) — so a date plus a number shows as a date (FF-001, FF-002).</item>
    /// <item>A date minus a date is a number of days, and gives no format (FF-003): a subtraction
    /// whose two sides would each give a date format gives none, and the search for a format
    /// goes on past it.</item>
    /// <item><c>MIN</c> and <c>MAX</c> over References take the format of the first cell, in the
    /// order written, of the first argument whose first cell is formatted — so the latest of
    /// two dates shows as a date (FF-012). They count as a Reference in the arithmetic above.</item>
    /// </list>
    /// Anything else — multiplication, division, <c>&amp;</c>, comparisons, other functions —
    /// gives no format (FF-011, FF-013, FF-014 observed; other functions uncertain).
    /// </summary>
    private NumberFormat? FormatOnEntry(Node formula)
    {
        return Simple(formula) ? First(formula) : null;

        static bool Simple(Node node) => node switch
        {
            NumberNode => true,
            ReferenceNode r => r.Reference.Shape == ReferenceShape.Cell,
            ParenthesesNode p => Simple(p.Inner),
            UnaryNode u => u.Operator is '-' or '+' && Simple(u.Operand),
            BinaryNode b => b.Operator is "+" or "-" && Simple(b.Left) && Simple(b.Right),
            FunctionNode f => f.Name is "MIN" or "MAX" && f.Function is not null && f.Arguments.All(a => a is ReferenceNode),
            _ => false,
        };

        NumberFormat? First(Node node)
        {
            switch (node)
            {
                case ReferenceNode r when IsLocal(r.Reference):
                    return FormatOf(new CellAddress(r.Reference.Row1, r.Reference.Column1));
                case ParenthesesNode p:
                    return First(p.Inner);
                case UnaryNode u:
                    return First(u.Operand);
                case BinaryNode { Operator: "-" } b:
                    var left = First(b.Left);
                    var right = First(b.Right);
                    return left is { IsDate: true } && right is { IsDate: true } ? null : left ?? right;
                case BinaryNode b:
                    return First(b.Left) ?? First(b.Right);
                case FunctionNode f:
                    foreach (var argument in f.Arguments)
                    {
                        if (First(argument) is { } format) return format;
                    }
                    return null;
                default:
                    return null;
            }
        }

        NumberFormat? FormatOf(CellAddress address) => GetFormat(address) is { ShowsNumbersAsGeneral: false } format ? format : null;
    }
}
