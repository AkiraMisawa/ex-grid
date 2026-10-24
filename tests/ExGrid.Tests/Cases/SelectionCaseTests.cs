using System.Text.Json;
using ExGrid.Selection;
using Xunit;

namespace ExGrid.Tests.Cases;

/// <summary>
/// The selection's shared cases (<c>tests/cases/selection</c>, ADR-0350 section 5), run against the C#
/// core. Each case's name starts with the ADR it holds to.
/// </summary>
public class SelectionCaseTests
{
    private const string Area = "selection";

    public static TheoryData<string, string> SelectionCases() => SharedCases.Of(Area, "selection");

    public static TheoryData<string, string> RangeCases() => SharedCases.Of(Area, "range");

    public static TheoryData<string, string> EdgeBandCases() => SharedCases.Of(Area, "edgeBand");

    public static TheoryData<string, string> HeldCases() => SharedCases.Of(Area, "held");

    [Theory, MemberData(nameof(SelectionCases))] // ADR-0350: a selection case holds on the C# core
    public void Selection(string file, string name)
    {
        var (document, entry) = SharedCases.Find(Area, file, name);
        var extent = SharedCases.Extent(entry.TryGetProperty("extent", out var own) ? own : document.GetProperty("extent"));
        var saved = new Dictionary<string, GridSelection>();
        var selection = GridSelection.Empty;
        var before = selection;
        foreach (var step in entry.GetProperty("steps").EnumerateArray())
        {
            if (step.TryGetProperty("expect", out var expected))
            {
                Holds(selection, expected, before, saved, extent);
            }
            else if (step.TryGetProperty("save", out var save))
            {
                saved[save.GetString()!] = selection;
            }
            else if (step.TryGetProperty("restore", out var restore))
            {
                selection = saved[restore.GetString()!];
            }
            else
            {
                var stepExtent = step.TryGetProperty("extent", out var pair) ? SharedCases.Extent(pair) : extent;
                before = selection;
                if (step.TryGetProperty("throws", out var refusal))
                {
                    var from = selection;
                    SharedCases.Refused(() => Apply(from, step, stepExtent), refusal.GetString()!);
                }
                else
                {
                    selection = Apply(selection, step, stepExtent);
                }
            }
        }
    }

    private static GridSelection Apply(GridSelection selection, JsonElement step, GridExtent extent)
    {
        if (step.TryGetProperty("cycle", out var key))
        {
            var (order, backward) = key.GetString() switch
            {
                "enter" => (CycleOrder.ColumnMajor, false),
                "shift+enter" => (CycleOrder.ColumnMajor, true),
                "tab" => (CycleOrder.RowMajor, false),
                "shift+tab" => (CycleOrder.RowMajor, true),
                var other => throw new InvalidOperationException($"\"{other}\" is not a key that cycles."),
            };
            return Presses(selection, step, s => s.CycleFocus(order, backward, extent));
        }
        if (step.TryGetProperty("nextCorner", out _))
            return Presses(selection, step, s => s.MoveFocusToNextCorner(extent));
        return Gesture(selection, step, extent);
    }

    /// <summary>A key pressed <c>times</c> times, or once for each Focus in <c>focuses</c>, which it is
    /// held to.</summary>
    private static GridSelection Presses(GridSelection selection, JsonElement step, Func<GridSelection, GridSelection> press)
    {
        var focuses = step.TryGetProperty("focuses", out var list) ? list.EnumerateArray().ToArray() : null;
        var times = step.TryGetProperty("times", out var count) ? count.GetInt32() : focuses?.Length ?? 1;
        for (var i = 0; i < times; i++)
        {
            selection = press(selection);
            if (focuses is not null && i < focuses.Length)
                Assert.Equal(SharedCases.Cell(focuses[i]), selection.Focus);
        }
        return selection;
    }

