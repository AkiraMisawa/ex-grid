using System.Globalization;
using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Tests.CsvFixtures;

namespace ExGrid.Data.Tests;

/// <summary>A Schema is the Consumer's declaration (ADR-0064): one that contradicts itself is refused
/// before a byte is read, as the Consumer's mistake rather than the file's; one that is whole is a
/// value, changed with <c>with</c> and compared by what it declares.</summary>
public class CsvSchemaTests
{
    public static TheoryData<string, CsvSchema> Contradictions => new()
    {
        { "A Schema declares at least one column.", new CsvSchema([]) },
        { "A column named 'A' is declared twice", new CsvSchema([new("A", SnapshotKind.Text), new("A", SnapshotKind.Integer)]) },
        { "Columns 'A' and 'B' both match the header 'H'", new CsvSchema([new("A", SnapshotKind.Text) { Header = "H" }, new("B", SnapshotKind.Text) { Header = "H" }]) },
        { "Columns 'A' and 'B' are both declared at position 1", new CsvSchema([new("A", SnapshotKind.Text) { Position = 1 }, new("B", SnapshotKind.Text)]) { HasHeader = false } },
        { "Column 'A' is declared at position -1", new CsvSchema([new("A", SnapshotKind.Text) { Position = -1 }]) },
        { "Column 'A' matches an empty header", new CsvSchema([new("A", SnapshotKind.Text) { Header = "" }]) },
        { "Column 'A' reads its decimal point and its thousands separator alike", new CsvSchema([new("A", SnapshotKind.Decimal) { DecimalPoint = ",", ThousandsSeparator = "," }]) },
        { "Column 'A' reads its decimal point and its thousands separator alike", new CsvSchema([new("A", SnapshotKind.Decimal) { Culture = CultureInfo.GetCultureInfo("de-DE"), DecimalPoint = "." }]) },
        { "Column 'A' reads '1' as its decimal point", new CsvSchema([new("A", SnapshotKind.Double) { DecimalPoint = "1" }]) },
        { "Column 'A' declares the date format 'yyyy-MM-dd'', which .NET cannot read", new CsvSchema([new("A", SnapshotKind.Date) { DateFormats = ["yyyy-MM-dd'"] }]) },
        { "Column 'A' declares no date format.", new CsvSchema([new("A", SnapshotKind.Date) { DateFormats = [] }]) },
        { "Column 'A' declares the date format 'dd.MM': it has a month or a day but no year, so a date read under it would take the year it is read in.", new CsvSchema([new("A", SnapshotKind.Date) { DateFormats = ["yyyy-MM-dd", "dd.MM"] }]) },
        { "Column 'A' declares the date format 'M': it has a month or a day but no year", new CsvSchema([new("A", SnapshotKind.Date) { DateFormats = ["M"] }]) },
        { "Column 'A' declares the date format 'HH:mm zzz': it has an offset but no date, so a date read under it would take the day it is read on.", new CsvSchema([new("A", SnapshotKind.Date) { DateFormats = ["HH:mm zzz"] }]) },
        { "Column 'A' reads 'yes' as both true and false.", new CsvSchema([new("A", SnapshotKind.Boolean) { TrueText = ["yes"], FalseText = ["YES"] }]) },
        { "Column 'A' reads 'N' as both a Blank and a Boolean.", new CsvSchema([new("A", SnapshotKind.Boolean) { TrueText = ["Y"], FalseText = ["N"], BlankText = ["N"] }]) },
        { "Column 'A' declares an empty spelling of true or false", new CsvSchema([new("A", SnapshotKind.Boolean) { TrueText = [""] }]) },
        { "The Record Key names 'Id', which is not a declared column.", new CsvSchema([new("A", SnapshotKind.Text)]) { RecordKey = "Id" } },
        { "Only a Text or an Integer column can be the Record Key; 'A' is Decimal.", new CsvSchema([new("A", SnapshotKind.Decimal)]) { RecordKey = "A" } },
        { "is not one a CSV is read with", new CsvSchema([new("A", SnapshotKind.Text)]) { Separator = (CsvSeparator)7 } },
        { "which a Snapshot does not hold", new CsvSchema([new("A", (SnapshotKind)42)]) },
    };

    [Theory] // ADR-0064: a Schema that contradicts itself is refused before a byte is read
    [MemberData(nameof(Contradictions))]
    public async Task A_schema_that_contradicts_itself_is_refused_before_reading(string message, CsvSchema schema)
    {
        var stream = new Trickle(Utf8("A\nx\n"));

        var refusal = await Assert.ThrowsAsync<ArgumentException>(async () => await schema.ReadAsync(stream, null, TestContext.Current.CancellationToken));

        Assert.Contains(message, refusal.Message, StringComparison.Ordinal);
        Assert.Equal(0, stream.Reads);
    }

    [Fact] // ADR-0064: a Schema is a value: changed with `with`, and equal to another that declares the same
    public void A_schema_is_a_value()
    {
        var schema = new CsvSchema([new("A", SnapshotKind.Date) { DateFormats = ["yyyy-MM-dd"] }, new("B", SnapshotKind.Text)]) { BlankText = ["-"] };
        var same = new CsvSchema([new("A", SnapshotKind.Date) { DateFormats = ["yyyy-MM-dd"] }, new("B", SnapshotKind.Text)]) { BlankText = ["-"] };

        Assert.Equal(schema, same);
        Assert.Equal(schema.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(schema, schema with { Separator = CsvSeparator.Tab });
        Assert.NotEqual(schema, schema with { Columns = [schema.Columns[0] with { DateFormats = ["dd.MM.yyyy"] }, schema.Columns[1]] });
        Assert.NotEqual(schema, schema with { Encoding = CsvEncoding.ShiftJis });
    }
}
