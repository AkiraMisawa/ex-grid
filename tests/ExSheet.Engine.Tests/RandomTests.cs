using System.Globalization;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// <c>RAND</c> and <c>RANDBETWEEN</c> (ADR-0180, docs/specs/exsheet-functions ticket 15, SH-66): each
/// draw a function of the Sheet Seed, the cell, the call's place in its Formula and the count of
/// changes since the Sheet was opened. Excel's own numbers cannot be matched — its generator is not
/// documented — so these are the contract Excel documents and was seen to keep, with a fixed seed.
/// The observations with one outcome are in <c>ExcelCases/rand.json</c> and <c>randbetween.json</c>.
/// </summary>
public class RandomTests
{
    private const ulong Seed = 42;

    private static Sheet Seeded()
    {
        var sheet = NewSheet();
        sheet.SetSeed(Seed);
        return sheet;
    }

    private static double[] Column(Sheet sheet, string column, int rows) =>
        [.. Enumerable.Range(1, rows).Select(r => sheet.Number($"{column}{r}"))];

    [Fact] // ADR-0180, SH-66: every draw of RAND lies in [0,1), and they spread over it (RAND-001's 128 recalculations)
    public void ADR0180_rand_draws_in_zero_to_one()
    {
        var sheet = Seeded();
        for (var r = 1; r <= 512; r++) sheet.Enter($"A{r}", "=RAND()");

        var draws = Column(sheet, "A", 512);

        Assert.All(draws, d => Assert.InRange(d, 0, Math.BitDecrement(1.0)));
        Assert.Equal(512, draws.Distinct().Count());
        // Evenly distributed: each tenth of [0,1) holds some of 512 draws, and none holds most of them.
        Assert.All(Enumerable.Range(0, 10), tenth => Assert.InRange(draws.Count(d => (int)(d * 10) == tenth), 20, 90));
    }

    [Theory] // ADR-0180, SH-66: RANDBETWEEN draws a whole number between its bounds, both bounds drawn (RANDBETWEEN-001, 003)
    [InlineData(1, 3)]
    [InlineData(-3, -1)]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    public void ADR0180_randbetween_draws_a_whole_number_between_its_bounds(int bottom, int top)
    {
        var sheet = Seeded();
        for (var r = 1; r <= 64; r++) sheet.Enter($"A{r}", $"=RANDBETWEEN({bottom},{top})");

        var draws = Column(sheet, "A", 64);

        Assert.All(draws, d => Assert.Equal(Math.Floor(d), d));
        Assert.All(draws, d => Assert.InRange(d, bottom, top));
        Assert.Equal(Enumerable.Range(bottom, top - bottom + 1).Select(n => (double)n), draws.Distinct().Order());
    }

    [Fact] // ADR-0180, SH-66: the bounds at 2^53 are drawn exactly, past which a double no longer holds every whole number
    public void ADR0180_randbetween_draws_up_to_two_to_the_fifty_third()
    {
        var sheet = Seeded();
        for (var r = 1; r <= 32; r++) sheet.Enter($"A{r}", "=RANDBETWEEN(-2^53,2^53)");
        sheet.Enter("B1", "=RANDBETWEEN(2^53,2^53)");

        Assert.All(Column(sheet, "A", 32), d => Assert.InRange(d, -9007199254740992, 9007199254740992));
        Assert.Equal(9007199254740992, sheet.Number("B1"));
        Assert.Equal(ErrorValue.Value, sheet.Evaluate("=RANDBETWEEN(1,2^53+2)").Error);
    }

    [Fact] // ADR-0180: RANDBETWEEN reads a numeric string as its number, and a blank Reference as 0 (RANDBETWEEN-010, 011)
    public void ADR0180_randbetween_reads_numeric_text_and_a_blank()
    {
        var sheet = Seeded();
        for (var r = 1; r <= 32; r++)
        {
            sheet.Enter($"A{r}", "=RANDBETWEEN(\"1\",3)");
            sheet.Enter($"B{r}", "=RANDBETWEEN(Z1,3)");
        }

        Assert.All(Column(sheet, "A", 32), d => Assert.InRange(d, 1, 3));
        var fromBlank = Column(sheet, "B", 32);
        Assert.All(fromBlank, d => Assert.InRange(d, 0, 3));
        Assert.Contains(0, fromBlank);
    }

