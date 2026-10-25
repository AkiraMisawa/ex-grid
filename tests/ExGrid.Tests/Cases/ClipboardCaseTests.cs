using System.Text.Json;
using ExGrid.Clipboard;
using ExGrid.Selection;
using Xunit;
using FillDecision = ExGrid.Clipboard.FillDecision;

namespace ExGrid.Tests.Cases;

/// <summary>
/// The clipboard rules' shared cases (<c>tests/cases/clipboard</c>, kind <c>clipboard</c>; ADR-0350
/// section 5), run against the C# core: a selection made by the selection cases' gestures, then a copy, a
/// paste or a fill planned on it, and what the plan is or why it is refused.
/// </summary>
public class ClipboardCaseTests
{
    private const string Area = "clipboard";

    public static TheoryData<string, string> Cases() => SharedCases.Of(Area, "clipboard");

    [Theory, MemberData(nameof(Cases))] // ADR-0350: a clipboard case holds on the C# core
    public void Clipboard(string file, string name)
    {
        var (document, entry) = SharedCases.Find(Area, file, name);
        var extent = SharedCases.Extent(entry.TryGetProperty("extent", out var own) ? own : document.GetProperty("extent"));
        var selection = GridSelection.Empty;
        var number = 0;
        foreach (var step in entry.GetProperty("steps").EnumerateArray())
        {
            number++;
            if (!Plans(selection, step, extent, $"step {number}"))
                selection = SelectionCaseTests.Apply(selection, step, extent);
        }
    }

    private static string Camel(string name) => JsonNamingPolicy.CamelCase.ConvertName(name);

    /// <summary>The Editable declaration a step names: every column, every column but those listed, or none.</summary>
    private static Func<int, bool> EditableFrom(JsonElement options)
    {
        if (!options.TryGetProperty("notEditable", out var locked))
            return _ => true;
        if (locked.ValueKind == JsonValueKind.String)
        {
            return locked.GetString() == "every"
                ? _ => false
                : throw new InvalidOperationException($"\"notEditable\" is a list of columns or \"every\", not {locked}.");
        }
        var columns = locked.EnumerateArray().Select(c => c.GetInt32()).ToHashSet();
        return column => !columns.Contains(column);
    }

    private static bool Plans(GridSelection selection, JsonElement step, GridExtent extent, string label)
    {
        Func<object> plan;
        if (step.TryGetProperty("planCopy", out var copy))
        {
            var cap = copy.TryGetProperty("cellCap", out var c) ? c.GetInt64() : ClipboardRules.DefaultCellCap;
            var headers = copy.TryGetProperty("withHeaders", out var h) && h.GetBoolean();
            plan = () => ClipboardRules.PlanCopy(selection, cap, headers);
        }
        else if (step.TryGetProperty("planPaste", out var paste))
        {
            var source = paste.GetProperty("source");
            var spill = paste.TryGetProperty("spill", out var s) && s.GetBoolean() ? extent : (GridExtent?)null;
            var plainText = paste.TryGetProperty("plainTextOnly", out var p) && p.GetBoolean();
            plan = () => ClipboardRules.PlanPaste(
                selection, new PasteShape(source[0].GetInt32(), source[1].GetInt32()), EditableFrom(paste), spill, plainText);
        }
        else if (step.TryGetProperty("planFill", out var fill))
        {
            var direction = Enum.Parse<GridDirection>(fill.GetProperty("direction").GetString()!, ignoreCase: true);
            var cap = fill.TryGetProperty("cellCap", out var c) ? c.GetInt64() : ClipboardRules.DefaultCellCap;
            plan = () => ClipboardRules.PlanFill(selection, direction, EditableFrom(fill), cap);
        }
        else
        {
            return false;
        }

        if (step.TryGetProperty("throws", out var refusal))
        {
            SharedCases.Refused(() => plan(), refusal.GetString()!);
            return true;
        }
        var decision = plan();
        (bool Refused, string? Reason, int Column) answer = decision switch
        {
            CopyDecision d => (d.IsRefused, d.IsRefused ? Camel(d.Reason.ToString()) : null, -1),
            PasteDecision d => (d.IsRefused, d.IsRefused ? Camel(d.Reason.ToString()) : null, d.RefusedColumn),
            FillDecision d => (d.IsRefused, d.IsRefused ? Camel(d.Reason.ToString()) : null, d.RefusedColumn),
            _ => throw new InvalidOperationException("Not a decision."),
        };
        var (refused, reason, column) = answer;
        if (step.TryGetProperty("refused", out var expectedReason))
        {
            Assert.Equal(expectedReason.GetString(), refused ? reason : "approved");
            if (step.TryGetProperty("column", out var expectedColumn))
                Assert.Equal(expectedColumn.GetInt32(), column);
            return true;
        }
        if (!step.TryGetProperty("plan", out var expected))
            throw new InvalidOperationException($"{label} says neither \"refused\", \"plan\" nor \"throws\".");
        Assert.False(refused, $"{label}: expected a plan, refused as {reason}.");
        switch (decision)
        {
            case CopyDecision d:
                HoldsCopy(d.Plan, expected, label);
                break;
            case PasteDecision d:
                HoldsPaste(d.Plan, expected, label);
                break;
            case FillDecision d:
                HoldsFill(d.Plan, expected, label);
                break;
        }
        return true;
    }

