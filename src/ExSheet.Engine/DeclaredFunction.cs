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
}
