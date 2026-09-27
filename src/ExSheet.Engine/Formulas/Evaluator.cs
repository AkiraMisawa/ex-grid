using System.Globalization;

namespace ExSheet.Engine.Formulas;

/// <summary>What the evaluator reads cells through: the Values of one recalculation, blank as <see langword="null"/>.</summary>
internal interface ICellReader
{
    Value? Read(CellAddress address);
}

/// <summary>
/// Evaluates a parsed Formula to a Value, as Excel does: IEEE doubles, a blank read as 0, and an
/// Error Value carried through (ADR-0047).
/// </summary>
internal sealed class Evaluator(ICellReader cells, CultureInfo culture)
{
    /// <summary>A Formula's result is never blank: a Formula that reads an empty cell shows 0.</summary>
    public Value Evaluate(Node node) => Scalar(node) ?? Value.FromNumber(0);

    private Value? Scalar(Node node) => node switch
    {
        NumberNode n => Value.FromNumber(n.Number),
        ReferenceNode r => cells.Read(new CellAddress(r.Reference.Row1, r.Reference.Column1)),
        ParenthesesNode p => Scalar(p.Inner),
        UnaryNode u => Negate(u),
        BinaryNode b => Arithmetic(b),
        _ => throw new InvalidOperationException($"No evaluation for {node.GetType().Name}."),
    };

    private Value Negate(UnaryNode node)
    {
        var operand = ToNumber(Scalar(node.Operand), out var error);
        if (error is { } e) return Value.FromError(e);
        return node.Operator == '-' ? Value.FromNumber(-operand) : Value.FromNumber(operand);
    }

    private Value Arithmetic(BinaryNode node)
    {
        var left = ToNumber(Scalar(node.Left), out var leftError);
        if (leftError is { } le) return Value.FromError(le);
        var right = ToNumber(Scalar(node.Right), out var rightError);
        if (rightError is { } re) return Value.FromError(re);
        double result;
        switch (node.Operator)
        {
            case "+": result = left + right; break;
            case "-": result = left - right; break;
            case "*": result = left * right; break;
            case "/":
                if (right == 0) return Value.FromError(ErrorValue.Div0);
                result = left / right;
                break;
            default: throw new InvalidOperationException($"No operator {node.Operator}.");
        }
        return Number(result);
    }

    /// <summary>A result that is not a finite double is <c>#NUM!</c>, as in Excel.</summary>
    internal static Value Number(double result) =>
        double.IsFinite(result) ? Value.FromNumber(result) : Value.FromError(ErrorValue.Num);

    /// <summary>
    /// Excel's coercion of an operand to a number: a blank is 0, a boolean 1 or 0, text that reads
    /// as a number under the Sheet's culture is that number, other text is <c>#VALUE!</c>.
    /// </summary>
    private double ToNumber(Value? value, out ErrorValue? error)
    {
        error = null;
        if (value is not { } v) return 0;
        switch (v.Kind)
        {
            case ValueKind.Number: return v.Number;
            case ValueKind.Boolean: return v.Boolean ? 1 : 0;
            case ValueKind.Error:
                error = v.Error;
                return 0;
            default:
                if (double.TryParse(v.Text, NumberStyles.Float, culture, out var parsed) && double.IsFinite(parsed)) return parsed;
                error = ErrorValue.Value;
                return 0;
        }
    }
}
