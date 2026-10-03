using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// ADR-0124: a Formula that calls a volatile function is recalculated in every recalculation, a
/// Reference computed while it is evaluated is read as it stands after the recalculation, and
/// <c>NOW</c> answers the moment the Sheet's host gives it.
/// </summary>
public class VolatileTests
{
    private static Sheet WithColumnA(params double[] values)
    {
        var sheet = NewSheet();
        for (var i = 0; i < values.Length; i++) sheet.Enter($"A{i + 1}", values[i].ToString(System.Globalization.CultureInfo.InvariantCulture));
        return sheet;
    }

    [Fact] // ADR-0124: OFFSET reads a cell its text does not name, and follows a change to that cell
    public void Offset_follows_a_cell_its_text_does_not_name()
    {
        var sheet = WithColumnA(1, 2, 3, 4, 5, 6);
        sheet.Enter("C1", "5");
        sheet.Enter("B1", "=OFFSET(A1,C1,0)");
        Assert.Equal(6, sheet.Number("B1"));

        sheet.Enter("A6", "60");

        Assert.Equal(60, sheet.Number("B1"));
    }

    [Fact] // ADR-0124: a cell the same recalculation has still to compute is computed first, whatever the order
    public void Offset_reads_a_cell_computed_in_the_same_recalculation()
    {
        var sheet = NewSheet();
        sheet.Enter("D1", "1");
        sheet.Enter("A6", "=D1*2");
        // B1 sorts before A6 in no order the graph knows: its text names A1, not A6.
        sheet.Enter("B1", "=OFFSET(A1,5,0)+0");
        Assert.Equal(2, sheet.Number("B1"));

        sheet.Enter("D1", "10");

        Assert.Equal(20, sheet.Number("A6"));
        Assert.Equal(20, sheet.Number("B1"));
    }

    [Fact] // ADR-0124 / ADR-0047: a Formula that reaches itself through a computed Reference is #CIRC!
    public void A_computed_reference_to_itself_is_circ()
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "=OFFSET(B1,0,0)+1");

        Assert.Equal(ErrorValue.Circ, sheet.Error("B1"));
    }

    [Fact] // ADR-0124: OFFSET gives SUM a range whose size follows the data
    public void Offset_sizes_a_range_for_sum()
    {
        var sheet = WithColumnA(1, 2, 3, 4, 5);
        sheet.Enter("C1", "3");
        sheet.Enter("B1", "=SUM(OFFSET(A1,0,0,C1,1))");
        Assert.Equal(6, sheet.Number("B1"));

        sheet.Enter("C1", "5");

        Assert.Equal(15, sheet.Number("B1"));
    }

    [Fact] // ADR-0124: NOW answers the moment its host gives, read once per recalculation, and moves on with any change
    public void Now_answers_the_moment_it_is_given()
    {
        var moment = new DateTime(2026, 10, 3, 18, 0, 0);
        var sheet = NewSheet();
        sheet.NowSource = () => moment;
        sheet.Enter("A1", "=NOW()");
        Assert.Equal(46298.75, sheet.Number("A1"));

        moment = moment.AddHours(3);
        sheet.Enter("Z1", "1");

        Assert.Equal(46298.875, sheet.Number("A1"));
    }

    [Fact] // ADR-0124: moving the clock on recalculates the volatile Formulas and what reads them, and nothing else
    public void Recalculating_the_volatile_formulas_reaches_their_readers_only()
    {
        var moment = new DateTime(2026, 10, 3, 18, 0, 0);
        var sheet = NewSheet();
        sheet.NowSource = () => moment;
        sheet.Enter("A1", "=NOW()");
        sheet.Enter("B1", "=A1+1");
        sheet.Enter("C1", "=2*3");
        Assert.True(sheet.HasVolatileFormulas);

        moment = moment.AddMinutes(1);
        var change = sheet.RecalculateVolatile();

        Assert.Equal(["A1", "B1"], change.Recalculated.Addresses());
    }

    [Fact] // ADR-0124: without a moment, NOW waits, and a Formula that calls it waits whichever branch it takes
    public void Now_waits_without_a_moment()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=NOW()");
        sheet.Enter("A2", "=IF(TRUE,1,NOW())");

        Assert.Equal(ErrorValue.GettingData, sheet.Error("A1"));
        Assert.Equal(ErrorValue.GettingData, sheet.Error("A2"));
    }

    [Fact] // ADR-0124: a Sheet with no volatile Formula is not recalculated by the clock
    public void A_sheet_without_volatile_formulas_recalculates_nothing()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=1+1");

        Assert.False(sheet.HasVolatileFormulas);
        Assert.Empty(sheet.RecalculateVolatile().Recalculated);
    }
}
