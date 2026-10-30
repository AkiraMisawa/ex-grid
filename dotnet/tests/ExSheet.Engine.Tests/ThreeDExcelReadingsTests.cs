using System.Text.Json;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Engine.Tests.SheetTestExtensions;

namespace ExSheet.Engine.Tests;

/// <summary>
/// 3D References against Excel's own answers (ADR-0331 §7, exsheet-workbook tickets 11 and 12): every case
/// the 2026-10-24 Windows run put to Excel, from <c>ThreeDReadings/excel-readings.json</c>, which
/// <c>verification/2026-10-24-windows-workbook-12/to-fixture.py</c> wrote from its records. Each Workbook is
/// the run's: Out, then Sheet1 to Sheet5, B2 on SheetN holding 10^(N-1) and C3 twice that, so a sum names the
/// Sheets it read.
/// </summary>
public class ThreeDExcelReadingsTests
{
    private static readonly JsonElement Readings = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ThreeDReadings", "excel-readings.json"))).RootElement;

    private static readonly string[] SheetNames = ["Out", "Sheet1", "Sheet2", "Sheet3", "Sheet4", "Sheet5"];

    /// <summary>
    /// The forms ExSheet does not read for any Reference, a 3D one's or another's, and refuses on entry: the
    /// intersection operator, and the range operator between a Reference and anything but its own cells.
    /// </summary>
    private static readonly string[] NotRead =
    [
        "=SUM(Sheet2:Sheet4!B2:C3 Sheet2:Sheet4!B2)",
        "=SUM(Sheet2:Sheet4!B2:Sheet2:Sheet4!C3)",
        "=SUM(Sheet2:Sheet4!A1:INDEX(Sheet2!A:A,2))",
    ];