    private static GridSelection Gesture(GridSelection selection, JsonElement step, GridExtent extent)
    {
        int Int(string property) => step.GetProperty(property).GetInt32();
        CellPosition At(string property) => SharedCases.Cell(step.GetProperty(property));
        Func<CellPosition, GridDirection, CellPosition>? answer = step.TryGetProperty("answer", out var answered)
            ? (_, _) => SharedCases.Cell(answered)
            : null;
        GridDirection Direction(string property) => step.GetProperty(property).GetString() switch
        {
            "up" => GridDirection.Up,
            "down" => GridDirection.Down,
            "left" => GridDirection.Left,
            "right" => GridDirection.Right,
            var other => throw new InvalidOperationException($"\"{other}\" is not a direction."),
        };

        foreach (var property in step.EnumerateObject())
        {
            switch (property.Name)
            {
                case "click": return selection.Click(At("click"), extent);
                case "extendTo": return selection.ExtendTo(At("extendTo"), extent);
                case "toggleRange": return selection.ToggleRange(At("toggleRange"), extent);
                case "toggleColumn": return selection.ToggleColumn(Int("toggleColumn"), extent, Int("focusRow"));
                case "toggleRow": return selection.ToggleRow(Int("toggleRow"), extent, Int("focusColumn"));
                case "move": return selection.Move(Direction("move"), extent);
                case "extend": return selection.Extend(Direction("extend"), extent);
                case "moveToEdge": return selection.MoveToEdge(Direction("moveToEdge"), extent, answer);
                case "extendToEdge": return selection.ExtendToEdge(Direction("extendToEdge"), extent, answer);
                case "moveByViewport": return selection.MoveByViewport(Int("moveByViewport"), extent);
                case "extendByViewport": return selection.ExtendByViewport(Int("extendByViewport"), extent);
                case "selectAll":
                    return property.Value.ValueKind == JsonValueKind.True
                        ? selection.SelectAll(extent)
                        : selection.SelectAll(extent, property.Value.GetProperty("firstRow").GetInt32(), property.Value.GetProperty("rowCount").GetInt32());
                case "selectWholeColumns": return selection.SelectWholeColumns(extent);
                case "selectWholeRows": return selection.SelectWholeRows(extent);
                case "collapseToFocus": return selection.CollapseToFocus(extent);
                case "focusOn": return selection.FocusOn(At("focusOn"));
                case "extendToColumn": return selection.ExtendToColumn(Int("extendToColumn"), extent, Int("focusRowIfEmpty"));
                case "extendToRow": return selection.ExtendToRow(Int("extendToRow"), extent, Int("focusColumnIfEmpty"));
                case "place": return selection.Place(SharedCases.Range(step.GetProperty("place")), At("focus"), extent);
                case "selectColumn": return selection.SelectColumn(Int("selectColumn"), extent, Int("focusRow"));
                case "selectRow": return selection.SelectRow(Int("selectRow"), extent, Int("focusColumn"));
            }
        }
        throw new InvalidOperationException($"The step {step} names no gesture this runner knows.");
    }

    private static void Holds(
        GridSelection selection, JsonElement expected, GridSelection before, Dictionary<string, GridSelection> saved, GridExtent extent)
    {
        foreach (var property in expected.EnumerateObject())
        {
            var value = property.Value;
            switch (property.Name)
            {
                case "empty":
                    Assert.Equal(value.GetBoolean(), selection.IsEmpty);
                    break;
                case "ranges":
                    Assert.Equal(SharedCases.Ranges(value), selection.Ranges);
                    break;
                case "rangeCount":
                    Assert.Equal(value.GetInt32(), selection.Ranges.Count);
                    break;
                case "rangeAt":
                    Assert.Equal(SharedCases.Range(value[1]), selection.Ranges[value[0].GetInt32()]);
                    break;
                case "includesRange":
                    Assert.Contains(SharedCases.Range(value), selection.Ranges);
                    break;
                case "focus":
                    Nullable(value, () => selection.Focus, SharedCases.Cell);
                    break;
                case "extent":
                    Nullable(value, () => selection.Extent, SharedCases.Cell);
                    break;
                case "focusRange":
                    Nullable(value, () => selection.FocusRange, SharedCases.Range);
                    break;
                case "cellCount":
                    Assert.Equal(value.GetInt64(), selection.CellCount);
                    break;
                case "contains":
                    foreach (var cell in value.EnumerateArray())
                        Assert.True(selection.Contains(SharedCases.Cell(cell)), $"contains {cell}");
                    break;
                case "notContains":
                    foreach (var cell in value.EnumerateArray())
                        Assert.False(selection.Contains(SharedCases.Cell(cell)), $"contains {cell}");
                    break;
                case "coversColumn":
                    foreach (var pair in value.EnumerateArray())
                        Assert.Equal(pair[1].GetBoolean(), selection.CoversColumn(pair[0].GetInt32(), extent));
                    break;
                case "coversRow":
                    foreach (var pair in value.EnumerateArray())
                        Assert.Equal(pair[1].GetBoolean(), selection.CoversRow(pair[0].GetInt32(), extent));
                    break;
                case "wholeColumns":
                    Assert.Equal(value.EnumerateArray().Select(c => c.GetInt32()), selection.WholeColumns(extent));
                    break;
                case "topLeftOfRangeMadeLast":
                    Nullable(value, () => selection.TopLeftOfRangeMadeLast, SharedCases.Cell);
                    break;
                case "sameAs":
                    Assert.Equal(saved[value.GetString()!], selection);
                    break;
                case "differsFrom":
                    Assert.NotEqual(saved[value.GetString()!], selection);
                    break;
                case "unchanged":
                    Assert.Equal(value.GetBoolean(), before.Equals(selection));
                    break;
                default:
                    throw new InvalidOperationException($"The expectation \"{property.Name}\" is not one this runner knows.");
            }
        }
    }

