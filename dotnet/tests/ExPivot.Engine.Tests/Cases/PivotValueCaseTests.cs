using System.Globalization;
using System.Text.Json.Nodes;
using ExPivot.Engine;
using Xunit;

namespace ExPivot.Engine.Tests.Cases;

/// <summary>
/// The cases of what a report is made of (<c>tests/cases/pivot</c>; ADR-0370 sections 2 and 5): the words
/// (kind <c>pivotWords</c>), each id with its English, its Japanese and its English (United Kingdom) word;
/// General (kind <c>pivotGeneral</c>), the text a number with no format of its own is shown as under a
/// locale; and an Item's key (kind <c>pivotItemKey</c>), the key a value is the Item of, a written key
/// accepted or refused, and whether two keys name one Item. The Script Core's runner is
/// <c>packages/pivot-engine/test/values.test.ts</c>.
/// </summary>
public class PivotValueCaseTests
{
    public static TheoryData<string, string> Words() => PivotTheories.Of("pivotWords");

    public static TheoryData<string, string> General() => PivotTheories.Of("pivotGeneral");

    public static TheoryData<string, string> ItemKeys() => PivotTheories.Of("pivotItemKey");

    [Theory, MemberData(nameof(Words))] // ADR-0370, ADR-0060: the words are one table on both engines
    public void Word(string file, string name) => Hold(file, name, (_, _) => WordTables());

    [Theory, MemberData(nameof(General))] // ADR-0370, ADR-0060: General is one text on both engines
    public void GeneralText(string file, string name) => Hold(file, name, (document, entry) =>
    {
        var culture = Culture((string)PivotCases.Inherited(document, entry, "locale")!);
        return new JsonArray([.. PivotCases.Must(entry, "numbers").AsArray().Select(n => (JsonNode)JsonValue.Create(PivotCases.Value(n) switch
        {
            decimal exact => PivotGeneral.Text(exact, culture),
            double number => PivotGeneral.Text(number, culture),
            var other => throw new InvalidOperationException($"{n!.ToJsonString()} is not a number a General case writes ({other?.GetType().Name})."),
        }))]);
    });

    [Theory, MemberData(nameof(ItemKeys))] // ADR-0370, ADR-0060: an Item's key is one key on both engines
    public void ItemKey(string file, string name) => Hold(file, name, (_, entry) =>
    {
        if (entry["of"] is { } values)
            return new JsonArray([.. values.AsArray().Select(v => PivotCases.ItemKey(PivotItemKey.For(PivotCases.Value(v))))]);
        if (entry["named"] is { } named)
        {
            return new JsonArray([.. named.AsArray().Select(written =>
            {
                try
                {
                    var key = written!.AsObject();
                    return PivotCases.ItemKey(new PivotItemKey(PivotCases.Member<PivotItemKind>((string)key["kind"]!), (string?)key["value"]));
                }
                catch (ArgumentException thrown)
                {
                    return PivotCases.Refusal(thrown);
                }
            })]);
        }
        return new JsonArray([.. PivotCases.Must(entry, "pairs").AsArray().Select(pair =>
            (JsonNode)JsonValue.Create(PivotCases.ItemKeyOf(pair![0]!).Equals(PivotCases.ItemKeyOf(pair[1]!))))]);
    });

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

    // Every id, a date part's after the rest, with its word in each edition the engine bundles.
    private static JsonObject WordTables()
    {
        var words = new JsonObject();
        foreach (var id in PivotWords.Ids.Concat(PivotDateWords.Ids))
        {
            words[id] = new JsonObject
            {
                ["english"] = PivotWords.EnglishFor(id) ?? PivotDateWords.EnglishFor(id),
                ["japanese"] = PivotWords.JapaneseFor(id),
                ["unitedKingdom"] = PivotWords.EnglishUnitedKingdomFor(id),
            };
        }
        return new JsonObject { ["words"] = words };
    }

    private static CultureInfo Culture(string locale)
        => locale == "invariant" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(locale);
}
