using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// The References in the text being edited (ADR-0057, "Who decides what"; SH-30): ExSheet's answer
/// to the References function, against ADR-0057's readings. Each answer is written as the text of
/// its span, <c>=</c>, and what it names: its cells, or a Linked Table's column as
/// <c>Table[Column]</c>.
/// </summary>
public class FormulaReferencesTests
{
    /// <summary>Each Reference answered in <paramref name="text"/>, as the tests write what they expect.</summary>
    private static string[] Answered(string text, string sheetName = Sheet.DefaultName)
    {
        var answers = FormulaEntry.References(text, sheetName);
        foreach (var answer in answers)
        {
            // Either the cells or a Linked Table's column, never both and never neither.
            Assert.True(answer.Cells is null != answer.LinkedColumn is null, $"'{text}' answered {answer}");
        }
        return [.. answers.Select(a => $"{text.Substring(a.Start, a.Length)} = {(a.Cells is { } cells ? cells.ToString() : $"{a.LinkedColumn!.Table}[{a.LinkedColumn.Column}]")}")];
    }

    [Theory] // ADR-0057 / SH-30: every Reference in the Formula is answered, with its span and the cells it names, in the order written
    [InlineData("=A1", new[] { "A1 = A1" })]
    [InlineData("=$A$1", new[] { "$A$1 = A1" })]
    [InlineData("=A$1+$B2", new[] { "A$1 = A1", "$B2 = B2" })]
    [InlineData("=A1:B2", new[] { "A1:B2 = A1:B2" })]
    [InlineData("=$A$1:$B$2", new[] { "$A$1:$B$2 = A1:B2" })]
    [InlineData("=a1", new[] { "a1 = A1" })]
    [InlineData("=-A1", new[] { "A1 = A1" })]
    [InlineData("=SUM(A1,B2:C3)*D4%", new[] { "A1 = A1", "B2:C3 = B2:C3", "D4 = D4" })]
    [InlineData("=IF(A1>0,B1,\"no\")", new[] { "A1 = A1", "B1 = B1" })]
    [InlineData("= A1 +\nB1", new[] { "A1 = A1", "B1 = B1" })]
    [InlineData("=XFD1048576", new[] { "XFD1048576 = XFD1048576" })]
    public void SH30_each_reference_is_answered_with_its_span_and_cells(string text, string[] expected)
    {
        Assert.Equal(expected, Answered(text));
    }

    [Fact] // ADR-0057 / SH-30: a span is its start and length in the whole text, the = at 0, a Sheet qualifier included
    public void SH30_a_span_counts_from_the_start_of_the_text()
    {
        var answers = FormulaEntry.References("=SUM(A1, Sheet1!B2:C3)", Sheet.DefaultName);

        Assert.Equal([(5, 2), (9, 12)], answers.Select(a => (a.Start, a.Length)));
    }

    [Theory] // ADR-0057 reading: the same cells share a colour however they are written — =B2:A1 outlines A1:B2
    [InlineData("=B2:A1", "B2:A1 = A1:B2")]
    [InlineData("=B1:A2", "B1:A2 = A1:B2")]
    [InlineData("=A2:B1", "A2:B1 = A1:B2")]
    [InlineData("=$B$2:A1", "$B$2:A1 = A1:B2")]
    [InlineData("=A1:A1", "A1:A1 = A1")]
    public void SH30_a_range_written_from_any_corner_names_its_rectangle(string text, string expected)
    {
        Assert.Equal([expected], Answered(text));
    }

    [Theory] // ADR-0057 reading: the same cells share a colour however they are written — =A1+A1 and =A1+$A$1 each name A1 alike
    [InlineData("=A1+A1")]
    [InlineData("=A1+$A$1")]
    [InlineData("=a1+$A1")]
    [InlineData("=A1:B2+B2:A1")]
    [InlineData("=Sheet1!A1+A1")]
    [InlineData("=A:A+A1:A1048576")]
    [InlineData("=1:1+A1:XFD1")]
    public void SH30_the_same_cells_are_answered_alike_however_they_are_written(string text)
    {
        var answers = FormulaEntry.References(text, Sheet.DefaultName);

        Assert.Equal(2, answers.Count);
        Assert.NotNull(answers[0].Cells);
        Assert.Equal(answers[0].Cells, answers[1].Cells);
    }

