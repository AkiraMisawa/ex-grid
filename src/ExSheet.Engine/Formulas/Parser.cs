namespace ExSheet.Engine.Formulas;

/// <summary>
/// Reads a Formula in Excel's invariant syntax (ADR-0047) into nodes, with Excel's operator
/// precedence, from the loosest binding:
/// comparison (<c>= &lt;&gt; &lt; &gt; &lt;= &gt;=</c>), <c>&amp;</c>, <c>+ -</c>, <c>* /</c>,
/// <c>^</c>, <c>%</c>, then negation — so <c>-2^2</c> is 4 and <c>2^3^2</c> is 64, as in Excel.
/// Anything it cannot read is refused with a <see cref="FormulaSyntaxException"/>, as Excel
/// refuses such a Formula; that includes the union (<c>,</c>) operator, array constants, a range
/// operator between anything but two addresses, and the intersection (space) operator between
/// two References, which ExSheet does not read. An intersection with a name in it is read, as
/// Excel reads <c>- item one</c> typed as the Formula <c>=- item one</c> (ADR-0047, second run):
/// ExSheet defines no names, so it is <c>#NAME?</c> whatever the other side is.
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
    public static Node Parse(string formula) => Parse(formula, out _);

    /// <summary>Parses a Formula, and gives the tokens it was read from.</summary>
    public static Node Parse(string formula, out IReadOnlyList<Token> tokens)
    {
        if (formula.Length == 0 || formula[0] != '=') throw new FormulaSyntaxException(formula, 0, "a Formula begins with '='.");
        var parser = new Parser(formula, Lexer.Tokenize(formula, 1));
        tokens = parser._tokens;
        if (parser.Peek.Kind == TokenKind.End) throw new FormulaSyntaxException(formula, 1, "the Formula is empty.");
        var node = parser.ParseComparison();
        if (parser.Peek.Kind != TokenKind.End) throw parser.Unexpected();
        return node;
    }

    private Token Peek => _tokens[_at];

    private Token Next() => _tokens[_at++];

    private bool PeekOperator(params string[] ops) => Peek.Kind == TokenKind.Operator && Array.IndexOf(ops, Peek.Text) >= 0;

    private FormulaSyntaxException Unexpected() =>
        Peek.Kind == TokenKind.End
            ? new FormulaSyntaxException(_formula, Peek.Position, "the Formula ends too early.")
            : new FormulaSyntaxException(_formula, Peek.Position, $"'{Peek.Text}' is not expected here.");

    private Node ParseComparison()
    {
        var left = ParseConcatenation();
        while (PeekOperator("=", "<>", "<", ">", "<=", ">="))
        {
            var op = Next().Text;
            left = new BinaryNode(op, left, ParseConcatenation());
        }
        return left;
    }

    private Node ParseConcatenation()
    {
        var left = ParseAdditive();
        while (PeekOperator("&"))
        {
            Next();
            left = new BinaryNode("&", left, ParseAdditive());
        }
        return left;
    }

    private Node ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (PeekOperator("+", "-"))
        {
            var op = Next().Text;
            left = new BinaryNode(op, left, ParseMultiplicative());
        }
        return left;
    }

    private Node ParseMultiplicative()
    {
        var left = ParsePower();
        while (PeekOperator("*", "/"))
        {
            var op = Next().Text;
            left = new BinaryNode(op, left, ParsePower());
        }
        return left;
    }

    /// <summary><c>^</c> associates to the left in Excel: <c>2^3^2</c> is <c>(2^3)^2</c>.</summary>
    private Node ParsePower()
    {
        var left = ParseUnary();
        while (PeekOperator("^"))
        {
            Next();
            left = new BinaryNode("^", left, ParseUnary());
        }
        return left;
    }

    /// <summary>Negation binds tighter than <c>%</c> and <c>^</c> in Excel.</summary>
    private Node ParseUnary()
    {
        if (PeekOperator("-", "+"))
        {
            var op = Next().Text[0];
            return new UnaryNode(op, ParseUnary());
        }
        return ParsePercent();
    }

    private Node ParsePercent()
    {
        var node = ParseIntersection();
        while (PeekOperator("%"))
        {
            Next();
            node = new PercentNode(node);
        }
        return node;
    }

    /// <summary>
    /// The intersection operator, whitespace between two References or names, which binds tighter
    /// than <c>%</c> and negation in Excel. It is read only when a name stands on one side of it;
    /// between two References it is refused by name, as an operator the engine does not implement
    /// (<see cref="FormulaSyntaxException.IsUnimplemented"/>; ADR-0047, third run, TYPED-054).
    /// </summary>
    private Node ParseIntersection()
    {
        var node = ParsePrimary();
        while (node is NameNode or ReferenceNode or IntersectionNode && Peek.AfterSpace && IsIntersectionOperand(_at))
        {
            var at = Peek;
            var right = ParsePrimary();
            if (node is ReferenceNode && right is ReferenceNode)
            {
                throw new FormulaSyntaxException(_formula, at.Position, "the intersection operator (a space between two References) is not implemented.") { IsUnimplemented = true };
            }
            node = new IntersectionNode(node, right);
        }
        return node;
    }

    /// <summary>Whether the token at <paramref name="index"/> is a Reference or a name that is not a function call nor a boolean.</summary>
    private bool IsIntersectionOperand(int index)
    {
        var token = _tokens[index];
        if (token.Kind == TokenKind.Reference) return true;
        if (token.Kind != TokenKind.Name) return false;
        if (_tokens[index + 1] is { Kind: TokenKind.LeftParenthesis, AfterSpace: false }) return false;
        return !token.Text.Equals("TRUE", StringComparison.OrdinalIgnoreCase) && !token.Text.Equals("FALSE", StringComparison.OrdinalIgnoreCase);
    }

    private Node ParsePrimary()
    {
        var token = Peek;
        switch (token.Kind)
        {
            case TokenKind.Number:
                Next();
                return new NumberNode(token.Number);
            case TokenKind.Text:
                Next();
                return new TextNode(token.Text);
            case TokenKind.Error:
                Next();
                return new ErrorNode(token.Error);
            case TokenKind.Reference:
                Next();
                return new ReferenceNode(token.Reference!);
            case TokenKind.StructuredReference:
                Next();
                return new StructuredReferenceNode(token.TableName!, token.Text);
            case TokenKind.LeftParenthesis:
                Next();
                var inner = ParseComparison();
                if (Peek.Kind != TokenKind.RightParenthesis) throw Unexpected();
                Next();
                return new ParenthesesNode(inner);
            case TokenKind.Name:
                Next();
                if (Peek.Kind == TokenKind.LeftParenthesis && !Peek.AfterSpace) return ParseCall(token);
                if (token.Text.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) return new BooleanNode(true);
                if (token.Text.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) return new BooleanNode(false);
                return new NameNode(token.Text);
            default:
                throw Unexpected();
        }
    }

    private FunctionNode ParseCall(Token name)
    {
        Next(); // (
        var arguments = new List<Node>();
        if (Peek.Kind == TokenKind.RightParenthesis)
        {
            Next();
        }
        else
        {
            while (true)
            {
                arguments.Add(Peek.Kind is TokenKind.Comma or TokenKind.RightParenthesis ? MissingNode.Instance : ParseComparison());
                if (Peek.Kind == TokenKind.Comma)
                {
                    Next();
                    continue;
                }
                if (Peek.Kind == TokenKind.RightParenthesis)
                {
                    Next();
                    break;
                }
                throw Unexpected();
            }
        }

        var function = FunctionLibrary.Find(name.Text);
        if (function is null) return new FunctionNode(name.Text, arguments, null);
        // Excel refuses a call with too few or too many arguments when the Formula is entered.
        if (arguments.Count < function.MinimumArguments)
        {
            throw new FormulaSyntaxException(_formula, name.Position, $"{function.Name} takes at least {function.MinimumArguments} argument(s).");
        }
        if (arguments.Count > function.MaximumArguments)
        {
            throw new FormulaSyntaxException(_formula, name.Position, $"{function.Name} takes at most {function.MaximumArguments} argument(s).");
        }
        if (function.InPairs && (arguments.Count - function.PairOffset) % 2 != 0)
        {
            throw new FormulaSyntaxException(_formula, name.Position, $"{function.Name} takes its arguments in pairs.");
        }
        return new FunctionNode(function.Name, arguments, function);
    }
}
