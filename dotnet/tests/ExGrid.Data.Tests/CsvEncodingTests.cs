using System.Reflection;
using System.Text;
using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Tests.CsvFixtures;
using static ExGrid.Data.Tests.Fixtures;

namespace ExGrid.Data.Tests;

/// <summary>
/// A CSV is read in UTF-8, with or without its byte-order mark, and in Shift-JIS when its Schema
/// declares it (ADR-0064, DA-8). Only the opt-in encoding refers to the code pages, so an application
/// that never asks for Shift-JIS never loads them — and, trimmed for a browser, never downloads them.
/// </summary>
public class CsvEncodingTests
{
    /// <summary>Shift-JIS as Windows writes it, for the tests to write files in.</summary>
    private static readonly Encoding Cp932 = CodePagesEncodingProvider.Instance.GetEncoding(932)!;

    private static readonly CsvSchema Japanese = new(
    [
        new("取引先", SnapshotKind.Text), new("金額", SnapshotKind.Decimal) { Culture = System.Globalization.CultureInfo.GetCultureInfo("ja-JP") },
        new("確定", SnapshotKind.Boolean) { TrueText = ["はい"], FalseText = ["いいえ"] },
    ])
    {
        Encoding = CsvEncoding.ShiftJis,
        BlankText = ["なし"],
    };

    /// <summary>
    /// A file as Excel on Japanese Windows saves one. 表, ソ and 能 end in the byte 0x5C, a backslash
    /// in ASCII; ｱ is a single byte above 0x7F; a quoted field holds the separator and a line break.
    /// </summary>
    private const string JapaneseFile = "取引先,金額,確定\r\n東京表,\"1,234.5\",はい\r\nソ能ｱ,-5,いいえ\r\n\"大阪, 本店\r\n2階\",なし,なし\r\n";

    [Fact] // ADR-0064: UTF-8 is read with or without its byte-order mark, alike
    public void Utf8_is_read_with_or_without_its_byte_order_mark()
    {
        var schema = CsvTests.Mixed();

        var without = Read(schema, Utf8(CsvTests.MixedFile));
        var with = Read(schema, Utf8(CsvTests.MixedFile, bom: true));

        Assert.Equal(Dump(without), Dump(with));
        Assert.Equal("Region", without.Columns[0].Name);
        Assert.Equal(["AMER", "APAC", "EMEA", null, "AMER"], Values(with, "Region"));
    }

    [Fact] // ADR-0064: Shift-JIS is read when the Schema declares it: headers, text, numbers, Booleans and blank texts
    public void Shift_jis_is_read_when_the_schema_declares_it()
    {
        var snapshot = Read(Japanese, Cp932.GetBytes(JapaneseFile));

        Assert.Equal(["取引先", "金額", "確定"], snapshot.Columns.Select(c => c.Name));
        Assert.Equal(["東京表", "ソ能ｱ", "大阪, 本店\r\n2階"], Values(snapshot, "取引先"));
        Assert.Equal([1234.5m, -5m, null], Values(snapshot, "金額"));
        Assert.Equal([true, false, null], Values(snapshot, "確定"));
    }

    [Fact] // ADR-0064: a Shift-JIS record cut across any read boundary, a character's two bytes included, reads as the whole file does
    public void A_shift_jis_record_cut_across_any_read_boundary_reads_alike()
    {
        var bytes = Cp932.GetBytes(JapaneseFile);
        var whole = Dump(Read(Japanese, bytes));

        for (var size = 4; size <= 48; size++)
            Assert.Equal(whole, Dump(Read(Japanese, bytes, bufferSize: size)));
        Assert.Equal(whole, Dump(Read(Japanese, bytes, chunk: 1)));
    }

    [Fact] // ADR-0064: a file that begins with UTF-8's byte-order mark is read as UTF-8, whatever the Schema declares
    public void A_utf8_byte_order_mark_wins_over_a_declared_encoding()
    {
        var snapshot = Read(Japanese, Utf8(JapaneseFile, bom: true));

        Assert.Equal(["東京表", "ソ能ｱ", "大阪, 本店\r\n2階"], Values(snapshot, "取引先"));
    }

    [Fact] // ADR-0064: Shift-JIS read where UTF-8 is declared is refused, naming the row and the column, rather than garbled
    public void Shift_jis_read_as_utf8_is_refused()
    {
        var schema = new CsvSchema([new("Name", SnapshotKind.Text), new("Qty", SnapshotKind.Integer)]);

        var refusal = Refusal(schema, [.. Utf8("Name,Qty\nok,1\n"), .. Cp932.GetBytes("東京"), .. Utf8(",2\n")]);

        Assert.Equal("Row 2, column 'Name': the text is not valid UTF-8 (line 3).", refusal.Message);
    }

    [Fact] // ADR-0064: bytes that are not Shift-JIS are refused by row and column when Shift-JIS is declared
    public void Invalid_shift_jis_is_refused()
    {
        var schema = new CsvSchema([new("Name", SnapshotKind.Text), new("Qty", SnapshotKind.Integer)]) { Encoding = CsvEncoding.ShiftJis };

        // 0x82 begins a two-byte character, and a comma cannot end one.
        var refusal = Refusal(schema, [.. Utf8("Name,Qty\nok,1\n"), 0x41, 0x82, .. Utf8(",2\n")]);

        Assert.Equal("Row 2, column 'Name': the text is not valid Shift-JIS (line 3).", refusal.Message);
        Assert.Equal("Shift-JIS", CsvEncoding.ShiftJis.ToString());
        Assert.Same(CsvEncoding.ShiftJis, CsvEncoding.ShiftJis);
    }

    [Fact] // ADR-0064 (DA-8): only the opt-in encoding refers to the code pages, so an application that never asks for them never loads them
    public void Only_the_opt_in_encoding_refers_to_the_code_pages()
    {
        var referrers = AssemblyInspection.Referrers(typeof(CsvEncoding).Assembly.Location, "System.Text.Encoding.CodePages");

        Assert.Equal(["ExGrid.Data.CsvEncoding.get_ShiftJis"], referrers);
        // Never inlined, so a Consumer's method that mentions it in a branch it does not take is
        // compiled without loading the code pages.
        var getter = typeof(CsvEncoding).GetProperty(nameof(CsvEncoding.ShiftJis))!.GetMethod!;
        Assert.True(getter.MethodImplementationFlags.HasFlag(MethodImplAttributes.NoInlining));
    }

    [Fact] // ADR-0064 (DA-8): the package is trimmable, so a browser application that never asks for Shift-JIS publishes without the code pages
    public void The_package_is_trimmable_so_a_browser_never_downloads_the_code_pages()
    {
        // Blazor trims an assembly member by member only when it is marked trimmable; otherwise it
        // keeps all of it, CsvEncoding.ShiftJis included, and with it System.Text.Encoding.CodePages
        // (686 KB) in every application. Measured with a published Blazor WebAssembly application,
        // 2026-10-01: reading only UTF-8, it ships no code pages; asking for Shift-JIS, it does.
        var marks = typeof(CsvEncoding).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>();

        Assert.Contains(marks, m => m.Key == "IsTrimmable" && m.Value == "True");
    }
}
