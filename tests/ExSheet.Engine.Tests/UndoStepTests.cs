using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>Each user operation is one reversible step (ticket 12): SH-13's engine half.</summary>
public class UndoStepTests
{
    private static CellAddress At(string address) => CellAddress.Parse(address);

    /// <summary>Everything a Sheet records and shows: its document, and every held cell's Value.</summary>
    private static string Picture(Sheet sheet) =>
        sheet.ToDocument().ToJson() + "\n" + string.Join("\n", sheet.EntryAddresses.Select(a => $"{a}={sheet.GetValue(a)}"));

    private static Sheet Populated()
    {
        var sheet = NewSheet();
        sheet.Enter("A1", "1");
        sheet.Enter("A2", "2");
        sheet.Enter("A3", "3");
        sheet.Enter("A5", "5");
        sheet.Enter("B1", "=SUM(A1:A5)");
        sheet.Enter("B2", "=A3*10");
        sheet.Enter("B3", "=$A$5+A2");
        sheet.Enter("C1", "=SUM(A:A)");
        sheet.Enter("C2", "=B2+B3");
        sheet.Enter("D4", "2026-09-27");
        sheet.SetFormat(At("A2"), NumberFormat.Parse("0.00"));
        sheet.SetAlignment(At("B3"), HorizontalAlignment.Center);
        sheet.SetFormat(At("E1048576"), NumberFormat.Parse("0%"));
        sheet.SetFormat(At("XFD9"), NumberFormat.Parse("0%"));
        return sheet;
    }

    public static TheoryData<string> Operations =>
    [
        "enter", "enter formula", "clear", "batched enter", "set entries", "format", "alignment",
        "insert rows", "insert rows inside", "delete rows", "delete referenced row", "insert columns", "delete columns",
    ];

    private static SheetEdit Operation(string name) => name switch
    {
        "enter" => SheetEdit.Enter(At("A3"), "30"),
        "enter formula" => SheetEdit.Enter(At("A4"), "=B2/2"),
        "clear" => SheetEdit.Enter(At("A1"), ""),
        "batched enter" => SheetEdit.Enter([new(At("A1"), "10"), new(At("F1"), "12%"), new(At("A2"), "=C1"), new(At("G7"), "9/28/2026")]),
        "set entries" => SheetEdit.SetEntries([new(At("A5"), null), new(At("B9"), Entry.FromFormula("=B1"))]),
        "format" => SheetEdit.SetFormat([At("A1"), At("A2"), At("Z1")], NumberFormat.Parse("#,##0")),
        "alignment" => SheetEdit.SetAlignment([At("B3"), At("B4")], HorizontalAlignment.Right),
        "insert rows" => SheetEdit.InsertRows(0, 2),
        "insert rows inside" => SheetEdit.InsertRows(2),
        "delete rows" => SheetEdit.DeleteRows(1, 2),
        "delete referenced row" => SheetEdit.DeleteRows(4),
        "insert columns" => SheetEdit.InsertColumns(1, 3),
        "delete columns" => SheetEdit.DeleteColumns(0),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Theory] // ADR-0048 (SH-13): undoing a step restores every Entry, format, Reference and Value exactly
    [MemberData(nameof(Operations))]
    public void Undo_after_do_is_the_identity(string operation)
    {
        var sheet = Populated();
        var before = Picture(sheet);

        var step = sheet.Do(Operation(operation));
        Assert.NotEqual(before, Picture(sheet));
        step.Undo();

        Assert.Equal(before, Picture(sheet));
    }

    [Theory] // ADR-0048 (SH-13): redoing a step gives exactly what doing it gave
    [MemberData(nameof(Operations))]
    public void Redo_after_undo_is_the_same_as_doing(string operation)
    {
        var sheet = Populated();
        var step = sheet.Do(Operation(operation));
        var done = Picture(sheet);

        step.Undo();
        step.Redo();

        Assert.Equal(done, Picture(sheet));
    }

    [Fact] // ADR-0048 (SH-13): steps undo in reverse order and redo in order, back to each state
    public void A_stack_of_steps_walks_back_and_forth()
    {
        var sheet = Populated();
        var pictures = new List<string> { Picture(sheet) };
        var steps = new List<SheetStep>();
        foreach (var name in new[] { "delete referenced row", "enter formula", "insert columns", "batched enter", "delete rows", "format" })
        {
            steps.Add(sheet.Do(Operation(name)));
            pictures.Add(Picture(sheet));
        }

        for (var i = steps.Count - 1; i >= 0; i--)
        {
            steps[i].Undo();
            Assert.Equal(pictures[i], Picture(sheet));
        }
        for (var i = 0; i < steps.Count; i++)
        {
            steps[i].Redo();
            Assert.Equal(pictures[i + 1], Picture(sheet));
        }
    }

