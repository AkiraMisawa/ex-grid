using ExGrid.Data;
using Xunit;
using static ExGrid.Data.Tests.CsvFixtures;
using static ExGrid.Data.Tests.Fixtures;

namespace ExGrid.Data.Tests;

/// <summary>
/// A date read from a CSV is the clock it shows, wherever it is read (ADR-0064). .NET reads a value
/// marked <c>Z</c> or <c>GMT</c> as UTC and turns it into the reading machine's own clock, so a file
/// read in Tokyo would hold other dates than the same file read in London. The tests here move the
/// process's local time zone, where the platform lets them, and so run alone.
/// </summary>
[Collection(nameof(LocalTimeZone))]
public class CsvClockTests
{
    [Fact] // ADR-0064: a date marked UTC is held as the clock it shows, whatever the reading machine's time zone
    public void A_date_marked_utc_is_held_as_its_clock_in_any_time_zone()
    {
        var schema = new CsvSchema([new("When", SnapshotKind.Date) { DateFormats = ["yyyy-MM-ddTHH:mm:ssZ", "yyyy-MM-dd HH:mm:ss GMT", "yyyy-MM-ddTHH:mm:ssK"] }]);

        using (LocalTimeZone.Set("Asia/Tokyo"))
        {
            Assert.SkipWhen(TimeZoneInfo.Local.BaseUtcOffset == TimeSpan.Zero, "The platform does not let a process move its local time zone through TZ.");

            var snapshot = Read(schema, "When\n2026-09-30T10:00:00Z\n2026-09-30 10:00:00 GMT\n2026-09-30T10:00:00+09:00\n");

            var ten = new DateTime(2026, 9, 30, 10, 0, 0);
            Assert.Equal([ten, ten, ten], Values(snapshot, "When"));
        }
    }
}

/// <summary>The tests that move the process's local time zone, which run apart from every other.</summary>
[CollectionDefinition(nameof(LocalTimeZone), DisableParallelization = true)]
public sealed class LocalTimeZone
{
    /// <summary>Moves the local time zone to <paramref name="id"/> until the result is disposed. .NET
    /// reads <c>TZ</c> on Linux and macOS; elsewhere the zone stays as it was.</summary>
    public static IDisposable Set(string id)
    {
        var before = Environment.GetEnvironmentVariable("TZ");
        Environment.SetEnvironmentVariable("TZ", id);
        TimeZoneInfo.ClearCachedData();
        return new Restore(before);
    }

    private sealed class Restore(string? before) : IDisposable
    {
        public void Dispose()
        {
            Environment.SetEnvironmentVariable("TZ", before);
            TimeZoneInfo.ClearCachedData();
        }
    }
}