    [Fact] // ADR-0057 / SH-30: different cells are answered apart, a cell inside a range included (=A1:B2+B2)
    public void SH30_different_cells_are_answered_apart()
    {
        var answers = FormulaEntry.References("=A1:B2+B2", Sheet.DefaultName);

        Assert.NotEqual(answers[0].Cells, answers[1].Cells);
    }

    [Theory] // ADR-0057 reading: a whole column or row (A:A, 1:1) is outlined across the whole of it
    [InlineData("=SUM(A:A)", 0, 0, true)]
    [InlineData("=SUM($A:$A)", 0, 0, true)]
    [InlineData("=SUM(C:A)", 0, 2, true)]
    [InlineData("=SUM(1:1)", 0, 0, false)]
    [InlineData("=SUM($2:$5)", 1, 4, false)]
    [InlineData("=SUM(5:2)", 1, 4, false)]
    public void SH30_a_whole_column_or_row_is_answered_whole(string text, int first, int last, bool columns)
    {
        var answer = Assert.Single(FormulaEntry.References(text, Sheet.DefaultName));

        Assert.Equal(columns ? CellRange.WholeColumns(first, last) : CellRange.WholeRows(first, last), answer.Cells);
        Assert.Equal(5, answer.Start);
        Assert.Equal(text.Length - 6, answer.Length);
    }

    [Theory] // ADR-0057 reading: Sheet1!A1, with this Sheet's own name, is outlined — its span holds the qualifier
    [InlineData("=Sheet1!A1", "Sheet1", "Sheet1!A1 = A1")]
    [InlineData("=sheet1!a1", "Sheet1", "sheet1!a1 = A1")]
    [InlineData("='Sheet1'!A1", "Sheet1", "'Sheet1'!A1 = A1")]
    [InlineData("=Sheet1!$B$2:A1", "Sheet1", "Sheet1!$B$2:A1 = A1:B2")]
    [InlineData("=Sheet1!A:A", "Sheet1", "Sheet1!A:A = A1:A1048576")]
    [InlineData("=Sheet1!1:1", "Sheet1", "Sheet1!1:1 = A1:XFD1")]
    [InlineData("='Sheet 1'!A1", "Sheet 1", "'Sheet 1'!A1 = A1")]
    [InlineData("='sheet 1'!A1", "Sheet 1", "'sheet 1'!A1 = A1")]
    [InlineData("='It''s'!B3", "It's", "'It''s'!B3 = B3")]
    [InlineData("=Positions!C4", "Positions", "Positions!C4 = C4")]
    public void SH30_this_sheets_own_qualifier_is_answered(string text, string sheetName, string expected)
    {
        Assert.Equal([expected], Answered(text, sheetName));
    }

    [Theory] // ADR-0057 reading: a Reference qualified with another name names no cells (#REF!, ADR-0046) and is not coloured
    [InlineData("=Sheet2!A1+B1", "Sheet1", new[] { "B1 = B1" })]
    [InlineData("=Sheet2!A1", "Sheet1", new string[0])]
    [InlineData("='Sheet 2'!A1:B2", "Sheet1", new string[0])]
    [InlineData("=SUM(Sheet2!A:A)", "Sheet1", new string[0])]
    [InlineData("=Sheet1!A1", "Sheet 1", new string[0])]
    [InlineData("='Sheet1 '!A1", "Sheet1", new string[0])]
    public void SH30_another_sheets_qualifier_names_no_cells(string text, string sheetName, string[] expected)
    {
        Assert.Equal(expected, Answered(text, sheetName));
    }

    [Fact] // ADR-0046/0057: this Sheet's own qualifier is the name the Sheet has now
    public void SH30_a_sheet_answers_by_its_own_name()
    {
        var sheet = NewSheet();
        Assert.Equal(CellRange.Parse("A1"), Assert.Single(sheet.References("=Sheet1!A1")).Cells);

        sheet.Rename("Risk");

        Assert.Empty(sheet.References("=Sheet1!A1"));
        Assert.Equal(CellRange.Parse("A1"), Assert.Single(sheet.References("=risk!A1")).Cells);
    }

