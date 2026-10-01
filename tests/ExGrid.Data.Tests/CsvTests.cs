using System.Globalization;
using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Tests.CsvFixtures;
using static ExGrid.Data.Tests.Fixtures;

namespace ExGrid.Data.Tests;

/// <summary>A CSV read under a Schema (ADR-0063, DA-7): RFC 4180's quoting, the header matched per
/// declared column, the separator and the readings as declared, an empty field a Blank in every kind,
/// and nothing guessed.</summary>
public class CsvTests
{
    [Fact] // ADR-0063: a quoted separator is part of the field
    public void A_quoted_separator_is_part_of_the_field()
    {
        var snapshot = Read(Texts("A", "B"), "A,B\r\n\"x,y\",z\r\n");

        Assert.Equal(["x,y"], Values(snapshot, "A"));
        Assert.Equal(["z"], Values(snapshot, "B"));
    }

    [Fact] // ADR-0063: a doubled quote inside quotes is one quote
    public void A_doubled_quote_is_one_quote()
    {
        var snapshot = Read(Texts("A", "B"), "A,B\n\"say \"\"hi\"\"\",\"\"\"\"\n");

        Assert.Equal(["say \"hi\""], Values(snapshot, "A"));
        Assert.Equal(["\""], Values(snapshot, "B"));
    }

    [Fact] // ADR-0063: a line break inside quotes is part of the field, kept as written
    public void A_line_break_inside_quotes_is_kept_as_written()
    {
        var snapshot = Read(Texts("A", "B"), "A,B\r\n\"one\r\ntwo\",\"three\nfour\"\r\n\"five\rsix\",x\r\n");

        Assert.Equal(["one\r\ntwo", "five\rsix"], Values(snapshot, "A"));
        Assert.Equal(["three\nfour", "x"], Values(snapshot, "B"));
    }

    [Fact] // ADR-0063: a record ends at CR LF, at LF and at CR, and the last needs no line end
    public void A_record_ends_at_any_line_end()
    {
        var snapshot = Read(Texts("A"), "A\r\n1\n2\r3\r\n4");

        Assert.Equal(["1", "2", "3", "4"], Values(snapshot, "A"));
    }

    [Fact] // ADR-0063: a trailing separator makes one more field, which is empty
    public void A_trailing_separator_makes_an_empty_field()
    {
        var snapshot = Read(Texts("A", "B"), "A,B\nx,\n,\n");

        Assert.Equal(["x", null], Values(snapshot, "A"));
        Assert.Equal([null, null], Values(snapshot, "B"));
    }

    [Fact] // ADR-0063: a record cut across any read boundary reads as the whole file does
    public void A_record_cut_across_any_read_boundary_reads_alike()
    {
        var schema = Mixed();
        var bytes = Utf8(MixedFile, bom: true);
        var whole = Dump(Read(schema, bytes));

        // Every buffer size from the smallest, and a stream that gives one byte a read: every record,
        // every field, every quote and every CR LF is cut at every place it can be.
        for (var size = 4; size <= 80; size++)
            Assert.Equal(whole, Dump(Read(schema, bytes, bufferSize: size)));
        for (var chunk = 1; chunk <= 7; chunk++)
            Assert.Equal(whole, Dump(Read(schema, bytes, chunk: chunk)));
    }

    [Fact] // ADR-0063: the header is matched per declared column, in whatever order the file has them
    public void The_header_is_matched_per_declared_column()
    {
        var schema = new CsvSchema([new("Desk", SnapshotKind.Text), new("Qty", SnapshotKind.Integer)]);

        var snapshot = Read(schema, "Qty,Book,Desk\n5,B1,FX\n7,B2,Rates\n");

        Assert.Equal(["Desk", "Qty"], snapshot.Columns.Select(c => c.Name));
        Assert.Equal(["FX", "Rates"], Values(snapshot, "Desk"));
        Assert.Equal([5L, 7L], Values(snapshot, "Qty"));
    }