    [Theory] // ADR-0180: a fractional bound and a bound past 2^53 are refused; a boolean is #VALUE! as observed, in a cell as typed
    [InlineData("=RANDBETWEEN(1.5,3)")]
    [InlineData("=RANDBETWEEN(1,2.5)")]
    [InlineData("=RANDBETWEEN(1,1E+16)")]
    [InlineData("=RANDBETWEEN(-1E+16,1)")]
    [InlineData("=RANDBETWEEN(Z2,3)")]
    [InlineData("=RANDBETWEEN(1,Z2)")]
    [InlineData("=RANDBETWEEN(Z3:Z4,3)")]
    public void ADR0180_randbetween_refuses_what_it_cannot_answer(string formula)
    {
        var sheet = Seeded();
        sheet.Enter("Z2", "TRUE");

        Assert.Equal(ErrorValue.Value, sheet.Evaluate(formula).Error);
    }

    [Fact] // ADR-0180, SH-66: two calls are two draws, in one Formula and in two cells
    public void ADR0180_two_calls_are_two_draws()
    {
        var sheet = Seeded();
        sheet.Enter("A1", "=RAND()");
        sheet.Enter("A2", "=RAND()");

        Assert.False(sheet.Evaluate("=RAND()=RAND()").Boolean);
        Assert.NotEqual(0, sheet.Evaluate("=RAND()-RAND()").Number);
        Assert.NotEqual(sheet.Number("A1"), sheet.Number("A2"));
    }

    [Fact] // ADR-0180, ADR-0181, SH-66: a LET binding is one draw however often it is read (LET-017, LET-018)
    public void ADR0180_a_let_binding_is_one_draw()
    {
        var sheet = Seeded();

        Assert.Equal(0, sheet.Evaluate("=LET(x,RAND(),x-x)").Number);
        Assert.True(sheet.Evaluate("=LET(x,RAND(),x=x)").Boolean);
        Assert.Equal(0, sheet.Evaluate("=LET(x,RANDBETWEEN(1,1000000),y,x,x-y)").Number);
        Assert.False(sheet.Evaluate("=LET(x,RAND(),y,RAND(),x=y)").Boolean);
    }

    [Fact] // ADR-0180, SH-66: everything that reads a draw in one recalculation reads the same Value, and follows it at each change
    public void ADR0180_every_reader_of_a_draw_reads_the_same_value()
    {
        var sheet = Seeded();
        sheet.Enter("A1", "=RAND()");
        sheet.Enter("B1", "=A1*2");
        sheet.Enter("C1", "=B1-A1-A1");

        for (var change = 0; change < 5; change++)
        {
            Assert.Equal(sheet.Number("A1") * 2, sheet.Number("B1"));
            Assert.Equal(0, sheet.Number("C1"));
            sheet.Enter("Z9", change.ToString(CultureInfo.InvariantCulture));
        }
    }

    [Fact] // ADR-0180, SH-66, SH-17: the same Entries, seed and count of changes give the same draws, whatever order the cells were entered and computed in
    public void ADR0180_the_same_seed_and_count_give_the_same_draws_whatever_the_order()
    {
        string[] formulas = ["=RAND()", "=RANDBETWEEN(1,100)", "=A1+A2", "=LET(x,RAND(),x*2)", "=RAND()+RAND()"];
        var forward = NewSheet();
        for (var i = 0; i < formulas.Length; i++) forward.Enter($"A{i + 1}", formulas[i]);
        var backward = NewSheet();
        for (var i = formulas.Length - 1; i >= 0; i--) backward.Enter($"A{i + 1}", formulas[i]);
        var atOnce = NewSheet();
        atOnce.Enter(formulas.Select((f, i) => new KeyValuePair<CellAddress, string>(new CellAddress(i, 0), f)));

        forward.SetSeed(Seed, 7);
        backward.SetSeed(Seed, 7);
        atOnce.SetSeed(Seed, 7);

        var expected = Column(forward, "A", formulas.Length);
        Assert.Equal(expected, Column(backward, "A", formulas.Length));
        Assert.Equal(expected, Column(atOnce, "A", formulas.Length));

        // Another seed or another count draws otherwise.
        backward.SetSeed(Seed + 1, 7);
        atOnce.SetSeed(Seed, 8);
        Assert.NotEqual(expected[0], backward.Number("A1"));
        Assert.NotEqual(expected[0], atOnce.Number("A1"));
    }

