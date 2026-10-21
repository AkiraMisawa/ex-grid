namespace ExSheet.Engine.Formulas;

/// <summary>
/// The names a parsed Formula reads that nothing in it binds (<see cref="Entry.UnboundNames"/>): every
/// <see cref="NameNode"/> of the tree the parser has already built, less those a <c>LET</c> binds and those
/// a <c>LAMBDA</c> takes as parameters, whether or not the engine declares <c>LAMBDA</c>. Today it does not (a call is
/// <c>#NAME?</c>), but Excel's workbooks write it, and its parameters are no names a workbook defines
/// (ADR-0330 §4): <c>LAMBDA(x, y, x+y)</c> binds <c>x</c> and <c>y</c> in its last argument, and its other
/// arguments are the parameters themselves.
/// </summary>
internal static class UnboundNameWalk
{
    public static IReadOnlyList<string> Of(Node formula)
    {
        var found = new List<string>();
        Walk(formula, [], found);
        return found.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void Walk(Node node, List<string> parameters, List<string> found)
    {
        switch (node)
        {
            case NameNode name:
                if (name.Binding is null
                    && Lexer.FirstWide(name.Name) < 0
                    && !parameters.Contains(name.Name, StringComparer.OrdinalIgnoreCase))
                {
                    found.Add(name.Name);
                }
                break;
            case LetNode let:
                foreach (var binding in let.Bindings) Walk(binding.Value, parameters, found);
                Walk(let.Calculation, parameters, found);
                break;
            case FunctionNode call when call.Name.Equals("LAMBDA", StringComparison.OrdinalIgnoreCase) && call.Arguments.Count > 0:
                var inner = new List<string>(parameters);
                for (var i = 0; i < call.Arguments.Count - 1; i++)
                {
                    if (call.Arguments[i] is NameNode parameter)
                        inner.Add(parameter.Name);
                    else
                        Walk(call.Arguments[i], parameters, found);
                }
                Walk(call.Arguments[^1], inner, found);
                break;
            case FunctionNode call:
                foreach (var argument in call.Arguments) Walk(argument, parameters, found);
                break;
            case IntersectionNode intersection:
                Walk(intersection.Left, parameters, found);
                Walk(intersection.Right, parameters, found);
                break;
            case BinaryNode binary:
                // Along the chain of left operands in a loop: a chain of thousands of operators does not deepen the stack.
                foreach (var operand in binary.Operands) Walk(operand, parameters, found);
                break;
            case ParenthesesNode parentheses:
                Walk(parentheses.Inner, parameters, found);
                break;
            case UnaryNode unary:
                Walk(unary.Operand, parameters, found);
                break;
            case PercentNode percent:
                Walk(percent.Operand, parameters, found);
                break;
        }
    }
}