    [Fact] // ADR-0047/0048 (SH-5): one undo restores a Formula a deletion made #REF!, and the range it shrank
    public void Undoing_a_deletion_restores_the_references()
    {
        var sheet = NewSheet();
        sheet.Enter("A2", "2");
        sheet.Enter("A3", "3");
        sheet.Enter("A4", "4");
        sheet.Enter("C1", "=A3*2");
        sheet.Enter("D1", "=SUM(A2:A4)");
        sheet.Enter("E1", "=SUM(A3:A4)");

        var step = sheet.Do(SheetEdit.DeleteRows(2));
        Assert.Equal("=#REF!*2", sheet.GetEntry(At("C1"))!.Formula);
        Assert.Equal("=SUM(A2:A3)", sheet.GetEntry(At("D1"))!.Formula);
        Assert.Equal("=SUM(A3:A3)", sheet.GetEntry(At("E1"))!.Formula);

        var change = step.Undo();

        Assert.Equal("=A3*2", sheet.GetEntry(At("C1"))!.Formula);
        Assert.Equal("=SUM(A2:A4)", sheet.GetEntry(At("D1"))!.Formula);
        Assert.Equal("=SUM(A3:A4)", sheet.GetEntry(At("E1"))!.Formula);
        Assert.Equal(6, sheet.Number("C1"));
        Assert.Equal(9, sheet.Number("D1"));
        Assert.Equal(7, sheet.Number("E1"));
        Assert.Equal([0, 2, 3], change.Rows);
    }

    [Fact] // ADR-0048: the change an undo reports names the rows it repainted
    public void An_undo_reports_its_change()
    {
        var sheet = Populated();
        var step = sheet.Do(SheetEdit.Enter(At("A3"), "30"));
        Assert.Equal(["B1", "C1", "B2", "C2", "A3"], step.Change.ValueChanges.Addresses());

        var change = step.Undo();

        Assert.Equal(["B1", "C1", "B2", "C2", "A3"], change.ValueChanges.Addresses());
        Assert.Equal([0, 1, 2], change.Rows);
    }

    [Fact] // ADR-0048: a refused operation is no step, and changes nothing
    public void A_refused_operation_is_no_step()
    {
        var sheet = NewSheet();
        sheet.Enter("A1048576", "1");
        var before = Picture(sheet);

        var edit = SheetEdit.InsertRows(0);

        Assert.Equal(SheetRefusalReason.EntriesWouldLeaveSheet, sheet.Check(edit)!.Reason);
        Assert.Throws<SheetRefusedException>(() => sheet.Do(edit));
        Assert.Equal(before, Picture(sheet));
    }

    [Fact] // ADR-0047/0048: an unreadable Formula in a batch is no step, and changes nothing
    public void An_unreadable_formula_is_no_step()
    {
        var sheet = Populated();
        var before = Picture(sheet);

        Assert.Throws<FormulaSyntaxException>(() => sheet.Do(SheetEdit.Enter([new(At("A1"), "7"), new(At("A2"), "=SUM(")])));

        Assert.Equal(before, Picture(sheet));
    }

    [Fact] // ADR-0048: a step is undone once, and redone only after being undone
    public void A_step_cannot_be_undone_twice()
    {
        var sheet = NewSheet();
        var step = sheet.Do(SheetEdit.Enter(At("A1"), "1"));

        Assert.Throws<InvalidOperationException>(() => step.Redo());
        step.Undo();
        Assert.True(step.IsUndone);
        Assert.Throws<InvalidOperationException>(() => step.Undo());
    }

    [Fact] // ADR-0048: formatting an insertion pushed off the edge comes back with the undo
    public void Undoing_an_insertion_restores_formatting_pushed_off_the_edge()
    {
        var sheet = NewSheet();
        sheet.SetFormat(At("A1048576"), NumberFormat.Parse("0.00"));
        sheet.Enter("A1", "1");

        var step = sheet.Do(SheetEdit.InsertRows(0));
        Assert.True(sheet.GetFormat(At("A1048576")).IsGeneral);
        step.Undo();

        Assert.Equal("0.00", sheet.GetFormat(At("A1048576")).Code);
    }
}