    [Fact] // ADR-0180, SH-66: a change draws again; the clock, a format and a seed's own recalculation do not
    public void ADR0180_a_change_draws_again_and_the_clock_does_not()
    {
        var moment = new DateTime(2026, 10, 11, 9, 0, 0);
        var sheet = Seeded();
        sheet.NowSource = () => moment;
        sheet.SetToday(new DateOnly(2026, 10, 11));
        sheet.Enter("A1", "=RAND()");
        sheet.Enter("B1", "=NOW()+TODAY()");
        var drawn = sheet.Number("A1");

        moment = moment.AddMinutes(1);
        sheet.RecalculateVolatile();
        sheet.SetToday(new DateOnly(2026, 10, 12));
        sheet.Do(SheetEdit.SetNumberFormat(CellRange.Parse("A1"), NumberFormat.Parse("0.00")));
        sheet.Do(SheetEdit.SetColumnWidth(CellRange.Parse("A:A"), 20));
        Assert.Equal(drawn, sheet.Number("A1"));

        sheet.Enter("Z1", "x");
        Assert.NotEqual(drawn, sheet.Number("A1"));
    }

    [Fact] // ADR-0180, SH-66: each kind of change counts once — an edit, an insertion, a deletion, an undo, a redo, a snapshot — and nothing else counts
    public void ADR0180_each_change_counts_once()
    {
        var sheet = Seeded();
        sheet.DeclareLinkedTable("Rates", ["Pair", "Spot"]);
        sheet.Enter("A1", "=RAND()");
        var changes = sheet.Changes;

        void Counts(int by, Action change)
        {
            change();
            Assert.Equal(changes + by, sheet.Changes);
            changes = sheet.Changes;
        }

        Counts(1, () => sheet.Enter("B1", "1"));
        Counts(1, () => sheet.Enter([new(CellAddress.Parse("B2"), "2"), new(CellAddress.Parse("B3"), "3")]));
        Counts(0, () => sheet.Enter("B1", "1"));                                          // nothing changed
        var inserted = sheet.Do(SheetEdit.InsertRows(5));
        Assert.Equal(changes + 1, sheet.Changes);
        changes = sheet.Changes;
        Counts(1, () => inserted.Undo());                                                 // an inverse insertion and the cells put back: one change
        Counts(1, () => inserted.Redo());
        Counts(1, () => sheet.DeleteRows(8));
        var pasted = sheet.Do(SheetEdit.Fill(CellRange.Parse("B1"), CellRange.Parse("B1:B4"), FillDirection.Down));
        Assert.Equal(changes + 1, sheet.Changes);
        changes = sheet.Changes;
        Counts(1, () => pasted.Undo());
        Counts(1, () => sheet.PushLinkedTable("Rates", [[Value.FromText("EURUSD"), Value.FromNumber(1.08)]]));
        Counts(0, () => sheet.Do(SheetEdit.SetNumberFormat(CellRange.Parse("A1:B4"), NumberFormat.Parse("0.00"))));
        Counts(0, () => sheet.Do(SheetEdit.SetColumnWidth(CellRange.Parse("A:B"), 12)));
        Counts(0, () => sheet.RecalculateVolatile());
        Counts(0, () => sheet.SetToday(new DateOnly(2026, 10, 11)));
        Counts(0, () => sheet.SetSeed(Seed + 1));
    }

