using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

public class RecalculationTests
{
    [Fact] // ADR-0047 (SH-8): only the dependents of a change recompute, counted
    public void Only_dependents_of_a_change_recompute()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("B1", "=A1*2");
        sheet.Enter("C1", "=B1+1");
        sheet.Enter("D1", "5");
        sheet.Enter("E1", "=D1*2");
        sheet.Enter("F1", "=E1+C1");
        sheet.Enter("G1", "=D1");

        var change = sheet.Enter("A1", "2");

        Assert.Equal(["B1", "C1", "F1"], change.Recalculated.Addresses());
        Assert.Equal(15, sheet.Number("F1"));
    }

    [Fact] // ADR-0047 (SH-8): a change to a cell nothing reads recomputes nothing
    public void A_change_nothing_reads_recomputes_nothing()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("B1", "=A1*2");

        var change = sheet.Enter("Z9", "7");

        Assert.Empty(change.Recalculated);
    }

    [Fact] // ADR-0047 (SH-8): entering a Formula recomputes it and what reads it, not its precedents
    public void Entering_a_formula_recomputes_it_and_its_dependents()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=1+1");
        sheet.Enter("A2", "=A3*2");
        sheet.Enter("B1", "=A1");

        var change = sheet.Enter("A3", "=A1+1");

        Assert.Equal(["A2", "A3"], change.Recalculated.Addresses());
        Assert.Equal(6, sheet.Number("A2"));
    }

    [Fact] // ADR-0047: a diamond recomputes each cell once, after all of its precedents
    public void A_diamond_recomputes_each_cell_once_in_order()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("B1", "=A1+1");
        sheet.Enter("B2", "=A1*10");
        sheet.Enter("C1", "=B1+B2");

        var change = sheet.Enter("A1", "2");

        Assert.Equal(["B1", "C1", "B2"], change.Recalculated.Addresses());
        Assert.Equal(23, sheet.Number("C1"));
    }

    [Fact] // ADR-0047: a long chain recomputes in dependency order without running out of stack
    public void A_long_chain_recomputes()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter(Enumerable.Range(2, 50_000).Select(r => new KeyValuePair<CellAddress, string>(new CellAddress(r - 1, 0), $"=A{r - 1}+1")));

        var change = sheet.Enter("A1", "2");

        Assert.Equal(50_002, sheet.Number("A50001"));
        Assert.Equal(50_000, change.Recalculated.Count);
    }

    [Fact] // ADR-0047 (SH-8): Values are published only from a completed recalculation
    public void Values_are_published_all_at_once()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("B1", "=A1+1");
        sheet.Enter("C1", "=B1+1");

        // A batch is one change and one recalculation: every Value it reports is from the end
        // state, and none from a state between its parts.
        var change = sheet.Enter(
        [
            new(CellAddress.Parse("A1"), "10"),
            new(CellAddress.Parse("B1"), "=A1*2"),
        ]);

        Assert.Equal(["A1", "B1", "C1"], change.ValueChanges.Addresses());
        Assert.Equal(21, sheet.Number("C1"));
        Assert.Equal(["B1", "C1"], change.Recalculated.Addresses());
    }

    [Fact] // ADR-0048: opening a document computes every Value again, cycles included
    public void Opening_a_document_recomputes_everything()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=B1");
        sheet.Enter("B1", "=A1");
        sheet.Enter("C1", "2");
        sheet.Enter("D1", "=C1*C1");

        var reopened = Sheet.Open(sheet.ToDocument());

        Assert.Equal(ErrorValue.Circ, reopened.Error("A1"));
        Assert.Equal(ErrorValue.Circ, reopened.Error("B1"));
        Assert.Equal(4, reopened.Number("D1"));
    }
}