    /// <summary>A value that is <c>null</c> in the cases where C# refuses to answer: nothing selected has
    /// no Focus, and asking for one throws (ADR-0012).</summary>
    private static void Nullable<T>(JsonElement value, Func<T> actual, Func<JsonElement, T> read)
    {
        if (value.ValueKind == JsonValueKind.Null)
            Assert.Throws<InvalidOperationException>(() => actual());
        else
            Assert.Equal(read(value), actual());
    }

    [Theory, MemberData(nameof(RangeCases))] // ADR-0350: a range case holds on the C# core
    public void Range(string file, string name)
    {
        var (_, entry) = SharedCases.Find(Area, file, name);
        if (entry.TryGetProperty("fromCorners", out var corners))
        {
            Assert.Equal(SharedCases.Range(entry.GetProperty("expect")),
                SelectionRange.FromCorners(SharedCases.Cell(corners[0]), SharedCases.Cell(corners[1])));
        }
        else if (entry.TryGetProperty("create", out var create))
        {
            SelectionRange Make() => new(create[0].GetInt32(), create[1].GetInt32(), create[2].GetInt32(), create[3].GetInt32());
            if (entry.TryGetProperty("throws", out var refusal))
                SharedCases.Refused(() => Make(), refusal.GetString()!);
            else
                Assert.Equal(SharedCases.Range(entry.GetProperty("expect")), Make());
        }
        else if (entry.TryGetProperty("contains", out var contains))
        {
            var range = SharedCases.Range(contains.GetProperty("range"));
            foreach (var pair in contains.GetProperty("cells").EnumerateArray())
                Assert.Equal(pair[1].GetBoolean(), range.Contains(SharedCases.Cell(pair[0])));
        }
        else if (entry.TryGetProperty("cellCount", out var counted))
        {
            Assert.Equal(entry.GetProperty("expect").GetInt64(), SharedCases.Range(counted).CellCount);
        }
        else if (entry.TryGetProperty("subtract", out var subtract))
        {
            Assert.Equal(SharedCases.Ranges(entry.GetProperty("expect")),
                SharedCases.Range(subtract.GetProperty("range")).Subtract(SharedCases.Cell(subtract.GetProperty("cell"))));
        }
        else if (entry.TryGetProperty("subtractArea", out var area))
        {
            Assert.Equal(SharedCases.Ranges(entry.GetProperty("expect")),
                SharedCases.Range(area.GetProperty("range")).SubtractArea(SharedCases.Range(area.GetProperty("area"))));
        }
        else
        {
            throw new InvalidOperationException($"The case \"{name}\" names nothing this runner knows.");
        }
    }

    [Theory, MemberData(nameof(EdgeBandCases))] // ADR-0350: an edge band case holds on the C# core
    public void EdgeBandRate(string file, string name)
    {
        var (_, entry) = SharedCases.Find(Area, file, name);
        Assert.Equal(
            entry.GetProperty("expect").GetInt32(),
            EdgeBand.RowsPerTick(
                entry.GetProperty("positionPx").GetDouble(),
                entry.GetProperty("lengthPx").GetDouble(),
                entry.GetProperty("rowsPerViewport").GetInt32()));
    }

    [Theory, MemberData(nameof(HeldCases))] // ADR-0350: a held selection case holds on the C# core
    public void Held(string file, string name)
    {
        var (document, entry) = SharedCases.Find(Area, file, name);
        var extent = SharedCases.Extent(document.GetProperty("extent"));
        var held = HeldSelection.Empty(entry.GetProperty("version").GetInt32());
        var before = held;
        foreach (var step in entry.GetProperty("steps").EnumerateArray())
        {
            if (step.TryGetProperty("click", out var cell))
            {
                before = held;
                held = held.With(held.Selection.Click(SharedCases.Cell(cell), extent));
            }
            else if (step.TryGetProperty("under", out var version))
            {
                before = held;
                held = held.Under(version.GetInt32());
            }
            else
            {
                var expected = step.GetProperty("expect");
                if (expected.TryGetProperty("version", out var v))
                    Assert.Equal(v.GetInt32(), held.RowSequenceVersion);
                if (expected.TryGetProperty("empty", out var empty))
                    Assert.Equal(empty.GetBoolean(), held.Selection.IsEmpty);
                if (expected.TryGetProperty("sameAsBefore", out var same))
                    Assert.Equal(same.GetBoolean(), held == before);
            }
        }
    }
}
