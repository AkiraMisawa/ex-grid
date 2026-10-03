using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>The engine's answers for formula entry (tickets 10 and 11): completion, the hint, the Point predicate.</summary>
public class FormulaEntryTests
{
    /// <summary>Text with a <c>|</c> marking the caret.</summary>
    private static (string Text, int Caret) AtCaret(string marked) => (marked.Replace("|", "", StringComparison.Ordinal), marked.IndexOf('|', StringComparison.Ordinal));

    private static string[]? Names(string marked, params string[] tables)
    {
        var (text, caret) = AtCaret(marked);
        return FormulaEntry.Complete(text, caret, tables)?.Candidates.Select(c => c.Name).ToArray();
    }

    [Theory] // ADR-0051: completion offers the declared functions that begin with what was typed, without regard to case
    [InlineData("=SU|", new[] { "SUBSTITUTE", "SUM", "SUMIF", "SUMIFS", "SUMPRODUCT" })]
    [InlineData("=su|", new[] { "SUBSTITUTE", "SUM", "SUMIF", "SUMIFS", "SUMPRODUCT" })]
    [InlineData("=I|", new[] { "IF", "IFERROR", "IFNA", "IFS", "INDEX", "INT", "ISBLANK", "ISERROR", "ISNA", "ISNUMBER", "ISTEXT" })]
    [InlineData("=IFE|", new[] { "IFERROR" })]
    [InlineData("=1+co|", new[] { "COLUMN", "COLUMNS", "CONCAT", "CONCATENATE", "COS", "COSH", "COT", "COTH", "COUNT", "COUNTA", "COUNTBLANK", "COUNTIF", "COUNTIFS" })]
    [InlineData("=SUM(A1,m|", new[] { "MATCH", "MAX", "MAXIFS", "MEDIAN", "MID", "MIN", "MINIFS", "MINUTE", "MOD", "MONTH", "MROUND" })]
    [InlineData("=IF(A1>0,x|", new[] { "XLOOKUP", "XMATCH", "XNPV", "XOR" })]
    [InlineData("=LOG1|", new[] { "LOG10" })]
    public void Completion_offers_declared_functions(string marked, string[] expected)
    {
        Assert.Equal(expected, Names(marked));
    }

    [Theory] // ADR-0051: no completion where no name is being typed, or nothing matches
    [InlineData("=F|3")]
    [InlineData("=SUM(F|3)")]
    [InlineData("=|")]
    [InlineData("=SUM(|")]
    [InlineData("=\"SU|")]
    [InlineData("=$A|")]
    [InlineData("=1|")]
    [InlineData("=QQ|")]
    [InlineData("SU|")]
    [InlineData("=A1 SU|")]
    [InlineData("=Positions[P|")]
    public void No_completion_where_no_name_is_typed(string marked)
    {
        Assert.Null(Names(marked, "Positions"));
    }

    [Fact] // ADR-0049/0051: Linked Tables' names are offered beside the functions, in one alphabetical list
    public void Completion_offers_linked_tables()
    {
        Assert.Equal(["ABS", "Accounts", "ACOS", "ACOSH", "ACOT", "ACOTH", "AND", "ASIN", "ASINH", "ATAN", "ATAN2", "ATANH", "AVERAGE", "AVERAGEIF", "AVERAGEIFS"], Names("=a|", "Positions", "Accounts")!);
        Assert.Equal(["Positions"], Names("=SUM(pos|", "Positions", "Accounts")!);
    }

    [Fact] // ADR-0051: accepting a candidate replaces the whole name at the caret with the candidate's text
    public void A_completion_names_what_it_replaces()
    {
        var (text, caret) = AtCaret("=1+SUM|(A1)");
        var completion = FormulaEntry.Complete(text, caret, [])!;

        Assert.Equal(3, completion.Start);
        Assert.Equal(3, completion.Length);
        var sum = Assert.Single(completion.Candidates, c => c.InsertText == "SUM(");
        Assert.Equal("SUM(", sum.InsertText);
        Assert.Equal(CompletionKind.Function, sum.Kind);
        Assert.Equal("Adds its arguments.", sum.Description);
    }

    [Fact] // ADR-0049/0051: a Sheet offers its own Linked Tables
    public void A_sheet_offers_its_tables()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Positions", ["Id", "PV"]);

        var candidate = Assert.Single(sheet.Complete("=XLOOKUP(1,Pos", 14)!.Candidates);

