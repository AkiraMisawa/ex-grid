using System.Text.Json.Nodes;
using ExPivot.Engine;
using Xunit;

namespace ExPivot.Engine.Tests.Cases;

/// <summary>
/// The layout cases (<c>tests/cases/pivot</c>; ADR-0370 sections 2 and 5). Kind <c>pivotLayoutEdits</c>:
/// a layout and the Field List gestures and Layout menu choices made on it (ADR-0061), each answered by the
/// layout it makes, <c>same</c> where it hands back the layout it was given, the refusal of an edit that
/// may be refused, or what it throws; a step may also read the Layout menu's current and enabled choices
/// (<c>menu</c>) and the report its layout lays out over the case's data (<c>report</c>). A step makes its
/// edit on the case's layout, or on the step before's with <c>"on": "previous"</c>. Kind
/// <c>pivotLayoutJson</c>: a saved layout read and written again (ADR-0059), or what refuses it. The
/// Script Core's runner is <c>packages/pivot-engine/test/layout.test.ts</c>.
/// </summary>
public class PivotLayoutCaseTests
{
    public static TheoryData<string, string> Edits() => PivotTheories.Of("pivotLayoutEdits");

    public static TheoryData<string, string> Json() => PivotTheories.Of("pivotLayoutJson");

    [Theory, MemberData(nameof(Edits))] // ADR-0370, ADR-0061: a gesture means one layout on both engines
    public void Edit(string file, string name) => Hold(file, name, EditAnswer);

    [Theory, MemberData(nameof(Json))] // ADR-0370, ADR-0059: a saved layout reads alike on both engines
    public void LayoutJson(string file, string name) => Hold(file, name, (_, entry) =>
    {
        try
        {
            return JsonValue.Create(PivotLayoutJson.Write(PivotLayoutJson.Read((string)PivotCases.Must(entry, "read")!)));
        }
        catch (Exception thrown) when (thrown is FormatException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            return PivotCases.Refusal(thrown);
        }
    });

    private static JsonNode EditAnswer(JsonObject document, JsonObject entry)
    {
        var data = PivotCases.Data(document, entry);
        var infos = data.Fields.ToDictionary(f => f.Name, f => f.Info, StringComparer.Ordinal);
        var options = PivotCases.Options(document, entry);
        var start = PivotCases.Layout(PivotCases.Must(entry, "layout"));
        var previous = start;
        var steps = new JsonArray();
        foreach (var node in PivotCases.Must(entry, "edits").AsArray())
        {
            var step = node!.AsObject();
            var on = (string?)step["on"] == "previous" ? previous : start;
            var answer = new JsonObject();
            try
            {
                var (made, result) = Apply(on, step, infos, options);
                if (result is { IsRefused: true })
                {
                    answer["refused"] = PivotCases.Name(result.Refusal!.Value);
                    answer["word"] = PivotWords.EnglishFor(result.RefusalWord!);
                }
                else if (ReferenceEquals(made, on))
                {
                    answer["same"] = true;
                }
                else
                {
                    answer["layout"] = PivotCases.LayoutJson(made);
                }
                if (step["menu"] is not null)
                {
                    var choices = Enum.GetValues<PivotLayoutChoice>();
                    answer["chosen"] = new JsonArray([.. choices.Where(c => PivotLayoutEdits.IsChosen(made, c)).Select(c => JsonValue.Create(PivotCases.Name(c)))]);
                    answer["enabled"] = new JsonArray([.. choices.Where(c => PivotLayoutEdits.Changes(made, c)).Select(c => JsonValue.Create(PivotCases.Name(c)))]);
                }
                if (step["report"] is not null)
                {
                    var report = PivotEngine.Compute(data.Records, data.Fields, made, options);
                    answer["report"] = PivotReadings.Read(report, ["captions"], data);
                }
                previous = made;
            }
            catch (Exception thrown) when (thrown is ArgumentException or InvalidOperationException)
            {
                answer = PivotCases.Refusal(thrown);
            }
            steps.Add(answer);
        }
        return steps;
    }