    private static void HoldsCopy(CopyPlan plan, JsonElement expected, string label)
    {
        foreach (var key in expected.EnumerateObject())
        {
            switch (key.Name)
            {
                case "segments": Assert.Equal(SharedCases.Ranges(key.Value), plan.Segments); break;
                case "orientation": Assert.Equal(key.Value.GetString(), Camel(plan.Orientation.ToString())); break;
                case "totalRows": Assert.Equal(key.Value.GetInt32(), plan.TotalRows); break;
                case "totalColumns": Assert.Equal(key.Value.GetInt32(), plan.TotalColumns); break;
                default: throw new InvalidOperationException($"{label}: \"{key.Name}\" is not a key of a copy plan this runner knows.");
            }
        }
    }

    private static void HoldsFill(FillPlan plan, JsonElement expected, string label)
    {
        foreach (var key in expected.EnumerateObject())
        {
            switch (key.Name)
            {
                case "source": Assert.Equal(SharedCases.Range(key.Value), plan.Source); break;
                case "shape": Assert.Equal(new PasteShape(key.Value[0].GetInt32(), key.Value[1].GetInt32()), plan.Paste.Source); break;
                default: HoldsPasteKey(plan.Paste, key, label); break;
            }
        }
    }

    private static void HoldsPaste(PastePlan plan, JsonElement expected, string label)
    {
        foreach (var key in expected.EnumerateObject())
            HoldsPasteKey(plan, key, label);
    }

    private static void HoldsPasteKey(PastePlan plan, JsonProperty key, string label)
    {
        switch (key.Name)
        {
            case "targets": Assert.Equal(SharedCases.Ranges(key.Value), plan.Targets); break;
            case "source": Assert.Equal(new PasteShape(key.Value[0].GetInt32(), key.Value[1].GetInt32()), plan.Source); break;
            case "collapses": Assert.Equal(key.Value.GetBoolean(), plan.CollapsesSelection); break;
            case "sourceCells":
                foreach (var pair in key.Value.EnumerateArray())
                {
                    var block = pair[1];
                    Assert.Equal(new SourceCell(block[0].GetInt32(), block[1].GetInt32()), plan.SourceCellFor(SharedCases.Cell(pair[0])));
                }
                break;
            case "outside":
                foreach (var at in key.Value.EnumerateArray())
                    SharedCases.Refused(() => plan.SourceCellFor(SharedCases.Cell(at)), "outOfRange");
                break;
            default: throw new InvalidOperationException($"{label}: \"{key.Name}\" is not a key of a paste plan this runner knows.");
        }
    }
}