    [Fact] // ADR-0063: a column matches the header it declares, under a name and a caption of its own
    public void A_column_matches_its_declared_header_under_its_own_name()
    {
        var schema = new CsvSchema([new("Notional", SnapshotKind.Decimal) { Header = "Amount (USD)", Caption = "Notional, USD" }]);

        var snapshot = Read(schema, "Desk,Amount (USD)\nFX,12.5\n");

        Assert.Equal("Notional", snapshot.Columns[0].Name);
        Assert.Equal("Notional, USD", snapshot.Columns[0].Caption);
        Assert.Equal([12.5m], Values(snapshot, "Notional"));
    }

    [Fact] // ADR-0063: the header is matched exactly; case and spaces are not set aside
    public void The_header_is_matched_exactly()
    {
        var refusal = Refusal(Texts("Desk"), "desk\nFX\n");

        Assert.Equal("Column 'Desk': it is missing from the header; the header has 'desk'.", refusal.Message);
    }

    [Fact] // ADR-0063: a declared column missing from the header is refused by name, before any record is read
    public void A_declared_column_missing_from_the_header_is_refused_by_name()
    {
        var schema = new CsvSchema([new("Desk", SnapshotKind.Text), new("Notional", SnapshotKind.Decimal)]);

        var refusal = Refusal(schema, "Desk,Book\nFX,not a number at all\n");

        Assert.Null(refusal.Row);
        Assert.Equal("Notional", refusal.Column);
        Assert.Equal("Column 'Notional': it is missing from the header.", refusal.Message);
    }

    [Fact] // ADR-0063: every declared column missing from the header is named
    public void Every_missing_column_is_named()
    {
        var refusal = Refusal(Texts("A", "B", "C", "D"), "B\nx\n");

        Assert.Equal("A", refusal.Column);
        Assert.Equal("Column 'A': it is missing from the header, and so are 'C' and 'D'.", refusal.Message);
    }

    [Fact] // ADR-0063: a column the Schema does not declare is skipped, whatever it holds
    public void An_undeclared_column_is_skipped()
    {
        var schema = new CsvSchema([new("Qty", SnapshotKind.Integer)]);
        byte[] file = [.. Utf8("Note,Qty,Junk\n\"a, b\",1,"), 0xFF, 0xFE, .. Utf8("\nplain,2,abc\n")];

        var snapshot = Read(schema, file);

        Assert.Single(snapshot.Columns);
        Assert.Equal([1L, 2L], Values(snapshot, "Qty"));
    }

    [Theory] // ADR-0063: the separator is as declared: a comma, a tab or a semicolon
    [InlineData(CsvSeparator.Comma, ",")]
    [InlineData(CsvSeparator.Tab, "\t")]
    [InlineData(CsvSeparator.Semicolon, ";")]
    public void The_separator_is_as_declared(CsvSeparator separator, string character)
    {
        var schema = Texts("A", "B") with { Separator = separator };
        var others = string.Concat(",\t;".Where(c => c.ToString() != character));

        var snapshot = Read(schema, $"A{character}B\nx{others}y{character}z\n");

        Assert.Equal([$"x{others}y"], Values(snapshot, "A"));
        Assert.Equal(["z"], Values(snapshot, "B"));
    }

    [Fact] // ADR-0063: without a header row, columns are fields by their place, or by the position they declare
    public void Without_a_header_columns_are_matched_by_position()
    {
        var schema = new CsvSchema([new("Desk", SnapshotKind.Text), new("Qty", SnapshotKind.Integer) { Position = 3 }]) { HasHeader = false };

        var snapshot = Read(schema, "FX,skip,skip,5\nRates,skip,skip,7\n");

        Assert.Equal(["FX", "Rates"], Values(snapshot, "Desk"));
        Assert.Equal([5L, 7L], Values(snapshot, "Qty"));
    }

