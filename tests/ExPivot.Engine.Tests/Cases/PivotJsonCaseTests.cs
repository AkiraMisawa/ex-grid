using System.Text.Json.Nodes;
using ExPivot.Engine;
using Xunit;

namespace ExPivot.Engine.Tests.Cases;

/// <summary>
/// The wire cases (<c>tests/cases/pivot</c>; ADR-0370 sections 2 and 3, ADR-0066), kind <c>pivotJson</c>:
/// a Pivot Source document read as the <c>document</c> it names and written again, which both engines write
/// as one text, or what refuses it. The Script Core's runner is
/// <c>src/npm/pivot-engine/test/pivot-json.test.ts</c>.
/// </summary>
public class PivotJsonCaseTests
{
    public static TheoryData<string, string> Documents() => PivotTheories.Of("pivotJson");

    [Theory, MemberData(nameof(Documents))] // ADR-0370, ADR-0066: a document crosses alike to either engine
    public void Document(string file, string name)
    {
        var (_, entry) = PivotCases.Find(file, name);
        var answered = Answer((string)PivotCases.Must(entry, "document")!, (string)PivotCases.Must(entry, "read")!);
        if (PivotCases.Writing)
        {
            PivotCases.Write(file, name, "expect", answered);
            return;
        }
        Assert.Equal(PivotCases.Must(entry, "expect").ToJsonString(), answered.ToJsonString());
    }

    private static JsonNode Answer(string document, string json)
    {
        try
        {
            return JsonValue.Create(document switch
            {
                "query" => PivotJson.Write(PivotJson.ReadQuery(json)),
                "answer" => PivotJson.Write(PivotJson.ReadAnswer(json)),
                "itemsQuery" => PivotJson.Write(PivotJson.ReadItemsQuery(json)),
                "itemPage" => PivotJson.Write(PivotJson.ReadItemPage(json)),
                "detailsQuery" => PivotJson.Write(PivotJson.ReadDetailsQuery(json)),
                "detailPage" => PivotJson.Write(PivotJson.ReadDetailPage(json)),
                "sourceChanged" => PivotJson.Write(PivotJson.ReadSourceChanged(json)),
                "itemCounts" => PivotJson.Write(PivotJson.ReadItemCounts(json)),
                _ => throw new InvalidOperationException($"'{document}' is no document the cases read."),
            });
        }
        catch (Exception thrown) when (thrown is FormatException or NotSupportedException or ArgumentException)
        {
            return PivotCases.Refusal(thrown);
        }
    }
}
