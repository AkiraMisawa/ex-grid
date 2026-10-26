using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// Completion's triggers, aligned with Excel (ticket 39, ADR-0058 "Completion, aligned with Excel", SH-36), that a
/// shared case does not hold: what is listed, where, and what the lists declare is in
/// <c>tests/cases/sheet/completion-trigger.json</c> and <c>completion-trigger-sheet.json</c>. What stays here, mirrored
/// in the Script Core's <c>surface.test.ts</c>, is a walk over every declared function, and lists read only for one
/// name in them, which a case would have to write out whole.
/// </summary>
public class CompletionTriggerTests
{
    [Fact] // ADR-0058, SH-36: unrestricted arguments do not declare a choice list.
    public void Other_arguments_declare_no_values()
    {
        Assert.All(DeclaredFunction.All.Where(f => f.Name is not ("XLOOKUP" or "VLOOKUP" or "HLOOKUP" or "MATCH" or "YEARFRAC" or "SUBTOTAL" or "AGGREGATE")),
            f => Assert.All(Enumerable.Range(0, 8), index => Assert.Empty(f.ValuesOf(index))));
    }

    [Theory] // ADR-0058 (Q54), SH-36: outside a value-list argument a letter lists the functions and Linked Tables it begins, as before — at another argument, after an operator, inside a call of its own, and inside a grouping parenthesis, which holds an expression of its own (decided with the user 2026-10-01, not asked of Excel)
    [InlineData("=A|", "AVERAGE")]
    [InlineData("=1+A|", "AVERAGE")]
    [InlineData("=SUM(A|", "AVERAGE")]
    [InlineData("=XLOOKUP(1,A|", "AVERAGE")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,P|", "Positions")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,0,1,P|", "Positions")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,SUM(A|", "AVERAGE")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,(A|", "AVERAGE")]
    [InlineData("=XLOOKUP(1,A2:A4,B2:B4,,ROUND(1,X|", "XLOOKUP")]
    public void ADR0058_Q54_outside_a_value_list_argument_a_letter_lists_names(string marked, string listed)
    {
        var sheet = NewSheet();
        sheet.DeclareLinkedTable("Positions", ["Id", "PV"]);
        var caret = marked.IndexOf('|', StringComparison.Ordinal);

        var completion = sheet.Complete(marked.Replace("|", "", StringComparison.Ordinal), caret)!;

        Assert.Contains(listed, completion.Candidates.Select(c => c.Name));
        Assert.DoesNotContain(completion.Candidates, c => c.Kind == CompletionKind.ArgumentValue);
    }
}
