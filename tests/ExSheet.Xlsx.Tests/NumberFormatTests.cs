using System.Globalization;
using ExGrid.Data.Xlsx;
using ExSheet.Engine;
using Xunit;
using static ExSheet.Xlsx.Tests.Book;

namespace ExSheet.Xlsx.Tests;

/// <summary>
/// Number Formats cross both ways (ADR-0330 §4, ticket 03): every built-in id the engine reads is its format, every other is
/// refused by name, a format of the file's own is read as written, and every format the engine holds is written as the id or
/// the code that reads back as it. A format that cannot cross refuses the file, naming the Sheet, the cell and the code: shown
/// in General instead, a date would show as a serial, or a currency lose its symbol.
/// </summary>
public class NumberFormatTests
{
    /// <summary>The built-ins the engine reads, by id, in its invariant codes.</summary>
    public static readonly Dictionary<int, string> Readable = new()
    {
        [0] = "General", [1] = "0", [2] = "0.00", [3] = "#,##0", [4] = "#,##0.00",
        [9] = "0%", [10] = "0.00%", [11] = "0.00E+00",
        [14] = "m/d/yyyy", [15] = "d-mmm-yy", [16] = "d-mmm", [17] = "mmm-yy", [18] = "h:mm AM/PM", [19] = "h:mm:ss AM/PM",
        [20] = "h:mm", [21] = "h:mm:ss", [22] = "m/d/yyyy h:mm",
        [37] = "#,##0 ;(#,##0)", [38] = "#,##0 ;[Red](#,##0)", [39] = "#,##0.00;(#,##0.00)", [40] = "#,##0.00;[Red](#,##0.00)",
        [45] = "mm:ss", [49] = "@",
    };

    /// <summary>The built-ins whose fixed code the engine does not read: fractions, elapsed time, tenths of a second, engineering notation.</summary>
    public static readonly Dictionary<int, string> FixedButUnread = new()
    {
        [12] = "# ?/?", [13] = "# ??/??", [46] = "[h]:mm:ss", [47] = "mmss.0", [48] = "##0.0E+0",
    };

    public static TheoryData<int> EveryBuiltInId => [.. Enumerable.Range(0, XlsxNumberFormats.FirstCustomId)];

    private static XlsxStyles StylesOf(int numberFormatId, IReadOnlyDictionary<int, string>? codes = null) => XlsxStyles.Default with
    {
        NumberFormats = codes ?? new Dictionary<int, string>(),
        CellFormats = [new XlsxCellFormat(), new XlsxCellFormat(numberFormatId)],
    };

    private static Task<byte[]> OneCellAsync(XlsxStyles styles, XlsxCell? cell = null) =>
        WriteAsync(new XlsxWriteOptions { Styles = styles }, new Page("Trades", [cell ?? Number("B7", "45000", style: 1)]));

