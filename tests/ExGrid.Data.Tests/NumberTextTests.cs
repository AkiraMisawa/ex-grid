using System.Globalization;
using System.Text;
using ExGrid.Data;
using ExGrid.Data.Csv;
using Xunit;
using static ExGrid.Data.Tests.CsvFixtures;
using static ExGrid.Data.Tests.Fixtures;

namespace ExGrid.Data.Tests;

/// <summary>
/// A number written the common way is read by a short path (ticket 07). It must read exactly what the
/// full reading reads, and leave everything else to it, so nothing a column reads or refuses changes
/// (ADR-0063).
/// </summary>
public class NumberTextTests
{
    private static readonly NumberReading Invariant = new(new CsvColumn("V", SnapshotKind.Decimal), CsvEncoding.Utf8);
    private static readonly NumberReading Grouped = new(new CsvColumn("V", SnapshotKind.Decimal) { ThousandsSeparator = "," }, CsvEncoding.Utf8);
    private static readonly NumberReading German = new(new CsvColumn("V", SnapshotKind.Decimal) { Culture = CultureInfo.GetCultureInfo("de-DE") }, CsvEncoding.Utf8);

    public static TheoryData<string> Shapes =>
    [
        "0", "-0", "+0", "7", "-7", "+7", "007", "1234567", "12.5", "-0.25", "1.50", "1.500", "0.000", ".5", "-.5", ".00", "7.",
        "1,234", "12,345", "123,456", "1,234,567.89", "-1,234.50", "1,23", "12,34", "1,2345", "1234,567", ",123", "1,,234", "1,234,",
        "1.234,5", "1.234.567,89", "-1.234", "1.23", "12,5", "-", "+", ".", "-.", "", " 1", "1 ", "1e3", "0x10", "1_000", "١٢",
        "999999999999999999", "9999999999999999999", "99999999999999999999", "0.000000000000000001", "0.0000000000000000001",
        "123456789.123456789", "12345678.123456789", "--1", "+-1", "1-", "1.2.3", "1,234.5,6", "000000000000000000001",
    ];

    [Theory] // ADR-0063: what the short path reads, the full reading reads alike; what it leaves, the full reading decides
    [MemberData(nameof(Shapes))]
    public void The_short_path_reads_what_the_full_reading_reads(string text)
    {
        foreach (var reading in new[] { Invariant, Grouped, German })
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            Assert.True(reading.Short);
            if (!NumberText.TryParseShort(bytes, reading, out var value, out var scale, out var hasPoint))
                continue;
            var status = NumberText.Parse(bytes, reading, out var number);

            Assert.Equal(NumberStatus.Ok, status);
            Assert.True(number.TryLong(out var full));
            Assert.Equal(full, value);
            Assert.Equal(number.Scale, scale);
            Assert.Equal(number.HasPoint, hasPoint);
        }
    }

    [Fact] // ADR-0063: the short path is taken only where its reading is the common one
    public void The_short_path_is_taken_only_for_the_common_reading()
    {
        Assert.False(new NumberReading(new CsvColumn("V", SnapshotKind.Decimal) { Culture = CultureInfo.GetCultureInfo("sv-SE") }, CsvEncoding.Utf8).Short);
        Assert.False(new NumberReading(new CsvColumn("V", SnapshotKind.Decimal) { Culture = CultureInfo.GetCultureInfo("hi-IN") }, CsvEncoding.Utf8).Short);
        Assert.False(new NumberReading(new CsvColumn("V", SnapshotKind.Decimal) { DecimalPoint = "<>" }, CsvEncoding.Utf8).Short);
    }

    [Fact] // ADR-0063: random numbers of every shape read alike through the short path and the full reading, and as a CSV
    public void Random_numbers_read_alike()
    {
        var random = new Random(20261001);
        var file = new StringBuilder("D;I\n");
        var decimals = new List<object?>();
        var integers = new List<object?>();
        for (var i = 0; i < 5_000; i++)
        {
            var digits = random.Next(1, 20);
            var whole = string.Concat(Enumerable.Range(0, digits).Select(_ => (char)('0' + random.Next(10))));
            var places = random.Next(0, 4) == 0 ? "" : string.Concat(Enumerable.Range(0, random.Next(0, 6)).Select(_ => (char)('0' + random.Next(10))));
            var sign = random.Next(4) switch { 0 => "-", 1 => "+", _ => "" };
            var grouped = random.Next(2) == 0 ? Group(whole) : whole;
            var text = places.Length > 0 || random.Next(8) == 0 ? $"{sign}{grouped},{places}" : $"{sign}{grouped}";
            // The integer column takes at most eighteen of the digits, which a long always holds.
            var integer = whole[..Math.Min(18, whole.Length)];
            file.Append(text).Append(';').Append(sign).Append(random.Next(2) == 0 ? Group(integer) : integer).Append('\n');

            decimals.Add(decimal.Parse($"{sign}{whole}.{(places.Length > 0 ? places : "0")}", NumberStyles.Number, CultureInfo.InvariantCulture));
            integers.Add(long.Parse(sign + integer, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));
        }
        var german = CultureInfo.GetCultureInfo("de-DE");
        var schema = new CsvSchema([new("D", SnapshotKind.Decimal) { Culture = german }, new("I", SnapshotKind.Integer) { Culture = german }])
        {
            Separator = CsvSeparator.Semicolon,
        };

        var snapshot = Read(schema, file.ToString());

        Assert.Equal(decimals, Values(snapshot, "D"));
        Assert.Equal(integers, Values(snapshot, "I"));

        static string Group(string digits)
        {
            var text = new StringBuilder();
            for (var i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0)
                    text.Append('.');
                text.Append(digits[i]);
            }
            return text.ToString();
        }
    }
}
