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

    /// <summary>Whether a Linked Table of this name is declared, the name read without regard to case (ADR-0049).</summary>
    bool IsLinkedTable(string name);

    /// <summary>The Sheet Day <c>TODAY</c> answers (ADR-0121), or <see langword="null"/> while it is not known.</summary>
    DateOnly? Today { get; }

    /// <summary>The moment <c>NOW</c> answers, in the Sheet's zone, read once for the recalculation (ADR-0124); <see langword="null"/> while it is not known.</summary>
    DateTime? Now { get; }

    /// <summary>
    /// The cells of the Sheet a Reference's qualifier names, as this recalculation reads them: this
    /// Sheet's for no qualifier, another Sheet's of the Workbook for its name, without regard to case;
    /// <see langword="null"/> for a name no Sheet of the Workbook has, which is <c>#REF!</c> (ADR-0331).
    /// </summary>
    ICellReader? Resolve(string? sheetName);

    /// <summary>
    /// The draw of the <c>RAND</c> or <c>RANDBETWEEN</c> call at <paramref name="place"/> in the
    /// Formula of <paramref name="cell"/> (ADR-0180), from the Sheet Seed and the count of changes;
    /// <see langword="null"/> while the Sheet has no seed.
    /// </summary>
    ulong? Draw(CellAddress cell, int place);

    /// <summary>
    /// <c>A1#</c> (ADR-0125): the array the Anchor at <paramref name="anchor"/> spills, as this
    /// recalculation leaves it; <c>#REF!</c> when it does not spill.
    /// </summary>
    Operand Spill(CellAddress anchor);

    /// <summary>
    /// The Formula whose result a cell shows: its own, or for a cell of a Spill Range its Anchor's
    /// (ADR-0125); <see langword="null"/> for a constant or a blank. What <c>SUBTOTAL</c> and
    /// <c>AGGREGATE</c> read to tell a nested call (ADR-0047, 2026-10-10).
    /// </summary>
    Node? FormulaAt(CellAddress address);
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
    /// final addition's rule applied as in <see cref="Evaluate"/>. A Formula a call or an operator
    /// refused (<see cref="Refuse"/>) is the refusal, <c>#VALUE!</c>, whatever the rest of it made of it
    /// (<see cref="Value.Refusal"/>).
    /// </summary>
    public Operand EvaluateResult(Node node)
    {
        // A Formula computed on demand while another is evaluated has its own refusal; the outer
        // Formula's is put back after it.
        var outerRefusal = _refusal;
        _refusal = null;
        try
        {
            var result = Result(node);
            return _refusal is { } refusal ? Formulas.Operand.Of(refusal) : result;
        }
        finally
        {
            _refusal = outerRefusal;
        }
    }

    /// <summary>
    /// The refusal of the Formula being evaluated, or <see langword="null"/>: <see cref="Value.Refusal"/> once a
    /// function or an operator has refused it (<see cref="Refuse"/>), else <see cref="Value.RefusedName"/> once it
    /// has called a function Excel has and ExSheet does not declare, or named a Linked Table bare
    /// (<see cref="RefuseName"/>).
    /// </summary>
    private Value? _refusal;

    /// <summary>
    /// Refuses the Formula being evaluated: its result is the refusal, <c>#VALUE!</c>
    /// (<see cref="Value.Refusal"/>), whatever the functions and operators around it make of the
    /// Error Value returned here, so <c>IFERROR</c> cannot catch it, <c>ISBLANK</c> cannot read it as
    /// not blank and <c>COUNTA</c> cannot count it as one Value, as a Formula that meets <c>#CIRC!</c> is
    /// <c>#CIRC!</c> (ADR-0047). The cell's Value carries the refusal on: a Formula that reads it is
    /// refused in turn, whatever it does with it (2026-10-12, decided with the user: a refusal is never
    /// caught, anywhere). Every function and operator refuses so, and never with a plain <c>#VALUE!</c>,
    /// where the readings of its evaluation part or no run has settled the answer: where it is given an
    /// array and wants one Value, which it is not yet applied to each Value of (ADR-0125, "Not yet:
    /// functions lifted over arrays"; IFERROR-009); where it would read a kept blank, and no run has
    /// settled how (ADR-0125, <see cref="FunctionCall.Operand"/>); and at every argument value "admitted
    /// with some argument values refused" keeps out (ADR-0047). A <c>#VALUE!</c> that is Excel's own
    /// answer, such as <c>=1+"a"</c>'s, is an Error Value like any other.
    /// </summary>
    public Operand Refuse()
    {
        _refusal = Value.Refusal;
        return Formulas.Operand.Of(Value.Refusal);
    }

    /// <summary>
    /// Refuses the Formula being evaluated with <c>#NAME?</c> (<see cref="Value.RefusedName"/>): a call of a
    /// function Excel has and ExSheet does not declare (<see cref="ExcelFunctionNames"/>), or a declared Linked
    /// Table's bare name, which Excel reads as the table's data (TABLE-024). The cell shows the <c>#NAME?</c> it
    /// showed before, but no Formula catches it, in its own cell or in any cell that reads it (2026-10-13,
    /// decided with the user): Excel answers these, and an ordinary <c>#NAME?</c> that <c>IFERROR</c> caught
    /// would answer otherwise. A refusal by <see cref="Refuse"/> in the same Formula comes before it.
    /// </summary>
    public Operand RefuseName()
    {
        _refusal ??= Value.RefusedName;
        return Formulas.Operand.Of(Value.RefusedName);
    }

    /// <summary>
    /// <see cref="Refuse"/>, for a function that answers with a Value: the Formula is refused, and the
    /// refusal is returned to stand where the Value would.
    /// </summary>
    public Value Refused()
    {
        _refusal = Value.Refusal;
        return Value.Refusal;
    }

    private Operand Result(Node node)
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
                var cells = operand.Cells!;
                foreach (var address in cells.NonBlankIn(area)) array[address.Row - area.Row1, address.Column - area.Column1] = cells.Read(address);
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
        // A Reference reads the Sheet its qualifier names; a name no Sheet of the Workbook has names nothing (ADR-0046, ADR-0331).
        ReferenceNode r => Cells.Resolve(r.Reference.SheetName) is not { } sheet ? Formulas.Operand.Of(ErrorValue.Ref)
            : r.Reference.Spilled ? sheet.Spill(new CellAddress(r.Reference.Row1, r.Reference.Column1))
            : Formulas.Operand.Of(r.Reference.Area, sheet),
        StructuredReferenceNode s => Cells.TableColumn(s.Table, s.Column),
        NameNode { Binding: { } binding } => Read(binding),
        NameNode or IntersectionNode => NamesLinkedTable(node) ? RefuseName() : Formulas.Operand.Of(ErrorValue.Name),
        LetNode l => Let(l),
        ParenthesesNode p => Operand(p.Inner),
        UnaryNode u => Map(Operand(u.Operand), value => Negate(u.Operator, value)),
        PercentNode p => Map(Operand(p.Operand), Percent),
        BinaryNode b => Binary(b),
        FunctionNode f => f.Function is null ? ExcelFunctionNames.Undeclared(f.Name) ? RefuseName() : Formulas.Operand.Of(ErrorValue.Name)
            : f.Function.Invoke(new FunctionCall(this, f.Arguments) { Place = f.Place }),
        _ => throw new InvalidOperationException($"No evaluation for {node.GetType().Name}."),
    };

    /// <summary>Whether an unbound name, or a side of an intersection, is a declared Linked Table's bare name (ADR-0049).</summary>
    private bool NamesLinkedTable(Node node) => node switch
    {
        NameNode { Binding: null } name => Cells.IsLinkedTable(name.Name),
        IntersectionNode i => NamesLinkedTable(i.Left) || NamesLinkedTable(i.Right),
        ParenthesesNode p => NamesLinkedTable(p.Inner),
        _ => false,
    };

    /// <summary>
    /// An operand used as one Value: a single cell is read; a rectangle of more than one cell refuses
    /// the Formula (<see cref="Refused"/>), because Excel 365 applies what wants one Value to each Value
    /// of it, which ExSheet does not yet (ADR-0125), and ExSheet will not pick one cell of it silently.
    /// An empty argument is blank.
    /// </summary>
    public Value? ScalarOf(Operand operand) => operand.Kind switch
    {
        OperandKind.Scalar => operand.Scalar,
        OperandKind.Area when operand.Area.IsSingleCell => operand.Cells!.Read(new CellAddress(operand.Area.Row1, operand.Area.Column1)),
        OperandKind.Area => Refused(),
        OperandKind.Column when operand.Column!.Count == 1 => operand.Column[0],
        OperandKind.Column => Refused(),
        OperandKind.Array => operand.Array!.IsSingle ? operand.Array[0, 0] : Refused(),
        _ => null,
    };

    /// <summary>The Values of a range that are not blank, in order: row-major for cells, top to bottom for a Linked Table's column.</summary>
    public IEnumerable<Value> RangeValues(Operand range) => range.Kind switch
    {
        OperandKind.Area => range.Cells!.NonBlankIn(range.Area).Select(a => range.Cells.Read(a)!.Value),
        OperandKind.Column => range.Column!.Where(v => v is not null).Select(v => v!.Value),
        OperandKind.Array => range.Array!.NonBlank(),
        _ => throw new ArgumentException("Not a range.", nameof(range)),
    };

    public Value? Scalar(Node node) => ScalarOf(Operand(node));

    /// <summary>The <c>LET</c> bindings in scope while their <c>LET</c> is evaluated, each computed when first read (ADR-0181).</summary>
    private readonly Dictionary<LetBinding, BindingSlot> _bindings = [];

    /// <summary>A binding's one Value, once it has been read.</summary>
    private sealed class BindingSlot
    {
        public Operand? Value { get; set; }
    }

    /// <summary>
    /// <c>LET</c> (ADR-0181): its calculation, with its names in scope. A binding is computed when its
    /// name is first read and at most once, every read giving that one Value; one never read is never
    /// computed, so its Error Value does not reach the result.
    /// </summary>
    private Operand Let(LetNode let)
    {
        var outer = new BindingSlot?[let.Bindings.Count];
        for (var i = 0; i < outer.Length; i++)
        {
            var binding = let.Bindings[i];
            outer[i] = _bindings.GetValueOrDefault(binding);
            _bindings[binding] = new BindingSlot();
        }
        try
        {
            return Operand(let.Calculation);
        }
        finally
        {
            for (var i = 0; i < outer.Length; i++)
            {
                if (outer[i] is { } slot) _bindings[let.Bindings[i]] = slot;
                else _bindings.Remove(let.Bindings[i]);
            }
        }
    }

    /// <summary>
    /// A name's binding: its Value, computed now if this is its first read. The parser resolves a name
    /// only where its binding is in scope, so a binding is never read outside its <c>LET</c> nor
    /// inside its own value; were it, it would be <c>#NAME?</c>, as an unbound name is.
    /// </summary>
    private Operand Read(LetBinding binding)
    {
        if (!_bindings.TryGetValue(binding, out var slot)) return Formulas.Operand.Of(ErrorValue.Name);
        if (slot.Value is { } value) return value;
        _bindings.Remove(binding);
        try
        {
            value = Operand(binding.Value);
        }
        finally
        {
            _bindings[binding] = slot;
        }
        slot.Value = value;
        return value;
    }

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
