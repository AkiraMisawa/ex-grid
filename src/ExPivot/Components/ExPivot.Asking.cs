using System.Globalization;
using ExGrid;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;

namespace ExPivot.Components;

// The asking half (ADR-0066): what the report is computed from, the question in flight, the answer
// held, and the layouts — the one the report shows, the one it is on its way to, and Defer Layout
// Update's pending one (ADR-0061). Every question says what it is for (Question), which decides
// what its refusal, its failure and its answer mean (ADR-0067).
public partial class ExPivot
{
    /// <summary>
    /// How ExPivot shares the thread while it makes an answer's cube and lays out its report
    /// (ADR-0066, PV-40): in slices of about 30 ms, yielding between them, so that a browser keeps
    /// painting, the report on screen stays as it was — under the loading indication — until the
    /// new one is complete, and a newer gesture supersedes the work. A quick layout yields nothing.
    /// Null, the default, is <see cref="PivotSlicing.Default"/>; a test hands in its own to count
    /// the yields, or to hold the work at one.
    /// </summary>
    [Parameter] public PivotSlicing? Slicing { get; set; }

    private PivotSource? _source;

    // The answer held, and the source that gave it: what a change that needs no new question is
    // laid out from (ADR-0060/0066). And the source the report on screen came from, whose Source
    // Version a field's Items and a cell's records are asked under: the answer held can be newer
    // than the report, when the caps refused its layout.
    private PivotCube? _cube;
    private PivotSource? _cubeSource;
    private PivotSource? _reportSource;

    // When the answer held arrived, and when the answer the report on screen was laid out from did:
    // the time a Stale Report says it shows the data as of, and the change time of the cells a
    // data version marks (ADR-0067/0068).
    private DateTimeOffset _cubeAt;
    private DateTimeOffset _shownAt;

    // The layout the report shows; the one it is on its way to — what the Field List shows, and
    // what a question in flight was asked for; and, while Defer Layout Update is ticked, the
    // pending one the pane builds (ADR-0061). _layout differs from _shown only while a question is
    // out, or after the source failed.
    private PivotLayout _shown = PivotLayout.Empty;
    private PivotLayout _layout = PivotLayout.Empty;
    private PivotLayout? _pending;

    // Whether _layout is a user's change not yet raised: a question that supersedes the user's own
    // — Refresh, Retry, a new source — carries the raise with it, so LayoutChanged still comes.
    private bool _raisePending;

    // A further change cancels the question in flight, and an answer to a superseded question is
    // discarded, always: the generation is what makes that correct, the cancellation is a courtesy
    // to the source (ADR-0025's rule, which ADR-0066 gives the pivot).
    private int _generation;
    private CancellationTokenSource? _asking;
    private bool _loading;

    // What the report could not do with the user's last change, in words, said on the Pivot
    // Toolbar; and the source's last failure.
    private string? _refusal;
    private Exception? _lastError;

    /// <summary>What a question is asked for, which decides what its answer, its refusal and its
    /// failure mean (ADR-0066/0067).</summary>
    private enum Question
    {
        /// <summary>For a layout: the user's gesture, the Consumer's layout or caps, or the first
        /// report. A refusal sends the layout back; its answer marks nothing.</summary>
        Layout,

        /// <summary>For newer data, asked by a person or the Consumer — Refresh, Retry, a new
        /// source — under the loading indication. An answer it cannot show leaves a Stale Report;
        /// its answer marks what changed.</summary>
        Data,

        /// <summary>For newer data, asked because the source said its data moved on: gathered, and
        /// asked quietly, so a live report does not flicker. Otherwise as <see cref="Data"/>.</summary>
        Live,
    }

    /// <summary>The layout the pane shows and edits: the pending one while Defer Layout Update is
    /// ticked, otherwise the one the report is on, or on its way to.</summary>
    private PivotLayout PaneLayout => _pending ?? _layout;

