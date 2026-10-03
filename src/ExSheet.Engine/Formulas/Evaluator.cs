using System.Globalization;

namespace ExSheet.Engine.Formulas;

/// <summary>What the evaluator reads cells through: the Values of one recalculation, blank as <see langword="null"/>.</summary>
internal interface ICellReader
{
    Value? Read(CellAddress address);

    /// <summary>The addresses inside <paramref name="area"/> that are not blank, in row-major order.</summary>
    IEnumerable<CellAddress> NonBlankIn(Area area);

    /// <summary>
    /// A Linked Table's column (ADR-0049): the column's Values, or <c>#NAME?</c> for a table never
    /// declared, <c>#REF!</c> for a column it does not have, and <c>#GETTING_DATA</c> before its
    /// first snapshot.
    /// </summary>
    Operand TableColumn(string table, string column);

    /// <summary>The Sheet Day <c>TODAY</c> answers (ADR-0121), or <see langword="null"/> while it is not known.</summary>
    DateOnly? Today { get; }

    /// <summary>The moment <c>NOW</c> answers, in the Sheet's zone, read once for the recalculation (ADR-0124); <see langword="null"/> while it is not known.</summary>
    DateTime? Now { get; }

    /// <summary>Whether a Reference names the Sheet's own cells: unqualified, or qualified with the Sheet's name (ADR-0046).</summary>
    bool IsLocal(Reference reference);

    /// <summary>
    /// <c>A1#</c> (ADR-0125): the array the Anchor at <paramref name="anchor"/> spills, as this
    /// recalculation leaves it; <c>#REF!</c> when it does not spill.
    /// </summary>
    Operand Spill(CellAddress anchor);
}

/// <summary>
/// Evaluates a parsed Formula to a Value, as Excel does: IEEE doubles, a blank read as 0, Error
/// Values carried through, left to right (ADR-0047).
/// </summary>
internal sealed class Evaluator(ICellReader cells, CultureInfo culture)
{
    public CultureInfo Culture { get; } = culture;

    public ICellReader Cells { get; } = cells;

    /// <summary>The cell whose Formula is being evaluated, which <c>ROW()</c> and <c>COLUMN()</c> read; null for a Formula evaluated in no cell.</summary>
    public CellAddress? Self { get; set; }

    /// <summary>
    /// A Formula's result. It is never blank: a Formula that reads an empty cell shows 0. When the
    /// Formula's last operation is an addition or a subtraction whose result nearly cancels, the
    /// result is 0, as Excel's is (<see cref="Arithmetic.FinalAdd"/>). Parentheses around it make
    /// it no longer the last: Excel was observed to leave <c>=(0.1+0.2-0.3)</c> at 5.55E-17
    /// (ADR-0047, second run).
    /// </summary>
    public Value Evaluate(Node node) => ScalarOf(EvaluateResult(node)) ?? Value.FromNumber(0);

    /// <summary>
    /// A Formula's result as it spills (ADR-0125): a range or an array of more than one Value is
    /// returned as it is, for the Sheet to spill; anything else is one Value, never blank, with the
    /// final addition's rule applied as in <see cref="Evaluate"/>.
    /// </summary>
    public Operand EvaluateResult(Node node)
    {
        if (node is BinaryNode { Operator: "+" or "-" } final)
        {
            var left = Operand(final.Left);
            var right = Operand(final.Right);
            if (IsMulti(left) || IsMulti(right)) return Elementwise(final.Operator, left, right);
            return Formulas.Operand.Of(BinaryValues(final.Operator, ScalarOf(left), ScalarOf(right), finalOperation: true));
        }
        var result = Operand(node);
        if (IsMulti(result)) return result;
        return Formulas.Operand.Of(ScalarOf(result) ?? Value.FromNumber(0));
    }