    [Fact] // ADR-0063: with a header row, a declared position is matched instead of the header
    public void A_declared_position_wins_over_the_header()
    {
        var schema = new CsvSchema([new("Second", SnapshotKind.Text) { Position = 1 }]);

        var snapshot = Read(schema, "Amount,Amount\n1,2\n");

        Assert.Equal(["2"], Values(snapshot, "Second"));
    }

    [Fact] // ADR-0063: the decimal point is as declared
    public void The_decimal_point_is_as_declared()
    {
        var schema = new CsvSchema([new("V", SnapshotKind.Decimal) { DecimalPoint = "," }]) { Separator = CsvSeparator.Semicolon };

        var snapshot = Read(schema, "V\n12,5\n-0,25\n3\n");

        Assert.Equal([12.5m, -0.25m, 3m], Values(snapshot, "V"));
    }

    [Fact] // ADR-0063: the thousands separator is as declared, and read only between groups of three digits
    public void The_thousands_separator_is_as_declared_and_grouped()
    {
        var schema = new CsvSchema([new("V", SnapshotKind.Decimal) { DecimalPoint = ",", ThousandsSeparator = "." }]) { Separator = CsvSeparator.Semicolon };

        Assert.Equal([1234567.5m, 1234m, 12m], Values(Read(schema, "V\n1.234.567,5\n1.234\n12\n"), "V"));
        // "1.5" is not one thousand and five, and not one and a half: under this reading it is no number.
        Assert.Equal("Row 1, column 'V': '1.5' is not a number (line 2).", Refusal(schema, "V\n1.5\n").Message);
        Assert.Equal("Row 1, column 'V': '12.34.567' is not a number (line 2).", Refusal(schema, "V\n12.34.567\n").Message);
        Assert.Equal("Row 1, column 'V': '1.234,5.6' is not a number (line 2).", Refusal(schema, "V\n1.234,5.6\n").Message);
    }

    [Fact] // ADR-0063: with no thousands separator declared, a comma in a number is refused rather than dropped
    public void Without_a_thousands_separator_a_comma_is_refused()
    {
        var schema = new CsvSchema([new("V", SnapshotKind.Decimal)]);

        Assert.Equal("Row 1, column 'V': '1,234' is not a number (line 2).", Refusal(schema, "V\n\"1,234\"\n").Message);
    }

    [Fact] // ADR-0063: a culture gives the decimal point, the thousands separator, its group sizes and its signs
    public void The_culture_gives_the_reading()
    {
        var german = new CsvSchema([new("V", SnapshotKind.Decimal) { Culture = CultureInfo.GetCultureInfo("de-DE") }]) { Separator = CsvSeparator.Semicolon };
        Assert.Equal([1234.5m, -2m], Values(Read(german, "V\n1.234,5\n-2\n"), "V"));

        // Swedish groups with a no-break space and writes its minus as U+2212; a hyphen-minus is read too.
        var swedish = new CsvSchema([new("V", SnapshotKind.Double) { Culture = CultureInfo.GetCultureInfo("sv-SE") }]) { Separator = CsvSeparator.Semicolon };
        Assert.Equal([-1234.5, -1.5], Values(Read(swedish, "V\n−1 234,5\n-1,5\n"), "V"));

        // India groups by three, then by two.
        var indian = new CsvSchema([new("V", SnapshotKind.Integer) { Culture = CultureInfo.GetCultureInfo("hi-IN") }]);
        Assert.Equal([1234567L], Values(Read(indian, "V\n\"12,34,567\"\n"), "V"));
        Assert.Equal(1, Refusal(indian, "V\n\"1,234,567\"\n").Row);
    }

    [Fact] // ADR-0063: a declared separator wins over the culture's
    public void A_declared_separator_wins_over_the_culture()
    {
        var schema = new CsvSchema([new("V", SnapshotKind.Decimal) { Culture = CultureInfo.GetCultureInfo("de-DE"), ThousandsSeparator = "" }]) { Separator = CsvSeparator.Semicolon };

        Assert.Equal([1234.5m], Values(Read(schema, "V\n1234,5\n"), "V"));
        Assert.Equal(1, Refusal(schema, "V\n1.234,5\n").Row);
    }

