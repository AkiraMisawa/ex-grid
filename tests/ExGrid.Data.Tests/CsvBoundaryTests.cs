using System.Text;
using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Tests.CsvFixtures;
using static ExGrid.Data.Tests.Fixtures;

namespace ExGrid.Data.Tests;

/// <summary>
/// The CSV reader finds separators, line ends and quotes sixteen bytes at a time, and reads the file
/// through a buffer (ticket 07). Neither may change what is read or refused: a field reads alike
/// wherever a block or a buffer cuts it, and a refusal names the same row, column and line
/// (ADR-0063).
/// </summary>
public class CsvBoundaryTests
{
    /// <summary>Buffer sizes around the block's sixteen bytes and its multiples, and some that are not.</summary>
    private static readonly int[] BufferSizes = [4, 5, 7, 15, 16, 17, 31, 32, 33, 47, 64, 100, 257, 1024, 4096];

    [Fact] // ADR-0063: fields of every length, quoted or not, read exactly wherever a block or a buffer cuts them
    public void Fields_of_every_length_read_alike_wherever_the_bytes_are_cut()
    {
        var (text, expected) = RandomFile(new Random(20261001), columns: 4, rows: 400);
        var schema = Texts("C0", "C1", "C2", "C3");
        var bytes = Utf8(text);

        foreach (var size in BufferSizes)
            AssertValues(expected, Read(schema, bytes, bufferSize: size));
        foreach (var chunk in new[] { 1, 3, 13 })
            AssertValues(expected, Read(schema, bytes, chunk: chunk));
        AssertValues(expected, Read(schema, bytes));
    }

    [Fact] // ADR-0063: a field far longer than a block or the buffer is read whole
    public void A_very_long_field_is_read_whole()
    {
        var plain = string.Concat(Enumerable.Range(0, 20_000).Select(i => (char)('a' + (i % 26))));
        var quoted = "say \"\"hi\"\", " + string.Concat(Enumerable.Repeat("東京\r\n, ", 3_000)) + "end";
        var text = $"A,B,C\r\n{plain},\"{quoted}\",x\r\ny,,{plain}\r\n";
        var schema = Texts("A", "B", "C");

        foreach (var size in new[] { 16, 17, 1024, 65_536 })
        {
            var snapshot = Read(schema, Utf8(text), bufferSize: size);

            Assert.Equal([plain, "y"], Values(snapshot, "A"));
            Assert.Equal([quoted.Replace("\"\"", "\"", StringComparison.Ordinal), null], Values(snapshot, "B"));
            Assert.Equal(["x", plain], Values(snapshot, "C"));
        }
    }

    [Theory] // ADR-0063: the line a refusal names counts the line breaks inside quotes, CR LF as one, wherever a block cuts them
    [InlineData("\r\n")]
    [InlineData("\n")]
    [InlineData("\r")]
    public void Line_breaks_inside_quotes_are_counted_wherever_a_block_cuts_them(string lineBreak)
    {
        var schema = new CsvSchema([new("A", SnapshotKind.Text), new("B", SnapshotKind.Integer)]);
        for (var offset = 0; offset < 40; offset++)
        {
            // The first record begins on line 2 and holds two line breaks; the second begins on line 5.
            var text = $"A,B\r\n\"{new string('x', offset)}{lineBreak}y{lineBreak}\",1\r\nz,bad\r\n";

            foreach (var size in new[] { 16, 17, 33, 4096 })
            {
                Assert.Equal("Row 2, column 'B': 'bad' is not an integer (line 5).", Refusal(schema, Utf8(text), size).Message);
            }
        }
    }

    [Fact] // ADR-0063: a CR that ends a quoted field's content is a line break of its own, and the LF after the quote ends the record
    public void A_cr_before_the_closing_quote_is_a_break_of_its_own()
    {
        var schema = new CsvSchema([new("A", SnapshotKind.Text), new("B", SnapshotKind.Integer)]);
        for (var offset = 0; offset < 34; offset++)
        {
            var text = $"A,B\n\"{new string('x', offset)}\r\",1\nz,bad\n";

            Assert.Equal("Row 2, column 'B': 'bad' is not an integer (line 4).", Refusal(schema, Utf8(text)).Message);
            Assert.Equal([new string('x', offset) + "\r", "z"], Values(Read(schema with { Columns = [schema.Columns[0]] }, text), "A"));
        }
    }

