namespace ExSheet.Engine.Formulas;

/// <summary>A function of the declared set (ADR-0047): its name, how many arguments it takes, and how it evaluates.</summary>
internal sealed class FunctionDefinition(string name, int minimum, int maximum, string arguments, string description, Func<FunctionCall, Operand> invoke)
{
    public string Name { get; } = name;

    public int MinimumArguments { get; } = minimum;

    /// <summary>The most arguments it takes; Excel's limit of 255 for the open-ended ones.</summary>
    public int MaximumArguments { get; } = maximum;

    /// <summary>The argument list as a hint shows it, such as <c>number1, [number2], ...</c>.</summary>
    public string Arguments { get; } = arguments;

    public string Description { get; } = description;

    /// <summary>
    /// The arguments that take one of a fixed list of values, by index, each list in Excel's order
    /// with Excel's texts (ADR-0058): what completion lists at that argument. An argument absent
    /// here takes any value.
    /// </summary>
    public IReadOnlyDictionary<int, IReadOnlyList<ArgumentValue>> Values { get; init; } = NoValues;

    /// <summary>Whether arguments after <see cref="PairOffset"/> come in pairs: an incomplete pair is refused on entry.</summary>
    public bool InPairs { get; init; }

    /// <summary>Arguments before the repeating pairs: one for the result range of SUMIFS and its partners.</summary>
    public int PairOffset { get; init; }

    /// <summary>
    /// The arguments, by index from <c>First</c> to <c>Last</c>, that Excel takes only as a Reference:
    /// a Formula that gives one of them something that can never be a Reference — a constant, an
    /// operator's result, a call of a function that never returns one — is refused on entry, as Excel
    /// refuses it (ADR-0047; the seventeenth Windows run: SUBTOTAL-029, SUBTOTAL-030, RANKEQ-005,
    /// AGGREGATE-040). <see langword="null"/> for a function that takes a Value everywhere.
    /// </summary>
    public (int First, int Last)? ReferenceOnly { get; init; }

    /// <summary>
    /// Whether a call of it can give a Reference (<c>INDIRECT</c>, <c>OFFSET</c>, <c>INDEX</c>,
    /// <c>CHOOSE</c>, <c>IF</c> and their kin): such a call is taken where only a Reference is
    /// (<see cref="ReferenceOnly"/>), and what it gives is judged when it is computed.
    /// </summary>
    public bool ReturnsReference { get; init; }

    private static readonly IReadOnlyDictionary<int, IReadOnlyList<ArgumentValue>> NoValues = new Dictionary<int, IReadOnlyList<ArgumentValue>>();

    public Operand Invoke(FunctionCall call) => invoke(call);
}

/// <summary>One call of a function: its argument nodes, evaluated only when the function asks (so <c>IF</c> is lazy, as in Excel).</summary>
internal sealed class FunctionCall(Evaluator evaluator, IReadOnlyList<Node> arguments)
{
    public Evaluator Evaluator { get; } = evaluator;

    public int Count => arguments.Count;

    /// <summary>
    /// Argument <paramref name="index"/>, evaluated. One that holds a kept blank
    /// (<see cref="Formulas.Operand.KeptBlank"/>) refuses the Formula (<see cref="Evaluator.Refuse"/>):
    /// what this function reads it as is a reading no run has settled. A function takes it instead
    /// through <see cref="KeepingBlanks"/> or <see cref="OneValue"/> where the area passes read its
    /// reading (<c>COUNTA</c> over <c>SORT</c>'s and <c>UNIQUE</c>'s array; <c>INDEX</c> taking it out
    /// of <c>SORT</c>'s, which <c>ISBLANK</c> found blank, and out of <c>UNIQUE</c>'s, which <c>=</c>
    /// found equal to empty text), where it gives the same answer for every reading of it
    /// (<c>SUMPRODUCT</c>, <c>CONCAT</c>, <c>SUM</c> where both readings give the one sum, a lookup that
    /// never reads it), or where what the passes read is carried to it by generalisation, which no run
    /// has read: <c>COUNTA</c> over <c>SORTBY</c>'s, <c>FILTER</c>'s and <c>TRANSPOSE</c>'s array,
    /// <c>INDEX</c> taking it out of those three and out of <c>UNIQUE</c>'s still blank, and
    /// <c>ISBLANK</c> beyond <c>SORT</c>'s, <c>LEN</c>, <c>ISTEXT</c>, <c>MATCH</c>'s lookup value and
    /// every other one Value read through <see cref="OneValue"/>, reading the blank <c>INDEX</c> takes
    /// out as a blank cell's. What is read through this refuses it: <c>XLOOKUP</c>'s and
    /// <c>XMATCH</c>'s lookup value, <c>ROUND</c>'s arguments and <c>ERROR.TYPE</c>'s argument among
    /// them.
    /// </summary>
    public Operand Operand(int index)
    {
        var operand = Evaluator.Operand(arguments[index]);
        return operand.KeptBlank ? Evaluator.Refuse() : operand;
    }

