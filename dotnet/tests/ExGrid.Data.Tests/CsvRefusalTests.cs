using ExGrid.Data;
using ExGrid.Data.Csv;
using Xunit;
using static ExGrid.Data.Tests.CsvFixtures;

namespace ExGrid.Data.Tests;

/// <summary>A CSV that cannot be read fails whole, naming the row — the data record, from one — the
/// column and the line it begins on, and yields no Snapshot (ADR-0064, DA-6).</summary>
public class CsvRefusalTests
{
    private static readonly CsvSchema Trades = new(
    [
        new("Desk", SnapshotKind.Text), new("Notional", SnapshotKind.Decimal), new("Qty", SnapshotKind.Integer),
        new("When", SnapshotKind.Date), new("Price", SnapshotKind.Double),
    ]);

    [Fact] // ADR-0064: a value its kind cannot read fails the whole load, naming the row, the column and the line
    public void A_value_that_is_not_a_number_is_refused_by_row_and_column()
    {
        var refusal = Refusal(Trades, "Desk,Notional,Qty,When,Price\nFX,1,1,2026-09-30,1\nFX,abc,1,2026-09-30,1\n");

        Assert.Equal(2, refusal.Row);
        Assert.Equal("Notional", refusal.Column);
        Assert.Equal("Row 2, column 'Notional': 'abc' is not a number (line 3).", refusal.Message);
    }

    [Fact] // ADR-0064: the row counts data records, and the line is where the record begins, line breaks in quotes counted
    public void The_row_counts_records_and_the_line_counts_lines()
    {
        var refusal = Refusal(Trades, "Desk,Notional,Qty,When,Price\n\"F\r\nX\",1,1,2026-09-30,1\n\"F\nX\",1,1,2026-09-30,x\n");

        Assert.Equal("Row 2, column 'Price': 'x' is not a number (line 4).", refusal.Message);
    }

    [Fact] // ADR-0064: the row is numbered as a person reads it, with its thousands separated
    public void The_row_is_named_as_a_person_reads_it()
    {
        var text = new System.Text.StringBuilder("Qty\n");
        for (var i = 0; i < 12_344; i++)
            text.Append("1\n");
        text.Append("x\n");

        var refusal = Refusal(new CsvSchema([new("Qty", SnapshotKind.Integer)]), text.ToString());

        Assert.Equal(12_345, refusal.Row);
        Assert.Equal("Row 12,345, column 'Qty': 'x' is not an integer (line 12,346).", refusal.Message);
    }

    [Fact] // ADR-0064: an impossible date is refused
    public void An_impossible_date_is_refused()
    {
        var schema = new CsvSchema([new("When", SnapshotKind.Date) { DateFormats = ["yyyy-MM-dd"] }]);

        Assert.Equal("Row 1, column 'When': '2026-02-30' is not a date in the format 'yyyy-MM-dd' (line 2).", Refusal(schema, "When\n2026-02-30\n").Message);
        Assert.Equal(1, Refusal(schema, "When\n2026-13-01\n").Row);
        Assert.Equal(1, Refusal(schema, "When\n0000-01-01\n").Row);
        Assert.Equal(1, Refusal(schema, "When\n2026-9-30\n").Row);
    }

    [Fact] // ADR-0064: an integer beyond 64 bits is refused rather than wrapped, and a decimal point is no integer
    public void An_integer_overflow_is_refused()
    {
        var schema = new CsvSchema([new("Qty", SnapshotKind.Integer)]);

        Assert.Equal("Row 1, column 'Qty': '9223372036854775808' is outside the range of a 64-bit Integer (line 2).", Refusal(schema, "Qty\n9223372036854775808\n").Message);
        Assert.Equal("Row 1, column 'Qty': '-99999999999999999999999999999999999999999' is outside the range of a 64-bit Integer (line 2).",
            Refusal(schema, "Qty\n-99999999999999999999999999999999999999999\n").Message);
        Assert.Equal("Row 1, column 'Qty': '12.0' is not an integer (line 2).", Refusal(schema, "Qty\n12.0\n").Message);
    }

    [Fact] // ADR-0064: a decimal that a decimal cannot hold exactly is refused rather than rounded
    public void A_decimal_that_does_not_fit_is_refused()
    {
        var schema = new CsvSchema([new("V", SnapshotKind.Decimal)]);

        Assert.Equal("Row 1, column 'V': '79228162514264337593543950336' has more digits than a Decimal holds exactly (line 2).", Refusal(schema, "V\n79228162514264337593543950336\n").Message);
        Assert.Equal(1, Refusal(schema, "V\n1.0000000000000000000000000000001\n").Row);
        Assert.Equal(1, Refusal(schema, "V\n0.00000000000000000000000000001\n").Row);
    }

