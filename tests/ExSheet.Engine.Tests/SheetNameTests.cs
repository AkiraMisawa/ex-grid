using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

public class SheetNameTests
{
    private static string Formula(Sheet sheet, string address) => sheet.GetEntry(CellAddress.Parse(address))!.Formula!;

    [Fact] // ADR-0046: a Sheet is named Sheet1 unless it is given a name
    public void A_sheet_is_named_sheet1_by_default()
    {
        Assert.Equal("Sheet1", NewSheet().Name);
        Assert.Equal("Positions", new Sheet(EnUs, "Positions").Name);
    }

    [Theory] // ADR-0046: a Reference qualified with the Sheet's own name reads its cells, without regard to case
    [InlineData("=Sheet1!A1*2")]
    [InlineData("=sheet1!A1*2")]
    [InlineData("='Sheet1'!A1*2")]
    [InlineData("=(SUM(Sheet1!A1:A2)-Sheet1!A2)*2")]
    public void A_reference_qualified_with_the_sheets_own_name_resolves(string formula)
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "21");
        sheet.Enter("A2", "5");

        Assert.Equal(42, sheet.Evaluate(formula).Number);
    }

    [Fact] // ADR-0046: a name that needs quoting is read quoted, and resolves
    public void A_quoted_name_resolves()
    {
        var sheet = new Sheet(EnUs, "My Sheet");
        sheet.Enter("A1", "3");

        Assert.Equal(6, sheet.Evaluate("='My Sheet'!A1*2").Number);
        Assert.Equal(ErrorValue.Ref, sheet.Evaluate("=Sheet1!A1").Error);
    }

    [Fact] // ADR-0046: any other qualifier stays #REF!
    public void Any_other_qualifier_is_ref()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "3");

        Assert.Equal(ErrorValue.Ref, sheet.Evaluate("=Sheet2!A1").Error);
        Assert.Equal(ErrorValue.Ref, sheet.Evaluate("='Sheet1 '!A1").Error);
    }

    [Fact] // ADR-0046/0047: a Formula qualified with the Sheet's name recalculates when the cell it reads changes
    public void A_qualified_reference_is_a_dependency()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("B1", "=Sheet1!A1+1");
        sheet.Enter("C1", "=SUM(Sheet1!A1:A3)");

        var change = sheet.Enter("A1", "10");

        Assert.Equal(11, sheet.Number("B1"));
        Assert.Equal(10, sheet.Number("C1"));
        Assert.Equal(["A1", "B1", "C1"], change.ValueChanges.Addresses());
    }

    [Fact] // ADR-0046/0047: a cycle through the Sheet's own name is a cycle
    public void A_cycle_through_a_qualified_reference_is_circ()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=Sheet1!B1");
        sheet.Enter("B1", "=A1");

        Assert.Equal(ErrorValue.Circ, sheet.Error("A1"));
        Assert.Equal(ErrorValue.Circ, sheet.Error("B1"));
    }

    [Fact] // ADR-0046/0047: an insertion rewrites a Reference qualified with the Sheet's own name, keeping the qualifier
    public void An_insertion_rewrites_a_reference_qualified_with_the_own_name()
    {
        var sheet = NewSheet();
        sheet.Enter("A5", "4");
        sheet.Enter("B1", "=Sheet1!A5+Sheet2!A5");

        sheet.InsertRows(0);

        Assert.Equal("=Sheet1!A6+Sheet2!A5", Formula(sheet, "B2"));
        Assert.Equal(ErrorValue.Ref, sheet.Error("B2"));
    }

    [Fact] // ADR-0046/0047: a deletion of every cell a qualified Reference names makes it #REF!
    public void A_deletion_makes_a_qualified_reference_ref()
    {
        var sheet = NewSheet();
        sheet.Enter("A5", "4");
        sheet.Enter("B1", "=Sheet1!A5*2");

        sheet.DeleteRows(4);

        Assert.Equal("=#REF!*2", Formula(sheet, "B1"));
    }

    [Theory] // ADR-0046: Excel's rules for a Sheet's name
    [InlineData("")]
    [InlineData("a:b")]
    [InlineData("a\\b")]
    [InlineData("a/b")]
    [InlineData("a?b")]
    [InlineData("a*b")]
    [InlineData("a[b")]
    [InlineData("a]b")]
    [InlineData("'quoted")]
    [InlineData("quoted'")]
    [InlineData("History")]
    [InlineData("history")]
    [InlineData("12345678901234567890123456789012")]
    public void An_invalid_name_is_refused(string name)
    {
        Assert.False(Sheet.IsValidName(name, out var reason));
        Assert.NotNull(reason);
        Assert.Throws<ArgumentException>(() => new Sheet(EnUs, name));
        Assert.Throws<ArgumentException>(() => NewSheet().Rename(name));
        Assert.Throws<ArgumentException>(() => SheetEdit.Rename(name));
    }

    [Theory] // ADR-0046: names Excel accepts
    [InlineData("Sheet1")]
    [InlineData("My Sheet")]
    [InlineData("It's")]
    [InlineData("1234567890123456789012345678901")]
    [InlineData("損益 (円)")]
    [InlineData("A1")]
    [InlineData("History2")]
    public void A_valid_name_is_accepted(string name)
    {
        Assert.True(Sheet.IsValidName(name, out var reason));
        Assert.Null(reason);
        Assert.Equal(name, new Sheet(EnUs, name).Name);
    }

    [Theory] // ADR-0046/0047: a qualifier is written bare when Excel writes it bare, quoted otherwise
    [InlineData("Sheet1", "=Sheet1!A1")]
    [InlineData("My Sheet", "='My Sheet'!A1")]
    [InlineData("It's", "='It''s'!A1")]
    [InlineData("A1", "='A1'!A1")]
    [InlineData("R1C1", "='R1C1'!A1")]
    [InlineData("R", "='R'!A1")]
    [InlineData("TRUE", "='TRUE'!A1")]
    [InlineData("1st", "='1st'!A1")]
    public void A_qualifier_is_quoted_when_it_must_be(string name, string written)
    {
        var sheet = new Sheet(EnUs, name);
        sheet.Enter("A1", "9");
        sheet.Enter("B1", written);

        Assert.Equal(written, Formula(sheet, "B1"));
        Assert.Equal(9, sheet.Number("B1"));
    }

    [Fact] // ADR-0046: renaming rewrites every Reference qualified with the old name, as Excel does, and leaves other qualifiers alone
    public void Renaming_rewrites_references_qualified_with_the_old_name()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "5");
        sheet.Enter("B1", "=Sheet1!A1+1");
        sheet.Enter("B2", "=SHEET1!A1:A2");
        sheet.Enter("B3", "=Other!A1");

        sheet.Rename("Risk Q3");

        Assert.Equal("Risk Q3", sheet.Name);
        Assert.Equal("='Risk Q3'!A1+1", Formula(sheet, "B1"));
        Assert.Equal("='Risk Q3'!A1:A2", Formula(sheet, "B2"));
        Assert.Equal("=Other!A1", Formula(sheet, "B3"));
        Assert.Equal(6, sheet.Number("B1"));
        Assert.Equal(ErrorValue.Ref, sheet.Error("B3"));
    }

    [Fact] // ADR-0046: a qualifier that comes to name the Sheet after a rename resolves, and one that stops is #REF!
    public void Resolution_follows_the_current_name()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "5");
        sheet.Enter("B1", "=Data!A1");
        Assert.Equal(ErrorValue.Ref, sheet.Error("B1"));

        var change = sheet.Rename("Data");

        Assert.Equal(5, sheet.Number("B1"));
        Assert.Contains(CellAddress.Parse("B1"), change.ValueChanges);
        sheet.Enter("A1", "6");
        Assert.Equal(6, sheet.Number("B1"));
    }

    [Fact] // ADR-0046/0048: undoing a rename puts back the name and every Formula exactly, and redoing does it again
    public void Undoing_a_rename_restores_exactly()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "5");
        sheet.Enter("B1", "=Sheet1!A1");
        sheet.Enter("B2", "=Data!A1");
        var before = sheet.ToDocument().ToJson();

        var step = sheet.Do(SheetEdit.Rename("Data"));
        Assert.Equal("=Data!A1", Formula(sheet, "B1"));
        Assert.Equal(5, sheet.Number("B2"));

        step.Undo();
        Assert.Equal("Sheet1", sheet.Name);
        Assert.Equal(before, sheet.ToDocument().ToJson());
        Assert.Equal(5, sheet.Number("B1"));
        Assert.Equal(ErrorValue.Ref, sheet.Error("B2"));

        step.Redo();
        Assert.Equal("Data", sheet.Name);
        Assert.Equal("=Data!A1", Formula(sheet, "B1"));
        Assert.Equal(5, sheet.Number("B2"));
    }
}