    [Fact] // ADR-0063: the date format is as declared
    public void The_date_format_is_as_declared()
    {
        var schema = new CsvSchema([new("When", SnapshotKind.Date) { DateFormats = ["dd.MM.yyyy HH:mm"] }]);

        var snapshot = Read(schema, "When\n30.09.2026 14:05\n01.01.2027 00:00\n");

        Assert.Equal([new DateTime(2026, 9, 30, 14, 5, 0), new DateTime(2027, 1, 1)], Values(snapshot, "When"));
        Assert.Equal(1, Refusal(schema, "When\n2026-09-30 14:05\n").Row);
    }

    [Fact] // ADR-0063: several date formats are tried in order
    public void Several_date_formats_are_tried_in_order()
    {
        var schema = new CsvSchema([new("When", SnapshotKind.Date) { DateFormats = ["yyyy-MM-dd", "yyyy/M/d", "d MMMM yyyy"] }]);

        var snapshot = Read(schema, "When\n2026-09-30\n2026/9/3\n7 October 2026\n");

        Assert.Equal([new DateTime(2026, 9, 30), new DateTime(2026, 9, 3), new DateTime(2026, 10, 7)], Values(snapshot, "When"));
    }

    [Fact] // ADR-0063: a Date column that declares no format reads ISO 8601, with or without a time
    public void A_date_column_reads_iso_8601_by_default()
    {
        var schema = new CsvSchema([new("When", SnapshotKind.Date)]);

        var snapshot = Read(schema, "When\n2026-09-30\n2026-09-30T14:05:06\n2026-09-30 14:05:06.5\n2026-09-30T14:05:06.1234567\n");

        Assert.Equal(
            [new DateTime(2026, 9, 30), new DateTime(2026, 9, 30, 14, 5, 6), new DateTime(2026, 9, 30, 14, 5, 6, 500), new DateTime(2026, 9, 30, 14, 5, 6).AddTicks(1_234_567)],
            Values(snapshot, "When"));
        Assert.Equal("Row 1, column 'When': '30/09/2026' is not a date in any of the formats 'yyyy-MM-dd', 'yyyy-MM-ddTHH:mm:ss', 'yyyy-MM-dd HH:mm:ss', 'yyyy-MM-ddTHH:mm:ss.FFFFFFF', 'yyyy-MM-dd HH:mm:ss.FFFFFFF' (line 2).",
            Refusal(schema, "When\n30/09/2026\n").Message);
    }

    [Fact] // ADR-0063: a date with an offset is held as the clock it shows, with its offset dropped
    public void A_date_with_an_offset_is_held_as_its_clock()
    {
        var schema = new CsvSchema([new("When", SnapshotKind.Date) { DateFormats = ["yyyy-MM-ddTHH:mm:sszzz", "yyyy-MM-ddTHH:mm:ssK"] }]);

        var snapshot = Read(schema, "When\n2026-09-30T10:00:00+09:00\n2026-09-30T10:00:00Z\n2026-09-30T10:00:00-05:00\n");

        Assert.Equal([new DateTime(2026, 9, 30, 10, 0, 0), new DateTime(2026, 9, 30, 10, 0, 0), new DateTime(2026, 9, 30, 10, 0, 0)], Values(snapshot, "When"));
    }

    [Fact] // ADR-0063: a date read from a format's own culture uses its names and separators
    public void A_date_is_read_in_its_culture()
    {
        var schema = new CsvSchema([new("When", SnapshotKind.Date) { DateFormats = ["d. MMMM yyyy", "dd/MM/yyyy"], Culture = CultureInfo.GetCultureInfo("de-DE") }]);

        var snapshot = Read(schema, "When\n30. September 2026\n01.10.2026\n");

        // In a custom format, '/' is the culture's date separator: a full stop in German.
        Assert.Equal([new DateTime(2026, 9, 30), new DateTime(2026, 10, 1)], Values(snapshot, "When"));
    }

