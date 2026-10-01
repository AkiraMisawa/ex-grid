using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Tests.CsvFixtures;
using static ExGrid.Data.Tests.Fixtures;

namespace ExGrid.Data.Tests;

/// <summary>
/// The CSV reader reads the common cases by short paths over the field's bytes (ticket 07): a
/// Boolean's ASCII spellings, a date in a fixed layout. Each must read what the full reading reads,
/// and refuse what it refuses (ADR-0064).
/// </summary>
public class CsvShortPathTests
{
    [Fact] // ADR-0064: a Boolean's spellings are matched ignoring case exactly as .NET's ordinal comparison does, whatever bytes the field holds
    public void Boolean_spellings_match_as_an_ordinal_comparison_ignoring_case_does()
    {
        string[] fields = ["TRUE", "true", "tRuE", "FALSE", "false", "FaLsE", "FALſE", "ＴＲＵＥ", "TRUÉ", "truE ", "  false", "T", "TRUEX", "ı", "FALSE\t", "FALSE ", "Ｆalse", "tRUE"];
        var schema = new CsvSchema([new("B", SnapshotKind.Boolean)]);

        foreach (var field in fields)
        {
            var text = field.Trim(' ');
            bool? expected = text.Equals("TRUE", StringComparison.OrdinalIgnoreCase) ? true
                : text.Equals("FALSE", StringComparison.OrdinalIgnoreCase) ? false
                : null;
            if (expected is { } value)
                Assert.Equal([value], Values(Read(schema, $"B\n{field}\n"), "B"));
            else
                Assert.Equal($"Row 1, column 'B': '{field}' is not a spelling of true or false ('TRUE', 'FALSE') (line 2).", Refusal(schema, $"B\n{field}\n").Message);
        }
    }

    [Theory] // ADR-0064: a date read from its bytes under a compiled format is what .NET's TryParseExact reads, valid or not
    [InlineData("yyyy-MM-dd")]
    [InlineData("dd.MM.yyyy HH:mm:ss")]
    [InlineData("yyyyMMdd")]
    [InlineData("yyyy-MM-dd HH:mm:ss.fff")]
    [InlineData("yyyy-MM-dd HH:mm:ss.fffffff")]
    [InlineData("yyyy/M/d")]
    [InlineData("d.M.yyyy H:mm")]
    public void A_date_read_from_its_bytes_is_what_dotnet_reads(string format)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var reader = new ExGrid.Data.Csv.DateFormat(format, culture);
        Assert.True(reader.ReadsBytes);
        var random = new Random(20261001);
        int[] years = [0, 1, 4, 100, 400, 1900, 1999, 2000, 2024, 2025, 2026, 9999];
        for (var i = 0; i < 20_000; i++)
        {
            var year = years[random.Next(years.Length)];
            var month = random.Next(0, 14);
            var day = random.Next(0, 33);
            var text = format
                .Replace("yyyy", year.ToString("0000", culture), StringComparison.Ordinal)
                .Replace("MM", month.ToString("00", culture), StringComparison.Ordinal)
                .Replace("M", month.ToString(culture), StringComparison.Ordinal)
                .Replace("dd", day.ToString("00", culture), StringComparison.Ordinal)
                .Replace("d", day.ToString(culture), StringComparison.Ordinal)
                .Replace("HH", random.Next(0, 25).ToString("00", culture), StringComparison.Ordinal)
                .Replace("H", random.Next(0, 25).ToString(culture), StringComparison.Ordinal)
                .Replace("mm", random.Next(0, 61).ToString("00", culture), StringComparison.Ordinal)
                .Replace("ss", random.Next(0, 61).ToString("00", culture), StringComparison.Ordinal)
                .Replace("fffffff", random.Next(0, 10_000_000).ToString("0000000", culture), StringComparison.Ordinal)
                .Replace("fff", random.Next(0, 1000).ToString("000", culture), StringComparison.Ordinal);
            if (random.Next(10) == 0)
                text = text.Remove(random.Next(text.Length), 1);

            var expected = DateTime.TryParseExact(text, format, culture,
                System.Globalization.DateTimeStyles.NoCurrentDateDefault | System.Globalization.DateTimeStyles.AdjustToUniversal, out var date);
            var read = reader.TryRead(System.Text.Encoding.ASCII.GetBytes(text), out var ticks);

            Assert.True(expected == read, $"'{text}' under '{format}': .NET {expected}, the reader {read}");
            if (read)
                Assert.Equal(date.Ticks, ticks);
        }
    }

    [Fact] // ADR-0064: a date column reads every value alike, whether it repeats or the column holds too many to remember
    public void Dates_read_alike_whether_they_repeat_or_not()
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var text = new System.Text.StringBuilder("When\n");
        var expected = new List<object?>();
        var start = new DateTime(2026, 1, 2);
        for (var i = 0; i < 150_000; i++)
        {
            // Trade dates repeating, then times that rarely do: past where the column stops remembering.
            var when = i < 50_000 ? start.AddDays(i % 270) : start.AddSeconds(i * 7L);
            var written = i < 50_000 ? when.ToString("yyyy-MM-dd", culture) : when.ToString("yyyy-MM-dd HH:mm:ss", culture);
            text.Append(i % 10 == 0 ? $" {written} " : written).Append('\n');
            expected.Add(when);
        }

        var snapshot = Read(new CsvSchema([new("When", SnapshotKind.Date)]), text.ToString());

        Assert.Equal(expected, Values(snapshot, "When"));
    }

    [Fact] // ADR-0064: declared spellings of any case, ASCII or not, are matched ignoring case
    public void Declared_spellings_are_matched_ignoring_case()
    {
        var ascii = new CsvSchema([new("B", SnapshotKind.Boolean) { TrueText = ["Yes", "y"], FalseText = ["No", "n"] }]);
        var japanese = new CsvSchema([new("B", SnapshotKind.Boolean) { TrueText = ["はい", "Ok"], FalseText = ["いいえ"] }]);

        Assert.Equal([true, true, true, false, false, false], Values(Read(ascii, "B\nYES\ny\nyEs\nnO\nN\nno\n"), "B"));
        Assert.Equal(1, Refusal(ascii, "B\nyess\n").Row);
        Assert.Equal([true, false, true, true], Values(Read(japanese, "B\nはい\nいいえ\nOK\nok\n"), "B"));
        Assert.Equal(1, Refusal(japanese, "B\nいい\n").Row);
    }
}