    /// <summary>
    /// Excel's Refresh (ADR-0066/0067): the source is told to refresh, and the report is asked for
    /// again — the whole answer, whether or not the answer held would lay it out — as when the
    /// source says its data moved on, but at once and under the loading indication. The report
    /// stays as it was until the answer lands, and the cells whose painted values changed are
    /// marked. A source that cannot be refreshed — the bundled one — answers the same data again;
    /// it is refreshed by handing ExPivot a new source, or a Change Batch. A Refresh that fails —
    /// the source cannot refresh, or its answer cannot be shown — leaves a Stale Report: the report
    /// stays on the version shown, and the notice under the Pivot Toolbar says the source could not
    /// answer, as of when, with Retry, which refreshes again (ADR-0067). The failure is kept
    /// (<see cref="LastError"/>). Before the first report there is nothing to be stale, and the
    /// Pivot Toolbar says it instead.
    /// </summary>
    public Task RefreshAsync() => InvokeAsync(async () =>
    {
        var source = _source ?? throw new InvalidOperationException("ExPivot has no Source yet.");
        var generation = _generation;
        // A source that refreshes says its data moved on: the question below answers that notice,
        // so it is not asked twice. A Stale Report's Retry is not offered meanwhile: the refresh is
        // out.
        _refreshing = true;
        if (_stale is not null)
            StateHasChanged();
        Exception? failure = null;
        try
        {
            await source.RefreshAsync();
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            failure = error;
        }
        finally
        {
            _refreshing = false;
        }
        if (_disposed)
            return;
        if (failure is not null)
        {
            // A source handed over meanwhile is the one the report waits on now, and its own
            // question answers it: the old one's failure says nothing about what is shown.
            if (!ReferenceEquals(source, _source))
                return;
            // A failure is never silent, and never takes the report away (ADR-0025). Changes the
            // source announced meanwhile are still asked for.
            RefreshFailed(failure);
            await ScheduleRedrawAsync();
            StateHasChanged();
            return;
        }
        if (!ReferenceEquals(source, _source))
            return;
        if (generation == _generation)
        {
            await PursueAsync(raise: _raisePending, force: true, Question.Data);
            StateHasChanged();
            return;
        }
        // A question asked meanwhile — a gesture's — is the newer one, and stands; it may have been
        // answered from the data before the refresh, so the report is asked again once it lands.
        _changed = true;
        _changedTo = null;
        await ScheduleRedrawAsync();
    });

    /// <summary>
    /// Asks again for the layout the report is on, or on its way to, whatever the answer held — a
    /// Stale Report's Retry (ADR-0067). The report stays as it was, under the loading indication,
    /// until the answer lands; one it still cannot show leaves the report stale.
    /// </summary>
    internal Task AskAgainAsync() => PursueAsync(raise: _raisePending, force: true, Question.Data);

    /// <summary>A layout the user's gesture produced, for the report (ADR-0061): applied at once
    /// when the answer held lays it out, asked of the source otherwise. A question for newer data
    /// in flight gives way to it: a user's layout always wins over a refresh.</summary>
    private Task ApplyAsync(PivotLayout layout)
    {
        if (ReferenceEquals(layout, _layout))
        {
            _refusal = null;
            StateHasChanged();
            return Task.CompletedTask;
        }
        _layout = layout;
        _raisePending = true;
        return PursueAsync(raise: true);
    }

    /// <summary>A change the pane made (ADR-0061): while Defer Layout Update is ticked it builds
    /// the pending layout, and the report and the source are left alone.</summary>
    private Task PaneApplyAsync(PivotLayout layout)
    {
        if (_pending is not null)
        {
            _pending = layout;
            _refusal = null;
            StateHasChanged();
            return Task.CompletedTask;
        }
        return ApplyAsync(layout);
    }

    /// <summary>A gesture on the report or the Pivot Toolbar — a ± button, a Context Menu command,
    /// the Layout menu, the report filter band — applied to the layout the report is on. While
    /// Defer Layout Update holds a pending layout the gesture reaches it too, so Update does not
    /// take it back. A gesture on a field that no longer stands where it did
    /// changes nothing.</summary>
    private Task ReportEditAsync(Func<PivotLayout, PivotLayout> edit)
    {
        if (_pending is { } pending && TryEdit(edit, pending) is { } edited)
            _pending = edited;
        return TryEdit(edit, _layout) is { } next ? ApplyAsync(next) : RenderAsync();
    }

