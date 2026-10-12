using System.Globalization;
using Bunit;
using ExSheet.Components.Tests.Support;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// SH-8's layer 2 half, ADR-0047: no pushed Window carries Values from an unfinished
/// recalculation. Every picture the grid paints while a change goes through — each render batch,
/// recorded as it lands — shows a chain of dependents spread over several rows either wholly before
/// the change or wholly after it, never one row's new Value beside another row's old one.
/// </summary>
public class WindowConsistencyTests : SheetTestContext
{
    private static readonly string[] Columns = ["Id", "PV"];

    private static IReadOnlyList<Value?>[] Positions(params double[] pvs) =>
        [.. pvs.Select((pv, i) => (IReadOnlyList<Value?>)[Value.FromText($"R-{i}"), Value.FromNumber(pv)])];

    private static SheetDocument Chain()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        // A table reader, and its dependents on rows of their own.
        sheet.Enter(CellAddress.Parse("A1"), "=SUM(Positions[PV])");
        sheet.Enter(CellAddress.Parse("B3"), "=A1*2");
        sheet.Enter(CellAddress.Parse("C7"), "=B3+1");
        sheet.Enter(CellAddress.Parse("A9"), "=A1");
        // A constant, and its dependents on rows of their own.
        sheet.Enter(CellAddress.Parse("E1"), "5");
        sheet.Enter(CellAddress.Parse("F4"), "=E1*3");
        sheet.Enter(CellAddress.Parse("G8"), "=F4+E1");
        return sheet.ToDocument();
    }

    private static double Read(IRenderedComponent<Components.ExSheet> cut, string address) =>
        double.Parse(CellText(cut, address), NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <summary>Records what each render batch shows of <paramref name="addresses"/>, from now on.</summary>
    private static List<Dictionary<string, double>> Record(IRenderedComponent<Components.ExSheet> cut, params string[] addresses)
    {
        var frames = new List<Dictionary<string, double>>();
        cut.OnMarkupUpdated += (_, _) => frames.Add(addresses.ToDictionary(a => a, a => Read(cut, a)));
        return frames;
    }

    [Fact] // SH-8, ADR-0047 / ADR-0049: a Linked Table snapshot reaches a chain over four rows, and every painted picture is whole
    public async Task SH8_no_painted_window_mixes_values_before_and_after_a_snapshot()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Chain()));
        await cut.Instance.DeclareLinkedTableAsync("Positions", Columns);
        await cut.Instance.PushLinkedTableAsync("Positions", Positions(4, 6));
        Assert.Equal("21", CellText(cut, "C7"));

        var frames = Record(cut, "A1", "B3", "C7", "A9");
        await cut.Instance.PushLinkedTableAsync("Positions", Positions(400, 600));

        Assert.NotEmpty(frames);
        foreach (var frame in frames)
        {
            Assert.Equal(frame["A1"] * 2, frame["B3"]);
            Assert.Equal(frame["B3"] + 1, frame["C7"]);
            Assert.Equal(frame["A1"], frame["A9"]);
        }
        Assert.Equal(1000, frames[^1]["A1"]);
        Assert.Equal(2001, frames[^1]["C7"]);
    }

    [Fact] // SH-8, ADR-0047: an edit reaches a chain over three rows, and every painted picture is whole
    public async Task SH8_no_painted_window_mixes_values_before_and_after_an_edit()
    {
        var cut = RenderSheet(ps => ps.Add(s => s.Document, Chain()));
        Assert.Equal("20", CellText(cut, "G8"));

        var frames = Record(cut, "E1", "F4", "G8");
        await EnterAsync(cut, "E1", "7");

        Assert.NotEmpty(frames);
        foreach (var frame in frames)
        {
            Assert.Equal(frame["E1"] * 3, frame["F4"]);
            Assert.Equal(frame["F4"] + frame["E1"], frame["G8"]);
        }
        Assert.Equal(28, frames[^1]["G8"]);
    }
}