    [Fact] // ADR-0064: a finite number too large for a Double is refused rather than read as infinity
    public void A_double_out_of_range_is_refused()
    {
        var schema = new CsvSchema([new("V", SnapshotKind.Double)]);

        Assert.Equal("Row 1, column 'V': '1e400' is outside the range of a Double (line 2).", Refusal(schema, "V\n1e400\n").Message);
        Assert.Equal("Row 1, column 'V': '1,5' is not a number (line 2).", Refusal(schema, "V\n\"1,5\"\n").Message);
    }

    [Fact] // ADR-0064: only the spaces around a number are set aside; a tab or a line break in one is no number, in every reading
    public void A_number_with_a_tab_or_a_line_break_is_refused()
    {
        foreach (var kind in new[] { SnapshotKind.Double, SnapshotKind.Decimal, SnapshotKind.Integer })
        {
            var plain = new CsvSchema([new("V", kind)]);
            var german = new CsvSchema([new("V", kind) { DecimalPoint = ",", ThousandsSeparator = "." }]) { Separator = CsvSeparator.Semicolon };

            Assert.Equal(1, Refusal(plain, "V\n1\t\n").Row);
            Assert.Equal(1, Refusal(plain, "V\n\"1\n\"\n").Row);
            Assert.Equal(1, Refusal(german, "V\n\t1\n").Row);
        }
    }

    [Fact] // ADR-0064: a record with too many fields is refused
    public void A_record_with_too_many_fields_is_refused()
    {
        var refusal = Refusal(Texts("A", "B"), "A,B\nx,y\nx,y,z\n");

        Assert.Equal(2, refusal.Row);
        Assert.Null(refusal.Column);
        Assert.Equal("Row 2: the record has 3 fields where the header has 2 (line 3).", refusal.Message);
    }

    [Fact] // ADR-0064: a record with too few fields is refused, and so is an empty line among wider records
    public void A_record_with_too_few_fields_is_refused()
    {
        Assert.Equal("Row 1: the record has 1 field where the header has 2 (line 2).", Refusal(Texts("A", "B"), "A,B\nx\n").Message);
        Assert.Equal("Row 2: the line is empty, where the header has 2 fields (line 3).", Refusal(Texts("A", "B"), "A,B\nx,y\n\nx,y\n").Message);
        Assert.Equal("Row 2: the record has 1 field where the first record has 2 (line 2).",
            Refusal(Texts("A", "B") with { HasHeader = false }, "x,y\nx\n").Message);
    }

    [Fact] // ADR-0064: in a file of one column, an empty line is a record whose one field is a Blank
    public void In_a_file_of_one_column_an_empty_line_is_a_blank()
    {
        var snapshot = Read(Texts("A"), "A\nx\n\ny\n");

        Assert.Equal(["x", null, "y"], Fixtures.Values(snapshot, "A"));
    }

    [Fact] // ADR-0064: a quote left open at the end of the file is refused
    public void An_unclosed_quote_is_refused()
    {
        var refusal = Refusal(Texts("A", "B"), "A,B\nx,y\nz,\"never\nclosed\n");

        Assert.Equal("Row 2, column 'B': a quoted field is not closed before the file ends (line 3).", refusal.Message);
    }

    [Fact] // ADR-0064: a quote inside a field that does not begin with one is refused
    public void A_quote_inside_an_unquoted_field_is_refused()
    {
        Assert.Equal("Row 1, column 'B': a quote stands inside a field that does not begin with one (line 2).", Refusal(Texts("A", "B"), "A,B\nx,5\" pipe\n").Message);
        // In a column the Schema skips, the field is named by its header.
        Assert.Equal("Row 1, column 'C': a quote stands inside a field that does not begin with one (line 2).", Refusal(Texts("A"), "A,C\nx,5\" pipe\n").Message);
    }

    [Fact] // ADR-0064: anything but a separator or a line end after a closing quote is refused
    public void Text_after_a_closing_quote_is_refused()
    {
        Assert.Equal("Row 1, column 'A': a quoted field's closing quote is followed by 'x' rather than a separator or a line end (line 2).",
            Refusal(Texts("A", "B"), "A,B\n\"ab\"x,y\n").Message);
    }

    [Fact] // ADR-0064: text that is not valid UTF-8 is refused rather than read as replacement characters
    public void Invalid_utf8_is_refused()
    {
        byte[] file = [.. Utf8("A,B\nok,"), 0x41, 0xC3, 0x28, .. Utf8("\n")];

        var refusal = Refusal(Texts("A", "B"), file);

        Assert.Equal("Row 1, column 'B': the text is not valid UTF-8 (line 2).", refusal.Message);
    }

    [Fact] // ADR-0064: a header that is not valid in the encoding is refused
    public void An_invalid_header_is_refused()
    {
        byte[] file = [0x41, 0xFF, .. Utf8("\nx\n")];

        Assert.Equal("The header is not valid UTF-8 (line 1).", Refusal(Texts("A"), file).Message);
    }