    [Theory] // ADR-0057 / SH-30: a structured reference is answered with its span and a key naming the table and the column
    [InlineData("=SUM(Positions[PV])", new[] { "Positions[PV] = Positions[PV]" })]
    [InlineData("=SUM(Positions[[PV]])", new[] { "Positions[[PV]] = Positions[PV]" })]
    [InlineData("=SUM(Positions[Market Value])", new[] { "Positions[Market Value] = Positions[Market Value]" })]
    [InlineData("=SUM(Positions[a'[b])", new[] { "Positions[a'[b] = Positions[a[b]" })]
    [InlineData("=SUM(Positions[PV])+A1", new[] { "Positions[PV] = Positions[PV]", "A1 = A1" })]
    [InlineData("=XLOOKUP(A1,Positions[Id],Positions[PV])", new[] { "A1 = A1", "Positions[Id] = Positions[Id]", "Positions[PV] = Positions[PV]" })]
    [InlineData("=AB1[PV]", new[] { "AB1[PV] = AB1[PV]" })]
    public void SH30_a_structured_reference_is_answered_with_its_table_and_column(string text, string[] expected)
    {
        Assert.Equal(expected, Answered(text));
    }

    [Fact] // ADR-0057 / SH-30: a structured reference's key compares equal however the table and the column were cased
    public void SH30_a_structured_references_key_compares_equal_however_cased()
    {
        var answers = FormulaEntry.References("=SUM(Positions[PV])+SUM(positions[pv])+SUM(POSITIONS[[Pv]])", Sheet.DefaultName);

        Assert.Equal(3, answers.Count);
        Assert.All(answers, a => Assert.Equal(answers[0].LinkedColumn, a.LinkedColumn));
        Assert.All(answers, a => Assert.Equal(answers[0].LinkedColumn!.GetHashCode(), a.LinkedColumn!.GetHashCode()));
        Assert.Equal("Positions", answers[0].LinkedColumn!.Table);
        Assert.Equal("pv", answers[1].LinkedColumn!.Column);
    }

    [Theory] // ADR-0057 / SH-30: another column, or another table's column of the same name, is another key
    [InlineData("=SUM(Positions[PV])+SUM(Positions[Id])")]
    [InlineData("=SUM(Positions[PV])+SUM(Accounts[PV])")]
    public void SH30_another_column_is_another_key(string text)
    {
        var answers = FormulaEntry.References(text, Sheet.DefaultName);

        Assert.NotEqual(answers[0].LinkedColumn, answers[1].LinkedColumn);
    }

    [Theory] // ADR-0057 reading: an unfinished Formula is coloured as far as it goes — =SUM(A1, colours A1
    [InlineData("=SUM(A1,", new[] { "A1 = A1" })]
    [InlineData("=A1+", new[] { "A1 = A1" })]
    [InlineData("=SUM(A1:B2", new[] { "A1:B2 = A1:B2" })]
    [InlineData("=IF(A1>0,C3", new[] { "A1 = A1", "C3 = C3" })]
    [InlineData("=A1&\"B2", new[] { "A1 = A1" })]
    [InlineData("=SUM(A1,\"x\",B2,\"C3", new[] { "A1 = A1", "B2 = B2" })]
    [InlineData("=SUM(Positions[PV]", new[] { "Positions[PV] = Positions[PV]" })]
    [InlineData("=A1+SUM(Positions[P", new[] { "A1 = A1" })]
    [InlineData("=A1+'Sheet 1'!B", new[] { "A1 = A1" })]
    [InlineData("=A1+SUM(", new[] { "A1 = A1" })]
    [InlineData("=A1)", new[] { "A1 = A1" })]
    public void SH30_an_unfinished_formula_is_answered_as_far_as_it_goes(string text, string[] expected)
    {
        Assert.Equal(expected, Answered(text));
    }