    [Fact] // ADR-0063: a time without a date is read on the first day, never on the day it is read
    public void A_time_without_a_date_is_read_on_the_first_day()
    {
        var schema = new CsvSchema([new("At", SnapshotKind.Date) { DateFormats = ["HH:mm"] }]);

        Assert.Equal([new DateTime(1, 1, 1, 14, 5, 0)], Values(Read(schema, "At\n14:05\n"), "At"));
    }

    [Fact] // ADR-0063 (Q55): an empty field is a Blank in every kind, quoted or not, as Excel reads it
    public void An_empty_field_is_a_blank_in_every_kind()
    {
        var schema = new CsvSchema(
        [
            new("T", SnapshotKind.Text), new("M", SnapshotKind.Decimal), new("D", SnapshotKind.Double),
            new("I", SnapshotKind.Integer), new("W", SnapshotKind.Date), new("B", SnapshotKind.Boolean),
        ]);

        var snapshot = Read(schema, "T,M,D,I,W,B\n,,,,,\n\"\",\"\",\"\",\"\",\"\",\"\"\n");

        foreach (var column in snapshot.Columns)
        {
            Assert.Equal([null, null], Values(snapshot, column.Name));
            Assert.True(snapshot.IsBlank(snapshot.Rows[0], column));
        }
        // Unlike a record's empty string, an empty field is not text: the dictionary holds nothing.
        Assert.Empty(((TextColumn)snapshot["T"]).Dictionary);
    }

    [Fact] // ADR-0063: each declared blank text is a Blank in every kind, quoted or not, and only it
    public void Each_declared_blank_text_is_a_blank_in_every_kind()
    {
        string[] blanks = ["NULL", "-"];
        var schema = new CsvSchema(
        [
            new("T", SnapshotKind.Text), new("M", SnapshotKind.Decimal), new("D", SnapshotKind.Double),
            new("I", SnapshotKind.Integer), new("W", SnapshotKind.Date), new("B", SnapshotKind.Boolean),
        ])
        { BlankText = blanks };

        var snapshot = Read(schema, "T,M,D,I,W,B\nNULL,NULL,-,\"NULL\",-,\"-\"\n-,1,2,3,2026-09-30,TRUE\nnull,1,2,3,2026-09-30,TRUE\n");

        foreach (var column in snapshot.Columns)
            Assert.Null(Values(snapshot, column.Name)[0]);
        Assert.Equal([null, null, "null"], Values(snapshot, "T"));
        // Matched exactly: "null" is no declared blank, so a number column refuses it.
        Assert.Equal("Row 1, column 'M': 'null' is not a number (line 2).", Refusal(schema, "T,M,D,I,W,B\nx,null,1,1,2026-09-30,TRUE\n").Message);
    }

    [Fact] // ADR-0063: a column's own blank texts replace the Schema's, and an empty list declares none
    public void A_columns_own_blank_texts_replace_the_schemas()
    {
        var schema = new CsvSchema([new("Note", SnapshotKind.Text) { BlankText = [] }, new("Qty", SnapshotKind.Integer) { BlankText = ["n/a"] }])
        {
            BlankText = ["-"],
        };

        var snapshot = Read(schema, "Note,Qty\n-,n/a\n");

        Assert.Equal(["-"], Values(snapshot, "Note"));
        Assert.Equal([null], Values(snapshot, "Qty"));
    }

    [Fact] // ADR-0063: nothing is guessed: digits read as Text stay exactly as written, leading zeros included
    public void Nothing_is_guessed()
    {
        var schema = new CsvSchema([new("Account", SnapshotKind.Text), new("Qty", SnapshotKind.Integer), new("Code", SnapshotKind.Text)]);

        var snapshot = Read(schema, "Account,Qty,Code\n00123,00123,1.50\n123,123,TRUE\n");

        Assert.Equal(["00123", "123"], Values(snapshot, "Account"));
        Assert.Equal([123L, 123L], Values(snapshot, "Qty"));
        Assert.Equal(["1.50", "TRUE"], Values(snapshot, "Code"));
        Assert.Equal(["00123", "123"], ((TextColumn)snapshot["Account"]).Dictionary);
    }

