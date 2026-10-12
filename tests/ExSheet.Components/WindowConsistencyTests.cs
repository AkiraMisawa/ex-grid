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

    [Fact] // SH-8, ADR-0047 / ADR-0048: an undo of one operation of several steps (a Number Format and the widening it brought) paints every row of it at once
    public async Task SH8_no_painted_window_mixes_rows_before_and_after_an_undo_of_several_steps()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        sheet.Enter(CellAddress.Parse("A1"), "1234567.5");
        sheet.Enter(CellAddress.Parse("A3"), "7654321.5");
        var cut = RenderSheet(ps => ps.Add(s => s.Document, sheet.ToDocument()));
        await GoToAsync(cut, "A1:A3");
        await PressAsync(cut, "!", ctrl: true, shift: true);
        Assert.Equal("1,234,567.50", CellText(cut, "A1"));
        Assert.Equal("7,654,321.50", CellText(cut, "A3"));

        var frames = new List<(string A1, string A3)>();
        cut.OnMarkupUpdated += (_, _) => frames.Add((CellText(cut, "A1"), CellText(cut, "A3")));
        Assert.True(await cut.Instance.UndoAsync());

        Assert.NotEmpty(frames);
        // Both rows formatted, or both not: never one row of the operation undone beside one that is not.
        Assert.All(frames, frame => Assert.Equal(frame.A1.Contains(','), frame.A3.Contains(',')));
        Assert.DoesNotContain(",", frames[^1].A1);
        Assert.DoesNotContain(",", frames[^1].A3);
    }

    [Fact] // SH-8, ADR-0047 / ADR-0124: a change of zone is two engine steps (the day, then the volatile Formulas); every painted picture shows the second step's rows whole
    public async Task SH8_no_painted_window_mixes_rows_when_a_change_of_zone_moves_now_on()
    {
        var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
        sheet.Enter(CellAddress.Parse("C2"), "=TEXT(NOW(),\"hh:mm\")");
        sheet.Enter(CellAddress.Parse("D6"), "=C2&\"!\"");
        // 10:00 in London and 11:00 in Paris, on the same day: the day step changes nothing.
        Clock.SetUtcNow(new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero));
        var cut = RenderSheet(ps => ps
            .Add(s => s.Document, sheet.ToDocument())
            .Add(s => s.TimeZone, TimeZoneInfo.FindSystemTimeZoneById("Europe/London")));
        Assert.Equal("10:00!", CellText(cut, "D6"));

        var frames = new List<(string C2, string D6)>();
        cut.OnMarkupUpdated += (_, _) => frames.Add((CellText(cut, "C2"), CellText(cut, "D6")));
        cut.Render(ps => ps.Add(s => s.TimeZone, TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris")));
        await cut.InvokeAsync(() => { });

        cut.WaitForAssertion(() => Assert.Equal("11:00", CellText(cut, "C2")));
        Assert.NotEmpty(frames);
        Assert.All(frames, frame => Assert.Equal(frame.C2 + "!", frame.D6));
        Assert.Equal("11:00!", CellText(cut, "D6"));
    }
}
