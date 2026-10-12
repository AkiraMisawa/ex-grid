namespace ExSheet.Engine.Formulas;

/// <summary>
/// Reads a Formula in Excel's invariant syntax (ADR-0047) into nodes, with Excel's operator
/// precedence, from the loosest binding:
/// comparison (<c>= &lt;&gt; &lt; &gt; &lt;= &gt;=</c>), <c>&amp;</c>, <c>+ -</c>, <c>* /</c>,
/// <c>^</c>, <c>%</c>, then negation — so <c>-2^2</c> is 4 and <c>2^3^2</c> is 64, as in Excel.
/// Anything it cannot read is refused with a <see cref="FormulaSyntaxException"/>, as Excel
/// refuses such a Formula; that includes the union (<c>,</c>) operator, array constants, a range
/// operator between anything but two addresses, and the intersection (space) operator between
/// two References, which ExSheet does not read; and, as Excel refuses it, a call that gives an
/// argument Excel takes only as a Reference something that can never be one
/// (<see cref="FunctionDefinition.ReferenceOnly"/>). An intersection with a name in it is read, as
/// Excel reads <c>- item one</c> typed as the Formula <c>=- item one</c> (ADR-0047, second run):
/// ExSheet defines no names, so it is <c>#NAME?</c> whatever the other side is.
/// </summary>
internal sealed partial class Parser
{
    private readonly string _formula;
    private readonly List<Token> _tokens;
    private int _at;

    /// <summary>The <c>LET</c>s being read, the innermost on top: the scopes a name is resolved in (ADR-0181).</summary>
    private readonly Stack<LetScope> _lets = new();

    /// <summary>
    /// The names of another script than ASCII that no scope bound where they stand, with the
    /// <c>LET</c>s around them. Each must be a name one of those <c>LET</c>s binds, later in its text,
    /// or the Formula is refused, as it was before <c>LET</c> was admitted (ADR-0181).
    /// </summary>
    private readonly List<(NameNode Name, LetScope[] Around)> _wideNames = [];

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
        // Outside a LET, a name of another script is refused where it stands, before anything after
        // it, as the lexer refused it before LET was admitted (ADR-0181).
        if (Lexer.WideNameOutsideLet(parser._tokens) is { } wide) throw parser.NotSyntax(wide.Position + Lexer.FirstWide(wide.Text));
        var node = parser.ParseComparison();
        if (parser.Peek.Kind != TokenKind.End) throw parser.Unexpected();
        parser.RefuseUnboundWideNames();
        return node;
    }

    private Token Peek => _tokens[_at];

    /// <summary>
    /// For <c>RAND</c> and <c>RANDBETWEEN</c>: how many calls of either stand before this one in the
    /// Formula's text — its place among the Formula's draws (ADR-0180). 0 for any other function.
    /// </summary>
    private int DrawPlace(Token name)
    {
        if (!IsDraw(name.Text)) return 0;
        var place = 0;
        for (var i = 0; _tokens[i].Position < name.Position; i++)
        {
            if (_tokens[i].Kind == TokenKind.Name && IsDraw(_tokens[i].Text) && _tokens[i + 1] is { Kind: TokenKind.LeftParenthesis, AfterSpace: false }) place++;
        }
        return place;

        static bool IsDraw(string text) =>
            text.Equals("RAND", StringComparison.OrdinalIgnoreCase) || text.Equals("RANDBETWEEN", StringComparison.OrdinalIgnoreCase);
    }

    private Token Next() => _tokens[_at++];

    private bool PeekOperator(params string[] ops) => Peek.Kind == TokenKind.Operator && Array.IndexOf(ops, Peek.Text) >= 0;

    private FormulaSyntaxException Unexpected() =>
        Peek.Kind == TokenKind.End
            ? new FormulaSyntaxException(_formula, Peek.Position, "the Formula ends too early.")
            // A name of another script that no LET around it binds is refused as the lexer refused one (ADR-0181).
            : Peek.Kind == TokenKind.Name && Lexer.FirstWide(Peek.Text) is var wide and >= 0 && Resolve(Peek.Text) is null
                ? NotSyntax(Peek.Position + wide)
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
            // A LET name stands for what it is bound to, which may be a range: Excel would intersect them (ADR-0181).
            if (node is ReferenceNode or NameNode { Binding: not null } && right is ReferenceNode or NameNode { Binding: not null })
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
                var name = new NameNode(token.Text, token.Position, Resolve(token.Text));
                if (name.Binding is null && Lexer.FirstWide(token.Text) >= 0) _wideNames.Add((name, [.. _lets]));
                return name;
            default:
                throw Unexpected();
        }
    }

    private Node ParseCall(Token name)
    {
        // A function's name is ASCII: one of another script is refused as it was before LET (ADR-0181).
        if (Lexer.FirstWide(name.Text) is var wide and >= 0) throw NotSyntax(name.Position + wide);
        var function = FunctionLibrary.Find(name.Text);
        if (function?.Name == "LET") return ParseLet(name);
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
        if (function.ReferenceOnly is var (first, last))
        {
            for (var i = first; i <= last && i < arguments.Count; i++)
            {
                if (NeverAReference(arguments[i]))
                {
                    throw new FormulaSyntaxException(_formula, name.Position, $"{function.Name} takes only a Reference as its argument {i + 1}.");
                }
            }
        }
        return new FunctionNode(function.Name, arguments, function) { Place = DrawPlace(name) };
    }

    /// <summary>
    /// Whether an argument can never be a Reference, as Excel tells one when a Formula is entered
    /// (<see cref="FunctionDefinition.ReferenceOnly"/>): a number, a text or a boolean, an operator's
    /// result (but parentheses around a Reference), or a call of a declared function that never gives
    /// one, such as <c>SEQUENCE</c>. A Reference, a structured reference, a name (a <c>LET</c>'s may
    /// stand for a range), an Error Value (where a deletion left <c>#REF!</c>), an argument left empty,
    /// a <c>LET</c>, and a call of a function that can give a Reference (<c>INDIRECT</c>,
    /// <c>OFFSET</c>, <c>INDEX</c>, <c>CHOOSE</c>, <c>IF</c> and their kin) or of a name outside the
    /// declared set are taken, and judged when they are computed.
    /// </summary>
    private static bool NeverAReference(Node argument) => argument switch
    {
        NumberNode or TextNode or BooleanNode => true,
        BinaryNode or UnaryNode or PercentNode => true,
        ParenthesesNode parentheses => NeverAReference(parentheses.Inner),
        FunctionNode { Function: { } declared } => !declared.ReturnsReference,
        _ => false,
    };
}