    [Fact] // ADR-0063: text is kept exactly, spaces and case included; other kinds set the spaces around a value aside
    public void Text_is_exact_and_other_kinds_set_spaces_aside()
    {
        var schema = new CsvSchema([new("T", SnapshotKind.Text), new("V", SnapshotKind.Decimal), new("W", SnapshotKind.Date), new("B", SnapshotKind.Boolean)]);

        var snapshot = Read(schema, "T,V,W,B\n  Amer ,  1.5 , 2026-09-30 , true \nAMER,2,2026-10-01,FALSE\n");

        Assert.Equal(["  Amer ", "AMER"], Values(snapshot, "T"));
        Assert.Equal([1.5m, 2m], Values(snapshot, "V"));
        Assert.Equal([new DateTime(2026, 9, 30), new DateTime(2026, 10, 1)], Values(snapshot, "W"));
        Assert.Equal([true, false], Values(snapshot, "B"));
        // A field of spaces is not empty, so it is no Blank, and it is no number either.
        Assert.Equal("Row 1, column 'V': '  ' is not a number (line 2).", Refusal(schema, "T,V,W,B\nx,  ,2026-09-30,TRUE\n").Message);
    }

    [Fact] // ADR-0063: text read from a CSV takes a dictionary in the order values first appear, as from objects
    public void Text_takes_a_dictionary_in_order_of_first_appearance()
    {
        var snapshot = Read(Texts("V"), "V\nb\na\n\"b\"\nc\na\n");

        Assert.Equal(["b", "a", "c"], ((TextColumn)snapshot["V"]).Dictionary);
        Assert.Equal([0, 1, 0, 2, 1], Codes(snapshot, "V"));
    }

    [Fact] // ADR-0063: a Decimal is read exactly, and 1.5 and 1.50 read back alike
    public void A_decimal_is_read_exactly()
    {
        var schema = new CsvSchema([new("V", SnapshotKind.Decimal)]);

        var snapshot = Read(schema, "V\n0.1\n1.50\n-0.0000000000000000000000000001\n79228162514264337593543950335\n12345678901234567890.123456789\n1.000000000000000000000000000000000000000\n.5\n7.\n");

        Assert.Equal(
            [0.1m, 1.5m, -0.0000000000000000000000000001m, decimal.MaxValue, 12345678901234567890.123456789m, 1m, 0.5m, 7m],
            Values(snapshot, "V"));
        Assert.Equal("1.5", ((decimal)Values(snapshot, "V")[1]!).ToString(CultureInfo.InvariantCulture));
    }

    [Fact] // ADR-0063: a Double is read as .NET reads one, exponent and non-finite values included
    public void A_double_is_read_as_dotnet_reads_one()
    {
        var schema = new CsvSchema([new("V", SnapshotKind.Double)]);

        var snapshot = Read(schema, "V\n0.1\n1.5e-3\n-2E+10\nNaN\nInfinity\n-Infinity\n");

        Assert.Equal([0.1, 0.0015, -2e10, double.NaN, double.PositiveInfinity, double.NegativeInfinity], Values(snapshot, "V"));
    }

    [Fact] // ADR-0063: an Integer is a 64-bit integer, to its limits
    public void An_integer_is_64_bit()
    {
        var schema = new CsvSchema([new("V", SnapshotKind.Integer)]);

        var snapshot = Read(schema, "V\n9223372036854775807\n-9223372036854775808\n+5\n-0\n");

        Assert.Equal([long.MaxValue, long.MinValue, 5L, 0L], Values(snapshot, "V"));
    }

