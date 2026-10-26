using System.Globalization;
using System.Text.Json.Nodes;
using ExSheet.Engine.Formulas;
using Xunit;

namespace ExSheet.Engine.Tests.Cases;

/// <summary>
/// The shared cases of kind <c>portableMath</c> (<c>tests/cases/sheet/portable-math.json</c>): the platform's math
/// functions at random arguments over each one's range and at its edges, each the exact value rounded once, from
/// mpmath at 400 bits. Vitest holds the Script Core's portable-math.ts to the same table.
/// </summary>
public class PortableMathCaseTests
{
    private static readonly Dictionary<string, Func<double[], double>> Functions = new(StringComparer.Ordinal)
    {
        ["atan"] = a => PortableMath.Atan(a[0]),
        ["asin"] = a => PortableMath.Asin(a[0]),
        ["acos"] = a => PortableMath.Acos(a[0]),
        ["atan2"] = a => PortableMath.Atan2(a[0], a[1]),
        ["sinh"] = a => PortableMath.Sinh(a[0]),
        ["cosh"] = a => PortableMath.Cosh(a[0]),
        ["tanh"] = a => PortableMath.Tanh(a[0]),
        ["asinh"] = a => PortableMath.Asinh(a[0]),
        ["acosh"] = a => PortableMath.Acosh(a[0]),
        ["atanh"] = a => PortableMath.Atanh(a[0]),
        ["log10"] = a => PortableMath.Log10(a[0]),
        ["powWhole"] = a => PortableMath.PowWhole(a[0], a[1]),
    };

    private static double FromBits(JsonNode? hex) =>
        BitConverter.Int64BitsToDouble(long.Parse((string)hex!, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));

    [Fact] // ADR-0047, ADR-0370: the same double on every platform, and the correctly rounded one
    public void Each_function_answers_its_exact_value_rounded_once()
    {
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(SheetCases.Root, "portable-math.json")))!;
        Assert.Equal("portableMath", (string?)document["kind"]);
        var wrong = new List<string>();
        var cases = document["cases"]!.AsArray();
        foreach (var entry in cases)
        {
            var name = (string)entry![0]!;
            var function = Functions.TryGetValue(name, out var found) ? found : throw new InvalidDataException($"No function {name}.");
            var given = entry[1]!.AsArray().Select(FromBits).ToArray();
            var answer = function(given);
            var expected = FromBits(entry[2]);
            if (BitConverter.DoubleToInt64Bits(answer) != BitConverter.DoubleToInt64Bits(expected))
                wrong.Add($"{name}({string.Join(", ", given.Select(g => g.ToString("R", CultureInfo.InvariantCulture)))}) is {answer.ToString("R", CultureInfo.InvariantCulture)}, not {expected.ToString("R", CultureInfo.InvariantCulture)}");
        }
        Assert.NotEmpty(cases);
        Assert.Empty(wrong);
    }
}
