using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// The arguments Excel takes only as a Reference (ADR-0047; the seventeenth Windows run): Excel
/// refuses <c>SUBTOTAL(9,5)</c>, <c>SUBTOTAL(9,SEQUENCE(3))</c>, <c>RANK.EQ(1,1)</c> and
/// <c>AGGREGATE(14,6,A1:A11,1,2)</c> when they are entered, and so does the engine: a constant, an
/// operator's result, or a call of a function that never gives a Reference, given where only a
/// Reference is taken. What can give a Reference is taken, and judged when it is computed.
/// </summary>
public class ReferenceArgumentTests
{
    [Theory] // ADR-0047 (SUBTOTAL-029, SUBTOTAL-030, RANKEQ-005, AGGREGATE-040): what can never be a Reference is refused on entry where only a Reference is taken
    [InlineData("=SUBTOTAL(9,5)")]
    [InlineData("=SUBTOTAL(9,SEQUENCE(3))")]
    [InlineData("=SUBTOTAL(9,\"A1:A3\")")]
    [InlineData("=SUBTOTAL(9,TRUE)")]
    [InlineData("=SUBTOTAL(9,A1:A3*1)")]
    [InlineData("=SUBTOTAL(9,-A1:A3)")]
    [InlineData("=SUBTOTAL(9,A1:A3%)")]
    [InlineData("=SUBTOTAL(9,(5))")]
    [InlineData("=SUBTOTAL(9,A1:A3,SUM(A1:A3))")]
    [InlineData("=SUBTOTAL(109,A1:A3,B1:B3,7)")]
    [InlineData("=RANK.EQ(1,1)")]
    [InlineData("=RANK.EQ(1,SEQUENCE(3))")]
    [InlineData("=RANK.EQ(1,A1:A3+0,1)")]
    [InlineData("=AGGREGATE(14,6,A1:A3,1,2)")]
    [InlineData("=AGGREGATE(9,6,A1:A3,B1:B3,5)")]
    [InlineData("=SUM(1,SUBTOTAL(9,5))")]
    [InlineData("=IF(TRUE,SUBTOTAL(9,5))")]
    public void ADR0047_what_can_never_be_a_reference_is_refused_on_entry_where_only_a_reference_is_taken(string formula)
    {
        var sheet = NewSheet();

        Assert.Throws<FormulaSyntaxException>(() => sheet.Enter("A5", formula));
        Assert.Null(sheet.Value("A5"));
    }

    [Theory] // ADR-0047 (SUBTOTAL-031, AGGREGATE-036, 039, 041): a Reference, and what can give one, is taken; and AGGREGATE's third and fourth arguments take a Value
    [InlineData("=SUBTOTAL(9,A1:A3)")]
    [InlineData("=SUBTOTAL(9,(A1:A3))")]
    [InlineData("=SUBTOTAL(9,A1:A3,B1:B3)")]
    [InlineData("=SUBTOTAL(9,OFFSET(A1,0,0,3,1))")]
    [InlineData("=SUBTOTAL(9,INDIRECT(\"A1:A3\"))")]
    [InlineData("=SUBTOTAL(9,INDEX(A1:B3,0,1))")]
    [InlineData("=SUBTOTAL(9,CHOOSE(1,A1:A3,B1:B3))")]
    [InlineData("=SUBTOTAL(9,IF(TRUE,A1:A3))")]
    [InlineData("=SUBTOTAL(9,IFS(TRUE,A1:A3))")]
    [InlineData("=SUBTOTAL(9,SWITCH(1,1,A1:A3))")]
    [InlineData("=SUBTOTAL(9,XLOOKUP(1,A1:A3,B1:B3))")]
    [InlineData("=SUBTOTAL(9,LET(r,A1:A3,r))")]
    [InlineData("=LET(r,A1:A3,SUBTOTAL(9,r))")]
    [InlineData("=SUBTOTAL(9,#REF!)")]
    [InlineData("=SUBTOTAL(9,NOSUCHFUNCTION(1))")]
    [InlineData("=SUBTOTAL(9,nosuchname)")]
    [InlineData("=RANK.EQ(1,A1:A3)")]
    [InlineData("=RANK.EQ(A1*1,A1:A3,1)")]
    [InlineData("=AGGREGATE(9,6,A1:A3*1)")]
    [InlineData("=AGGREGATE(14,6,5,1)")]
    [InlineData("=AGGREGATE(14,6,A1:A3*2,1.5)")]
    [InlineData("=AGGREGATE(9,6,B1:B3,5)")]
    [InlineData("=AGGREGATE(14,6,A1:A3,1,B1:B3)")]
    [InlineData("=SUBTOTAL(9+0,A1:A3)")]
    public void ADR0047_a_reference_and_what_can_give_one_are_taken(string formula)
    {
        var sheet = NewSheet();

        sheet.Enter("A5", formula);

        Assert.NotNull(sheet.Value("A5"));
    }

    [Fact] // ADR-0047, ADR-0048: a deletion that takes a Reference away leaves #REF! where only a Reference is taken, and the Formula is kept
    public void ADR0047_a_deletion_leaves_ref_where_only_a_reference_is_taken()
    {
        var sheet = NewSheet();
        sheet.Enter("B1", "1");
        sheet.Enter("A1", "=SUBTOTAL(9,B1:B3)");
        sheet.Enter("A2", "=RANK.EQ(1,B1:B3)");

        sheet.DeleteColumns(1);

        Assert.Equal("=SUBTOTAL(9,#REF!)", sheet.GetEntry(CellAddress.Parse("A1"))!.Formula);
        Assert.Equal(ErrorValue.Ref, sheet.Error("A1"));
        Assert.Equal(ErrorValue.Ref, sheet.Error("A2"));
    }

    [Fact] // ADR-0047, ADR-0048: a Sheet Document holding a Formula Excel refuses on entry is refused when it is read, naming the Formula, as one that cannot be read is
    public void ADR0047_a_document_holding_a_formula_now_refused_on_entry_is_refused()
    {
        const string Json = """{"version":9,"culture":"en-US","name":"Sheet1","cells":[{"at":"A1","formula":"=SUBTOTAL(9,5)"}]}""";

        var refusal = Assert.Throws<SheetDocumentException>(() => SheetDocument.FromJson(Json));

        Assert.Contains("=SUBTOTAL(9,5)", refusal.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0047, ADR-0048: pasted, a Formula Excel refuses on entry is text, as a pasted Formula that cannot be read is
    public void ADR0047_a_pasted_formula_refused_on_entry_is_text()
    {
        var sheet = NewSheet();

        sheet.Do(SheetEdit.PasteText([["=SUBTOTAL(9,5)"]], CellAddress.Parse("A1")));

        Assert.Equal(Value.FromText("=SUBTOTAL(9,5)"), sheet.Value("A1"));
    }
}