    [Fact] // ADR-0063: a Boolean is read by its declared spellings, ignoring case; TRUE and FALSE by default
    public void A_boolean_is_read_by_its_declared_spellings()
    {
        var excel = new CsvSchema([new("V", SnapshotKind.Boolean)]);
        Assert.Equal([true, false, true], Values(Read(excel, "V\nTRUE\nFALSE\ntrue\n"), "V"));
        Assert.Equal("Row 1, column 'V': '1' is not a spelling of true or false ('TRUE', 'FALSE') (line 2).", Refusal(excel, "V\n1\n").Message);

        var declared = new CsvSchema([new("V", SnapshotKind.Boolean) { TrueText = ["1", "Yes"], FalseText = ["0", "No"] }]);
        Assert.Equal([true, false, true, false], Values(Read(declared, "V\n1\n0\nyes\nNO\n"), "V"));
        Assert.Equal(1, Refusal(declared, "V\nTRUE\n").Row);
    }

    [Fact] // ADR-0063: a Record Key declared in the Schema keys the Snapshot
    public void A_record_key_keys_the_snapshot()
    {
        var schema = new CsvSchema([new("Id", SnapshotKind.Integer), new("Desk", SnapshotKind.Text)]) { RecordKey = "Id" };

        var snapshot = Read(schema, "Id,Desk\n7,FX\n9,Rates\n");

        Assert.Equal("Id", snapshot.RecordKey?.Name);
        var next = snapshot.Apply(ChangeBatch.Of(removedKeys: [7L])).After;
        Assert.Equal(["Rates"], Values(next, "Desk"));
    }

    [Fact] // ADR-0063: the same records read from a CSV and built from objects hold the same values
    public void A_csv_holds_what_the_same_records_hold_as_objects()
    {
        var records = Trades(3_000);
        var fromObjects = Fixtures.Trades().Build(records);
        var text = new System.Text.StringBuilder("Id,Desk,Notional,Price,When,Live\r\n");
        foreach (var t in records)
        {
            text.Append(CultureInfo.InvariantCulture, $"{t.Id},{Quoted(t.Desk)},{t.Notional},{t.Price:R},{t.When:yyyy-MM-ddTHH:mm:ss},{t.Live}\r\n");
        }
        var schema = new CsvSchema(
        [
            new("Id", SnapshotKind.Integer), new("Desk", SnapshotKind.Text), new("Notional", SnapshotKind.Decimal),
            new("Price", SnapshotKind.Double), new("When", SnapshotKind.Date), new("Live", SnapshotKind.Boolean),
        ])
        { RecordKey = "Id" };

        var fromCsv = Read(schema, text.ToString());

        // The empty string is a value among objects but a Blank in a CSV (Q55); every other value is alike.
        foreach (var column in fromObjects.Columns)
        {
            var expected = Values(fromObjects, column.Name).Select(v => v is "" ? null : v);
            Assert.Equal(expected, Values(fromCsv, column.Name));
        }

        static string Quoted(string? text) => text is null ? "" : $"\"{text}\"";
    }

    /// <summary>A Schema with every kind, and a file for it with quotes, line breaks and Blanks.</summary>
    internal static CsvSchema Mixed() => new(
    [
        new("Region", SnapshotKind.Text), new("Notional", SnapshotKind.Decimal), new("Price", SnapshotKind.Double),
        new("Qty", SnapshotKind.Integer), new("When", SnapshotKind.Date), new("Live", SnapshotKind.Boolean),
        new("Note", SnapshotKind.Text),
    ])
    { RecordKey = "Qty" };

    internal const string MixedFile =
        "Region,Notional,Price,Qty,When,Live,Note\r\n"
        + "AMER,1234.5,0.25,1,2026-09-30,TRUE,\"plain, with a comma\"\r\n"
        + "\"APAC\",-0.01,1e3,2,2026-09-30T14:05:06,FALSE,\"say \"\"hi\"\"\"\r\n"
        + "EMEA,,,3,,,\"two\r\nlines\"\r\n"
        + "\"\",99999999999999999999.99,-0,4,2026-10-01 00:00:00.5,true,ｱ東京\r\n"
        + "AMER,0,NaN,5,2026-01-01,false,\"\"\r\n";
}
