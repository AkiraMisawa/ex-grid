using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// The functions admitted after ADR-0047's first list (docs/specs/exsheet-functions, ticket 01):
/// what the Excel case corpus cannot state — Linked Tables, recalculation, and a text past a
/// cell's limit. Their answers to Excel's cases are in <c>ExcelCases/*.json</c>.
/// </summary>
public class FunctionAdditionTests
{
    private static Sheet WithRates(bool push = true)
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Rates", ["Pair", "Mid"]);
        if (push)
        {
            sheet.PushLinkedTable("Rates",
            [
                [Value.FromText("EURUSD"), Value.FromNumber(1.08)],
                [Value.FromText("USDJPY"), null],
            ]);
        }
        return sheet;
    }

    [Theory] // ADR-0049: a function that answers about any Value never answers about data that has not arrived
    [InlineData("=IFNA(Rates[Mid],0)")]
    [InlineData("=ISBLANK(INDEX(Rates[Mid],1))")]
    [InlineData("=ISNUMBER(INDEX(Rates[Mid],1))")]
    [InlineData("=NOT(INDEX(Rates[Mid],1))")]
    [InlineData("=AND(Rates[Mid])")]
    [InlineData("=CONCAT(Rates[Pair])")]
    public void Getting_data_is_not_answered_for(string formula)
    {
        var sheet = WithRates(push: false);
        sheet.Enter("A1", formula);

        Assert.Equal(ErrorValue.GettingData, sheet.Error("A1"));
    }

    [Fact] // ADR-0049: INDEX reads a Linked Table's column by position, and a missing Value is blank
    public void Index_reads_a_linked_tables_column()
    {
        var sheet = WithRates();

        Assert.Equal(1.08, sheet.Evaluate("=INDEX(Rates[Mid],1)").Number);
        Assert.True(sheet.Evaluate("=ISBLANK(INDEX(Rates[Mid],2))").Boolean);
        Assert.Equal(ErrorValue.Ref, sheet.Evaluate("=INDEX(Rates[Mid],3)").Error);
    }

    [Fact] // ADR-0047: INDEX's range is a dependency, so a change inside it recomputes the Formula
    public void Indexs_range_is_a_dependency()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "2");
        sheet.Enter("B1", "=INDEX(A1:A3,2)");

        var inside = sheet.Enter("A2", "5");

        Assert.Equal(5, sheet.Number("B1"));
        Assert.Equal(["B1"], inside.Recalculated.Addresses());
    }

    [Fact] // docs/specs/exsheet-functions: CONCAT past a cell's 32767 characters is #VALUE!, never a cut text
    public void Concat_past_a_cells_limit_is_refused()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", new string('a', 20_000));
        sheet.Enter("A2", new string('b', 12_767));
        sheet.Enter("A3", "c");

        Assert.Equal(32_767, sheet.Evaluate("=LEN(CONCAT(A1:A2))").Number);
        Assert.Equal(ErrorValue.Value, sheet.Evaluate("=CONCAT(A1:A3)").Error);
    }

    [Theory] // docs/specs/exsheet-functions: the new functions refuse a call with the wrong number of arguments, as Excel does
    [InlineData("=AND()")]
    [InlineData("=NOT(TRUE,FALSE)")]
    [InlineData("=IFNA(1)")]
    [InlineData("=ROUNDUP(1)")]
    [InlineData("=MOD(1)")]
    [InlineData("=DATE(2026,1)")]
    [InlineData("=EOMONTH(1)")]
    [InlineData("=LEFT()")]
    [InlineData("=MID(\"a\",1)")]
    [InlineData("=INDEX(A1:A2)")]
    public void A_call_with_the_wrong_number_of_arguments_is_refused(string formula)
    {
        Assert.Throws<FormulaSyntaxException>(() => Entry.FromFormula(formula));
    }

    [Fact] // ticket 04: ROW() and COLUMN() read their own cell's place, so a move recalculates them though their text is unchanged
    public void Row_and_column_follow_their_cell_when_it_moves()
    {
        var sheet = NewSheet();
        sheet.Enter("B2", "=ROW()*100+COLUMN()");
        Assert.Equal(202, sheet.Number("B2"));

        sheet.InsertRows(0);
        Assert.Equal(302, sheet.Number("B3"));

        sheet.InsertColumns(0, 2);
        Assert.Equal(304, sheet.Number("D3"));
    }
}