    [Theory] // ADR-0330 §4: every built-in id below 164 crosses as the engine's format of its meaning, or is refused by name
    [MemberData(nameof(EveryBuiltInId))]
    public async Task ADR0330_every_built_in_Number_Format_crosses_or_is_refused_by_name(int id)
    {
        var file = await OneCellAsync(StylesOf(id));

        if (Readable.TryGetValue(id, out var code))
        {
            var cell = (await ReadAsync(file)).Document.Sheet("Trades").Cells.Single();
            // General is every cell's default, which a cell records as nothing.
            Assert.Equal(id == 0 ? null : code, cell.NumberFormat?.Code);
            return;
        }
        var refused = await RefusedAsync(file);
        Assert.Equal(XlsxOpenRefusal.NumberFormat, refused.Reason);
        Assert.Equal("Trades", refused.Sheet);
        Assert.Equal("B7", refused.Cell);
        Assert.Equal(FixedButUnread.TryGetValue(id, out var unread) ? unread : $"Number Format {id}", refused.Held);
        Assert.StartsWith("Sheet 'Trades', cell B7 (", refused.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0330 §4: the built-in short date is the engine's, shown in the Workbook's culture as Excel shows it in the computer's
    public async Task ADR0330_the_built_in_short_date_shows_in_the_Workbooks_culture()
    {
        var file = await OneCellAsync(StylesOf(14));

        var (document, _) = await ReadAsync(file, CultureInfo.GetCultureInfo("en-GB"));

        Assert.Equal("15/03/2023", Workbook.Open(document).Sheets[0].GetDisplay(CellAddress.Parse("B7")).Text);
    }

    [Theory] // ADR-0330 §4: a format of the file's own is read as written, and one that spells a built-in is the file's own still
    [InlineData("#,##0.00;[Red]-#,##0.00", "#,##0.00;[Red]-#,##0.00")]
    [InlineData("yyyy-mm-dd", "yyyy-mm-dd")]
    [InlineData("0.000%", "0.000%")]
    [InlineData("\"USD\" #,##0", "\"USD\" #,##0")]
    [InlineData("[Blue]0;[Red]-0;0;@", "[Blue]0;[Red]-0;0;@")]
    [InlineData("0.00E+00", "0.00E+00")]
    [InlineData("m/d/yyyy", "m\\/d\\/yyyy")]
    [InlineData("d-mmm-yy", "d\\-mmm\\-yy")]
    [InlineData("h:mm", "h\\:mm")]
    [InlineData("$#,##0.00_);[Red]($#,##0.00)", "\\$#,##0.00_);[Red](\\$#,##0.00)")]
    [InlineData("d\\-mmm\\-yy", "d\\-mmm\\-yy")]
    public async Task ADR0330_a_format_of_the_files_own_is_read_as_written(string stored, string code)
    {
        var file = await OneCellAsync(StylesOf(164, new Dictionary<int, string> { [164] = stored }));

        var cell = (await ReadAsync(file)).Document.Sheet("Trades").Cells.Single();

        Assert.Equal(code, cell.NumberFormat!.Code);
    }

    [Fact] // ADR-0330 §4: a file's own m/d/yyyy shows as it is spelled under every culture, where the built-in shows the culture's date
    public async Task ADR0330_a_files_own_short_date_shows_as_spelled()
    {
        var file = await OneCellAsync(StylesOf(164, new Dictionary<int, string> { [164] = "m/d/yyyy" }));

        var (document, _) = await ReadAsync(file, CultureInfo.GetCultureInfo("en-GB"));

        Assert.Equal("3/15/2023", Workbook.Open(document).Sheets[0].GetDisplay(CellAddress.Parse("B7")).Text);
    }

    [Theory] // ADR-0330 §4: a format of the file's own the engine does not read refuses the file, naming the Sheet, the cell and the code
    [InlineData("[$-409]mmmm d, yyyy")]
    [InlineData("_(* #,##0_);_(* (#,##0);_(* \"-\"_);_(@_)")]
    [InlineData("# ?/?")]
    [InlineData("[Color10]0")]
    [InlineData("[<100]0;0")]
    [InlineData("[h]:mm:ss")]
    [InlineData("[$€-407]#,##0.00")]
    [InlineData("[$-F800]dddd, mmmm dd, yyyy")] // the Home tab's Long Date
    [InlineData("[$-F400]h:mm:ss AM/PM")] // the Home tab's Time
    [InlineData("[h]:mm")]
    [InlineData("mm:ss.0")]
    public async Task ADR0330_a_format_the_engine_does_not_read_refuses_the_file(string code)
    {
        var file = await OneCellAsync(StylesOf(164, new Dictionary<int, string> { [164] = code }));

        var refused = await RefusedAsync(file);

        Assert.Equal(XlsxOpenRefusal.NumberFormat, refused.Reason);
        Assert.Equal(("Trades", "B7", code), (refused.Sheet, refused.Cell, refused.Held));
        Assert.Contains("not one the engine reads", refused.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0330 §4: a text cell in a format the engine does not read refuses the file too: the format is the cell's, whatever it holds
    public async Task ADR0330_a_text_cell_in_an_unread_format_refuses_the_file()
    {
        var file = await OneCellAsync(StylesOf(164, new Dictionary<int, string> { [164] = "# ?/?" }), Text("C2", "note") with { StyleIndex = 1 });

        Assert.Equal("C2", (await RefusedAsync(file)).Cell);
    }

    [Fact] // ADR-0330 §4: a built-in whose code follows the locale is carried as the code the file spells for it, and refused by it
    public async Task ADR0330_a_locale_built_in_is_the_code_the_file_spells()
    {
        var spelled = await OneCellAsync(StylesOf(5, new Dictionary<int, string> { [5] = "\"$\"#,##0_);\\(\"$\"#,##0\\)" }));
        var accounting = await OneCellAsync(StylesOf(44, new Dictionary<int, string> { [44] = "_(\"$\"* #,##0.00_);_(\"$\"* \\(#,##0.00\\);_(\"$\"* \"-\"??_);_(@_)" }));

        Assert.Equal("\"$\"#,##0_);\\(\"$\"#,##0\\)", (await ReadAsync(spelled)).Document.Sheet("Trades").Cells.Single().NumberFormat!.Code);
        Assert.Equal(XlsxOpenRefusal.NumberFormat, (await RefusedAsync(accounting)).Reason);
    }

    [Theory] // ADR-0330 §4: Excel's built-in ids, as ECMA-376 Part 1 §18.8.30 lists them, show as Excel shows them under en-US
    [InlineData(3, "1234567.891", "1,234,568")] // #,##0
    [InlineData(10, "0.1234", "12.34%")] // 0.00%
    [InlineData(14, "45000", "3/15/2023")] // mm-dd-yy, which Excel shows in the computer's short date
    [InlineData(22, "45000.5", "3/15/2023 12:00")] // m/d/yy h:mm, likewise
    [InlineData(49, "45000", "45000")] // @
    public async Task ADR0330_built_in_ids_show_as_ECMA_376_lists_them(int id, string value, string shown)
    {
        var file = await OneCellAsync(StylesOf(id), Number("B7", value, style: 1));

        var (document, _) = await ReadAsync(file, EnUs);

        Assert.Equal(shown, Workbook.Open(document).Sheets[0].GetDisplay(CellAddress.Parse("B7")).Text);
    }

    [Theory] // ADR-0330 §4: a built-in currency (5 to 8) is the code the file spells: a euro amount opens in euros under en-US
    [InlineData(8, """#,##0.00\ "€";[Red]\-#,##0.00\ "€" """, "1,000.00 €")]
    [InlineData(6, """#,##0\ "€";[Red]\-#,##0\ "€" """, "1,000 €")]
    [InlineData(7, """ "£"#,##0.00;\-"£"#,##0.00""", "£1,000.00")]
    [InlineData(5, """ "¥"#,##0;"¥"\-#,##0""", "¥1,000")]
    public async Task ADR0330_a_built_in_currency_is_the_code_the_file_spells(int id, string code, string shown)
    {
        var file = await OneCellAsync(StylesOf(id, new Dictionary<int, string> { [id] = code.Trim() }), Number("B7", "1000", style: 1));

        var (document, _) = await ReadAsync(file, EnUs);

        Assert.Equal(shown, Workbook.Open(document).Sheets[0].GetDisplay(CellAddress.Parse("B7")).Text);
    }

    [Theory] // ADR-0330 §4: a built-in currency the file does not spell is refused, naming its id: never shown in the reader's currency
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task ADR0330_a_built_in_currency_the_file_does_not_spell_is_refused(int id)
    {
        var refused = await RefusedAsync(await OneCellAsync(StylesOf(id)));

        Assert.Equal((XlsxOpenRefusal.NumberFormat, $"Number Format {id}"), (refused.Reason, refused.Held));
        Assert.Contains("follows the computer's locale", refused.Message, StringComparison.Ordinal);
    }

    [Fact] // ADR-0330 §4: a built-in the engine knows is that built-in, whatever code the styles part writes beside its id
    public async Task ADR0330_a_known_built_in_ignores_a_code_written_over_it()
    {
        var file = await OneCellAsync(StylesOf(14, new Dictionary<int, string> { [14] = "mm-dd-yy" }));

        Assert.Equal("m/d/yyyy", (await ReadAsync(file)).Document.Sheet("Trades").Cells.Single().NumberFormat!.Code);
    }

    [Fact] // ADR-0330 §4: only a format a cell, a row or a column takes is read; one no level takes refuses nothing
    public async Task ADR0330_a_format_no_level_takes_refuses_nothing()
    {
        var file = await OneCellAsync(StylesOf(164, new Dictionary<int, string> { [164] = "# ?/?" }), Number("A1", "1"));

        var (document, report) = await ReadAsync(file);

        Assert.Equal(Value.FromNumber(1), document.Sheet("Trades").EntryAt("A1")!.Constant);
        Assert.True(report.IsEmpty);
    }

    [Fact] // ADR-0330 §4: a column or a row in a format the engine does not read refuses the file, naming the columns or the row
    public async Task ADR0330_a_column_or_row_in_an_unread_format_is_refused_by_name()
    {
        var styles = StylesOf(46);
        var column = await WriteAsync(new XlsxWriteOptions { Styles = styles }, new Page("Trades", [], [new XlsxColumn(1, 2, null, StyleIndex: 1)]));
        var row = await WriteAsync(new XlsxWriteOptions { Styles = styles }, new Page("Trades", [], Rows: [new XlsxRow(4, 1)]));

        var byColumn = await RefusedAsync(column);
        var byRow = await RefusedAsync(row);

        Assert.Equal(("B:C", "[h]:mm:ss"), (byColumn.Cell, byColumn.Held));
        Assert.Equal(("5:5", "[h]:mm:ss"), (byRow.Cell, byRow.Held));
        Assert.Equal(XlsxOpenRefusal.NumberFormat, byRow.Reason);
    }

    // ---- writing ---------------------------------------------------------------------------------------------------------

    public static TheoryData<int> ReadableIds => [.. Readable.Keys];

    [Theory] // ADR-0330 §4: every built-in the engine reads is written as its own id, and reads back as itself
    [MemberData(nameof(ReadableIds))]
    public async Task ADR0330_every_built_in_is_written_as_its_id_and_reads_back(int id)
    {
        var format = NumberFormat.Parse(Readable[id]);
        Assert.Equal(id, XlsxNumberFormats.BuiltInId(format));

        var (written, read) = await RoundTripAsync(format);

        Assert.Equal(id, written);
        Assert.Equal(format, read ?? NumberFormat.General);
    }

    [Theory] // ADR-0330 §4: a built-in the engine shows in the culture's form is it in any case; it is written as its id, not as the file's own
    [InlineData("M/D/YYYY", 14)]
    [InlineData("D-MMM-YY", 15)]
    [InlineData("H:MM", 20)]
    public void ADR0330_a_localised_built_in_in_another_case_is_its_id(string code, int id)
    {
        Assert.Equal(id, XlsxNumberFormats.BuiltInId(NumberFormat.Parse(code)));
    }

    [Theory] // ADR-0330 §4: any other format the engine holds is written as the file's own, from 164, and reads back as itself
    [InlineData("#,##0.00;[Red]-#,##0.00")]
    [InlineData("yyyy-mm-dd")]
    [InlineData("0.000%")]
    [InlineData("\"USD\" #,##0")]
    [InlineData("[Blue]0;[Red]-0;0;@")]
    [InlineData("m\\/d\\/yyyy")]
    [InlineData("d\\-mmm\\-yy")]
    [InlineData("0.00e+00")]
    [InlineData("dd. mmm")]
    [InlineData("hh:mm")]
    [InlineData("0;[Red]@")]
    public async Task ADR0330_a_format_of_its_own_is_written_as_the_files_own_and_reads_back(string code)
    {
        var format = NumberFormat.Parse(code);
        Assert.Null(XlsxNumberFormats.BuiltInId(format));

        var (written, read) = await RoundTripAsync(format);

        Assert.Equal(XlsxNumberFormats.FirstCustomId, written);
        Assert.Equal(code, read!.Code);
    }

    [Theory] // ADR-0330 §4: the engine's built-in currency is written as a code of the file's own spelling the Workbook's currency, so it keeps its symbol under any locale
    [InlineData("de-DE", "€")]
    [InlineData("ja-JP", "¥")]
    [InlineData("en-GB", "£")]
    [InlineData("en-US", "$")]
    public async Task ADR0330_a_workbooks_built_in_currency_is_written_spelled_and_keeps_its_symbol(string name, string symbol)
    {
        var culture = CultureInfo.GetCultureInfo(name);
        var sheet = new SheetDocument(culture, "Trades",
            [new SheetDocumentCell(CellAddress.Parse("B7"), Entry.FromValue(Value.FromNumber(1234.5)), NumberFormat.BuiltInCurrency(culture), null, null, null, null)]);
        var workbook = new WorkbookDocument(culture, "Trades", [sheet]);
        var file = await Saved.WriteAsync(workbook);

        using var reader = await XlsxReader.OpenAsync(new MemoryStream(file), cancellationToken: Token);
        var id = reader.Styles.CellFormats[(await reader.ReadSheetAsync(0)).Cells.Single().StyleIndex].NumberFormatId;
        var before = Workbook.Open(workbook).Sheets[0].GetDisplay(CellAddress.Parse("B7")).Text;
        var after = Workbook.Open((await ReadAsync(file, EnUs)).Document).Sheets[0].GetDisplay(CellAddress.Parse("B7")).Text;

        Assert.True(id >= XlsxNumberFormats.FirstCustomId, $"written as {id}");
        Assert.Contains(symbol, reader.Styles.NumberFormats[id], StringComparison.Ordinal);
        Assert.Contains(symbol, after, StringComparison.Ordinal);
        // The same amount and symbol; only the separators are the reader's culture's.
        Assert.Equal(WithoutSeparators(before), WithoutSeparators(after));
    }

    private static string WithoutSeparators(string shown) =>
        new([.. shown.Where(c => c is not ('.' or ',' or ' ' or ' ' or ' '))]);

    [Theory] // ADR-0330 §6, ADR-0071: a code of its own that spells the Workbook culture's built-in is that built-in once the Workbook is open, as Excel records it, and is written as its id
    [InlineData("m\\/d\\/yyyy", 14)]
    [InlineData("d\\-mmm\\-yy", 15)]
    public async Task ADR0330_a_code_that_spells_the_cultures_built_in_is_written_as_the_built_in(string code, int id)
    {
        var (written, _) = await RoundTripAsync(NumberFormat.Parse(code), "en-US");

        Assert.Equal(id, written);
    }

    /// <summary>
    /// A cell in <paramref name="format"/> written from a Workbook of the culture <paramref name="name"/>: the id its cell format
    /// names, and the format read back. Under ja-JP no code of the theory above spells a built-in in the culture's own form.
    /// </summary>
    private static async Task<(int Written, NumberFormat? Read)> RoundTripAsync(NumberFormat format, string name = "ja-JP")
    {
        var culture = CultureInfo.GetCultureInfo(name);
        var sheet = new SheetDocument(culture, "Trades", [new SheetDocumentCell(CellAddress.Parse("B7"), Entry.FromValue(Value.FromNumber(45000.25)), format, null, null, null, null)]);
        var file = await Saved.WriteAsync(new WorkbookDocument(culture, "Trades", [sheet]));

        using var reader = await XlsxReader.OpenAsync(new MemoryStream(file), cancellationToken: Token);
        var cell = (await reader.ReadSheetAsync(0)).Cells.Single();
        var written = reader.Styles.CellFormats[cell.StyleIndex].NumberFormatId;
        var read = (await ReadAsync(file)).Document.Sheet("Trades").Cells.Single();
        return (written, read.NumberFormat);
    }
}