    [Fact] // ADR-0180 (2026-10-11): renaming the Sheet is not a change; it recalculates what INDIRECT reads by the Sheet's name, and the draws come out the same, as they do for its undo and redo
    public void ADR0180_a_rename_draws_nothing()
    {
        var sheet = Seeded();
        sheet.Enter("A1", "=RAND()");
        sheet.Enter("A2", "=RANDBETWEEN(1,1000000)");
        sheet.Enter("B1", "7");
        sheet.Enter("C1", "=INDIRECT(\"Prices!B1\")+RAND()*0");
        var changes = sheet.Changes;
        var drawn = (Rand: sheet.Number("A1"), Between: sheet.Number("A2"));
        Assert.Equal(ErrorValue.Ref, sheet.Error("C1"));

        var renamed = sheet.Do(SheetEdit.Rename("Prices"));

        Assert.Equal(7, sheet.Number("C1"));                                              // recalculated: INDIRECT's text names the Sheet now
        Assert.Equal(changes, sheet.Changes);
        Assert.Equal(drawn, (sheet.Number("A1"), sheet.Number("A2")));

        renamed.Undo();
        Assert.Equal(ErrorValue.Ref, sheet.Error("C1"));
        Assert.Equal(changes, sheet.Changes);
        Assert.Equal(drawn, (sheet.Number("A1"), sheet.Number("A2")));

        renamed.Redo();
        sheet.Rename("Costs");
        Assert.Equal(ErrorValue.Ref, sheet.Error("C1"));
        Assert.Equal(changes, sheet.Changes);
        Assert.Equal(drawn, (sheet.Number("A1"), sheet.Number("A2")));

        sheet.Enter("Z1", "x");                                                           // an edit still draws again
        Assert.Equal(changes + 1, sheet.Changes);
        Assert.NotEqual(drawn.Rand, sheet.Number("A1"));
    }

    [Fact] // ADR-0180: an undo is a change like any other, so the Values it brings back draw again
    public void ADR0180_an_undo_draws_again()
    {
        var sheet = Seeded();
        sheet.Enter("A1", "=RAND()");
        var before = sheet.Number("A1");
        var step = sheet.Do(SheetEdit.Enter(CellAddress.Parse("B1"), "1"));
        var after = sheet.Number("A1");

        step.Undo();

        Assert.NotEqual(before, sheet.Number("A1"));
        Assert.NotEqual(after, sheet.Number("A1"));
    }

    [Fact] // ADR-0180, SH-66: a Sheet whose seed was never set waits, whichever branch the Formula takes, and draws once it is given one
    public void ADR0180_without_a_seed_rand_waits()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=RAND()");
        sheet.Enter("A2", "=IF(TRUE,1,RANDBETWEEN(1,6))");
        sheet.Enter("A3", "=IFERROR(RAND(),0)");
        sheet.Enter("A4", "=A1*2");

        Assert.Null(sheet.Seed);
        Assert.All(["A1", "A2", "A3", "A4"], a => Assert.Equal(ErrorValue.GettingData, sheet.Error(a)));

        sheet.SetSeed(Seed);

        Assert.InRange(sheet.Number("A1"), 0, 1);
        Assert.Equal(1, sheet.Number("A2"));
        Assert.Equal(sheet.Number("A1") * 2, sheet.Number("A4"));

        sheet.SetSeed(null);

        Assert.Equal(ErrorValue.GettingData, sheet.Error("A1"));
    }

    [Fact] // ADR-0180, ADR-0048, SH-66: the Sheet Document records neither seed nor draw, and a Sheet opened from one counts its changes from 0
    public void ADR0180_a_document_records_no_seed_and_reopens_at_zero_changes()
    {
        var sheet = Seeded();
        sheet.Enter("A1", "=RAND()");
        sheet.Enter("B1", "1");
        Assert.True(sheet.Changes > 0);
        var json = sheet.ToDocument().ToJson();

        Assert.DoesNotContain("seed", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(sheet.Number("A1").ToString("R", CultureInfo.InvariantCulture), json, StringComparison.Ordinal);

        var reopened = Sheet.Open(SheetDocument.FromJson(json));

        Assert.Equal(0, reopened.Changes);
        Assert.Null(reopened.Seed);
        Assert.Equal(ErrorValue.GettingData, reopened.Error("A1"));
    }

    [Fact] // ADR-0180, SH-17: a document's Values computed by the engine alone, given the same seed and count of changes, equal the Sheet's own
    public void ADR0180_the_engine_alone_computes_the_same_draws_from_a_document()
    {
        var sheet = Seeded();
        sheet.Enter("A1", "=RAND()");
        sheet.Enter("A2", "=RANDBETWEEN(1,6)+A1");
        sheet.InsertRows(0);
        sheet.Enter("B1", "=LET(x,RAND(),x*100)");

        var alone = Sheet.Open(sheet.ToDocument());
        alone.SetSeed(Seed, sheet.Changes);

        foreach (var address in sheet.EntryAddresses) Assert.Equal(sheet.GetValue(address), alone.GetValue(address));
    }

    [Fact] // ADR-0180: the count of changes is never negative
    public void ADR0180_a_count_of_changes_below_zero_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewSheet().SetSeed(Seed, -1));
    }
}
