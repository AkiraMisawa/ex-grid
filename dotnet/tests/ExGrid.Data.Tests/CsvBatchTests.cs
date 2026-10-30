using System.Text;
using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Tests.CsvFixtures;
using static ExGrid.Data.Tests.Fixtures;

namespace ExGrid.Data.Tests;

/// <summary>
/// The CSV reader cuts a batch of records, then reads it column by column (ticket 07). A file that
/// holds several things it cannot read is still refused at the first a record-by-record read meets:
/// the earliest record, and in it the first declared column; a record that breaks RFC 4180, or has
/// another number of fields, only once every record before it is read (ADR-0064).
/// </summary>
public class CsvBatchTests
{
    private static readonly CsvSchema Two = new([new("A", SnapshotKind.Integer), new("B", SnapshotKind.Integer)]);

    [Fact] // ADR-0064: of two values that cannot be read, the one in the earlier record is refused, whatever its column
    public void The_earlier_record_is_refused_whatever_its_column()
    {
        Assert.Equal("Row 1, column 'B': 'x' is not an integer (line 2).", Refusal(Two, "A,B\n1,x\ny,1\n").Message);
        Assert.Equal("Row 2, column 'A': 'y' is not an integer (line 3).", Refusal(Two, "A,B\n1,1\ny,1\n2,x\n").Message);
    }

    [Fact] // ADR-0064: of two values in one record that cannot be read, the first declared column's is refused
    public void In_one_record_the_first_declared_column_is_refused()
    {
        Assert.Equal("Row 1, column 'A': 'x' is not an integer (line 2).", Refusal(Two, "A,B\nx,y\n").Message);
        // Declared in the other order than the file's, B is read first.
        var reversed = Two with { Columns = [Two.Columns[1], Two.Columns[0]] };
        Assert.Equal("Row 1, column 'B': 'y' is not an integer (line 2).", Refusal(reversed, "A,B\nx,y\n").Message);
    }

    [Fact] // ADR-0064: a record with another number of fields is refused after the records before it are read, and before those after it
    public void A_miscounted_record_is_refused_in_its_place()
    {
        Assert.Equal("Row 1, column 'B': 'x' is not an integer (line 2).", Refusal(Two, "A,B\n1,x\n1,2,3\n").Message);
        Assert.Equal("Row 2: the record has 3 fields where the header has 2 (line 3).", Refusal(Two, "A,B\n1,1\n1,2,3\ny,1\n").Message);
        Assert.Equal("Row 2: the line is empty, where the header has 2 fields (line 3).", Refusal(Two, "A,B\n1,1\n\nx,1\n").Message);
    }

    [Fact] // ADR-0064: a record that breaks RFC 4180 is refused after the records before it are read, and before those after it
    public void A_malformed_record_is_refused_in_its_place()
    {
        Assert.Equal("Row 1, column 'B': 'x' is not an integer (line 2).", Refusal(Two, "A,B\n1,x\n1,2\"\n").Message);
        Assert.Equal("Row 2, column 'B': a quote stands inside a field that does not begin with one (line 3).", Refusal(Two, "A,B\n1,1\n1,2\"\ny,1\n").Message);
        Assert.Equal("Row 3, column 'A': a quoted field is not closed before the file ends (line 4).", Refusal(Two, "A,B\n1,1\n2,2\n\"3,3\n").Message);
    }

    [Fact] // ADR-0064: a Blank Record Key is refused in its place among the other values that cannot be read
    public void A_blank_key_is_refused_in_its_place()
    {
        var keyed = new CsvSchema([new("V", SnapshotKind.Integer), new("Id", SnapshotKind.Text)]) { RecordKey = "Id" };

        Assert.Equal("Row 1, column 'Id': the Record Key is Blank (line 2).", Refusal(keyed, "Id,V\n,1\nb,x\n").Message);
        Assert.Equal("Row 1, column 'V': 'x' is not an integer (line 2).", Refusal(keyed, "Id,V\n,x\n").Message);
        Assert.Equal("Row 2, column 'V': 'x' is not an integer (line 3).", Refusal(keyed, "Id,V\na,1\nb,x\n,1\n").Message);
    }

    [Theory] // ADR-0064: a Blank early in a column of numbers, dates or Booleans is kept however many values follow it, as the column outgrows the room it began with
    [InlineData(SnapshotKind.Integer)]
    [InlineData(SnapshotKind.Decimal)]
    [InlineData(SnapshotKind.Double)]
    [InlineData(SnapshotKind.Date)]
    [InlineData(SnapshotKind.Boolean)]
    public void An_early_blank_is_kept_however_many_values_follow(SnapshotKind kind)
    {
        var value = kind switch { SnapshotKind.Date => "2026-10-01", SnapshotKind.Boolean => "TRUE", _ => "1" };
        var text = new StringBuilder("A,V\nx,\n");
        for (var row = 0; row < 1_000; row++)
            text.Append("x,").Append(value).Append('\n');

        var snapshot = Read(new CsvSchema([new("A", SnapshotKind.Text), new("V", kind)]), text.ToString());

        Assert.Equal(1_001, snapshot.RowCount);
        Assert.Null(Values(snapshot, "V")[0]);
        Assert.All(Values(snapshot, "V").Skip(1), Assert.NotNull);
    }

    [Fact] // ADR-0064: the first value that cannot be read is found wherever it falls among batches, by its row and its line
    public void The_first_refusal_is_found_across_batches()
    {
        foreach (var bad in new[] { 1, 1_023, 1_024, 1_025, 2_048, 3_000 })
        {
            var text = new StringBuilder("A,B\n");
            for (var row = 1; row <= 3_100; row++)
            {
                // A quoted line break in every 7th record moves the lines past the rows.
                text.Append(row == bad ? "1,x" : row % 7 == 0 ? "\"1\n\",2" : "1,2").Append('\n');
            }
            var lines = bad + 1 + ((bad - 1) / 7);
            var schema = new CsvSchema([new("A", SnapshotKind.Text), new("B", SnapshotKind.Integer)]);

            Assert.Equal($"Row {bad:N0}, column 'B': 'x' is not an integer (line {lines:N0}).", Refusal(schema, text.ToString()).Message);
        }
    }
}
