using System.Text;
using ExGrid.Data;
using ExGrid.Data.Storage;
using Xunit;
using static ExGrid.Data.Tests.CsvFixtures;

namespace ExGrid.Data.Tests;

/// <summary>
/// A Record Key is indexed a chunk of rows at a time, and a Text key by its code, a slot per code
/// (ticket 07). A Blank key and a key carried twice are still refused at the first row a row-by-row
/// index meets, by name, and every key is still found (ADR-0064).
/// </summary>
public class CsvKeyTests
{
    [Theory] // ADR-0064: a Record Key carried twice is refused by name wherever it falls, naming both rows
    [InlineData(SnapshotKind.Text)]
    [InlineData(SnapshotKind.Integer)]
    public void A_key_carried_twice_is_refused_wherever_it_falls(SnapshotKind kind)
    {
        var schema = new CsvSchema([new("Id", kind), new("V", SnapshotKind.Integer)]) { RecordKey = "Id" };
        foreach (var twice in new[] { 2, 1_023, 1_024, 1_025, 2_049, 3_000 })
        {
            var text = new StringBuilder("Id,V\n");
            for (var row = 1; row <= 3_000; row++)
                text.Append(Key(kind, row == twice ? twice / 2 : row)).Append(",1\n");

            var refusal = Refusal(schema, text.ToString());

            var shown = kind == SnapshotKind.Text ? $"'{Key(kind, twice / 2)}'" : Key(kind, twice / 2);
            Assert.Equal($"Row {twice:N0}, column 'Id': the Record Key {shown} is already carried by row {twice / 2:N0}.", refusal.Message);
        }
    }

    [Theory] // ADR-0064: of a Blank key and a key carried twice, the one in the earlier row is refused
    [InlineData(SnapshotKind.Text)]
    [InlineData(SnapshotKind.Integer)]
    public void A_blank_key_and_a_key_carried_twice_are_refused_in_row_order(SnapshotKind kind)
    {
        var schema = new CsvSchema([new("Id", kind), new("V", SnapshotKind.Integer)]) { RecordKey = "Id", BlankText = ["NULL"] };

        // A Blank key is refused as the row is read; a key carried twice once every row is.
        Assert.Equal("Row 10, column 'Id': the Record Key is Blank (line 11).", Refusal(schema, File(kind, blankAt: 10, twiceAt: 5)).Message);
        Assert.Equal("Row 5, column 'Id': the Record Key is Blank (line 6).", Refusal(schema, File(kind, blankAt: 5, twiceAt: 10)).Message);
        Assert.Contains("is already carried by row 4", Refusal(schema, File(kind, blankAt: -1, twiceAt: 9)).Message, StringComparison.Ordinal);

        static string File(SnapshotKind kind, int blankAt, int twiceAt)
        {
            var text = new StringBuilder("Id,V\n");
            for (var row = 1; row <= 2_000; row++)
                text.Append(row == blankAt ? "NULL" : Key(kind, row == twiceAt ? 4 : row)).Append(",1\n");
            return text.ToString();
        }
    }

    [Theory] // ADR-0064: built from columns, a Blank key and a key carried twice are refused at whichever row comes first
    [InlineData(SnapshotKind.Text, 10, 5)]
    [InlineData(SnapshotKind.Text, 5, 10)]
    [InlineData(SnapshotKind.Integer, 10, 5)]
    [InlineData(SnapshotKind.Integer, 5, 10)]
    [InlineData(SnapshotKind.Text, 1_500, 1_030)]
    [InlineData(SnapshotKind.Integer, 1_030, 1_500)]
    public void Built_from_columns_the_first_bad_key_is_refused(SnapshotKind kind, int blankAt, int twiceAt)
    {
        var columns = new SnapshotColumnsBuilder();
        var text = kind == SnapshotKind.Text ? columns.Text("Id") : null;
        var integer = kind == SnapshotKind.Integer ? columns.Integer("Id") : null;
        columns.Key("Id");
        for (var row = 1; row <= 2_000; row++)
        {
            if (row == blankAt)
            {
                text?.AppendBlank();
                integer?.AppendBlank();
                continue;
            }
            var key = row == twiceAt ? 4 : row;
            text?.Append($"K{key}");
            integer?.Append(key);
        }

        var refusal = Assert.Throws<SnapshotException>(() => columns.Build());

        Assert.Equal(Math.Min(blankAt, twiceAt), refusal.Row);
        Assert.Equal(blankAt < twiceAt ? "Row " + blankAt.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + ", column 'Id': the Record Key is Blank." : $"Row {twiceAt:N0}, column 'Id': the Record Key {(kind == SnapshotKind.Text ? "'K4'" : "4")} is already carried by row 4.", refusal.Message);
    }

    [Fact] // ADR-0064: a Text key's index by code finds every row, and no code it was not given
    public void A_text_keys_index_by_code_finds_every_row()
    {
        var segments = new[] { new Segment(4, [new TextData([2, 0, 3, 1], null)], null, 0, null, null) };
        var shape = new Shape([("Id", "Id", SnapshotKind.Text)], "Id", null, new SnapshotTuning(SegmentShift: 2));
        var indexer = new KeyIndexer(shape, segments, 4, codes: 4);

        Assert.True(indexer.Step(long.MaxValue));

        for (var code = 0; code < 4; code++)
        {
            Assert.True(indexer.Index.TryGet(code, out var row));
            Assert.Equal(new[] { 1, 3, 0, 2 }[code], row);
        }
        Assert.False(indexer.Index.TryGet(4, out _));
        Assert.False(indexer.Index.TryGet(-1, out _));
        Assert.Equal(4, indexer.Index.Count);
    }

    [Fact] // ADR-0064: a dictionary far larger than the rows keyed by it is indexed by hashing, as before
    public void A_dictionary_far_larger_than_its_rows_is_indexed_by_hashing()
    {
        var source = KeySource.Of(new TextData([1_000_000, 7], null));
        var index = KeyIndex.ForCodes(source, expected: 2, codes: 2_000_000);

        Assert.True(index.TryAdd(0, 1_000_000L, out _));
        Assert.True(index.TryAdd(1, 7L, out _));
        Assert.False(index.TryAdd(2, 7L, out var existing));
        Assert.Equal(1, existing);
        Assert.True(index.TryGet(1_000_000L, out var row));
        Assert.Equal(0, row);
    }

    private static string Key(SnapshotKind kind, int row)
        => kind == SnapshotKind.Text ? $"K{row:D5}" : (row * 10L).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