    /// <summary>Whether an operand holds more than one Value: a range of several cells, a Linked Table's column of several, or an array.</summary>
    internal static bool IsMulti(Operand operand) => operand.Kind switch
    {
        OperandKind.Area => !operand.Area.IsSingleCell,
        OperandKind.Column => operand.Column!.Count != 1,
        OperandKind.Array => true,
        _ => false,
    };

    /// <summary>An operand read whole, as an array: a range's cells, a Linked Table's column, or one Value.</summary>
    public ValueArray ToArray(Operand operand)
    {
        switch (operand.Kind)
        {
            case OperandKind.Array:
                return operand.Array!;
            case OperandKind.Area:
                var area = operand.Area;
                if ((long)area.Rows * area.Columns > ValueArray.MostCells) return ValueArray.Single(Value.FromError(ErrorValue.Num));
                var array = new ValueArray(area.Rows, area.Columns);
                foreach (var address in Cells.NonBlankIn(area)) array[address.Row - area.Row1, address.Column - area.Column1] = Cells.Read(address);
                return array;
            case OperandKind.Column:
                var column = operand.Column!;
                if (column.Count == 0) return ValueArray.Single(Value.FromError(ErrorValue.Value));
                var values = new ValueArray(column.Count, 1);
                for (var i = 0; i < column.Count; i++) values[i, 0] = column[i];
                return values;
            default:
                return ValueArray.Single(ScalarOf(operand));
        }
    }

