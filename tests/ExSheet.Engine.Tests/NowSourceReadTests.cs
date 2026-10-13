using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// SH-59, ADR-0124: the engine reads the moment from <see cref="Sheet.NowSource"/> once per
/// recalculation, so every <c>NOW</c> in one recalculation answers the same moment. The source here
/// counts its calls and moves on a second at each one, so a second read in the same recalculation
/// shows both in the count and in the Values.
/// </summary>
public class NowSourceReadTests
{
    private sealed class MovingClock
    {
        private DateTime _moment = new(2026, 10, 3, 18, 0, 0);

        public int Reads { get; private set; }

        public DateTime? Read()
        {
            Reads++;
            _moment = _moment.AddSeconds(1);
            return _moment;
        }
    }

    [Fact] // SH-59, ADR-0124: every NOW in one recalculation answers the one moment read for it
    public void SH59_every_now_in_one_recalculation_answers_the_same_moment()
    {
        var clock = new MovingClock();
        var sheet = NewSheet();
        sheet.NowSource = clock.Read;
        sheet.Enter("A1", "=NOW()");
        sheet.Enter("B1", "=NOW()");
        sheet.Enter("C1", "=NOW()+0");

        var before = clock.Reads;
        sheet.Enter("Z1", "1");

        Assert.Equal(1, clock.Reads - before);
        Assert.Equal(sheet.Number("A1"), sheet.Number("B1"));
        Assert.Equal(sheet.Number("A1"), sheet.Number("C1"));
    }

    [Fact] // SH-59, ADR-0124: a recalculation of several passes (a spill laid out) still reads the clock once
    public void SH59_a_recalculation_that_lays_out_a_spill_reads_the_clock_once()
    {
        var clock = new MovingClock();
        var sheet = NewSheet();
        sheet.NowSource = clock.Read;
        sheet.Enter("A1", "=NOW()");
        sheet.Enter("B1", "=NOW()");

        var before = clock.Reads;
        sheet.Enter("D1", "=SEQUENCE(3)");

        Assert.Equal(3, sheet.Number("D3"));
        Assert.Equal(1, clock.Reads - before);
        Assert.Equal(sheet.Number("A1"), sheet.Number("B1"));
    }

    [Fact] // SH-59, ADR-0124: moving the clock on reads it once, and the moment moves with it
    public void SH59_recalculating_the_volatile_formulas_reads_the_clock_once()
    {
        var clock = new MovingClock();
        var sheet = NewSheet();
        sheet.NowSource = clock.Read;
        sheet.Enter("A1", "=NOW()");
        sheet.Enter("B1", "=NOW()");
        var first = sheet.Number("A1");

        var before = clock.Reads;
        sheet.RecalculateVolatile();

        Assert.Equal(1, clock.Reads - before);
        Assert.Equal(sheet.Number("A1"), sheet.Number("B1"));
        Assert.True(sheet.Number("A1") > first);
    }
}
