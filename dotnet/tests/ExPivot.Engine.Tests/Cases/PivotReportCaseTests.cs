using System.Globalization;
using System.Text.Json.Nodes;
using ExPivot.Engine;
using Xunit;

namespace ExPivot.Engine.Tests.Cases;

/// <summary>
/// The report cases (<c>tests/cases/pivot</c>, kind <c>pivotReport</c>; ADR-0370 sections 2 and 5): a
/// layout over a data set, laid out by <see cref="PivotEngine.Compute{TRecord}"/> under the case's locale
/// and words, and the report read back as <see cref="Pivot"/> reads one — its leaf headers, a line a row,
/// its Header Group spans — or the refusal it meets. <c>also</c> asks for more of it: <c>names</c>, the
/// columns' names; <c>keys</c>, the rows' keys; <c>numbers</c>, the numbers behind each label and value; <c>captions</c>, the Value Fields'
/// captions; <c>empty</c>, whether it is the empty report; <c>toggles</c>, each label's button; <c>columns</c>,
/// each value column's role and Value Field; <c>tiers</c>, the Header Group tiers; <c>keyCalls</c>, the
/// values the Order Keys were called with. A case may also lay its report out from the cube of another
/// layout (<c>cubeOf</c>), and ask whether that cube holds other layouts (<c>holds</c>), what Filter… lists
/// for a field (<c>itemsOf</c>), which records stand behind a cell (<c>behind</c>, by row and value column)
/// and whether another report has the same rows (<c>sameRowsAs</c>, other records or another layout).
/// The Script Core's runner is <c>packages/pivot-engine/test/report.test.ts</c>.
/// </summary>
public class PivotReportCaseTests
{
    public static TheoryData<string, string> Cases() => PivotTheories.Of("pivotReport");

    [Theory, MemberData(nameof(Cases))] // ADR-0370: a report case holds on the C# engine
    public void Report(string file, string name)
    {
        var (document, entry) = PivotCases.Find(file, name);
        var answer = Answer(document, entry);
        if (PivotCases.Writing)
        {
            var key = answer["refusal"] is null ? "expect" : "throws";
            PivotCases.Write(file, name, key, answer);
            return;
        }
        if (entry["throws"] is { } refusal)
        {
            Assert.Equal(refusal.ToJsonString(), answer.ToJsonString());
            return;
        }
        Assert.Equal(PivotCases.Must(entry, "expect").ToJsonString(), answer.ToJsonString());
    }

    /// <summary>The C# engine's answer to a case: the report as the case reads it, or the refusal.</summary>
    internal static JsonObject Answer(JsonObject document, JsonObject entry)
    {
        var options = PivotCases.Options(document, entry);
        PivotCases.DataSet data;
        PivotCube cube;
        PivotLayout layout;
        PivotReport report;
        try
        {
            data = PivotCases.Data(document, entry);
            layout = PivotCases.Layout(PivotCases.Must(entry, "layout"));
            cube = PivotEngine.Aggregate(data.Records, data.Fields, entry["cubeOf"] is { } cubeOf ? PivotCases.Layout(cubeOf) : layout);
            report = PivotEngine.Report(cube, layout, options);
        }
        catch (Exception thrown) when (thrown is ArgumentException or InvalidOperationException or FormatException or NotSupportedException
            or ExGrid.Data.SnapshotException)
        {
            return PivotCases.Refusal(thrown);
        }
        var read = PivotReadings.Read(report, entry["also"]?.AsArray().Select(a => (string)a!).ToArray() ?? [], data);
        if (entry["holds"] is { } holds)
            read["holds"] = new JsonArray([.. holds.AsArray().Select(l => JsonValue.Create(cube.Holds(PivotCases.Layout(l!))))]);
        if (entry["itemsOf"] is { } itemsOf)
        {
            var items = new JsonObject();
            foreach (var field in itemsOf.AsArray().Select(f => (string)f!))
                items[field] = Attempt(() => new JsonArray([.. PivotEngine.ItemsOf(cube, layout, field, options).Select(Item)]));
            read["items"] = items;
        }
        if (entry["behind"] is { } behind)
        {
            read["behind"] = new JsonArray([.. behind.AsArray().Select(cell =>
            {
                var (row, column) = ((int)cell![0]!, (int)cell[1]!);
                var records = PivotEngine.RecordsBehind(data.Records, data.Fields, report, report.Rows[row], column);
                return (JsonNode)new JsonArray([.. records.Select(r => JsonValue.Create(Array.IndexOf(data.Records, r)))]);
            })]);
        }
        if (entry["sameRowsAs"] is { } others)
        {
            read["sameRows"] = new JsonArray([.. others.AsArray().Select(other =>
            {
                var given = other!.AsObject();
                var records = data.Records;
                if (given["records"] is { } written)
                {
                    var refreshed = entry.DeepClone().AsObject();
                    refreshed["records"] = written.DeepClone();
                    records = PivotCases.Data(document, refreshed).Records;
                }
                var otherLayout = given["layout"] is { } l ? PivotCases.Layout(l) : layout;
                return JsonValue.Create(report.HasSameRowsAs(PivotEngine.Compute(records, data.Fields, otherLayout, options)));
            })]);
        }
        return read;
    }

    // An Item as Filter… lists it: its key, its label and whether it is hidden.
    private static JsonObject Item(PivotItemInfo item) => new()
    {
        ["key"] = PivotCases.ItemKey(item.Key),
        ["label"] = item.Label,
        ["isHidden"] = item.IsHidden,
    };

    // A reading, or the refusal it meets.
    private static JsonNode Attempt(Func<JsonNode> read)
    {
        try
        {
            return read();
        }
        catch (Exception thrown) when (thrown is ArgumentException or InvalidOperationException)
        {
            return PivotCases.Refusal(thrown);
        }
    }
}
