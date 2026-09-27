using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

public sealed partial class Sheet
{
    /// <summary>
    /// The format a Formula gives a General cell when it is entered, as Excel's does (ADR-0047), or
    /// <see langword="null"/> for none. The rule taken is the one Excel is widely observed to
    /// follow in simple arithmetic: a Formula made only of References to single cells, constants,
    /// <c>+</c>, <c>-</c> (binary and unary) and parentheses takes the format of the first
    /// Reference, in the order written, whose cell shows numbers in a format other than General
    /// (a text format such as <c>@</c> is passed over) — so a date plus a number, and a date minus
    /// a date, show as dates. Microsoft does not document it, and
    /// every case of it is uncertain in the corpus until Excel is asked. Anything else —
    /// multiplication, division, <c>&amp;</c>, comparisons, functions, ranges — gives no format
    /// rather than a guessed one, though Excel may give one: those cases are recorded as the
    /// engine's answer, uncertain, to be observed.
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
            _ => false,
        };

        NumberFormat? First(Node node) => node switch
        {
            ReferenceNode r when IsLocal(r.Reference) =>
                GetFormat(new CellAddress(r.Reference.Row1, r.Reference.Column1)) is { ShowsNumbersAsGeneral: false } format ? format : null,
            ParenthesesNode p => First(p.Inner),
            UnaryNode u => First(u.Operand),
            BinaryNode b => First(b.Left) ?? First(b.Right),
            _ => null,
        };
    }
}