    [Fact] // ADR-0063: a quote inside an unquoted field is refused wherever it stands in it
    public void A_quote_inside_an_unquoted_field_is_refused_wherever_it_stands()
    {
        for (var offset = 1; offset < 40; offset++)
        {
            var text = $"A,B\r\nok,{new string('x', offset)}\"{new string('y', 20)}\r\n";

            foreach (var size in new[] { 16, 17, 4096 })
            {
                Assert.Equal("Row 1, column 'B': a quote stands inside a field that does not begin with one (line 2).",
                    Refusal(Texts("A", "B"), Utf8(text), size).Message);
            }
        }
    }

    [Fact] // ADR-0063: what follows a closing quote is checked wherever the quote falls
    public void Text_after_a_closing_quote_is_refused_wherever_the_quote_falls()
    {
        for (var length = 0; length < 40; length++)
        {
            var text = $"A,B\r\n\"{new string('x', length)}\"z,1\r\n";

            foreach (var size in new[] { 16, 17, 4096 })
            {
                Assert.Equal("Row 1, column 'A': a quoted field's closing quote is followed by 'z' rather than a separator or a line end (line 2).",
                    Refusal(Texts("A", "B"), Utf8(text), size).Message);
            }
        }
    }

    [Fact] // ADR-0063: a quote left open is refused however long the field runs
    public void A_quote_left_open_is_refused_however_long_the_field_runs()
    {
        for (var length = 0; length < 40; length++)
        {
            var text = $"A,B\r\nok,\"{new string('x', length)}";

            foreach (var size in new[] { 16, 17, 4096 })
            {
                Assert.Equal("Row 1, column 'B': a quoted field is not closed before the file ends (line 2).",
                    Refusal(Texts("A", "B"), Utf8(text), size).Message);
            }
        }
    }

    /// <summary>
    /// A file of <paramref name="rows"/> records of Text, with the values each column must read back:
    /// empty fields (a Blank), plain ones and quoted ones of every length up to seventy bytes, with
    /// characters of one to four bytes, and in quotes the separator, doubled quotes and every line break.
    /// </summary>
    internal static (string Text, string?[][] Expected) RandomFile(Random random, int columns, int rows)
    {
        string[] plainPieces = ["a", "Z", "0", " ", "é", "東", "😀", "-", "."];
        string[] quotedPieces = ["a", "Z", ",", "\"", "\r\n", "\n", "\r", "é", "東", "😀", " "];
        var expected = new string?[columns][];
        for (var c = 0; c < columns; c++)
            expected[c] = new string?[rows];
        var text = new StringBuilder(string.Join(",", Enumerable.Range(0, columns).Select(c => $"C{c}"))).Append("\r\n");
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < columns; c++)
            {
                if (c > 0)
                    text.Append(',');
                var kind = random.Next(5);
                var target = random.Next(0, 71);
                var value = new StringBuilder();
                var pieces = kind == 0 ? quotedPieces : plainPieces;
                while (Encoding.UTF8.GetByteCount(value.ToString()) < target)
                    value.Append(pieces[random.Next(pieces.Length)]);
                if (kind == 4)
                    value.Clear();
                var written = value.ToString();
                if (kind == 0)
                    text.Append('"').Append(written.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
                else
                    text.Append(written);
                expected[c][r] = written.Length == 0 ? null : written;
            }
            text.Append(random.Next(2) == 0 ? "\r\n" : "\n");
        }
        return (text.ToString(), expected);
    }

    private static void AssertValues(string?[][] expected, Snapshot snapshot)
    {
        for (var c = 0; c < expected.Length; c++)
            Assert.Equal(expected[c], Values(snapshot, $"C{c}"));
    }
}