    [Theory] // ADR-0057, cases 24-32 of the eighth Windows run: a range typed as far as its colon answers its first corner, the colon left out (case 26); an incomplete second corner likewise (a reading)
    [InlineData("=SUM(A1:", new[] { "A1 = A1" })]
    [InlineData("=B1+A1:", new[] { "B1 = B1", "A1 = A1" })]
    [InlineData("=SUM(A1:B", new[] { "A1 = A1" })]
    [InlineData("=SUM(Sheet1!A1:", new[] { "Sheet1!A1 = A1" })]
    [InlineData("=SUM($A$1:", new[] { "$A$1 = A1" })]
    [InlineData("=SUM(A1:B2", new[] { "A1:B2 = A1:B2" })]
    [InlineData("=SUM(A:", new string[0])]
    [InlineData("=SUM(Sheet2!A1:", new string[0])]
    public void SH30_a_range_typed_as_far_as_its_colon_answers_its_first_corner(string text, string[] expected)
    {
        Assert.Equal(expected, Answered(text));
    }

    [Fact] // ADR-0057 reading: a structured reference is coloured only when its table and column are declared — one naming neither names anything, as a Reference to another Sheet does not (decided with the user, 2026-09-29)
    public void SH30_a_structured_reference_to_an_undeclared_table_or_column_is_not_answered()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Positions", ["Id", "PV"]);

