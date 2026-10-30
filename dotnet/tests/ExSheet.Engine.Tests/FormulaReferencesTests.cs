using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// The References in the text being edited (ADR-0057, "Who decides what"; SH-30) that a shared case does not hold:
/// what each answer is, span and cells or column, is in <c>tests/cases/sheet/formula-references.json</c> and
/// <c>sheet-references.json</c>. What stays here is .NET's own: a key's equality and hash, the walk over every prefix
/// of a Formula being typed (mirrored in the Script Core's <c>surface.test.ts</c>), and a null argument.
/// </summary>
public class FormulaReferencesTests
{
    [Fact] // ADR-0057 / SH-30: a structured reference's key compares equal however the table and the column were cased
    public void SH30_a_structured_references_key_compares_equal_however_cased()
    {
        var answers = FormulaEntry.References("=SUM(Positions[PV])+SUM(positions[pv])+SUM(POSITIONS[[Pv]])", Sheet.DefaultName);

        Assert.Equal(3, answers.Count);
        Assert.All(answers, a => Assert.Equal(answers[0].LinkedColumn, a.LinkedColumn));
        Assert.All(answers, a => Assert.Equal(answers[0].LinkedColumn!.GetHashCode(), a.LinkedColumn!.GetHashCode()));
    }

    [Theory] // ADR-0057 / SH-30: another column, or another table's column of the same name, is another key
    [InlineData("=SUM(Positions[PV])+SUM(Positions[Id])")]
    [InlineData("=SUM(Positions[PV])+SUM(Accounts[PV])")]
    public void SH30_another_column_is_another_key(string text)
    {
        var answers = FormulaEntry.References(text, Sheet.DefaultName);

        Assert.NotEqual(answers[0].LinkedColumn, answers[1].LinkedColumn);
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
                        // In the text, after the =, in the order written, none overlapping another, and either cells or a column.
                        Assert.InRange(answer.Start, end, text.Length);
                        Assert.InRange(answer.Length, 1, text.Length - answer.Start);
                        Assert.True(answer.Cells is null != answer.LinkedColumn is null, $"'{text}' answered {answer}");
                        end = answer.Start + answer.Length;
                    }
                }
            }
        }
    }

    [Fact] // ADR-0057: no text, or no Sheet name to read a qualifier against, is an argument error, not a guess
    public void Missing_arguments_are_refused()
    {
        Assert.Throws<ArgumentNullException>(() => FormulaEntry.References(null!, Sheet.DefaultName));
        Assert.Throws<ArgumentNullException>(() => FormulaEntry.References("=A1", null!));
        Assert.Throws<ArgumentNullException>(() => NewSheet().References(null!));
    }
}