    private static PivotLayout? TryEdit(Func<PivotLayout, PivotLayout> edit, PivotLayout layout)
    {
        try
        {
            return edit(layout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private Task RenderAsync()
    {
        StateHasChanged();
        return Task.CompletedTask;
    }

    // ---- Defer Layout Update (ADR-0061) -------------------------------------------------------

    private Task SetDeferAsync(bool defer)
    {
        if (defer == _pending is not null)
            return Task.CompletedTask;
        if (defer)
        {
            _pending = _layout;
            StateHasChanged();
            return Task.CompletedTask;
        }
        // Unticking applies the pending layout, as Excel does.
        var pending = _pending!;
        _pending = null;
        return ApplyAsync(pending);
    }

    private Task UpdateAsync() => _pending is { } pending ? ApplyAsync(pending) : Task.CompletedTask;

    // ---- Asking ----------------------------------------------------------------------------

    /// <summary>
    /// Brings the report to <see cref="_layout"/> (ADR-0066). A layout the answer held lays out is
    /// laid out at once, and nothing is asked. Otherwise the question goes to the source and this
    /// returns: the Field List already shows the new layout, and the report stays as it was until
    /// the answer lands — under the grid's loading indication, unless the question is a live one.
    /// A question still in flight is cancelled, and its answer will be discarded; one that carried
    /// changes of data and is followed by no question of its own leaves them gathered, to be asked
    /// for again (ADR-0067). An Aggregation the source does not answer is refused here, by name,
    /// and never asked for.
    /// </summary>
    /// <param name="raise">Whether the layout, once shown, is raised through LayoutChanged — a
    /// user's change; not one the Consumer handed in.</param>
    /// <param name="force">Ask even when the answer held would lay the layout out: a new source,
    /// new caps, Refresh, Retry, a change of data.</param>
    /// <param name="kind">What the question is for.</param>
    private async Task PursueAsync(bool raise, bool force = false, Question kind = Question.Layout)
    {
        var source = _source!;
        var layout = _layout;
        // A refusal answers the user's last change, and goes with their next one; a question for
        // newer data is not one, and leaves it standing.
        if (kind == Question.Layout)
            _refusal = null;
        var carried = _asking is not null && _askingCarries;
        // Each way out renders ExPivot: the gesture's event may have been a view's, which renders
        // only itself, and the pane, the Pivot Toolbar and the report are all ExPivot's to paint.
        if (NotOffered(source, layout) is { } aggregation)
        {
            Supersede();
            Refuse(PivotWords.Fill(Word("aggregation-not-offered"), Word(PivotWords.AggregationName(aggregation))));
            // Newer data cannot be asked for under this layout either, and the refusal says why:
            // the changes are not gathered again, or they would be refused again at once.
            if (kind != Question.Layout)
                _changed = false;
            else
                await RegatherAsync(carried);
            StateHasChanged();
            return;
        }
        if (!force && _cube is { } cube && ReferenceEquals(_cubeSource, source) && cube.Holds(layout))
        {
            // A change that needs no new question asks none (ADR-0060/0066). It is laid out from the
            // answer held at once, in the gesture's own turn, when the layout is quick, as it nearly
            // always is; one that grows long is laid out in slices, and is the work in flight
            // meanwhile — under the loading indication, superseded by a further change, and waited
            // for by changes of data (PV-40).
            Supersede();
            var laying = new CancellationTokenSource();
            _asking = laying;
            _askingCarries = carried;
            var work = LayOutHeldAsync(cube, source, layout, raise, kind, _generation, laying);
            if (!work.IsCompleted)
            {
                SetLoading(true);
                StateHasChanged();
                return;
            }
            await work;
            StateHasChanged();
            return;
        }
        PivotQuery query;
        try
        {
            query = PivotQuery.For(layout, _caps.MaxLeaves);
        }
        catch (ArgumentException error)
        {
            throw new InvalidOperationException($"The layout cannot be asked: {error.Message}", error);
        }
        // Newer data under the question on screen is made from the cube on screen (ADR-0161), and
        // names its version, so that the answer can say which of its leaves changed since. A layout
        // whose every redraw starts afresh asks as a new layout does.
        var previous = kind != Question.Layout && !PivotEngine.StartsAfresh(layout) ? HeldOnScreen(source) : null;
        if (previous is not null)
            query = query.WithChangedSince(previous.SourceVersion);
        Supersede();
        var generation = _generation;
        var asking = new CancellationTokenSource();
        _asking = asking;
        // Asked after every change of data notified so far, the question answers them all.
        _askingCarries = carried || _changed || kind != Question.Layout;
        _changed = false;
        DisarmRedraw();
        SetLoading(kind != Question.Live);
        _ = AskAsync(source, query, layout, generation, asking, raise, kind, previous);
        StateHasChanged();
    }

    /// <summary>
    /// One question, out to the source. Its answer is laid out only while it is the current
    /// question: one a further change superseded is discarded, always, whether it arrives, fails or
    /// is cancelled. Run detached, so asking never blocks; whatever fails in ExPivot's own code
    /// meanwhile reaches the renderer rather than an unobserved task.
    /// <para>
    /// The question stays out until its report is complete (PV-40): the answer's cube is made and
    /// its report laid out in slices, the report on screen staying as it was meanwhile, and a
    /// further change supersedes the question there as it would at the source. A large answer that
    /// arrived from another turn than the one that asked — the source's last slice, or its
    /// transport's — is made into its cube in a turn of its own.
    /// </para>
    /// </summary>
    private async Task AskAsync(
        PivotSource source, PivotQuery query, PivotLayout layout, int generation, CancellationTokenSource asking, bool raise,
        Question kind, PivotCube? previous)
    {
        try
        {
            var pace = new Pace(Pacing, asking.Token);
            PivotAnswer answer;
            bool arrivedAtOnce;
            try
            {
                var question = source.AggregateAsync(query, asking.Token);
                arrivedAtOnce = question.IsCompleted;
                answer = await question;
            }
            catch (OperationCanceledException) when (asking.IsCancellationRequested)
            {
                // Cancelled by a further change, which has asked its own question.
                return;
            }
            catch (Exception error)
            {
                if (generation != _generation || _disposed)
                    return;
                var failedCarried = FinishAsking();
                // A failure is never silent (ADR-0025): the report stays as it was, and says why.
                FailFor(kind, layout, error);
                await LandedAsync(failedCarried, version: null);
                StateHasChanged();
                return;
            }
            if (generation != _generation || _disposed)
                return;
            // When the answer arrived: the time the report it makes says it shows the data as of.
            var arrivedAt = Now();
            if (answer.IsRefused)
            {
                var refusedCarried = FinishAsking();
                if (IsStaleFor(kind, layout))
                    MarkStale(StaleRefusalOf(answer.Refusal!), error: null, newest: null);
                else
                    Refuse(RefusalOf(answer.Refusal!));
                await LandedAsync(refusedCarried, version: null);
                StateHasChanged();
                return;
            }
            PivotCube cube;
            try
            {
                if (!arrivedAtOnce)
                {
                    pace.Restart();
                    if (answer.LeafCount >= LargeStep)
                        await pace.YieldAsync();
                }
                // The engine builds the cube afresh when the answer's leaves are not those of the cube
                // it is made from (ADR-0161).
                cube = await pace.AfterAsync(previous is not null
                    ? PivotEngine.NextCubeAsync(previous, query, answer, source.Fields, Pacing, asking.Token)
                    : PivotEngine.CubeAsync(query, answer, source.Fields, Pacing, asking.Token));
                await pace.GoOnAsync(answer.LeafCount);
            }
            catch (OperationCanceledException) when (asking.IsCancellationRequested)
            {
                // Superseded while the cube was made: the further change asks, or lays out, its own.
                return;
            }
            catch (InvalidOperationException error)
            {
                if (generation != _generation || _disposed)
                    return;
                // An answer to another question than the one asked is refused by name.
                var failedCarried = FinishAsking();
                FailFor(kind, layout, error);
                await LandedAsync(failedCarried, version: null);
                StateHasChanged();
                return;
            }
            catch (OutOfMemoryException) when (generation == _generation && !_disposed && IsStaleFor(kind, layout))
            {
                await OutOfMemoryAsync();
                return;
            }
            catch when (generation == _generation && !_disposed)
            {
                FinishAsking();
                throw;
            }
            if (generation != _generation || _disposed)
                return;
            await LayOutAsync(cube, source, layout, raise, fresh: true, kind, generation, asking, pace, arrivedAt);
        }
        catch (Exception error) when (!_disposed)
        {
            await DispatchExceptionAsync(error);
        }
        finally
        {
            // Each question owns its token source, and disposes it once nothing awaits it: never
            // at the moment it is superseded, while the source may still hold the token.
            asking.Dispose();
        }
    }

    /// <summary>A layout of the answer held, the work in flight: laid out, and shown when it is
    /// complete and still the current work. It owns its token source, as a question does.</summary>
    private async Task LayOutHeldAsync(
        PivotCube cube, PivotSource source, PivotLayout layout, bool raise, Question kind, int generation, CancellationTokenSource laying)
    {
        try
        {
            await LayOutAsync(cube, source, layout, raise, fresh: false, kind, generation, laying, new Pace(Pacing, laying.Token), arrivedAt: null);
        }
        catch (Exception error) when (!_disposed)
        {
            await DispatchExceptionAsync(error);
        }
        finally
        {
            laying.Dispose();
        }
    }

    /// <summary>
    /// Lays <paramref name="layout"/> out from <paramref name="cube"/> in slices, and shows it once
    /// it is complete (PV-40): the report on screen stays as it was meanwhile, and nothing half made
    /// is ever shown. Superseded meanwhile — its token cancelled, or the generation moved on — it
    /// ends where it is, and shows nothing.
    /// </summary>
    private async Task LayOutAsync(
        PivotCube cube, PivotSource source, PivotLayout layout, bool raise, bool fresh, Question kind, int generation,
        CancellationTokenSource working, Pace pace, DateTimeOffset? arrivedAt)
    {
        Built built;
        try
        {
            built = await BuildAsync(cube, layout, pace, working.Token, data: fresh && kind != Question.Layout);
        }
        catch (OperationCanceledException) when (working.IsCancellationRequested)
        {
            return;
        }
        catch (OutOfMemoryException) when (generation == _generation && !_disposed && fresh && IsStaleFor(kind, layout))
        {
            await OutOfMemoryAsync();
            return;
        }
        catch when (generation == _generation && !_disposed)
        {
            // A layout that failed — an Order Key that threw — is no longer out, and its failure
            // reaches the renderer.
            FinishAsking();
            throw;
        }
        if (generation != _generation || _disposed)
            return;
        var carried = FinishAsking();
        await PresentAsync(built, cube, source, layout, raise, fresh, kind, arrivedAt);
        // An answer for newer data has landed, with what it carried; a layout of the answer held
        // brought none, and what a question it superseded carried is gathered again.
        if (fresh)
            await LandedAsync(carried, cube.SourceVersion);
        else
            await RegatherAsync(carried);
        StateHasChanged();
    }

    /// <summary>
    /// Memory ran out while the cube or the report of newer data was made (ADR-0161; ADR-0067's
    /// note of 2026-10-07). What was being built is dropped — nothing of it was kept — and the report
    /// on screen stays, as a Stale Report that says memory ran out rather than that the data is
    /// wrong: a WebAssembly heap does not give memory back, so the redraw may fail again. The next
    /// change asks again. It is not the source's failure, so <see cref="LastError"/> is left as it
    /// was.
    /// </summary>
    private async Task OutOfMemoryAsync()
    {
        var carried = FinishAsking();
        MarkStale(Word(StaleReportWords.OutOfMemory), error: null, newest: null);
        await LandedAsync(carried, version: null);
        StateHasChanged();
    }

    /// <summary>The answer held, when it is <paramref name="source"/>'s and the report on screen was
    /// laid out from it: what a redraw for newer data is made from (ADR-0161).</summary>
    private PivotCube? HeldOnScreen(PivotSource source)
        => _cube is { } held && ReferenceEquals(_cubeSource, source) && ReferenceEquals(_report?.Cube, held) ? held : null;

    /// <summary>The question in flight has landed: nothing is out any more. Returns whether it
    /// carried changes of data.</summary>
    private bool FinishAsking()
    {
        var carried = _askingCarries;
        _asking = null;
        _askingCarries = false;
        SetLoading(false);
        return carried;
    }

    /// <summary>A question in flight is for a layout no longer wanted: its answer is discarded
    /// whenever it comes, and the source is told it need not finish.</summary>
    private void Supersede()
    {
        _generation++;
        if (_asking is { } asking)
        {
            _asking = null;
            _askingCarries = false;
            asking.Cancel();
        }
        SetLoading(false);
    }

    private void SetLoading(bool loading)
    {
        if (_loading == loading)
            return;
        _loading = loading;
        // The grid paints the indication (ADR-0010): it must hear of it.
        _gridVersion++;
    }

    /// <summary>How ExPivot's work shares the thread: the Consumer's slicing, or the default.</summary>
    private PivotSlicing Pacing => Slicing ?? PivotSlicing.Default;

    /// <summary>A report laid out, with what was measured of it on the way (PV-40): whether its rows
    /// are those of the report on screen when it was done, what changed in its painted values since
    /// that report when it is newer data under the same layout and words — what the Change Highlight marks
    /// — and its label columns' widths under the metrics they were sized by; none of these for a
    /// report a cap refuses, which is shown nowhere.</summary>
    private sealed record Built(
        PivotReport Report, bool SameRows, PivotReportChanges? Changes, double[]? LabelWidths, GridMetrics Metrics);

    /// <summary>
    /// Lays <paramref name="layout"/> out from <paramref name="cube"/> in slices (PV-40): the
    /// report, then its rows compared with the report on screen, then — for newer data under the
    /// layout and words on screen — its painted values compared with that report's, then its label
    /// columns sized from their labels; in the words and the culture the report is in when it is done:
    /// words that changed meanwhile lay it out again.
    /// </summary>
    private async Task<Built> BuildAsync(PivotCube cube, PivotLayout layout, Pace pace, CancellationToken token, bool data = false)
    {
        while (true)
        {
            var options = _options;
            var metrics = _metrics;
            // Newer data is made from the report on screen (ADR-0161): the engine shares the rows
            // whose painted text did not change, and lays it out afresh when it cannot.
            var previous = _report;
            var report = await pace.AfterAsync(data && previous is not null
                ? PivotEngine.NextReportAsync(previous, cube, layout, options, Pacing, token)
                : PivotEngine.ReportAsync(cube, layout, options, Pacing, token));
            var size = report.Rows.Count + report.ValueColumns.Count;
            await pace.GoOnAsync(size);
            var comparedTo = _report;
            var sameRows = false;
            PivotReportChanges? changes = null;
            double[]? widths = null;
            // Compared with the report on screen as it is when the work is done: one put up meanwhile
            // is compared again, in slices as before, so that what is shown was measured against what
            // it replaces (PV-40).
            while (CapBrokenBy(report) is null)
            {
                comparedTo = _report;
                sameRows = false;
                changes = null;
                if (comparedTo is not null)
                {
                    sameRows = await pace.AfterAsync(report.HasSameRowsAsAsync(comparedTo, Pacing, token));
                    await pace.GoOnAsync(size);
                    // What changed is what the Change Highlight marks: nothing to find while it marks
                    // nothing.
                    if (data && ChangeHighlightDuration > TimeSpan.Zero && ExtendsHistory(comparedTo, report))
                    {
                        changes = await pace.AfterAsync(report.ChangesSinceAsync(comparedTo, Pacing, token));
                        await pace.GoOnAsync(size);
                    }
                }
                // A report made from the one on screen has its labels, and their widths.
                widths = comparedTo is not null && report.WasMadeFrom(comparedTo) && ReferenceEquals(_labelWidthsOf, comparedTo) && _labelWidthsMetrics == metrics
                    ? _labelWidths
                    : await LabelWidthsAsync(report, metrics, pace);
                // What follows — the report put on screen, and painted — has a turn of its own.
                await pace.GoOnAsync(size);
                if (ReferenceEquals(comparedTo, _report))
                    break;
            }
            if (ReferenceEquals(options, _options))
                return new Built(report, sameRows, changes, widths, metrics);
        }
    }

    /// <summary>Whether newer data laid out as <paramref name="report"/> extends the history of
    /// <paramref name="previous"/>'s: the same layout and words, so that only data can have changed a
    /// cell (ADR-0067).</summary>
    private static bool ExtendsHistory(PivotReport previous, PivotReport report)
        => ReferenceEquals(previous.Layout, report.Layout) && Equals(previous.Options, report.Options);

    /// <summary>
    /// Shows a report laid out — unless it would pass a cap on its rows or columns, which refuses it
    /// by name and leaves the report on the layout before (ADR-0066), or, for newer data under the
    /// layout on screen, leaves the report stale (ADR-0067). A user's layout, once shown, is raised.
    /// </summary>
    /// <param name="built">The report laid out, and what was measured of it.</param>
    /// <param name="cube">The answer the report is laid out from.</param>
    /// <param name="source">The source that gave it.</param>
    /// <param name="layout">The layout it was laid out under.</param>
    /// <param name="raise">Whether the layout, once shown, is raised through LayoutChanged.</param>
    /// <param name="fresh">Whether <paramref name="cube"/> is an answer that has just arrived,
    /// rather than the one held.</param>
    /// <param name="kind">What the question it answers was asked for.</param>
    /// <param name="arrivedAt">When a fresh answer arrived.</param>
    private async Task PresentAsync(
        Built built, PivotCube cube, PivotSource source, PivotLayout layout, bool raise, bool fresh, Question kind, DateTimeOffset? arrivedAt)
    {
        if (fresh)
        {
            _cube = cube;
            _cubeSource = source;
            _cubeAt = arrivedAt ?? Now();
        }
        var report = built.Report;
        if (CapBrokenBy(report) is { } broken)
        {
            if (fresh && IsStaleFor(kind, layout))
                MarkStale(PivotWords.Fill(Word(broken.StaleWord), Count(broken.Cap)), error: null, newest: cube);
            else
                Refuse(PivotWords.Fill(Word(broken.RefusalWord), Count(broken.Cap)));
            return;
        }
        _reportSource = source;
        // Only data marks a cell (ADR-0067): an answer that has just arrived for newer data.
        Show(built, layout, _cubeAt, data: fresh && kind != Question.Layout);
        if (fresh)
            _lastError = null;
        // The notice goes when an answer is laid out: a new one, or the newest held, which a cap
        // refused under the layout before.
        if (fresh || ReferenceEquals(cube, _staleNewest))
        {
            _stale = null;
            _staleNewest = null;
            _staleRetryRefreshes = false;
        }
        LoadShownItems();
        if (ReferenceEquals(layout, _layout))
            _raisePending = false;
        if (raise && !_emitted.TryGetValue(layout, out _))
        {
            _emitted.AddOrUpdate(layout, Emitted);
            StateHasChanged();
            await LayoutChanged.InvokeAsync(layout);
        }
    }

    /// <summary>
    /// Puts the report <paramref name="built"/> holds on screen. The order a selection is written in is kept when
    /// only values moved (ADR-0011). The Change Highlight's history records what changed when the
    /// report is newer data under the layout and words on screen, and starts again otherwise — a
    /// new layout, a sort, a collapse, a form, new words — so that only data marks a cell
    /// (ADR-0067/0068); the grid is handed one delegate for as long as a history lasts (ADR-0161).
    /// The report before is let go here: the history keeps times, not reports.
    /// </summary>
    /// <param name="built">The report, laid out, and what was measured of it: its rows and its
    /// painted values compared with the report on screen, and its label columns' widths.</param>
    /// <param name="layout">The layout it was laid out under.</param>
    /// <param name="arrivedAt">When the answer it was laid out from arrived; null to keep the time
    /// of the report on screen, whose answer it is laid out from again.</param>
    /// <param name="data">Whether it is an answer for newer data, which marks what changed.</param>
    private void Show(Built built, PivotLayout layout, DateTimeOffset? arrivedAt, bool data)
    {
        var report = built.Report;
        // A new report on screen: a laying out again of the one before has nothing left to do.
        if (_relabelling is { } relabelling)
        {
            _relabelling = null;
            relabelling.Cancel();
        }
        // Measured against the report on screen, in slices: BuildAsync compares again until the
        // report it compared with is the one on screen.
        if (!built.SameRows)
            _rowSequenceVersion++;
        var at = arrivedAt ?? _shownAt;
        if (data && _history is { } history && _report is { } previous && ExtendsHistory(previous, report))
        {
            // A history that cannot mark — a duration of zero — finds and records nothing.
            if (history.Answer is not null && built.Changes is { } changes)
                history.Record(report, changes, at);
        }
        else
        {
            _history = ReportHistory.Start(report, Now, ChangeHighlightDurationNow);
            _cellChangedAt = _history.Answer;
        }
        ArmLetGo();
        _report = report;
        _shown = layout;
        _shownAt = at;
        if (built.LabelWidths is { } widths)
            KeepLabelWidths(report, built.Metrics, widths);
        BuildColumns();
    }

    // The words or the culture moving on lays the report on screen out again; the work, when it
    // grows long, is cancelled by the next such change, a new report on screen, or the disposal.
    private CancellationTokenSource? _relabelling;

    /// <summary>The words or the culture changed: the report on screen is laid out again from the
    /// answer held, in the new ones, asking nothing — at once when it is quick, and in slices when
    /// it grows long, the report on screen staying until the new one is complete (PV-40). Work in
    /// flight lays its own report out in the new words.</summary>
    private void LayOutAgain()
    {
        _relabelling?.Cancel();
        _relabelling = null;
        if (_report is not { } report)
        {
            BuildColumns();
            return;
        }
        var relabelling = new CancellationTokenSource();
        _relabelling = relabelling;
        _ = RelabelAsync(report, relabelling);
    }

    private async Task RelabelAsync(PivotReport report, CancellationTokenSource relabelling)
    {
        try
        {
            // The report's own answer, which holds its layout by construction.
            var built = await BuildAsync(report.Cube, report.Layout, new Pace(Pacing, relabelling.Token), relabelling.Token);
            if (_disposed || relabelling.IsCancellationRequested || !ReferenceEquals(_report, report))
                return;
            _relabelling = null;
            Show(built, report.Layout, arrivedAt: null, data: false);
            StateHasChanged();
        }
        catch (OperationCanceledException) when (relabelling.IsCancellationRequested)
        {
            // Another change of words, or a new report, made it moot.
        }
        catch (Exception error) when (!_disposed)
        {
            await DispatchExceptionAsync(error);
        }
        finally
        {
            if (ReferenceEquals(_relabelling, relabelling))
                _relabelling = null;
            relabelling.Dispose();
        }
    }

    /// <summary>A step of work this large or larger — leaves, or rows and columns — that did not
    /// yield is followed by a yield when it left less than half the slice (PV-40); a smaller one
    /// never is, so quick work never reads the clock, as the engine's never does.</summary>
    private const int LargeStep = 512;

    /// <summary>
    /// ExPivot's share of the thread across the steps of one piece of work (PV-40): the engine's —
    /// the cube made, the report laid out, its rows compared — and its own, the label columns
    /// sized. A slice begins when the work does and after every yield. A step that yielded ended in
    /// a turn that has done a slice's work, so the next begins after a yield of its own
    /// (<see cref="AfterAsync"/>); a large step that did not is followed by one when it left less
    /// than half the slice (<see cref="GoOnAsync"/>). Quick work, as nearly all of it is, is done
    /// in the turn that asked for it.
    /// </summary>
    private sealed class Pace(PivotSlicing slicing, CancellationToken token)
    {
        private long _start = slicing.TimeProvider.GetTimestamp();

        /// <summary>Whether the slice has run for its budget.</summary>
        public bool Spent => slicing.TimeProvider.GetElapsedTime(_start) >= slicing.Budget;

        /// <summary>A slice begins now: in a turn the work did not begin in.</summary>
        public void Restart() => _start = slicing.TimeProvider.GetTimestamp();

        /// <summary>Yields the thread, and begins a slice; throws once the work is cancelled.</summary>
        public async Task YieldAsync()
        {
            await slicing.YieldAsync(token);
            token.ThrowIfCancellationRequested();
            Restart();
        }

        /// <summary>Awaits a step of the engine's, begun already, and yields after it when it
        /// yielded.</summary>
        public async Task<T> AfterAsync<T>(ValueTask<T> step)
        {
            if (step.IsCompleted)
                return step.GetAwaiter().GetResult();
            var result = await step;
            await YieldAsync();
            return result;
        }

        /// <summary>After a step of <paramref name="size"/>: a yield when the step was large and
        /// less than half the slice is left.</summary>
        public Task GoOnAsync(int size)
            => size >= LargeStep && slicing.TimeProvider.GetElapsedTime(_start) * 2 >= slicing.Budget ? YieldAsync() : Task.CompletedTask;
    }

    /// <summary>The cap a report breaks, or null (ADR-0066): Excel's rows and columns unless the
    /// Consumer set others — with the word that refuses a layout for it and the word that says
    /// newer data broke it, the one used asked for only when it is used.</summary>
    private (string RefusalWord, string StaleWord, long Cap)? CapBrokenBy(PivotReport report)
    {
        if (report.Rows.Count > _caps.MaxRows)
            return ("refused-too-many-rows", StaleReportWords.TooManyRows, _caps.MaxRows);
        if ((long)report.LabelColumns.Count + report.ValueColumns.Count > _caps.MaxColumns)
            return ("refused-too-many-columns", StaleReportWords.TooManyColumns, _caps.MaxColumns);
        return null;
    }

    private string Count(long count) => count.ToString("N0", _culture);

    /// <summary>A refusal said where the user sees it, and the Pivot Layout back to the one the
    /// report shows (ADR-0066): nothing is raised for the refused layout. The pending layout, while
    /// Defer Layout Update is ticked, is the user's work in the pane, and stays.</summary>
    private void Refuse(string sentence)
    {
        _refusal = sentence;
        _layout = _shown;
        _raisePending = false;
    }

    /// <summary>The source failed: the report stays as it was, and the Pivot Toolbar
    /// says why.</summary>
    private void Fail(Exception error)
    {
        _lastError = error;
        _refusal = PivotWords.Fill(Word("source-failed"), error.Message);
    }

    /// <summary>
    /// A question failed. For newer data under the layout on screen, the report is left stale
    /// (ADR-0067). A failed question for a layout is not stale data: the Pivot Toolbar says it, and
    /// the layout goes back to the one the report shows, as a refused one does, so the pane shows
    /// what the report was laid out under (ADR-0067 refined); nothing is raised for it. Before the
    /// first report there is none to go back to, and the pane keeps the layout, so the next change
    /// asks for it again. The pending layout, while Defer Layout Update is ticked, is the user's
    /// work in the pane, and stays.
    /// </summary>
    private void FailFor(Question kind, PivotLayout layout, Exception error)
    {
        if (IsStaleFor(kind, layout))
        {
            MarkStale(PivotWords.Fill(Word(StaleReportWords.SourceFailed), error.Message), error, newest: null);
            return;
        }
        Fail(error);
        if (_report is not null)
        {
            _layout = _shown;
            _raisePending = false;
        }
    }

    /// <summary>
    /// A Refresh the source could not carry out (ADR-0067 refined): the newest data cannot be
    /// shown, so the report stays on the version shown as a Stale Report, whose notice says the
    /// source could not answer, as of when, and whose Retry refreshes again — what failed was the
    /// refresh, and asking the source that did not refresh would show its old data as the newest.
    /// Without a report there is nothing to be stale, and the Pivot Toolbar says it.
    /// </summary>
    private void RefreshFailed(Exception error)
    {
        if (_report is null)
        {
            Fail(error);
            return;
        }
        MarkStale(PivotWords.Fill(Word(StaleReportWords.SourceFailed), error.Message), error, newest: null, retryRefreshes: true);
    }

    /// <summary>
    /// Whether an answer that cannot be shown leaves a Stale Report (ADR-0067): it was asked for
    /// newer data, under the layout the report on screen has. A layout that cannot be shown is not
    /// a Stale Report — it is refused, and the layout goes back — and neither is a first report.
    /// </summary>
    private bool IsStaleFor(Question kind, PivotLayout layout)
        => kind != Question.Layout && _report is not null && ReferenceEquals(layout, _shown);

    private string RefusalOf(PivotSourceRefusal refusal) => refusal.Kind switch
    {
        PivotSourceRefusalKind.TooManyLeaves => PivotWords.Fill(Word("refused-too-many-cells"), Count(refusal.Limit ?? _caps.MaxLeaves)),
        PivotSourceRefusalKind.SourceVersionNotHeld => Word("data-changed"),
        _ => PivotWords.Fill(Word("source-refused"), refusal.Message),
    };

    /// <summary>What a refusal of newer data says, after the time of the data shown.</summary>
    private string StaleRefusalOf(PivotSourceRefusal refusal) => refusal.Kind switch
    {
        PivotSourceRefusalKind.TooManyLeaves => PivotWords.Fill(Word(StaleReportWords.TooManyCells), Count(refusal.Limit ?? _caps.MaxLeaves)),
        _ => PivotWords.Fill(Word(StaleReportWords.SourceRefused), refusal.Message),
    };

    /// <summary>The first Aggregation of <paramref name="layout"/>'s Value Fields that the source
    /// does not answer, or null: ExPivot never asks a source for one (ADR-0066).</summary>
    private static PivotAggregation? NotOffered(PivotSource source, PivotLayout layout)
    {
        foreach (var value in layout.Values)
        {
            if (!source.Features.Offers(value.Aggregation))
                return value.Aggregation;
        }
        return null;
    }

    /// <summary>Refuses by name a layout that places a field the source does not offer (ADR-0060).</summary>
    private static void CheckFields(PivotSource source, PivotLayout layout)
    {
        var offered = source.Fields.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var (area, field) in layout.Filters.Select(p => ("Filters", p.Field))
                     .Concat(layout.Columns.Select(p => ("Columns", p.Field)))
                     .Concat(layout.Rows.Select(p => ("Rows", p.Field)))
                     .Concat(layout.Values.Select(v => ("Values", v.Field))))
        {
            if (!offered.Contains(field))
                throw new InvalidOperationException($"The layout places '{field}' in {area}, and the source offers no Pivot Field of that name (ADR-0060).");
        }
    }
}
