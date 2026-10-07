using ExGrid.Cells;
using ExPivot.Chrome;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;

namespace ExPivot.Components;

// The live half (ADR-0067/0068): the source's Changed listened to, its changes gathered on the
// clock and asked for at most once per RedrawInterval, the Change Highlight's history of reports,
// and the Stale Report's notice when the newest data cannot be shown.
//
// How a change of data meets a question already out: it never cancels it. A user's layout question
// is answered first, and a question for newer data is too, so a source that changes faster than it
// answers still reaches the screen; the changes that arrived meanwhile are asked for once it lands,
// RedrawInterval after the last change reached the screen. A user's gesture, in turn, supersedes a
// question for newer data in flight — a user's layout always wins over a refresh — and its own
// question, asked after the change, answers it; a gesture laid out from the answer held asks
// nothing, so the changes the superseded question carried are gathered again. An answer to a
// superseded question is never painted. Refresh and Retry ask at once, as a person's request.
public partial class ExPivot
{
    // The clock everything here reads, and the grid is handed: the parameter, the registered
    // TimeProvider, or the system's, resolved as the grid resolves its own.
    private TimeProvider _time = TimeProvider.System;

    // The source whose Changed this ExPivot listens to, and the handler it listens with, held so
    // that the same one is removed.
    private PivotSource? _listened;
    private Action<PivotSourceChanged>? _onChanged;

    // A change of data no question asked since answers, and the Source Version the latest notice
    // named — null when it named none. Whether the question in flight answers changes of data:
    // one superseded with no question after it leaves them gathered again. Whether Refresh is
    // telling the source, which then asks itself.
    private bool _changed;
    private string? _changedTo;
    private bool _askingCarries;
    private bool _refreshing;

    // When a change of data last reached the screen — shown, or found unshowable — from which the
    // next redraw waits RedrawInterval; and the one timer that waits, made on the clock it runs on.
    private DateTimeOffset? _lastDataShown;
    private ITimer? _redrawTimer;
    private TimeProvider? _redrawTimerClock;
    private DateTimeOffset? _redrawDue;

    // The Change Highlight (ADR-0068/0161): the change times of the data versions under the layout
    // on screen — never their reports — and the delegate the grid is handed, one for as long as a
    // history lasts, null while the duration is zero.
    private ReportHistory? _history;
    private CellChangeOf<PivotReportRow>? _cellChangedAt;
    private Func<TimeSpan>? _durationNow;

    /// <summary>How long a mark lasts, read by the history when it is asked.</summary>
    private Func<TimeSpan> ChangeHighlightDurationNow => _durationNow ??= () => ChangeHighlightDuration;

    // The Stale Report (ADR-0067): what happened, while the newest data cannot be shown; the
    // answer held whose layout a cap refused, which the notice goes with once a layout that fits
    // lays it out; and whether what failed was a Refresh, which Retry then asks for again.
    private string? _stale;
    private PivotCube? _staleNewest;
    private bool _staleRetryRefreshes;

    private DateTimeOffset Now() => _time.GetUtcNow();

    /// <summary>Takes the clock up; a redraw waiting on another clock's timer is re-timed on this
    /// one.</summary>
    private void UseClock(TimeProvider clock)
    {
        if (ReferenceEquals(clock, _time))
            return;
        _time = clock;
        // The grid reads the time from it too.
        _gridVersion++;
        if (_redrawTimer is not null)
        {
            _redrawTimer.Dispose();
            _redrawTimer = null;
            _redrawTimerClock = null;
            _redrawDue = null;
        }
    }

    // ---- Listening (ADR-0067) ---------------------------------------------------------------

    /// <summary>Listens to <paramref name="source"/>'s <c>Changed</c>, and no longer to the one
    /// before it: a source handed over is a refresh, whose question answers whatever was gathered.</summary>
    private void Listen(PivotSource source)
    {
        StopListening();
        _changed = false;
        _changedTo = null;
        DisarmRedraw();
        Action<PivotSourceChanged> handler = change => OnSourceChanged(source, change);
        source.Changed += handler;
        _listened = source;
        _onChanged = handler;
    }