    /// <summary>
    /// Argument <paramref name="index"/>, evaluated, a kept blank included (ADR-0125), for the functions
    /// that read it the one way: <c>COUNTA</c> counts it in the array, as Excel's counted
    /// <c>SORT</c>'s and <c>UNIQUE</c>'s (SORT-021, UNIQUE-017), and <c>SORTBY</c>'s, <c>FILTER</c>'s
    /// and <c>TRANSPOSE</c>'s as those, a generalisation no run has read; <c>INDEX</c> picks it and
    /// keeps it marked, Excel's having taken <c>SORT</c>'s out blank to <c>ISBLANK</c> (SORT-022) and
    /// <c>UNIQUE</c>'s out equal to empty text (UNIQUE-018), and the engine taking <c>UNIQUE</c>'s and the
    /// other three's out as <c>SORT</c>'s, a generalisation; and <c>SUMPRODUCT</c> and <c>CONCAT</c> add
    /// nothing for it whichever way they read it, <c>SUM</c> where both readings give the one sum, and
    /// <c>VLOOKUP</c>, <c>HLOOKUP</c> and <c>XLOOKUP</c> answer where they never read it, answers by
    /// agreement that no run read; each refuses the Formula where its readings part.
    /// </summary>
    public Operand KeepingBlanks(int index) => Evaluator.Operand(arguments[index]);

    /// <summary>
    /// Argument <paramref name="index"/>, evaluated, for a function that reads one Value of it. A kept
    /// blank given as that one Value is read as a blank cell's (ADR-0125): what Excel's <c>ISBLANK</c>
    /// read of <c>SORT</c>'s (SORT-022), taken for every function that reads one Value through this and
    /// for the kept blank of all five, a generalisation no run has read beyond it. A function that reads
    /// its one Value through <see cref="Operand(int)"/> instead refuses it. An array that holds one
    /// refuses the Formula, as any array where one Value is wanted does (<c>FunctionLibrary.Unlifted</c>).
    /// </summary>
    public Operand OneValue(int index)
    {
        var operand = Evaluator.Operand(arguments[index]);
        return operand is { KeptBlank: true, Kind: OperandKind.Array } ? Evaluator.Refuse() : operand;
    }

    /// <summary>The argument's syntax, when a function must distinguish a literal Reference from a computed one.</summary>
    public Node Argument(int index) => arguments[index];

    /// <summary>For <c>RAND</c> and <c>RANDBETWEEN</c>: which of the Formula's draws this call is, counted in its text (ADR-0180).</summary>
    public int Place { get; init; }

    /// <summary>
    /// The argument evaluated for its extent alone, as <c>ROWS</c> and <c>COLUMNS</c> read it: no Value of
    /// it is read, so a kept blank is counted in the extent, as Excel's <c>ROWS</c> counted
    /// <c>UNIQUE</c>'s (UNIQUE-011, 012), never refused.
    /// </summary>
    public Operand Measured(int index) => Evaluator.Operand(arguments[index]);

    /// <summary>Whether argument <paramref name="index"/> was given and not left empty (<see cref="MissingNode.IsEmpty"/>).</summary>
    public bool Has(int index) => index < arguments.Count && !MissingNode.IsEmpty(arguments[index]);
}
