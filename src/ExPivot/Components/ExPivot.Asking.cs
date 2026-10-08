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
    /// How the local DataSource calculation shares the thread while it makes an answer's cube and lays out its report
    /// (ADR-0066, PV-40): in slices of about 30 ms, yielding between them, so that a browser keeps
    /// painting, the report on screen stays as it was — under the loading indication — until the
    /// new one is complete, and a newer gesture supersedes the work. A quick layout yields nothing.
    /// Null, the default, is <see cref="PivotSlicing.Default"/>; a test hands in its own to count
    /// the yields, or to hold the work at one. An explicit Source owns its calculation policy.
    /// </summary>
    [Parameter] public PivotSlicing? Slicing { get; set; }

    private PivotReportSource? _source;
    private PivotSource? _dataSource;
    private LocalPivotReportSource? _ownedSource;
    private TimeProvider? _ownedClock;
    private PivotSlicing? _ownedSlicing;
    private PivotReportClient? _client;
    private PivotReportState? _state;
    private PivotReportWindow _wantedWindow = new(0, 64);
    private async ValueTask<PivotReportSource> ResolveReportSourceAsync()
    {
        if (Source is { } reports)
        {
            if (_ownedSource is { } previous) _ = previous.DisposeAsync();
            _ownedSource = null;
            _dataSource = null;
            return reports;
        }
        if (_ownedSource is null || !ReferenceEquals(_dataSource, DataSource)
            || !ReferenceEquals(_ownedClock, _time) || !Equals(_ownedSlicing, Pacing))
        {
            var previous = _ownedSource;
            _dataSource = DataSource;
            _ownedClock = _time;
            _ownedSlicing = Pacing;
            var next = PivotReportSource.From(DataSource!, timeProvider: _time, slicing: Pacing);
            if (previous is not null)
                await next.ContinueFromAsync(previous);
            _ownedSource = next;
            if (previous is not null) await previous.DisposeAsync();
        }
        return _ownedSource;
    }

    // The answer held, and the source that gave it: what a change that needs no new question is
    // laid out from (ADR-0060/0066). And the source the report on screen came from, whose Source
    // Version a field's Items and a cell's records are asked under: the answer held can be newer
    // than the report, when the caps refused its layout.
    private PivotReportSource? _reportSource;

    // When the answer held arrived, and when the answer the report on screen was laid out from did:
    // the time a Stale Report says it shows the data as of, and the change time of the cells a
    // data version marks (ADR-0067/0068).
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
        Window,
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
        Supersede();
        var generation = _generation;
        var asking = new CancellationTokenSource();
        _asking = asking;
        _askingCarries = carried || _changed || kind is Question.Data or Question.Live;
        _changed = false;
        DisarmRedraw();
        SetLoading(kind != Question.Live);
        _ = AskWindowAsync(source, layout, generation, asking, raise, kind);
        StateHasChanged();
    }

    private async Task AskWindowAsync(PivotReportSource source, PivotLayout layout, int generation,
        CancellationTokenSource asking, bool raise, Question kind)
    {
        try
        {
            var client = _client!;
            var metrics = _metrics.CellMetrics;
            var settings = PivotReportSettings.From(_options) with
            {
                ChangeHighlightDuration = ChangeHighlightDuration,
                OrderKeyPolicies = OrderKeyPolicies ?? new Dictionary<string, string>(),
                LabelMetrics = new(metrics.WideWidthPx, metrics.DigitWidthPx, metrics.NarrowWidthPx,
                    metrics.FullWidthPx, metrics.OtherWidthPx, metrics.CellHorizontalPaddingPx, metrics.ExportGlyphWidths()),
            };
            // Refresh and Retry ask the provider again. So does a notice of newer data from a source
            // that declared it refreshes in full (ADR-0153): it cannot name its changes, and the
            // notice may be all its server knows of them; an incremental source is asked for them.
            var refreshData = kind == Question.Data
                || (kind == Question.Live && source.UpdateMode == PivotReportUpdateMode.FullRefresh);
            var adopted = await client.ReadAsync(layout, settings, _wantedWindow, _caps.MaxLeaves, asking.Token,
                markChanges: kind != Question.Layout, refreshData: refreshData);
            if (_disposed || generation != _generation) return;
            var carried = FinishAsking();
            if (!adopted || client.Current is not { } state)
            {
                var refusal = client.Refusal ?? new(PivotReportRefusalKind.InvalidResponse, "The report source returned no current Window.");
                if (refusal.SourceRefusal is { } sourceRefusal)
                {
                    if (IsStaleFor(kind, layout)) MarkStale(StaleRefusalOf(sourceRefusal), null, null);
                    else Refuse(RefusalOf(sourceRefusal));
                }
                else FailFor(kind, layout, new InvalidOperationException(refusal.Message));
                await LandedAsync(carried, null);
                StateHasChanged();
                return;
            }
            if (CapBrokenBy(state.Metadata) is { } broken)
            {
                if (IsStaleFor(kind, layout))
                    MarkStale(PivotWords.Fill(Word(broken.StaleWord), Count(broken.Cap)), null, state);
                else
                    Refuse(PivotWords.Fill(Word(broken.RefusalWord), Count(broken.Cap)));
                await LandedAsync(carried, state.Metadata.SourceVersion);
                StateHasChanged();
                return;
            }
            var previous = _report;
            if (previous is null || previous.RowSequenceVersion != state.Metadata.RowSequenceVersion)
                _rowSequenceVersion++;
            // The changes this Window carries are shown from now, on this component's clock.
            _changeTimes.Adopt(state, Now(), ChangeHighlightDuration);
            _state = state;
            _report = state.Metadata;
            _reportSource = source;
            _shown = layout;
            if (previous is null || previous.SourceVersion != _report.SourceVersion) _shownAt = Now();
            if (previous is null || previous.SourceVersion != _report.SourceVersion || kind is Question.Data or Question.Live)
            {
                _lastError = null;
                _stale = null;
                _staleNewest = null;
                _staleRetryRefreshes = false;
            }
            BuildColumns();
            LoadShownItems();
            if (ReferenceEquals(layout, _layout)) _raisePending = false;
            if (raise && !_emitted.TryGetValue(layout, out _))
            {
                _emitted.AddOrUpdate(layout, Emitted);
                await LayoutChanged.InvokeAsync(layout);
            }
            // A new version can change selected values outside the current Window.
            if (previous is not null && previous.Version != _report.Version && _grid is { } grid)
                await grid.RefreshSummaryAsync();
            if (kind == Question.Layout && carried && previous?.SourceVersion == _report.SourceVersion)
                await RegatherAsync(true);
            else await LandedAsync(carried, _report.SourceVersion);
            StateHasChanged();
        }
        catch (OperationCanceledException) when (asking.IsCancellationRequested) { }
        catch (OutOfMemoryException) when (!_disposed && generation == _generation)
        {
            await OutOfMemoryAsync(kind, layout);
        }
        catch (Exception error)
        {
            if (!_disposed && generation == _generation)
            {
                var carried = FinishAsking();
                FailFor(kind, layout, error);
                await LandedAsync(carried, null);
                StateHasChanged();
            }
        }
        finally { asking.Dispose(); }
    }

    /// <summary>
    /// Memory ran out while the report was computed (ADR-0067's note of 2026-10-07). What was being
    /// built is dropped — nothing of it was kept — and the report on screen stays. For newer data
    /// it is a Stale Report whose reason says memory ran out, not that the data is wrong: a
    /// WebAssembly heap does not give memory back, so a redraw may fail again, and the next change
    /// asks again. For a layout the user asked for, the layout is refused and goes back to the one
    /// the report shows, and the Pivot Toolbar says memory ran out. It is not the source's
    /// failure, so <see cref="LastError"/> is left as it was.
    /// </summary>
    private async Task OutOfMemoryAsync(Question kind, PivotLayout layout)
    {
        var carried = FinishAsking();
        if (IsStaleFor(kind, layout))
            MarkStale(Word(StaleReportWords.OutOfMemory), error: null, newest: null);
        else if (_report is not null)
            Refuse(Word("refused-out-of-memory"));
        else
            _refusal = Word("refused-out-of-memory");
        await LandedAsync(carried, null);
        StateHasChanged();
    }

    private Task WindowNeededAsync(RowRange range)
    {
        var wanted = new PivotReportWindow(range.Start, range.Count);
        if (wanted == _wantedWindow) return Task.CompletedTask;
        _wantedWindow = wanted;
        return PursueAsync(raise: _raisePending, kind: Question.Window);
    }

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

    private void LayOutAgain() => _ = PursueAsync(raise: _raisePending);

    /// <summary>The cap a report breaks, or null (ADR-0066): Excel's rows and columns unless the
    /// Consumer set others — with the word that refuses a layout for it and the word that says
    /// newer data broke it, the one used asked for only when it is used.</summary>
    private (string RefusalWord, string StaleWord, long Cap)? CapBrokenBy(PivotReportMetadata report)
    {
        if (report.RowCount > _caps.MaxRows)
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
    private static PivotAggregation? NotOffered(PivotReportSource source, PivotLayout layout)
    {
        foreach (var value in layout.Values)
        {
            if (!source.Features.Offers(value.Aggregation))
                return value.Aggregation;
        }
        return null;
    }

    /// <summary>Refuses by name a layout that places a field the source does not offer (ADR-0060).</summary>
    private static void CheckFields(PivotReportSource source, PivotLayout layout)
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