    private void StopListening()
    {
        if (_listened is { } source && _onChanged is { } handler)
            source.Changed -= handler;
        _listened = null;
        _onChanged = null;
    }

    /// <summary>The source's data moved on. Raised on whatever thread the change was learned on,
    /// so it is marshalled to the renderer's; nothing it does throws back into the source.</summary>
    private void OnSourceChanged(PivotSource source, PivotSourceChanged change)
    {
        if (_disposed)
            return;
        _ = InvokeAsync(async () =>
        {
            try
            {
                if (!_disposed && ReferenceEquals(source, _source))
                    await DataChangedAsync(change);
            }
            catch (Exception error) when (!_disposed)
            {
                await DispatchExceptionAsync(error);
            }
        });
    }

    /// <summary>A change of data, gathered: asked for at once when the last change reached the
    /// screen more than <see cref="RedrawInterval"/> ago and nothing is out, otherwise when that
    /// time comes, or when the question out lands.</summary>
    private Task DataChangedAsync(PivotSourceChanged change)
    {
        _changed = true;
        _changedTo = change.SourceVersion;
        // A notice of the very version on screen, with nothing out, has nothing to bring: the latest
        // notice says where the data stands now.
        if (_asking is null && _stale is null && _changedTo is not null && _report is { } report
            && ReferenceEquals(_reportSource, _source) && string.Equals(report.Cube.SourceVersion, _changedTo, StringComparison.Ordinal))
        {
            _changed = false;
            DisarmRedraw();
            return Task.CompletedTask;
        }
        return ScheduleRedrawAsync();
    }

    // ---- Gathering on the clock (ADR-0067) ----------------------------------------------------

    /// <summary>
    /// Asks for the changes gathered, from the newest version, no sooner than
    /// <see cref="RedrawInterval"/> after the last change reached the screen: at once when that
    /// time has passed, else when it comes. Nothing is asked while a question is out — its landing
    /// asks — or while Refresh is telling the source, which asks itself.
    /// </summary>
    private Task ScheduleRedrawAsync()
    {
        if (!_changed || _asking is not null || _refreshing || _disposed || _source is null)
            return Task.CompletedTask;
        var now = Now();
        var due = _lastDataShown is { } last ? last + RedrawInterval : now;
        if (due <= now)
        {
            DisarmRedraw();
            return AskForChangesAsync();
        }
        ArmRedraw(due, due - now);
        return Task.CompletedTask;
    }

    /// <summary>The whole answer, asked again for the layout the report is on, or on its way to
    /// (ADR-0067: the Leaf Aggregates are the size of the report, not of the data).</summary>
    private Task AskForChangesAsync() => PursueAsync(raise: _raisePending, force: true, Question.Live);