    [Fact] // ADR-0064: a declared header that stands twice in the file is refused, since which field it is cannot be told
    public void A_header_that_stands_twice_is_refused()
    {
        var refusal = Refusal(Texts("Amount"), "Amount,Desk,Amount\n1,FX,2\n");

        Assert.Equal("Column 'Amount': the header holds 'Amount' twice, so which field it is cannot be told; declare its Position.", refusal.Message);
    }

    [Fact] // ADR-0064: a declared position beyond the record is refused by name
    public void A_position_beyond_the_record_is_refused()
    {
        var schema = new CsvSchema([new("Late", SnapshotKind.Text) { Position = 5 }]);

        Assert.Equal("Column 'Late': it is declared at position 5, but the header has 2 fields.", Refusal(schema, "A,B\nx,y\n").Message);
        Assert.Equal("Column 'Late': it is declared at position 5, but the first record has 2 fields.", Refusal(schema with { HasHeader = false }, "x,y\n").Message);
    }

    [Fact] // ADR-0064: a Blank Record Key is refused, naming the row and the line
    public void A_blank_record_key_is_refused()
    {
        var schema = new CsvSchema([new("Id", SnapshotKind.Text), new("V", SnapshotKind.Integer)]) { RecordKey = "Id", BlankText = ["NULL"] };

        Assert.Equal("Row 2, column 'Id': the Record Key is Blank (line 3).", Refusal(schema, "Id,V\na,1\n,2\n").Message);
        Assert.Equal("Row 1, column 'Id': the Record Key is Blank (line 2).", Refusal(schema, "Id,V\nNULL,2\n").Message);
    }

    [Fact] // ADR-0064: a Record Key carried twice is refused, naming both rows and the key
    public void A_record_key_carried_twice_is_refused()
    {
        var schema = new CsvSchema([new("Id", SnapshotKind.Integer), new("V", SnapshotKind.Integer)]) { RecordKey = "Id" };

        var refusal = Refusal(schema, "Id,V\n7,1\n8,2\n7,3\n");

        Assert.Equal(3, refusal.Row);
        Assert.Equal("Id", refusal.Column);
        Assert.Equal(7L, refusal.Key);
        Assert.Equal("Row 3, column 'Id': the Record Key 7 is already carried by row 1.", refusal.Message);
    }

    [Fact] // ADR-0064: a file with nothing in it has no header row to match, and is refused
    public void An_empty_file_is_refused()
    {
        Assert.Equal("The file is empty.", Refusal(Texts("A"), "").Message);
        Assert.Equal("The file is empty.", Refusal(Texts("A"), Utf8("", bom: true)).Message);
        // Without a header row, an empty file is a Snapshot with no rows.
        Assert.Equal(0, Read(Texts("A") with { HasHeader = false }, "").RowCount);
    }

    [Fact] // ADR-0064: a file that begins with UTF-16's byte-order mark is refused, since a CSV is read in UTF-8 or Shift-JIS
    public void A_utf16_file_is_refused()
    {
        var file = System.Text.Encoding.Unicode.GetPreamble().Concat(System.Text.Encoding.Unicode.GetBytes("A\nx\n")).ToArray();

        Assert.Equal("The file begins with UTF-16's byte-order mark; a CSV is read in UTF-8 or Shift-JIS, and the Schema declares UTF-8.", Refusal(Texts("A"), file).Message);
    }

    [Fact] // ADR-0064: a record longer than any spreadsheet writes is refused as a quote left open, rather than read into memory whole
    public void A_record_too_long_to_be_one_is_refused()
    {
        Snapshot? built = null;
        var refusal = Assert.Throws<SnapshotException>(() => built = CsvLoad.ReadAsync(
            Texts("A"), new Endless(), null, TestContext.Current.CancellationToken, CsvLoad.DefaultBufferSize).AsTask().GetAwaiter().GetResult());

        Assert.Null(built);
        Assert.Equal("Row 1: the record is longer than 64 MiB, which no spreadsheet writes; a quote may be left open (line 2).", refusal.Message);
    }

    [Fact] // ADR-0064: a refused load builds nothing, whichever way it was read
    public async Task An_asynchronous_load_refuses_the_same_way()
    {
        Snapshot? built = null;
        var file = new MemoryStream(Utf8("Qty\n1\n2\nx\n"));

        var refusal = await Assert.ThrowsAsync<SnapshotException>(async () => built = await new CsvSchema([new("Qty", SnapshotKind.Integer)]).ReadAsync(
            file, new SnapshotLoadOptions { SliceBudget = TimeSpan.Zero, Yield = () => ValueTask.CompletedTask }, TestContext.Current.CancellationToken));

        Assert.Null(built);
        Assert.Equal(3, refusal.Row);
    }

    /// <summary>A header, then a quote opened and never closed, for as long as it is read.</summary>
    private sealed class Endless : Stream
    {
        private long position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            buffer.Fill((byte)'a');
            if (position == 0 && buffer.Length >= 3)
            {
                "A\n\""u8.CopyTo(buffer);
            }
            position += buffer.Length;
            return buffer.Length;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
