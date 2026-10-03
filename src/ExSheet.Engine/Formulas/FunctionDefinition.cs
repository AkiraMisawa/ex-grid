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

    /// <summary>Whether the arguments come in pairs, as <c>IFS</c>'s do: an odd count is refused on entry, as Excel refuses it.</summary>
    public bool InPairs { get; init; }

    private static readonly IReadOnlyDictionary<int, IReadOnlyList<ArgumentValue>> NoValues = new Dictionary<int, IReadOnlyList<ArgumentValue>>();

    public Operand Invoke(FunctionCall call) => invoke(call);
}

/// <summary>One call of a function: its argument nodes, evaluated only when the function asks (so <c>IF</c> is lazy, as in Excel).</summary>
internal sealed class FunctionCall(Evaluator evaluator, IReadOnlyList<Node> arguments)
{
    public Evaluator Evaluator { get; } = evaluator;

    public int Count => arguments.Count;

    public Operand Operand(int index) => Evaluator.Operand(arguments[index]);

    /// <summary>Whether argument <paramref name="index"/> was given and not left empty.</summary>
    public bool Has(int index) => index < arguments.Count && arguments[index] is not MissingNode;
}
