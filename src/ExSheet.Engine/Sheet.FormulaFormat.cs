using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

public sealed partial class Sheet
{
    /// <summary>The format a number constant with <c>%</c> gives a Formula's result beside an unformatted operand: <c>0.0%</c>, as Excel was observed to give <c>=10+50%</c>.</summary>
    private static readonly NumberFormat PercentConstant = NumberFormat.Parse("0.0%");

    /// <summary>
    /// The format a Formula gives a General cell when it is entered, as Excel's does (ADR-0047), or
    /// <see langword="null"/> for none. Microsoft does not document the rule; what is applied is
    /// the smallest rule consistent with every case the corpus has observed Excel to answer
    /// (ADR-0047, "What the third observation settled"), and nothing beyond it:
    /// <list type="bullet">
    /// <item>A Formula made only of References to single cells, number constants, <c>+</c>,
    /// <c>-</c> (binary and unary), <c>*</c>, <c>/</c> and parentheses takes the format of the
    /// first Reference, in the order written, whose cell shows numbers in a format other than
    /// General (a text format such as <c>@</c> is passed over) — so a date plus a number shows as
    /// a date (FF-001, FF-002), and so does a date times or divided by a number (FF-011, FF-023),
    /// and <c>=A1*A1</c> over a cell in <c>0.00E+00</c> takes <c>0.00E+00</c> (ARITH-064).</item>
    /// <item>A date minus a date is a number of days, and gives no format (FF-003): a subtraction
    /// whose two sides would each give a date format gives none, and the search for a format
    /// goes on past it.</item>
    /// <item>A percentage is not carried through <c>*</c> or <c>/</c>: <c>=A1*2</c> over a cell in
    /// <c>0%</c> gives none (FF-025).</item>
    /// <item>A number constant with <c>%</c> gives <c>0.0%</c> only when added to, or subtracted
    /// from, an operand that gives no format (ARITH-006: <c>=10+50%</c> shows <c>1050.0%</c>).
    /// Alone, in a product, or beside an operand that gives a format, it gives none, and the
    /// whole addition gives none (FF-024, FF-028, FF-029: <c>=A1+50%</c> over a date).</item>
    /// <item><c>MIN</c>, <c>MAX</c> and <c>SUM</c> over References and number constants take the
    /// format of the first cell, in the order written, of the first Reference whose first cell is
    /// formatted — so the latest of two dates shows as a date (FF-012), and so does their sum
    /// (FF-020) and <c>=SUM(A1,5)</c> (FF-026). They count as a Reference in the arithmetic
    /// above.</item>
    /// <item><c>DATE</c> and <c>TODAY</c> give the short date format, and <c>NOW</c> the short date with
    /// a time, as Microsoft documents they give a General cell (FF-036, FF-038, FF-039), whatever their
    /// arguments. It counts as a Reference to a date in the arithmetic above
    /// (FF-037, uncertain).</item>
    /// </list>
    /// Anything else — <c>&amp;</c>, comparisons, <c>^</c>, other functions, <c>%</c> on anything
    /// but a number constant — gives no format (FF-014, FF-027 observed; the others uncertain).
    /// </summary>
    private NumberFormat? FormatOnEntry(Node formula)
    {
        return Simple(formula) ? Operand(formula) : null;

        static bool Simple(Node node) => node switch
        {
            NumberNode => true,
            ReferenceNode r => r.Reference.Shape == ReferenceShape.Cell,
            ParenthesesNode p => Simple(p.Inner),
            UnaryNode u => u.Operator is '-' or '+' && Simple(u.Operand),
            PercentNode { Operand: NumberNode } => true,
            // Along the chain of left operands in a loop: a long chain does not deepen the stack.
            BinaryNode b => b.Spine.All(s => s.Operator is "+" or "-" or "*" or "/") && b.Operands.All(Simple),
            FunctionNode { Name: "DATE" or "TODAY" or "NOW", Function: not null } => true,
            FunctionNode f => f.Name is "MIN" or "MAX" or "SUM" && f.Function is not null && f.Arguments.All(a => a is ReferenceNode or NumberNode),
            _ => false,
        };

        // A number constant with %, possibly in parentheses or under a sign.
        static bool IsPercentConstant(Node node) => node switch
        {
            PercentNode => true,
            ParenthesesNode p => IsPercentConstant(p.Inner),
            UnaryNode u => IsPercentConstant(u.Operand),
            _ => false,
        };

        // The format an operand gives; a percentage constant gives none of its own (see the + and - case).
        NumberFormat? Operand(Node node)
        {
            switch (node)
            {
                case ReferenceNode r when SheetNamed(r.Reference.SheetName) is { } owner:
                    // A cell of another Sheet gives its format as one of this Sheet's does (ADR-0331).
                    return owner.GetNumberFormat(new CellAddress(r.Reference.Row1, r.Reference.Column1)) is { ShowsNumbersAsGeneral: false } given ? given : null;
                case ParenthesesNode p:
                    return Operand(p.Inner);
                case UnaryNode u:
                    return Operand(u.Operand);
                case BinaryNode chain:
                    // Along the chain of left operands in a loop, each operator over the format the
                    // operators before it gave: a long chain does not deepen the stack.
                    var spine = chain.Spine;
                    var left = Operand(spine[0].Left);
                    foreach (var b in spine) left = Binary(b, left, Operand(b.Right));
                    return left;
                case FunctionNode { Name: "DATE" or "TODAY" }:
                    return NumberFormat.ShortDate;
                case FunctionNode { Name: "NOW" }:
                    return NumberFormat.ShortDateTime;
                case FunctionNode f:
                    foreach (var argument in f.Arguments)
                    {
                        if (Operand(argument) is { } format) return format;
                    }
                    return null;
                default:
                    return null;
            }
        }

        // The format one operator gives, from the formats of its operands.
        static NumberFormat? Binary(BinaryNode b, NumberFormat? left, NumberFormat? right)
        {
            if (b.Operator is not ("+" or "-")) return (left ?? right) is { IsPercent: false } product ? product : null;
            if (IsPercentConstant(b.Left) || IsPercentConstant(b.Right))
            {
                var other = IsPercentConstant(b.Left) ? right : left;
                return other is null ? PercentConstant : null;
            }
            if (b.Operator == "-" && left is { IsDate: true } && right is { IsDate: true }) return null;
            return left ?? right;
        }
    }
}