    // An edit as a step writes it: its gesture's name, the field or entry it is made on, and its arguments.
    private static (PivotLayout Made, PivotEditResult? Result) Apply(
        PivotLayout layout, JsonObject step, IReadOnlyDictionary<string, PivotFieldInfo> infos, PivotOptions options)
    {
        PivotFieldInfo Info(JsonNode? field) => infos[(string)field!];
        PivotArea Area(string key) => PivotCases.Member<PivotArea>((string)PivotCases.Must(step, key)!);
        int Index(string key) => (int)PivotCases.Must(step, key);
        PivotEntry Entry(JsonNode written) => written is JsonValue
            ? PivotEntry.ValuesPseudoField(layout.ValuesAxis)
            : new PivotEntry(PivotCases.Member<PivotArea>((string)written["area"]!), (int)written["index"]!);
        if (step["tick"] is { } tick)
            return (PivotLayoutEdits.Tick(layout, Info(tick)), null);
        if (step["untick"] is { } untick)
            return (PivotLayoutEdits.Untick(layout, (string)untick!), null);
        if (step["place"] is { } place)
            return (PivotLayoutEdits.Place(layout, Info(place), Area("area"), Index("index")), null);
        if (step["move"] is { } move)
            return (PivotLayoutEdits.Move(layout, Entry(move), Area("to"), Index("index"), step["field"] is { } f ? Info(f) : null), null);
        if (step["remove"] is { } remove)
            return (PivotLayoutEdits.Remove(layout, Entry(remove)), null);
        if (step["setHiddenItems"] is { } hide)
        {
            var result = PivotLayoutEdits.SetHiddenItems(layout, (string)hide!,
                PivotCases.Must(step, "hidden").AsArray().Select(k => PivotCases.ItemKeyOf(k!)),
                [.. PivotCases.Must(step, "items").AsArray().Select(k => PivotCases.ItemKeyOf(k!))]);
            return (result.Layout, result);
        }
        if (step["setSort"] is { } sort)
        {
            var sorted = PivotCases.Layout(new JsonObject { ["rows"] = new JsonArray(new JsonObject { ["field"] = "f", ["sort"] = PivotCases.Must(step, "sort").DeepClone() }) });
            return (PivotLayoutEdits.SetSort(layout, (string)sort!, sorted.Rows[0].Sort), null);
        }
        if (step["setSubtotals"] is { } subtotals)
            return (PivotLayoutEdits.SetSubtotals(layout, (string)subtotals!, (bool)PivotCases.Must(step, "subtotals")), null);
        if (step["setCollapsed"] is { } collapse)
            return (PivotLayoutEdits.SetCollapsed(layout, (string)collapse!, PivotCases.ItemKeyOf(PivotCases.Must(step, "item")), (bool)PivotCases.Must(step, "collapsed")), null);
        if (step["setFieldCollapsed"] is { } field)
            return (PivotLayoutEdits.SetFieldCollapsed(layout, (string)field!, (bool)PivotCases.Must(step, "collapsed")), null);
        if (step["setValueField"] is { } index)
        {
            var value = PivotCases.Layout(new JsonObject { ["values"] = new JsonArray(PivotCases.Must(step, "value").DeepClone()) }).Values[0];
            var result = PivotLayoutEdits.SetValueField(layout, (int)index, value, infos, options);
            return (result.Layout, result);
        }
        if (step["choose"] is { } choice)
            return (PivotLayoutEdits.Choose(layout, PivotCases.Member<PivotLayoutChoice>((string)choice!)), null);
        throw new InvalidOperationException($"{step.ToJsonString()} names no edit the cases make.");
    }

    // Holds the case to the engine's answer, or writes the answer into it.
    private static void Hold(string file, string name, Func<JsonObject, JsonObject, JsonNode> answer)
    {
        var (document, entry) = PivotCases.Find(file, name);
        var answered = answer(document, entry);
        if (PivotCases.Writing)
        {
            PivotCases.Write(file, name, "expect", answered);
            return;
        }
        Assert.Equal(PivotCases.Must(entry, "expect").ToJsonString(), answered.ToJsonString());
    }
}
