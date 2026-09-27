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