    private void ArmRedraw(DateTimeOffset due, TimeSpan wait)
    {
        if (_redrawDue == due)
            return;
        if (_redrawTimer is null || !ReferenceEquals(_redrawTimerClock, _time))
        {
            _redrawTimer?.Dispose();
            _redrawTimerClock = _time;
            _redrawTimer = _time.CreateTimer(OnRedrawDue, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
        _redrawDue = due;
        _redrawTimer.Change(wait, Timeout.InfiniteTimeSpan);
    }

    private void DisarmRedraw()
    {
        if (_redrawDue is null)
            return;
        _redrawDue = null;
        _redrawTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private void OnRedrawDue(object? state)
    {
        // The timer fires off the renderer's synchronisation context; everything it touches has to
        // go back onto it.
        _ = InvokeAsync(async () =>
        {
            try
            {
                if (_disposed || _redrawDue is not { } due)
                    return;
                // A timer and the clock are two sources: a tick a moment early ends nothing, and
                // the timer waits again for what remains.
                var now = Now();
                if (now < due)
                {
                    _redrawTimer?.Change(due - now, Timeout.InfiniteTimeSpan);
                    return;
                }
                _redrawDue = null;
                await ScheduleRedrawAsync();
            }
            catch (Exception error) when (!_disposed)
            {
                await DispatchExceptionAsync(error);
            }
        });
    }

    /// <summary>A question that carried changes of data was dropped with no question after it — a
    /// gesture the answer held laid out — so the changes are gathered again.</summary>
    private Task RegatherAsync(bool carried)
    {
        if (carried)
            _changed = true;
        return ScheduleRedrawAsync();
    }

    /// <summary>A question landed — answered, refused or failed. One that carried changes of data
    /// brought them to the screen now, or found them unshowable, and the next redraw waits
    /// <see cref="RedrawInterval"/> from here; the changes gathered meanwhile are asked for then,
    /// unless the answer was already the version the latest notice named.</summary>
    private Task LandedAsync(bool carried, string? version)
    {
        if (carried)
            _lastDataShown = Now();
        if (_changed && _changedTo is not null && string.Equals(version, _changedTo, StringComparison.Ordinal))
            _changed = false;
        return ScheduleRedrawAsync();
    }

    // ---- The Stale Report (ADR-0067) ----------------------------------------------------------

    /// <summary>The newest data cannot be shown: the report stays on the last version it could
    /// compute, and says what happened and as of when.</summary>
    /// <param name="reason">What happened, as the notice's sentence ends.</param>
    /// <param name="error">The source's failure, or null.</param>
    /// <param name="newest">The answer that arrived and broke a cap, held; null when nothing newer
    /// than the report is held.</param>
    /// <param name="retryRefreshes">Whether what failed was the source's Refresh itself, which
    /// Retry then asks for again rather than the report alone.</param>
    private void MarkStale(string reason, Exception? error, PivotCube? newest, bool retryRefreshes = false)
    {
        _stale = reason;
        _staleNewest = newest;
        _staleRetryRefreshes = retryRefreshes;
        if (error is not null)
            _lastError = error;
    }

    /// <summary>The Stale Report's Retry: asks again what failed — the source's Refresh, when that
    /// is what failed, otherwise the report's question.</summary>
    private Task RetryAsync()
    {
        if (_disposed || _source is null)
            return Task.CompletedTask;
        return _staleRetryRefreshes ? RefreshAsync() : AskAgainAsync();
    }

    /// <summary>The Stale Report's notice, drawn by the Chrome or the built-in markup inside
    /// ExPivot's live region; nothing while the report is the newest.</summary>
    private RenderFragment StaleNotice() => builder =>
    {
        if (_stale is not { } reason || _report is null)
            return;
        var context = StaleReportContext(reason);
        if (PivotChrome?.StaleReport(context) is { } custom)
        {
            builder.AddContent(0, custom);
            return;
        }
        builder.OpenComponent<PivotStaleReportView>(1);
        builder.AddComponentParameter(2, nameof(PivotStaleReportView.Context), context);
        builder.CloseComponent();
    };

    private PivotStaleReportContext StaleReportContext(string reason)
    {
        var zone = _time.LocalTimeZone;
        var asOf = TimeZoneInfo.ConvertTime(_shownAt, zone);
        var today = TimeZoneInfo.ConvertTime(Now(), zone).Date;
        // The time, in the report's culture; the date too, when it is not today's — a time alone
        // would say the report is fresher than it is.
        var asOfText = asOf.ToString(asOf.Date == today ? "T" : "G", _culture);
        // Retry is not offered twice at once: the question it asked, or the Refresh, is out.
        var retry = new PivotCommand(PivotCommandIds.Retry, Word(PivotCommandIds.Retry), !_loading && !_refreshing, RetryAsync);
        return new PivotStaleReportContext(
            PivotWords.Fill(Word(StaleReportWords.Notice), asOfText, reason), reason, asOf, asOfText, retry, Word);
    }
}
