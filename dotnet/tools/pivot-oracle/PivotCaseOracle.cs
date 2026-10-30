using System.Text.Json.Nodes;
using ExPivot.Engine.Tests.Cases;

namespace ExPivot.Oracle;

/// <summary>
/// A pivot case's answer on the C# engine, as the shared cases' runner gives it (<c>tests/cases/README.md</c>):
/// <c>{"op": "case", "kind": "pivotSource", "file": {…}, "case": {…}}</c>, the file's keys a case inherits
/// (its locale and words) and the case itself, its data set written inline. A differential run sends random
/// cases here and holds the Script Core's runner to the answer.
/// </summary>
internal static class PivotCaseOracle
{
    public static JsonNode Answer(JsonObject request)
    {
        var file = request["file"]!.AsObject();
        var entry = request["case"]!.AsObject();
        return (string)request["kind"]! switch
        {
            "pivotSource" => PivotSourceCases.Answer(file, entry, CancellationToken.None).GetAwaiter().GetResult(),
            var kind => throw new NotSupportedException($"No case kind '{kind}' is answered here."),
        };
    }
}