    public static TheoryData<string, string, string> Formulas()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var c in Readings.GetProperty("formulas").EnumerateArray()) data.Add(c.GetProperty("formula").GetString()!, c.GetProperty("setup").GetString()!, c.GetProperty("cell").GetString()!);
        return data;
    }

    public static TheoryData<string> Cases(string group)
    {
        var data = new TheoryData<string>();
        foreach (var c in Readings.GetProperty(group).EnumerateArray()) data.Add(c.GetProperty("id").GetString()!);
        return data;
    }

    private static Workbook Run(bool more = false)
    {
        var workbook = new Workbook(EnUs, SheetNames);
        // RANDBETWEEN draws only once the host has given a seed (ADR-0180).
        workbook.SetSeed(7);
        for (var n = 1; n <= 5; n++)
        {
            var value = Math.Pow(10, n - 1);
            Enter(workbook, "Sheet" + n, "B2", value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Enter(workbook, "Sheet" + n, "C3", (2 * value).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        if (more)
        {
            Enter(workbook, "Out", "C1", "1");
            Enter(workbook, "Out", "C2", "2");
            workbook.Do("Sheet2", SheetEdit.SetEntries([new(CellAddress.Parse("D4"), Entry.FromValue(Value.FromText("5")))]));
            Enter(workbook, "Sheet3", "D4", "x");
            Enter(workbook, "Sheet3", "D5", "TRUE");
            Enter(workbook, "Sheet4", "D6", "=1/0");
            Enter(workbook, "Sheet2", "E1", "=NA()");
            Enter(workbook, "Sheet4", "E1", "=1/0");
        }
        return workbook;
    }

    private static void Enter(Workbook workbook, string sheet, string address, string typed) =>
        workbook.Do(sheet, SheetEdit.Enter(CellAddress.Parse(address), typed));

    private static JsonElement Case(string group, string id) =>
        Readings.GetProperty(group).EnumerateArray().Single(c => c.GetProperty("id").GetString() == id);

    /// <summary>Excel's answer in a cell of the record against the engine's, the Formula's text and its Value.</summary>
    private static void AssertCell(Workbook workbook, string cell, JsonElement excel, string because)
    {
        var parts = cell.Split('!');
        var sheet = workbook.Find(parts[0])!;
        var address = CellAddress.Parse(parts[1]);
        var formula = excel.GetProperty("formula").GetString()!;
        // ExSheet writes a Reference to a Sheet that is gone #REF!, where Excel writes #REF! before its cells
        // (ticket 12's answer 11, kept as ticket 03 built it).
        formula = formula.Replace("#REF!B2:C3", "#REF!", StringComparison.Ordinal).Replace("#REF!B2", "#REF!", StringComparison.Ordinal);
        Assert.True(formula == (sheet.GetEntry(address)?.Formula ?? ""), $"{because}: {cell} is written {sheet.GetEntry(address)?.Formula}, Excel {formula}");
        AssertValue(sheet.GetValue(address), excel, $"{because}: {cell}");
    }

    private static void AssertValue(Value? value, JsonElement excel, string because)
    {
        if (excel.TryGetProperty("error", out var error))
        {
            Assert.True(value is { Kind: ValueKind.Error, IsRefused: false } v && v.Error.ToText() == error.GetString(), $"{because}: {value?.ToString() ?? "blank"}, Excel {error.GetString()}");
        }
        else if (excel.TryGetProperty("number", out var number))
        {
            Assert.True(value is { Kind: ValueKind.Number }, $"{because}: {value?.ToString() ?? "blank"}, Excel {number.GetDouble()}");
            Assert.Equal(number.GetDouble(), value!.Value.Number, 9);
        }
        else if (excel.TryGetProperty("boolean", out var boolean))
        {
            Assert.True(value is { Kind: ValueKind.Boolean } v && v.Boolean == boolean.GetBoolean(), $"{because}: {value?.ToString() ?? "blank"}, Excel {boolean.GetBoolean()}");
        }
        else if (excel.TryGetProperty("text", out var text))
        {
            Assert.True(value is { Kind: ValueKind.Text } v && v.Text == text.GetString(), $"{because}: {value?.ToString() ?? "blank"}, Excel \"{text.GetString()}\"");
        }
        else
        {
            Assert.True(value is null, $"{because}: {value}, Excel blank");
        }
    }

    [Theory] // Ticket 12's answers 1 and 6: what each function, and each form around one, answers
    [MemberData(nameof(Formulas))]
    public void A_3d_reference_answers_as_excel_did(string formula, string setup, string at)
    {
        var excel = Readings.GetProperty("formulas").EnumerateArray().First(c =>
            c.GetProperty("formula").GetString() == formula && c.GetProperty("setup").GetString() == setup && c.GetProperty("cell").GetString() == at);
        var workbook = Run(more: setup == "more");
        var cell = CellAddress.Parse(at);
        if (NotRead.Contains(formula))
        {
            Assert.Throws<FormulaSyntaxException>(() => workbook.Do("Out", SheetEdit.Enter(cell, formula)));
            return;
        }

        workbook.Do("Out", SheetEdit.Enter(cell, formula));

        var written = excel.GetProperty("written").GetString()!;
        Assert.Equal(written, workbook.Find("Out")!.GetEntry(cell)?.Formula);
        AssertValue(workbook.Find("Out")!.GetValue(cell), excel, formula);
    }

    [Theory] // Ticket 12's answers 2 and 3: the span as Sheets are moved, deleted, inserted and copied
    [MemberData(nameof(Cases), "sheets")]
    public void A_span_follows_its_sheets_as_excel_did(string id)
    {
        var excel = Case("sheets", id);
        var workbook = Run();
        Enter(workbook, "Out", "A1", "=SUM(Sheet2:Sheet4!B2)");
        Enter(workbook, "Out", "A2", "=SUM(Sheet4:Sheet2!B2)");
        Enter(workbook, "Out", "A3", "=SUM(Sheet2:Sheet3!B2)");
        Enter(workbook, "Out", "A4", "=SUM(Sheet3:Sheet3!B2)");

        var before = workbook.Sheets.Select(s => s.Name).ToList();
        var steps = SheetOperation(workbook, id);

        Assert.Equal([.. excel.GetProperty("order").EnumerateArray().Select(n => n.GetString())], workbook.Sheets.Select(s => s.Name));
        foreach (var cell in excel.GetProperty("read").EnumerateObject()) AssertCell(workbook, cell.Name, cell.Value, id);

        // Undone, the Sheets and every Formula are as they were.
        foreach (var step in Enumerable.Reverse(steps)) step.Undo();
        Assert.Equal(before, workbook.Sheets.Select(s => s.Name));
        Assert.Equal("=SUM(Sheet2:Sheet4!B2)", workbook.Find("Out")!.GetEntry(CellAddress.Parse("A1"))?.Formula);
        Assert.Equal("=SUM(Sheet2:Sheet3!B2)", workbook.Find("Out")!.GetEntry(CellAddress.Parse("A3"))?.Formula);
        Assert.Equal(1110, workbook.Find("Out")!.Number("A1"));
    }

    /// <summary>The Sheet operation a case of the record names, as the engine's own: its steps, in the order done.</summary>
    private static List<WorkbookStep> SheetOperation(Workbook workbook, string id)
    {
        var parts = id.Split('-');
        int At(string name) => workbook.Sheets.Select(s => s.Name).ToList().IndexOf(name);
        string Name(string part) => part == "out" ? "Out" : "Sheet" + part;
        switch (parts[0])
        {
            case "move":
                var moved = Name(parts[1]);
                var to = parts[2] switch
                {
                    "to" when parts[3] == "end" => workbook.Sheets.Count - 1,
                    "to" => 0,
                    "before" => At(Name(parts[3])) - (At(moved) < At(Name(parts[3])) ? 1 : 0),
                    _ => At(Name(parts[3])) + (At(moved) < At(Name(parts[3])) ? 0 : 1),
                };
                return [workbook.Do(moved, SheetEdit.MoveSheet(to))];
            case "delete":
                return [.. parts.Skip(1).Where(p => p != "then").Select(part => workbook.Do(Name(part), SheetEdit.DeleteSheet()))];
            case "insert":
                return
                [
                    workbook.Do(Name(parts[2]), SheetEdit.InsertSheet("New", before: parts[1] == "before")),
                    workbook.Do("New", SheetEdit.Enter(CellAddress.Parse("B2"), "7")),
                ];
            case "copy":
                var source = Name(parts[1]);
                var target = Name(parts[3]);
                return [workbook.Do(source, SheetEdit.CopySheet(At(target) + (parts[2] == "after" ? 1 : 0)))];
            case "rename":
                return [workbook.Do(Name(parts[1]), SheetEdit.Rename("Mid"))];
            default:
                throw new ArgumentException(id);
        }
    }

    [Theory] // Ticket 12's answer 4: rows and columns inserted or deleted on a Sheet of a span never rewrite it
    [MemberData(nameof(Cases), "rows")]
    public void Rows_under_a_span_leave_it_as_excel_did(string id)
    {
        var excel = Case("rows", id);
        var workbook = Run();
        Enter(workbook, "Out", "A1", "=SUM(Sheet2:Sheet4!B2)");
        Enter(workbook, "Out", "A2", "=SUM(Sheet2:Sheet4!B2:C3)");
        Enter(workbook, "Out", "A3", "=SUM(Sheet2:Sheet4!$B$2)");
        Enter(workbook, "Sheet3", "A1", "=SUM(Sheet2:Sheet4!B2)");
        Enter(workbook, "Sheet1", "A1", "=SUM(Sheet2:Sheet4!B2)");
        Enter(workbook, "Out", "A5", "=Sheet3!B2");

        var parts = id.Split('-');
        var sheet = parts[0] switch { "out" => "Out", "all" => null, _ => "Sheet" + parts[0][^1] };
        SheetEdit Edit() => (parts[1], parts[2], parts[3]) switch
        {
            ("insert", "row", var n) => SheetEdit.InsertRows(int.Parse(n, System.Globalization.CultureInfo.InvariantCulture) - 1),
            ("delete", "row", var n) => SheetEdit.DeleteRows(int.Parse(n, System.Globalization.CultureInfo.InvariantCulture) - 1),
            ("insert", "col", var c) => SheetEdit.InsertColumns(c[0] - 'A'),
            ("delete", "col", var c) => SheetEdit.DeleteColumns(c[0] - 'A'),
            _ => throw new ArgumentException(id),
        };
        if (parts[2] == "cells")
        {
            // Cells shifted down or left under B2 alone: not a Sheet operation ExSheet has.
            return;
        }
        foreach (var name in sheet is null ? ["Sheet2", "Sheet3", "Sheet4"] : new[] { sheet }) workbook.Do(name, Edit());

        foreach (var cell in excel.GetProperty("read").EnumerateObject()) AssertCell(workbook, cell.Name, cell.Value, id);
    }

    [Theory] // Ticket 12's answers 5 and 12: a copy of a Sheet whose own Formulas hold a span, and its own name
    [MemberData(nameof(Cases), "copies")]
    public void A_copy_writes_its_spans_as_excel_did(string id)
    {
        var excel = Case("copies", id);
        var workbook = Run();
        var parts = id.Split('-');
        var source = "Sheet" + parts[1];
        Enter(workbook, source, "A1", "=SUM(Sheet2:Sheet4!B2)");
        Enter(workbook, source, "A2", $"={source}!B2");
        Enter(workbook, source, "A3", "=Sheet1!B2");
        Enter(workbook, source, "A4", "=B2");
        Enter(workbook, source, "A5", $"=SUM({source}!B2:C3)");
        Enter(workbook, source, "A6", $"=SUM({source}:Sheet5!B2)");

        int At(string name) => workbook.Sheets.Select(s => s.Name).ToList().IndexOf(name);
        var index = parts[2] switch
        {
            "to" when parts[3] == "end" => workbook.Sheets.Count,
            "to" => 0,
            "before" => At(source),
            _ => At(source) + 1,
        };
        if (parts[2] == "after" && parts[3] != parts[1]) index = At("Sheet" + parts[3]) + 1;
        workbook.Do(source, SheetEdit.CopySheet(index));

        Assert.Equal([.. excel.GetProperty("order").EnumerateArray().Select(n => n.GetString())], workbook.Sheets.Select(s => s.Name));
        foreach (var cell in excel.GetProperty("read").EnumerateObject()) AssertCell(workbook, cell.Name, cell.Value, id);
    }
}
