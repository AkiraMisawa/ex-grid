using System.Globalization;
using System.Reflection;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Engine.Tests;

/// <summary>A saved Sheet Document's Values computed by ExSheet.Engine alone (ticket 17): SH-17's layer 1 half.</summary>
public class HeadlessTests
{
    /// <summary>A document as a Consumer would have saved it: Entries only, never a Value (ADR-0048).</summary>
    private const string Saved = """
        {
          "version": 1,
          "culture": "de-DE",
          "cells": [
            { "at": "A1", "text": "Nominal" },
            { "at": "B1", "number": 1234.5, "format": "#,##0.00" },
            { "at": "A2", "text": "Rate" },
            { "at": "B2", "number": 0.0375, "format": "0.00%" },
            { "at": "A3", "text": "Start" },
            { "at": "B3", "number": 46292, "format": "dd.mm.yyyy" },
            { "at": "A4", "text": "Interest" },
            { "at": "B4", "formula": "=ROUND(B1*B2,2)", "format": "#,##0.00" },
            { "at": "B5", "formula": "=B3+30", "format": "dd.mm.yyyy" },
            { "at": "B6", "formula": "=IF(B4>40,\"high\",\"low\")" },
            { "at": "B7", "formula": "=SUM(B1,B4)/COUNT(B1:B4)" },
            { "at": "B8", "formula": "=XLOOKUP(\"Rate\",A1:A4,B1:B4)" },
            { "at": "B9", "formula": "=B10+1" },
            { "at": "B10", "formula": "=B9+1" },
            { "at": "B11", "formula": "=1/0" },
            { "at": "B12", "formula": "=IFERROR(B11,-1)" },
            { "at": "B13", "formula": "=SUM(Positions[PV])" }
          ]
        }
        """;

    [Fact] // ADR-0047/0048 (SH-17): a saved document's Values, computed with ExSheet.Engine alone, are the Values Excel gives
    public void A_saved_document_computes_headless()
    {
        var sheet = Sheet.Open(SheetDocument.FromJson(Saved));

        Assert.Equal(46.29, sheet.GetValue(CellAddress.Parse("B4"))!.Value.Number);
        Assert.Equal("46,29", sheet.GetDisplay(CellAddress.Parse("B4")).Text);
        Assert.Equal("27.10.2026", sheet.GetDisplay(CellAddress.Parse("B5")).Text);
        Assert.Equal("high", sheet.GetValue(CellAddress.Parse("B6"))!.Value.Text);
        Assert.Equal((1234.5 + 46.29) / 4, sheet.GetValue(CellAddress.Parse("B7"))!.Value.Number);
        Assert.Equal(0.0375, sheet.GetValue(CellAddress.Parse("B8"))!.Value.Number);
        Assert.Equal(ErrorValue.Circ, sheet.GetValue(CellAddress.Parse("B9"))!.Value.Error);
        Assert.Equal(ErrorValue.Div0, sheet.GetValue(CellAddress.Parse("B11"))!.Value.Error);
        Assert.Equal(-1, sheet.GetValue(CellAddress.Parse("B12"))!.Value.Number);
        Assert.Equal(ErrorValue.Name, sheet.GetValue(CellAddress.Parse("B13"))!.Value.Error);
    }

    [Theory] // ADR-0048 (SH-17): the same document gives the same Values whatever culture the process runs under
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("ja-JP")]
    [InlineData("")]
    public void The_process_culture_plays_no_part(string processCulture)
    {
        var reference = Picture(Sheet.Open(SheetDocument.FromJson(Saved)));
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(processCulture);
            Assert.Equal(reference, Picture(Sheet.Open(SheetDocument.FromJson(Saved))));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact] // ADR-0047/0048 (SH-17): a Sheet edited on one side and saved computes the same Values when opened on another
    public void An_edited_sheet_round_trips_to_the_same_values()
    {
        var edited = Sheet.Open(SheetDocument.FromJson(Saved));
        edited.Do(SheetEdit.InsertRows(0));
        edited.Do(SheetEdit.Fill(CellRange.Parse("B5"), CellRange.Parse("B6"), FillDirection.Down));
        edited.Do(SheetEdit.Paste(edited.Copy(CellRange.Parse("B5")).Block!, CellAddress.Parse("C5")));
        edited.Do(SheetEdit.PasteText([["=B2*2", "1.234,5"]], CellAddress.Parse("D1")));

        var opened = Sheet.Open(SheetDocument.FromJson(edited.ToDocument().ToJson()));

        Assert.Equal(Picture(edited), Picture(opened));
    }

    [Fact] // ADR-0047 (SH-1): nothing the engine loads, directly or through what it references, is Blazor or ours
    public void The_engine_loads_nothing_of_blazor()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<AssemblyName>([typeof(Sheet).Assembly.GetName()]);
        while (pending.Count > 0)
        {
            var name = pending.Dequeue();
            if (!seen.Add(name.Name!)) continue;
            var assembly = Assembly.Load(name);
            foreach (var referenced in assembly.GetReferencedAssemblies()) pending.Enqueue(referenced);
        }

        Assert.DoesNotContain(seen, n => n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        Assert.DoesNotContain(seen, n => n.StartsWith("Microsoft.JSInterop", StringComparison.Ordinal));
        Assert.DoesNotContain(seen, n => n.StartsWith("ExGrid", StringComparison.Ordinal));
    }

    /// <summary>Every held cell's Value and displayed text.</summary>
    private static string Picture(Sheet sheet) =>
        string.Join("\n", sheet.EntryAddresses.Select(a => $"{a}={sheet.GetValue(a)}|{sheet.GetDisplay(a).Text}"));
}
