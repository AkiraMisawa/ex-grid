using ExSheet.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace ExSheet.Components;

/// <summary>
/// The Sheet Day (ADR-0121): the day <c>TODAY()</c> answers. The engine is given it as data; this
/// part keeps it. It resolves the day from what the Consumer gave, or from the browser's time zone,
/// which the grid is told (ADR-0122), and moves it on when the day can have changed.
/// </summary>
public partial class ExSheet
{
    /// <summary>The longest a reading of the day stands: a clock change, a daylight-saving shift or a sleeping device makes a day late by at most this (ADR-0121).</summary>
    private static readonly TimeSpan LongestDayReading = TimeSpan.FromHours(1);

    /// <summary>
    /// A fixed Sheet Day: what <c>TODAY()</c> answers whatever the clock says (ADR-0121), for a report
    /// that must show the same day whenever it is opened. It goes before <see cref="TimeZone"/> and
    /// the browser's zone. Null, the default, leaves the day to the clock.
    /// </summary>
    [Parameter] public DateOnly? Today { get; set; }

    /// <summary>
    /// The time zone whose day <c>TODAY()</c> answers (ADR-0121), such as head office's for a site
    /// that keeps one business day for every user. Null, the default, takes the browser's zone, as
    /// Excel takes the day on the user's own device; until the browser has told it, <c>TODAY()</c>
    /// is <c>#GETTING_DATA</c>. The server's zone is never used. A fixed <see cref="Today"/> goes
    /// before it.
    /// </summary>
    [Parameter] public TimeZoneInfo? TimeZone { get; set; }

    /// <summary>Where the clock comes from: a registered <see cref="TimeProvider"/>, which a test controls, or the system's.</summary>
    [Inject] private IServiceProvider Services { get; set; } = default!;

    private TimeProvider? _clock;
    private TimeZoneInfo? _browserZone;
    private ITimer? _dayTimer;
    private bool _dayParametersSeen;
    private DateOnly? _todayParameter;
    private TimeZoneInfo? _timeZoneParameter;

    // What a change of Today or TimeZone recalculated, painted once the parameters are set.
    private SheetChange? _dayChange;

    private TimeProvider Clock => _clock ??= Services.GetService<TimeProvider>() ?? TimeProvider.System;

    /// <summary>The zone the day is read in: the Consumer's, else the browser's; null while neither is known or a fixed day is given.</summary>
    private TimeZoneInfo? DayZone => Today is null ? TimeZone ?? _browserZone : null;

    /// <summary>The Sheet Day as it stands now (ADR-0121): the fixed day, else the day in <see cref="DayZone"/>, else unknown.</summary>
    private DateOnly? ResolveDay()
    {
        if (Today is { } fixedDay) return fixedDay;
        if (DayZone is not { } zone) return null;
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(Clock.GetUtcNow(), zone).DateTime);
    }

    /// <summary>Gives a Sheet just opened its day, before any of its rows is read.</summary>
    private void GiveTheDay(Sheet sheet)
    {
        sheet.SetToday(ResolveDay());
        ArmTheDay();
    }

    /// <summary>
    /// Called as the parameters are set: a change of <see cref="Today"/> or <see cref="TimeZone"/>
    /// sets the new day at once, and what it recalculated is painted from
    /// <see cref="PaintTheDayAsync"/>.
    /// </summary>
    private void FollowTheDayParameters()
    {
        // Zones compare by what they are, so a Consumer that builds the same zone on every render
        // does not read the day again each time.
        if (_dayParametersSeen && Today == _todayParameter && Equals(TimeZone, _timeZoneParameter)) return;
        _dayParametersSeen = true;
        _todayParameter = Today;
        _timeZoneParameter = TimeZone;
        if (_sheet is not { } sheet) return;
        _dayChange = sheet.SetToday(ResolveDay());
        ArmTheDay();
    }

    private async Task PaintTheDayAsync()
    {
        if (_dayChange is not { } change) return;
        _dayChange = null;
        await ChangedAsync(change, raiseDocument: false, byUser: false);
    }

    /// <summary>
    /// The grid was told the browser's time zone (ADR-0122). A zone .NET does not know leaves the
    /// day unknown, and <c>TODAY()</c> waits, rather than take some other zone's day.
    /// </summary>
    private async Task OnBrowserTimeZoneAsync(string zone)
    {
        TimeZoneInfo? known;
        try
        {
            known = TimeZoneInfo.FindSystemTimeZoneById(zone);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            known = null;
        }
        _browserZone = known;
        if (_sheet is not { } sheet) return;
        var change = sheet.SetToday(ResolveDay());
        ArmTheDay();
        await ChangedAsync(change, raiseDocument: false, byUser: false);
    }

    /// <summary>
    /// Reads the day again when it can have changed: at the next midnight in the zone, and at most
    /// <see cref="LongestDayReading"/> after this reading. With a fixed day, or no zone, nothing is armed.
    /// </summary>
    private void ArmTheDay()
    {
        _dayTimer?.Dispose();
        _dayTimer = null;
        if (DayZone is not { } zone) return;
        var now = TimeZoneInfo.ConvertTime(Clock.GetUtcNow(), zone);
        var untilMidnight = now.Date.AddDays(1) - now.DateTime;
        var due = untilMidnight < LongestDayReading ? untilMidnight : LongestDayReading;
        if (due < TimeSpan.FromSeconds(1)) due = TimeSpan.FromSeconds(1);
        _dayTimer = Clock.CreateTimer(_ => _ = InvokeAsync(async () =>
        {
            try
            {
                await ReadTheDayAgainAsync();
            }
            catch (Exception ex)
            {
                // A timer is not a UI event, so nothing else would surface this.
                await DispatchExceptionAsync(ex);
            }
        }), null, due, Timeout.InfiniteTimeSpan);
    }

    private async Task ReadTheDayAgainAsync()
    {
        if (_disposedDay || _sheet is not { } sheet) return;
        var change = sheet.SetToday(ResolveDay());
        ArmTheDay();
        if (change.Rows.Count > 0 || change.Recalculated.Count > 0) await ChangedAsync(change, raiseDocument: false, byUser: false);
    }

    private bool _disposedDay;

    private void StopTheDay()
    {
        _disposedDay = true;
        _dayTimer?.Dispose();
        _dayTimer = null;
    }
}
