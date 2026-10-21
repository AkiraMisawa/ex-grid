namespace ExSheet.Engine.Formulas;

/// <summary>
/// <c>LET</c>'s names (ADR-0181), as Excel's keyboard accepts them. A name is seen by the values bound
/// after it and by its own <c>LET</c>'s calculation, and an inner <c>LET</c> may bind it again; the
/// parser resolves each name to its binding where it stands, so a name read before its binding, or
/// inside its own value, resolves to none and is <c>#NAME?</c>. What Excel's keyboard refused is refused
/// on entry, as any Formula the engine cannot read is.
/// </summary>
internal sealed partial class Parser
{
    /// <summary>The longest name Excel's keyboard took: 249 characters (LET-ADDITIONAL-002; 250 refused, LET-KEYS-013).</summary>
    private const int LongestLetName = 249;

    /// <summary>The most names one <c>LET</c> binds: 126 (LET-029; a 127th is refused, LET-030).</summary>
    private const int MostLetNames = 126;

    /// <summary>One <c>LET</c> being read: the names bound so far, in scope, and every name it binds, later ones included.</summary>
    private sealed class LetScope
    {
        public List<LetBinding> Bound { get; } = [];

        public HashSet<string> Names { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The binding a name reads where it stands: the innermost <c>LET</c> that has bound it so far; <see langword="null"/> for none.</summary>
    private LetBinding? Resolve(string name)
    {
        foreach (var scope in _lets)
        {
            foreach (var binding in scope.Bound)
            {
                if (string.Equals(binding.Name, name, StringComparison.OrdinalIgnoreCase)) return binding;
            }
        }
        return null;
    }

    /// <summary>
    /// <c>LET(name1, value1, …, calculation)</c>: each value read with the names bound before it in
    /// scope, the calculation with all of them. A value left empty is bound as the empty argument it
    /// is, as Excel's keyboard took <c>LET(x,,x)</c> and showed 0 (LET-107, run 17). Refused on entry,
    /// as Excel refuses each with a dialog: too few arguments, an even number of them, a missing
    /// calculation, a name bound twice, a 127th name, and a name <see cref="LetNameRefusal"/> refuses.
    /// </summary>
    private LetNode ParseLet(Token let)
    {
        Next(); // (
        var scope = new LetScope();
        _lets.Push(scope);
        var arguments = new List<Node>();
        LetBinding? named = null;
        if (Peek.Kind == TokenKind.RightParenthesis) throw new FormulaSyntaxException(_formula, let.Position, "LET takes at least 3 argument(s).");
        while (true)
        {
            var start = Peek;
            var argument = Peek.Kind is TokenKind.Comma or TokenKind.RightParenthesis ? MissingNode.Instance : ParseComparison();
            var index = arguments.Count;
            arguments.Add(argument);
            if (Peek.Kind == TokenKind.RightParenthesis)
            {
                Next();
                break;
            }
            if (Peek.Kind != TokenKind.Comma) throw Unexpected();
            Next();
            if (index % 2 == 0)
            {
                // A name, with its value to follow.
                if (scope.Names.Count == MostLetNames) throw new FormulaSyntaxException(_formula, start.Position, $"a LET binds at most {MostLetNames} names.");
                var name = LetName(argument, start);
                if (!scope.Names.Add(name)) throw new FormulaSyntaxException(_formula, start.Position, $"'{name}' is bound twice in one LET.");
                named = new LetBinding(name);
            }
            else
            {
                named!.Value = argument;
                scope.Bound.Add(named);
                named = null;
            }
        }
        _lets.Pop();
        if (arguments.Count < 3) throw new FormulaSyntaxException(_formula, let.Position, "LET takes at least 3 argument(s).");
        if (arguments.Count % 2 == 0) throw new FormulaSyntaxException(_formula, let.Position, "LET takes names and their values in pairs, then one calculation.");
        if (arguments[^1] is MissingNode) throw new FormulaSyntaxException(_formula, let.Position, "LET's calculation is missing.");
        return new LetNode(scope.Bound, arguments[^1]);
    }

    /// <summary>The argument as a <c>LET</c> name, or a refusal saying why it cannot be one.</summary>
    private string LetName(Node argument, Token start)
    {
        var refusal = argument switch
        {
            NameNode name => LetNameRefusal(name.Name),
            ReferenceNode => $"'{start.Text}' reads as a Reference, and cannot be a LET name.",
            BooleanNode => $"{start.Text.ToUpperInvariant()} cannot be a LET name.",
            _ => "a LET name is expected here.",
        };
        if (refusal is not null) throw new FormulaSyntaxException(_formula, start.Position, refusal);
        var bound = (NameNode)argument;
        // Its own name is no unbound use of one.
        _wideNames.RemoveAll(w => ReferenceEquals(w.Name, bound));
        _names.Remove(bound);
        return bound.Name;
    }

    /// <summary>
    /// Why a name cannot be a <c>LET</c> name, or <see langword="null"/> when it can (ADR-0181): it
    /// starts with a letter of any script or <c>_</c>, goes on with letters, ASCII digits and <c>_</c>,
    /// and is at most 249 characters. The name of a function, declared or not, and an R1C1 shape are
    /// names, as Excel's keyboard took <c>SUM</c>, <c>IRR</c>, <c>N</c>, <c>R1C1</c> and <c>RC</c>
    /// (LET-101, 111, 112, 103, 106; run 17); a name followed by <c>(</c> is still the function's
    /// call, and never reaches here. A name that reads as an A1 Reference (<c>A1</c>, <c>R2</c>,
    /// <c>C3</c>) is read as one, and <c>TRUE</c> and <c>FALSE</c> as booleans, and each is refused
    /// before it reaches here, as Excel's keyboard refuses them (LET-KEYS-004, LET-104, 105, 102).
    /// </summary>
    private static string? LetNameRefusal(string name)
    {
        if (name.Length > LongestLetName) return $"a LET name is at most {LongestLetName} characters.";
        if (!(char.IsLetter(name[0]) || name[0] == '_') || name.Skip(1).Any(c => !(char.IsLetter(c) || char.IsAsciiDigit(c) || c == '_')))
        {
            return $"'{name}' cannot be a LET name: a name begins with a letter or _, and holds only letters, digits and _.";
        }
        return null;
    }

    /// <summary>
    /// Refuses a name of another script than ASCII that no <c>LET</c> around it binds, at its first
    /// such character, as the lexer refused one before <c>LET</c> was admitted: such a name is a
    /// <c>LET</c> name or nothing. One that a <c>LET</c> around it binds later in its text is read
    /// before its binding, and is <c>#NAME?</c>, as an ASCII one is (ADR-0181).
    /// </summary>
    private void RefuseUnboundWideNames()
    {
        foreach (var (name, around) in _wideNames)
        {
            if (around.Any(scope => scope.Names.Contains(name.Name))) continue;
            throw NotSyntax(name.Position + Lexer.FirstWide(name.Name));
        }
    }

    private FormulaSyntaxException NotSyntax(int at) =>
        new(_formula, at, $"'{_formula[at]}' is not part of the formula syntax ExSheet reads.");
}
