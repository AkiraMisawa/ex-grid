using ExSheet.Engine.Formulas;

namespace ExSheet.Engine;

/// <summary>
/// A function of the declared set (ADR-0047): each gives Excel's result, and a name outside the
/// set is <c>#NAME?</c>. What completion offers and what the argument hint shows (ADR-0051).
/// </summary>
/// <param name="Name">The name in upper case, such as <c>SUM</c>.</param>
/// <param name="Arguments">The argument list as Excel documents it, optional ones in brackets: <c>number1, [number2], ...</c>.</param>
/// <param name="Description">One sentence saying what it returns.</param>
public sealed record DeclaredFunction(string Name, string Arguments, string Description)
{
    /// <summary>Every function the engine declares, in alphabetical order.</summary>
    public static IReadOnlyList<DeclaredFunction> All { get; } =
        [.. FunctionLibrary.All.Select(f => new DeclaredFunction(f.Name, f.Arguments, f.Description))];

    /// <summary>The signature a hint shows: <c>SUM(number1, [number2], ...)</c>.</summary>
    public string Signature => $"{Name}({Arguments})";

    /// <summary>The declared function of that name, in any case, or <see langword="null"/> when the name is not declared.</summary>
    public static DeclaredFunction? Find(string name) =>
        All.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The values argument <paramref name="index"/> takes when it takes one of a fixed list, in
    /// Excel's order and with Excel's texts, such as <c>XLOOKUP</c>'s <c>match_mode</c>: what
    /// completion lists there (ADR-0058). Empty for an argument that takes any value, and for a
    /// name that is not declared.
    /// </summary>
    /// <param name="index">The argument's position, from 0.</param>
    public IReadOnlyList<ArgumentValue> ValuesOf(int index) =>
        FunctionLibrary.Find(Name) is { } function && function.Values.TryGetValue(index, out var values) ? values : [];
}

/// <summary>
/// One of the values an argument takes from a fixed list (ADR-0058), such as <c>XLOOKUP</c>'s
/// <c>match_mode</c> <c>-1</c>: what choosing it writes, and the text Excel lists it by.
/// </summary>
/// <param name="Value">What choosing it writes into the Formula: <c>-1</c>.</param>
/// <param name="Text">The text the list shows, Excel's: <c>-1 - Exact match or next smaller item</c>.</param>
public sealed record ArgumentValue(string Value, string Text);
