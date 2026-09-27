namespace ExSheet.Engine.Formulas;

/// <summary>The declared set of functions (ADR-0047). A name outside it is <c>#NAME?</c>.</summary>
internal static partial class FunctionLibrary
{
    private static readonly Dictionary<string, FunctionDefinition> ByName =
        Declare().ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<FunctionDefinition> All => ByName.Values.OrderBy(f => f.Name, StringComparer.Ordinal);

    public static FunctionDefinition? Find(string name) => ByName.GetValueOrDefault(name);

    private static partial IEnumerable<FunctionDefinition> Declare();
}
