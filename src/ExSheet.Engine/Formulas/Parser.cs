namespace ExSheet.Engine.Formulas;

/// <summary>
/// Reads a Formula in Excel's invariant syntax (ADR-0047) into nodes. Anything it cannot read is
/// refused with a <see cref="FormulaSyntaxException"/>, as Excel refuses such a Formula.
/// </summary>
internal sealed class Parser
{
    private readonly string _formula;
    private readonly List<Token> _tokens;
    private int _at;

    private Parser(string formula, List<Token> tokens)
    {
        _formula = formula;
        _tokens = tokens;
    }

    /// <summary>Parses a Formula; <paramref name="formula"/> starts with <c>=</c>.</summary>
    public static Node Parse(string formula)
    {
        if (formula.Length == 0 || formula[0] != '=') throw new FormulaSyntaxException(formula, 0, "a Formula begins with '='.");
        var parser = new Parser(formula, Lexer.Tokenize(formula, 1));
        if (parser.Peek.Kind == TokenKind.End) throw new FormulaSyntaxException(formula, 1, "the Formula is empty.");
        var node = parser.ParseExpression();
        if (parser.Peek.Kind != TokenKind.End) throw parser.Unexpected();
        return node;
    }

    private Token Peek => _tokens[_at];

    private Token Next() => _tokens[_at++];

    private bool PeekOperator(string op) => Peek.Kind == TokenKind.Operator && Peek.Text == op;

    private FormulaSyntaxException Unexpected() =>
        Peek.Kind == TokenKind.End
            ? new FormulaSyntaxException(_formula, Peek.Position, "the Formula ends too early.")
            : new FormulaSyntaxException(_formula, Peek.Position, $"'{Peek.Text}' is not expected here.");

    private Node ParseExpression() => ParseAdditive();

    private Node ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (PeekOperator("+") || PeekOperator("-"))
        {
            var op = Next().Text;
            left = new BinaryNode(op, left, ParseMultiplicative());
        }
        return left;
    }

    private Node ParseMultiplicative()
    {
        var left = ParseUnary();
        while (PeekOperator("*") || PeekOperator("/"))
        {
            var op = Next().Text;
            left = new BinaryNode(op, left, ParseUnary());
        }
        return left;
    }

    private Node ParseUnary()
    {
        if (PeekOperator("-") || PeekOperator("+"))
        {
            var op = Next().Text[0];
            return new UnaryNode(op, ParseUnary());
        }
        return ParsePrimary();
    }

    private Node ParsePrimary()
    {
        var token = Peek;
        switch (token.Kind)
        {
            case TokenKind.Number:
                Next();
                return new NumberNode(token.Number);
            case TokenKind.Reference when token.Reference!.Shape == ReferenceShape.Cell && token.Reference.SheetName is null:
                Next();
                return new ReferenceNode(token.Reference);
            case TokenKind.LeftParenthesis:
                Next();
                var inner = ParseExpression();
                if (Peek.Kind != TokenKind.RightParenthesis) throw Unexpected();
                Next();
                return new ParenthesesNode(inner);
            default:
                throw Unexpected();
        }
    }
}
