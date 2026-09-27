using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

public sealed partial class Sheet
{
    /// <summary>The format a number constant with <c>%</c> gives a Formula's result: <c>0.0%</c>, as Excel was observed to give <c>=10+50%</c>.</summary>
    private static readonly NumberFormat PercentConstant = NumberFormat.Parse("0.0%");

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
    /// <item><c>MIN</c>, <c>MAX</c> and <c>SUM</c> over References take the format of the first
    /// cell, in the order written, of the first argument whose first cell is formatted — so the
    /// latest of two dates shows as a date (FF-012), and so does their sum (FF-020). They count as
    /// a Reference in the arithmetic above.</item>
    /// <item>Multiplication counts as <c>+</c> does (FF-011, FF-013, ARITH-064, observed with real
    /// keys in the second run): <c>=A1*1</c> over a date is a date, and <c>=A1*A1</c> over a cell in
    /// <c>0.00E+00</c> takes <c>0.00E+00</c>.</item>
    /// <item>A number constant with <c>%</c> gives <c>0.0%</c> (ARITH-006: <c>=10+50%</c> shows
    /// <c>1050.0%</c>).</item>
    /// </list>
    /// Anything else — division, <c>&amp;</c>, comparisons, other functions, <c>%</c> on anything
    /// but a number constant — gives no format (FF-014 observed; the others uncertain). This is
    /// the rule observed, extended by ADR-0047's second run to what that run observed, and no
    /// further.
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
            PercentNode { Operand: NumberNode } => true,
            BinaryNode b => b.Operator is "+" or "-" or "*" && Simple(b.Left) && Simple(b.Right),
            FunctionNode f => f.Name is "MIN" or "MAX" or "SUM" && f.Function is not null && f.Arguments.All(a => a is ReferenceNode),
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
                case PercentNode:
                    return PercentConstant;
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