        Assert.Equal(new CompletionCandidate("Positions", CompletionKind.LinkedTable, "Positions", null), candidate);
    }

    [Theory] // ADR-0051: the hint names the function whose argument list holds the caret, and the argument
    [InlineData("=SUM(|", "SUM", 0, "number1")]
    [InlineData("=SUM(1,|", "SUM", 1, "[number2]")]
    [InlineData("=SUM(1,2,3,|", "SUM", 3, "[number2]")]
    [InlineData("=IF(A1>0,SUM(1,|", "SUM", 1, "[number2]")]
    [InlineData("=IF(A1>0,SUM(1,2),|", "IF", 2, "[value_if_false]")]
    [InlineData("=IF(A1>0,(1+|", "IF", 1, "value_if_true")]
    [InlineData("=IF(\"a,b\",|", "IF", 1, "value_if_true")]
    [InlineData("=IF(\"a,|", "IF", 0, "logical_test")]
    [InlineData("=if(1,2,3,|", "IF", 3, null)]
    [InlineData("=XLOOKUP(A1,Positions[Id],|", "XLOOKUP", 2, "return_array")]
    [InlineData("=SUM(1,|2,3)", "SUM", 1, "[number2]")]
    [InlineData("=SUMIFS(A1:A3,B1:B3,1,|", "SUMIFS", 3, "criteria_range1")]
    [InlineData("=SUMIFS(A1:A3,B1:B3,1,C1:C3,|", "SUMIFS", 4, "criteria1")]
    [InlineData("=COUNTIFS(A1:A3,1,|", "COUNTIFS", 2, "criteria_range1")]
    [InlineData("=COUNTIFS(A1:A3,1,B1:B3,|", "COUNTIFS", 3, "criteria1")]
    public void The_hint_follows_the_caret(string marked, string function, int index, string? argument)
    {
        var (text, caret) = AtCaret(marked);

        var hint = FormulaEntry.HintAt(text, caret)!;

        Assert.Equal(function, hint.Function.Name);
        Assert.Equal(index, hint.ArgumentIndex);
        Assert.Equal(argument, hint.CurrentArgument);
    }

    [Theory] // ADR-0051: no hint outside a call, after it closes, or for a function that is not declared
    [InlineData("=|")]
    [InlineData("=SUM(1)|")]
    [InlineData("=(1+|")]
    [InlineData("=FOO(|")]
    [InlineData("=SUM (|")]
    [InlineData("SUM(|")]
    public void No_hint_outside_a_declared_call(string marked)
    {
        var (text, caret) = AtCaret(marked);

        Assert.Null(FormulaEntry.HintAt(text, caret));
    }

    [Theory] // ADR-0051: a Reference can go after =, an operator, ( or ,
    [InlineData("=|")]
    [InlineData("=A1+|")]
    [InlineData("=A1*|")]
    [InlineData("=A1&|")]
    [InlineData("=A1<>|")]
    [InlineData("=-|")]
    [InlineData("=SUM(|")]
    [InlineData("=SUM(A1,|")]
    [InlineData("=SUM(|)")]
    [InlineData("=SUM(A1, |)")]
    [InlineData("=|+A1")]
    public void A_reference_can_go_here(string marked)
    {
        var (text, caret) = AtCaret(marked);

        Assert.Equal(new PointSite(caret, 0), FormulaEntry.PointAt(text, caret));
    }

    [Theory] // ADR-0051/0012: anywhere else the arrows keep their meaning
    [InlineData("=A1|")]
    [InlineData("=SUM(A1)|")]
    [InlineData("=5%|")]
    [InlineData("=\"a+|")]
    [InlineData("=\"a+|\"")]
    [InlineData("=A1+|B2")]
    [InlineData("=SU|M(")]
    [InlineData("=Positions[|")]
    [InlineData("42|")]
    [InlineData("|=")]
    public void A_reference_cannot_go_here(string marked)
    {
        var (text, caret) = AtCaret(marked);

        Assert.Null(FormulaEntry.PointAt(text, caret));
    }

    [Theory] // ADR-0051: while pointing, the Reference just written is what a further arrow replaces
    [InlineData("=A3|", 1, 2)]
    [InlineData("=SUM(A1:B2|", 5, 5)]
    [InlineData("=A1+$B$2|", 4, 4)]
    [InlineData("=SUM(A1:B2|)", 5, 5)]
    public void The_pointed_reference_is_found(string marked, int start, int length)
    {
        var (text, caret) = AtCaret(marked);

        Assert.Equal(new PointSite(start, length), FormulaEntry.PointedReferenceAt(text, caret));
    }

    [Theory] // ADR-0051: what is not a Reference in a Reference's place is not replaced
    [InlineData("=SUM|")]
    [InlineData("=A1+|")]
    [InlineData("=5%|")]
    [InlineData("=Sheet2!A1|")]
    [InlineData("=A1|B")]
    public void No_pointed_reference_elsewhere(string marked)
    {
        var (text, caret) = AtCaret(marked);

        Assert.Null(FormulaEntry.PointedReferenceAt(text, caret));
    }

    [Fact] // ADR-0051: the Reference text for a pointed range is Excel's, from its top-left
    public void The_reference_text_for_a_range()
    {
        Assert.Equal("A3", FormulaEntry.ReferenceText(new CellRange(CellAddress.Parse("A3"))));
        Assert.Equal("B7:C9", FormulaEntry.ReferenceText(new CellRange(CellAddress.Parse("C9"), CellAddress.Parse("B7"))));
        Assert.Equal("B7:C9", FormulaEntry.ReferenceText(new CellRange(CellAddress.Parse("B9"), CellAddress.Parse("C7"))));
    }

    [Fact] // ADR-0051: a caret outside the text is an argument error
    public void A_caret_outside_the_text_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FormulaEntry.PointAt("=A1", 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => FormulaEntry.HintAt("=A1", -1));
    }
}