        Assert.Empty(sheet.References("=SUM(Nope[PV])"));
        Assert.Empty(sheet.References("=SUM(Positions[Nope])"));
        var answered = Assert.Single(sheet.References("=SUM(Nope[PV])+SUM(positions[pv])+A1"), a => a.LinkedColumn is not null);
        Assert.Equal(new LinkedTableColumn("positions", "pv"), answered.LinkedColumn);
    }

    [Fact] // ADR-0057 reading: a declared table still waiting for its data is coloured — it names a column that exists
    public void SH30_a_declared_table_waiting_for_its_data_is_answered()
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Positions", ["Id", "PV"]);

        Assert.Equal(new LinkedTableColumn("Positions", "PV"), Assert.Single(sheet.References("=SUM(Positions[PV])")).LinkedColumn);
    }

    [Fact] // ADR-0057: the text alone does not say which tables are declared, so FormulaEntry answers every structured reference and the Sheet keeps the declared ones
    public void SH30_the_text_alone_answers_every_structured_reference()
    {
        Assert.Equal(new LinkedTableColumn("Nope", "PV"), Assert.Single(FormulaEntry.References("=SUM(Nope[PV])", "Sheet1")).LinkedColumn);
    }

    [Fact] // ADR-0057 reading: an unfinished Formula is coloured as far as it goes — every prefix of a Formula as it is typed is answered, and none throws
    public void SH30_every_prefix_of_a_formula_being_typed_is_answered()
    {
        string[] formulas =
        [
            "=SUM(A1,Sheet1!B2:C3,\"x\"\"y\",Positions[[PV]],LOG10(D4))+'Sheet 1'!E5*$F$6%-G:G&1:1",
            "=IF(Sheet2!A1<>#N/A,XLOOKUP(A1,Positions[Id],Positions[a'[b]),'It''s'!B2)",
            "=-$XFD$1048576^2/Sheet1!#REF!",
        ];
        foreach (var formula in formulas)
        {
            for (var typed = 0; typed <= formula.Length; typed++)
            {
                var text = formula[..typed];
                foreach (var sheetName in new[] { "Sheet1", "Sheet 1", "It's" })
                {
                    var answers = FormulaEntry.References(text, sheetName);
                    var end = 1;
                    foreach (var answer in answers)
                    {
                        // In the text, after the =, in the order written, none overlapping another.
                        Assert.InRange(answer.Start, end, text.Length);
                        Assert.InRange(answer.Length, 1, text.Length - answer.Start);
                        end = answer.Start + answer.Length;
                    }
                }
            }
        }
        Assert.Equal(
            ["A1 = A1", "Sheet1!B2:C3 = B2:C3", "Positions[[PV]] = Positions[PV]", "D4 = D4", "$F$6 = F6", "G:G = G1:G1048576", "1:1 = A1:XFD1"],
            Answered(formulas[0]));
    }

    [Theory] // ADR-0057 reading: nothing inside a string is coloured
    [InlineData("=\"A1\"&B1", new[] { "B1 = B1" })]
    [InlineData("=\"A1:B2\"", new string[0])]
    [InlineData("=\"say \"\"A1\"\" \"&C3", new[] { "C3 = C3" })]
    [InlineData("=\"Sheet1!A1\"", new string[0])]
    [InlineData("=\"Positions[PV]\"", new string[0])]
    [InlineData("=\"A1", new string[0])]
    public void SH30_nothing_inside_a_string_is_answered(string text, string[] expected)
    {
        Assert.Equal(expected, Answered(text));
    }

    [Theory] // ADR-0057 reading: no function name is coloured, even where the name reads as a cell (LOG10)
    [InlineData("=LOG10(A1)", new[] { "A1 = A1" })]
    [InlineData("=log10(A1)", new[] { "A1 = A1" })]
    [InlineData("=LOG10(", new string[0])]
    [InlineData("=SUM(LOG10(B2),C3)", new[] { "B2 = B2", "C3 = C3" })]
    [InlineData("=SUM(A1)", new[] { "A1 = A1" })]
    public void SH30_no_function_name_is_answered(string text, string[] expected)
    {
        Assert.Equal(expected, Answered(text));
    }

    [Theory] // ADR-0057 / SH-30: what the formula grammar does not read as a Reference is not answered
    [InlineData("=1+2")]
    [InlineData("=1.5E3")]
    [InlineData("=TRUE")]
    [InlineData("=#REF!")]
    [InlineData("=Sheet1!#REF!")]
    [InlineData("=#DIV/0!")]
    [InlineData("=ZZZ1")]
    [InlineData("=A0")]
    [InlineData("=A1048577")]
    [InlineData("=A1B2")]
    [InlineData("=1A1")]
    [InlineData("=A1.5")]
    [InlineData("=A1:B2:C3")]
    [InlineData("=Positions")]
    [InlineData("=Positions[#All]")]
    [InlineData("=Positions[@PV]")]
    [InlineData("=Positions[]")]
    [InlineData("=Positions[PV]x")]
    [InlineData("=")]
    public void SH30_what_is_not_a_reference_is_not_answered(string text)
    {
        Assert.Empty(Answered(text));
    }

    [Theory] // ADR-0057: text that is not a Formula is answered with nothing; a Formula begins with =, or with + or - as Excel colours it (cases 24, 25)
    [InlineData("")]
    [InlineData("A1")]
    [InlineData("A1:B2")]
    [InlineData("'=A1")]
    [InlineData("'+A1")]
    [InlineData(" =A1")]
    [InlineData("SUM(A1)")]
    public void SH30_text_that_is_not_a_formula_is_answered_with_nothing(string text)
    {
        Assert.Empty(FormulaEntry.References(text, Sheet.DefaultName));
    }

    [Theory] // ADR-0057, cases 24-25 of the eighth Windows run: text beginning with + or - is coloured as a Formula is, as Excel colours it and ExSheet enters it (=+A1, =-B2)
    [InlineData("+A1", new[] { "A1 = A1" })]
    [InlineData("-B2", new[] { "B2 = B2" })]
    [InlineData("-B2*C3:D4", new[] { "B2 = B2", "C3:D4 = C3:D4" })]
    [InlineData("+SUM(A1,", new[] { "A1 = A1" })]
    [InlineData("+1", new string[0])]
    [InlineData("-", new string[0])]
    public void SH30_text_beginning_with_a_sign_is_answered_as_a_formula(string text, string[] expected)
    {
        Assert.Equal(expected, Answered(text));
    }

    [Fact] // ADR-0057: no text, or no Sheet name to read a qualifier against, is an argument error, not a guess
    public void Missing_arguments_are_refused()
    {
        Assert.Throws<ArgumentNullException>(() => FormulaEntry.References(null!, Sheet.DefaultName));
        Assert.Throws<ArgumentNullException>(() => FormulaEntry.References("=A1", null!));
        Assert.Throws<ArgumentNullException>(() => NewSheet().References(null!));
    }
}
