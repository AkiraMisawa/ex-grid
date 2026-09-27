using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

public class TracerTests
{
    [Fact] // ADR-0046/0047: editing A1 updates C1, and the change names exactly the cells whose Values changed
    public void Editing_a_precedent_reports_every_changed_value()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "2");
        sheet.Enter("B1", "3");
        sheet.Enter("C1", "=A1+B1");
        sheet.Enter("A5", "=C1*10");
        sheet.Enter("A9", "7");

        var change = sheet.Enter("A1", "4");

        Assert.Equal(7, sheet.Number("C1"));
        Assert.Equal(70, sheet.Number("A5"));
        Assert.Equal(["A1", "C1", "A5"], change.ValueChanges.Addresses());
        Assert.Equal([0, 4], change.Rows);
    }

    [Fact] // ADR-0046: an edit whose Values do not change repaints nothing else
    public void An_edit_that_leaves_a_dependent_unchanged_does_not_report_it()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "2");
        sheet.Enter("B3", "=A1*0");

        var change = sheet.Enter("A1", "5");

        Assert.Equal(["A1"], change.ValueChanges.Addresses());
        Assert.Equal([0], change.Rows);
    }

    [Fact] // ADR-0046: typing the same Entry again is no change
    public void Entering_the_same_entry_again_changes_nothing()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "2");

        var change = sheet.Enter("A1", "2");

        Assert.Empty(change.ValueChanges);
        Assert.Empty(change.Rows);
    }

    [Fact] // ADR-0047: a Formula that cannot be read is refused, and nothing changes
    public void An_unreadable_formula_is_refused_and_nothing_changes()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "=1+1");

        Assert.Throws<FormulaSyntaxException>(() => sheet.Enter("A1", "=1+"));
        Assert.Throws<FormulaSyntaxException>(() => sheet.Enter("A1", "=(1"));
        Assert.Throws<FormulaSyntaxException>(() => sheet.Enter("A1", "="));

        Assert.Equal(2, sheet.Number("A1"));
        Assert.Equal("=1+1", sheet.GetEntryText(CellAddress.Parse("A1")));
    }

    [Fact] // ADR-0047: a batch with one unreadable Formula changes nothing at all
    public void A_batch_with_one_unreadable_formula_changes_nothing()
    {
        var sheet = NewSheet();

        Assert.Throws<FormulaSyntaxException>(() => sheet.Enter(
        [
            new(CellAddress.Parse("A1"), "1"),
            new(CellAddress.Parse("A2"), "=*"),
        ]));

        Assert.Null(sheet.Value("A1"));
    }

    [Fact] // ADR-0048: the Cell Editor reopens on the Entry, and the Formula is in invariant syntax
    public void The_entry_text_is_the_formula_written_back()
    {
        var sheet = NewSheet();
        sheet.Enter("C1", "=a1+b1");
        sheet.Enter("A1", "2.5");
        sheet.Enter("A2", "Tokyo");

        Assert.Equal("=A1+B1", sheet.GetEntryText(CellAddress.Parse("C1")));
        Assert.Equal("2.5", sheet.GetEntryText(CellAddress.Parse("A1")));
        Assert.Equal("Tokyo", sheet.GetEntryText(CellAddress.Parse("A2")));
        Assert.Equal("", sheet.GetEntryText(CellAddress.Parse("A3")));
    }

    [Fact] // ADR-0048: text typed with a leading apostrophe reopens with it (its Value is in ExcelCases/typed-constants.json)
    public void An_apostrophe_reopens_with_the_text()
    {
        var sheet = NewSheet();

        sheet.Enter("A1", "'123");

        Assert.Equal("'123", sheet.GetEntryText(CellAddress.Parse("A1")));
    }

    [Fact] // ADR-0048: clearing a cell makes it blank, and its dependents see 0
    public void Clearing_a_cell_makes_it_blank()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "2");
        sheet.Enter("B1", "=A1+1");

        var change = sheet.Enter("A1", "");

        Assert.Null(sheet.Value("A1"));
        Assert.Null(sheet.GetEntry(CellAddress.Parse("A1")));
        Assert.Equal(1, sheet.Number("B1"));
        Assert.Equal(["A1", "B1"], change.ValueChanges.Addresses());
    }

    [Fact] // ADR-0048: the Sheet Document round-trips: out, in, the same Values
    public void The_sheet_document_round_trips()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "2");
        sheet.Enter("B1", "3");
        sheet.Enter("C1", "=A1+B1");
        sheet.Enter("D1", "Tokyo");
        sheet.Enter("E1", "TRUE");
        sheet.Enter("F1", "#N/A");
        sheet.Enter("XFD1048576", "=C1*2");

        var json = sheet.ToDocument().ToJson();
        var reopened = Sheet.Open(SheetDocument.FromJson(json));

        foreach (var address in sheet.EntryAddresses)
        {
            Assert.Equal(sheet.GetValue(address), reopened.GetValue(address));
            Assert.Equal(sheet.GetEntry(address), reopened.GetEntry(address));
        }
        Assert.Equal(sheet.EntryAddresses, reopened.EntryAddresses);
        Assert.Equal(10, reopened.Number("XFD1048576"));
    }

    [Fact] // ADR-0048: a Sheet Document holds Entries and never Values
    public void The_sheet_document_holds_no_values()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "2");
        sheet.Enter("B1", "=A1*21");

        var json = sheet.ToDocument().ToJson();

        Assert.Contains("\"=A1*21\"", json);
        Assert.DoesNotContain("42", json);
    }
}
