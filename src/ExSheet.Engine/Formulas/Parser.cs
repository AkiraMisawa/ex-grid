using System.Runtime.CompilerServices;

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

    /// <summary>
    /// The most levels of nested functions a Formula holds, a <c>LET</c> counted as one: 64, Excel's
    /// own limit, past which Excel refuses the Formula on entry ("more levels of nesting than allowed").
    /// </summary>
    internal const int MostNestedFunctions = 64;

    /// <summary>
    /// The deepest Formula the engine reads (<see cref="Node.Depth"/>): parentheses, signs, <c>%</c>,
    /// functions, the right operands of operators and the <c>LET</c> names read, each counted as a
    /// level, while a chain of operators down their left operands (<c>=1+1+…+1</c>) is not, since
    /// every walk loops along it. Deeper, a walk of the Formula could exhaust the stack and end the
    /// process, so the Formula is refused on entry rather than accepted and crash (the spine's first
    /// rule). Sixty-four functions nested with ordinary arguments stay well inside it.
    /// </summary>
    internal const int MostNesting = 256;

    /// <summary>How many parentheses, signs and calls the parser is inside: its own recursion, kept within <see cref="MostNesting"/>.</summary>
    private int _nesting;

    /// <summary>How many calls the parser is inside, kept within <see cref="MostNestedFunctions"/>.</summary>
    private int _functions;

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
        parser.Bounded(node, parser.Peek);
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

    /// <summary>
    /// An expression: operands joined by binary operators, read with a stack of operators rather than a
    /// method for each level of precedence, so that a level of parentheses or a call costs the parser
    /// few frames of its own (<see cref="MostNesting"/>). An operator applies every operator before it
    /// that binds at least as tightly, which makes each associate to the left, as in Excel.
    /// </summary>
    private Node ParseComparison()
    {
        var operands = new List<Node> { ParseUnary() };
        var operators = new List<Token>();
        while (Peek.Kind == TokenKind.Operator && Precedence(Peek.Text) is var precedence and > 0)
        {
            while (operators.Count > 0 && Precedence(operators[^1].Text) >= precedence) Apply();
            operators.Add(Next());
            operands.Add(ParseUnary());
        }
        while (operators.Count > 0) Apply();
        return operands[0];

        void Apply()
        {
            var op = operators[^1];
            operators.RemoveAt(operators.Count - 1);
            var right = operands[^1];
            operands.RemoveAt(operands.Count - 1);
            operands[^1] = Bounded(new BinaryNode(op.Text, operands[^1], right), op);
        }
    }

    /// <summary>
    /// How tightly a binary operator binds, from the loosest: comparison, <c>&amp;</c>, <c>+ -</c>,
    /// <c>* /</c>, <c>^</c>; 0 for a token that is no binary operator (<c>%</c>). Every one associates
    /// to the left, <c>^</c> too: <c>2^3^2</c> is <c>(2^3)^2</c>, as in Excel.
    /// </summary>
    private static int Precedence(string op) => op switch
    {
        "=" or "<>" or "<" or ">" or "<=" or ">=" => 1,
        "&" => 2,
        "+" or "-" => 3,
        "*" or "/" => 4,
        "^" => 5,
        _ => 0,
    };

    /// <summary>
    /// An operand: signs, then an intersection or a primary, then any <c>%</c>. Negation binds tighter
    /// than <c>^</c> in Excel, and <c>%</c> tighter than negation. A run of signs is read in a loop,
    /// not by recursion.
    /// </summary>
    private Node ParseUnary()
    {
        var signs = new List<Token>();
        while (PeekOperator("-", "+"))
        {
            signs.Add(Next());
            Enter(signs[^1]);
        }
        var node = ParseIntersection();
        while (PeekOperator("%")) node = Bounded(new PercentNode(node), Next());
        for (var i = signs.Count - 1; i >= 0; i--)
        {
            node = Bounded(new UnaryNode(signs[i].Text[0], node), signs[i]);
            _nesting--;
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
            node = Bounded(new IntersectionNode(node, right), at);
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
                Enter(Next());
                var inner = ParseComparison();
                if (Peek.Kind != TokenKind.RightParenthesis) throw Unexpected();
                _nesting--;
                return Bounded(new ParenthesesNode(inner), Next());
            case TokenKind.Name:
                Next();
                if (Peek.Kind == TokenKind.LeftParenthesis && !Peek.AfterSpace) return ParseCall(token);
                if (token.Text.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) return new BooleanNode(true);
                if (token.Text.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) return new BooleanNode(false);
                var name = Bounded(new NameNode(token.Text, token.Position, Resolve(token.Text)), token);
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
        EnterFunction(name);
        Node call = function?.Name == "LET" ? ParseLet(name) : ParseCall(name, function);
        _functions--;
        _nesting--;
        return Bounded(call, name);
    }

    /// <summary>
    /// Goes one level deeper into parentheses, a sign or a call, refusing the Formula once that is
    /// deeper than <see cref="MostNesting"/>: the parser's own recursion stays bounded.
    /// </summary>
    private void Enter(Token at)
    {
        // A host whose threads have little stack is refused the same way, never crashed on.
        if (++_nesting > MostNesting || !RuntimeHelpers.TryEnsureSufficientExecutionStack()) throw TooDeep(at.Position);
    }

    /// <summary>Goes into a call's arguments, refusing a 65th level of nested functions as Excel does (<see cref="MostNestedFunctions"/>).</summary>
    private void EnterFunction(Token name)
    {
        if (_functions + 1 > MostNestedFunctions)
        {
            throw new FormulaSyntaxException(_formula, name.Position,
                $"functions are nested more than {MostNestedFunctions} levels deep; Excel refuses such a Formula too.");
        }
        _functions++;
        Enter(name);
    }

    /// <summary>The node, or a refusal when it is deeper than <see cref="MostNesting"/> (<see cref="Node.Depth"/>).</summary>
    private T Bounded<T>(T node, Token at) where T : Node =>
        node.Depth > MostNesting ? throw TooDeep(at.Position) : node;

    private FormulaSyntaxException TooDeep(int position) =>
        new(_formula, position,
            $"the Formula is nested more than {MostNesting} levels deep (parentheses, signs, functions, and operators within operators); ExSheet cannot compute one so deep.");

    private FunctionNode ParseCall(Token name, FunctionDefinition? function)
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