    /// <summary>An operator applied element by element, the two arrays broadcast as Excel broadcasts them (ADR-0125).</summary>
    private Operand Elementwise(string op, Operand leftOperand, Operand rightOperand)
    {
        var left = ToArray(leftOperand);
        var right = ToArray(rightOperand);
        var rows = left.Rows == 1 ? right.Rows : right.Rows == 1 ? left.Rows : Math.Max(left.Rows, right.Rows);
        var columns = left.Columns == 1 ? right.Columns : right.Columns == 1 ? left.Columns : Math.Max(left.Columns, right.Columns);
        if ((long)rows * columns > ValueArray.MostCells) return Formulas.Operand.Of(ErrorValue.Num);
        var result = new ValueArray(rows, columns);
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < columns; c++) result[r, c] = BinaryValues(op, left.Broadcast(r, c), right.Broadcast(r, c), finalOperation: false);
        }
        return Formulas.Operand.Of(result);
    }

    /// <summary>A function of one Value applied to each Value of an operand that holds several.</summary>
    private Operand Map(Operand operand, Func<Value?, Value> each)
    {
        if (!IsMulti(operand)) return Formulas.Operand.Of(each(ScalarOf(operand)));
        var source = ToArray(operand);
        var result = new ValueArray(source.Rows, source.Columns);
        for (var r = 0; r < source.Rows; r++)
        {
            for (var c = 0; c < source.Columns; c++) result[r, c] = each(source[r, c]);
        }
        return Formulas.Operand.Of(result);
    }

    public Operand Operand(Node node) => node switch
    {
        NumberNode n => Formulas.Operand.Of(Value.FromNumber(n.Number)),
        TextNode t => Formulas.Operand.Of(Value.FromText(t.Text)),
        BooleanNode b => Formulas.Operand.Of(Value.FromBoolean(b.Value)),
        ErrorNode e => Formulas.Operand.Of(e.Error),
        MissingNode => Formulas.Operand.Missing,
        // One Sheet exists: a Reference qualified with its name reads it, any other qualifier names nothing (ADR-0046).
        ReferenceNode r => !Cells.IsLocal(r.Reference) ? Formulas.Operand.Of(ErrorValue.Ref)
            : r.Reference.Spilled ? Cells.Spill(new CellAddress(r.Reference.Row1, r.Reference.Column1))
            : Formulas.Operand.Of(r.Reference.Area),
        StructuredReferenceNode s => Cells.TableColumn(s.Table, s.Column),
        NameNode or IntersectionNode => Formulas.Operand.Of(ErrorValue.Name),
        ParenthesesNode p => Operand(p.Inner),
        UnaryNode u => Map(Operand(u.Operand), value => Negate(u.Operator, value)),
        PercentNode p => Map(Operand(p.Operand), Percent),
        BinaryNode b => Binary(b),
        FunctionNode f => f.Function is null ? Formulas.Operand.Of(ErrorValue.Name) : f.Function.Invoke(new FunctionCall(this, f.Arguments)),
        _ => throw new InvalidOperationException($"No evaluation for {node.GetType().Name}."),
    };

    /// <summary>
    /// An operand used as one Value: a single cell is read; a rectangle of more than one cell is
    /// <c>#VALUE!</c>, because ExSheet has no spilled arrays and will not pick one cell of it
    /// silently. An empty argument is blank.
    /// </summary>
    public Value? ScalarOf(Operand operand) => operand.Kind switch
    {
        OperandKind.Scalar => operand.Scalar,
        OperandKind.Area when operand.Area.IsSingleCell => Cells.Read(new CellAddress(operand.Area.Row1, operand.Area.Column1)),
        OperandKind.Area => Value.FromError(ErrorValue.Value),
        OperandKind.Column when operand.Column!.Count == 1 => operand.Column[0],
        OperandKind.Column => Value.FromError(ErrorValue.Value),
        OperandKind.Array => operand.Array!.IsSingle ? operand.Array[0, 0] : Value.FromError(ErrorValue.Value),
        _ => null,
    };

    /// <summary>The Values of a range that are not blank, in order: row-major for cells, top to bottom for a Linked Table's column.</summary>
    public IEnumerable<Value> RangeValues(Operand range) => range.Kind switch
    {
        OperandKind.Area => Cells.NonBlankIn(range.Area).Select(a => Cells.Read(a)!.Value),
        OperandKind.Column => range.Column!.Where(v => v is not null).Select(v => v!.Value),
        OperandKind.Array => range.Array!.NonBlank(),
        _ => throw new ArgumentException("Not a range.", nameof(range)),
    };

    public Value? Scalar(Node node) => ScalarOf(Operand(node));

    private Value Negate(char op, Value? value)
    {
        var operand = ToNumber(value, out var error);
        if (error is { } e) return Value.FromError(e);
        return Value.FromNumber(op == '-' ? -operand : operand);
    }

    private Value Percent(Value? value)
    {
        var operand = ToNumber(value, out var error);
        return error is { } e ? Value.FromError(e) : Number(operand / 100);
    }

    /// <summary>A binary operator over its operands: one Value from two, or element by element when either holds several (ADR-0125).</summary>
    private Operand Binary(BinaryNode node)
    {
        var left = Operand(node.Left);
        var right = Operand(node.Right);
        if (IsMulti(left) || IsMulti(right)) return Elementwise(node.Operator, left, right);
        return Formulas.Operand.Of(BinaryValues(node.Operator, ScalarOf(left), ScalarOf(right), finalOperation: false));
    }

    private Value BinaryValues(string op, Value? left, Value? right, bool finalOperation)
    {
        switch (op)
        {
            case "&":
                if (left is { IsError: true } le) return le;
                if (right is { IsError: true } re) return re;
                return Value.FromText(ToText(left) + ToText(right));
            case "=" or "<>" or "<" or ">" or "<=" or ">=":
                if (left is { IsError: true } cle) return cle;
                if (right is { IsError: true } cre) return cre;
                var order = Compare(left, right, Arithmetic.ApproximatelyEqual);
                return Value.FromBoolean(op switch
                {
                    "=" => order == 0,
                    "<>" => order != 0,
                    "<" => order < 0,
                    ">" => order > 0,
                    "<=" => order <= 0,
                    _ => order >= 0,
                });
        }

        var a = ToNumber(left, out var leftError);
        if (leftError is { } ae) return Value.FromError(ae);
        var b = ToNumber(right, out var rightError);
        if (rightError is { } be) return Value.FromError(be);
        switch (op)
        {
            case "+": return Number(finalOperation ? Arithmetic.FinalAdd(a, b) : a + b);
            case "-": return Number(finalOperation ? Arithmetic.FinalAdd(a, -b) : a - b);
            case "*": return Number(a * b);
            case "/": return b == 0 ? Value.FromError(ErrorValue.Div0) : Number(a / b);
            case "^":
                if (a == 0 && b == 0) return Value.FromError(ErrorValue.Num);
                if (a == 0 && b < 0) return Value.FromError(ErrorValue.Div0);
                return Arithmetic.Power(a, b) is { } power ? Number(power) : Value.FromError(ErrorValue.Num);
            default: throw new InvalidOperationException($"No operator {op}.");
        }
    }

    /// <summary>A result that is not a finite double is <c>#NUM!</c>, as in Excel.</summary>
    internal static Value Number(double result) =>
        double.IsFinite(result) ? Value.FromNumber(result) : Value.FromError(ErrorValue.Num);

    /// <summary>
    /// Excel's comparison: a blank is the empty value of the other side's kind (0, "" or FALSE);
    /// numbers sort before text, and text before booleans; numbers compare exactly; text compares
    /// as Excel collates it, without regard to case (<see cref="TextOrder"/>).
    /// </summary>
    internal static int Compare(Value? left, Value? right) => Compare(left, right, null);

    /// <summary>
    /// <see cref="Compare(Value?, Value?)"/>, except that two numbers for which
    /// <paramref name="equalNumbers"/> holds compare equal: the comparison operators compare at
    /// Excel's precision (<see cref="Arithmetic.ApproximatelyEqual"/>).
    /// </summary>
    internal static int Compare(Value? left, Value? right, Func<double, double, bool>? equalNumbers)
    {
        if (left is null && right is null) return 0;
        var a = left ?? EmptyOf(right!.Value.Kind);
        var b = right ?? EmptyOf(a.Kind);
        if (a.Kind != b.Kind) return Rank(a.Kind).CompareTo(Rank(b.Kind));
        return a.Kind switch
        {
            ValueKind.Number => equalNumbers is not null && equalNumbers(a.Number, b.Number) ? 0 : a.Number.CompareTo(b.Number),
            ValueKind.Text => TextOrder.Compare(a.Text, b.Text),
            _ => a.Boolean.CompareTo(b.Boolean),
        };

        static int Rank(ValueKind kind) => kind switch
        {
            ValueKind.Number => 0,
            ValueKind.Text => 1,
            _ => 2,
        };

        static Value EmptyOf(ValueKind kind) => kind switch
        {
            ValueKind.Text => Value.FromText(""),
            ValueKind.Boolean => Value.FromBoolean(false),
            _ => Value.FromNumber(0),
        };
    }

    /// <summary>
    /// Excel's coercion of an operand to a number: a blank is 0, a boolean 1 or 0, text that reads
    /// as a typed number would under the Sheet's culture is that number, other text is <c>#VALUE!</c>.
    /// </summary>
    public double ToNumber(Value? value, out ErrorValue? error)
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
                if (ConstantParser.TryParseNumber(v.Text, Culture, out var parsed)) return parsed;
                error = ErrorValue.Value;
                return 0;
        }
    }

    /// <summary>Excel's coercion to text: a blank is empty, a number is written as Excel writes one into text (<see cref="NumberText.Written"/>) under the Sheet's culture.</summary>
    public string ToText(Value? value) => value switch
    {
        null => "",
        { Kind: ValueKind.Text } v => v.Text,
        { Kind: ValueKind.Number } v => NumberText.Written(v.Number, Culture),
        { Kind: ValueKind.Boolean } v => v.Boolean ? "TRUE" : "FALSE",
        { } v => v.Error.ToText(),
    };
}
