using System.Globalization;
using ExPivot.Engine;

namespace ExPivot.Components;

// The asking half (ADR-0065): what the report is computed from, the question in flight, the answer
// held, and the layouts — the one the report shows, the one it is on its way to, and Defer Layout
// Update's pending one (ADR-0060).
public partial class ExPivot
{
    private PivotSource? _source;

    // The answer held, and the source that gave it: what a change that needs no new question is
    // laid out from (ADR-0059/0065). And the source the report on screen came from, whose Source
    // Version a field's Items and a cell's records are asked under: the answer held can be newer
    // than the report, when the caps refused its layout.
    private PivotCube? _cube;
    private PivotSource? _cubeSource;
    private PivotSource? _reportSource;

    // The layout the report shows; the one it is on its way to — what the Field List shows, and
    // what a question in flight was asked for; and, while Defer Layout Update is ticked, the
    // pending one the pane builds (ADR-0060). _layout differs from _shown only while a question is
    // out, or after the source failed.
    private PivotLayout _shown = PivotLayout.Empty;
    private PivotLayout _layout = PivotLayout.Empty;
    private PivotLayout? _pending;

    // A further change cancels the question in flight, and an answer to a superseded question is
    // discarded, always: the generation is what makes that correct, the cancellation is a courtesy
    // to the source (ADR-0025's rule, which ADR-0065 gives the pivot).
    private int _generation;
    private CancellationTokenSource? _asking;
    private bool _loading;

    // What the report could not do with the last change, in words, said on the toolbar; and the
    // source's last failure.
    private string? _refusal;
    private Exception? _lastError;

    /// <summary>The layout the pane shows and edits: the pending one while Defer Layout Update is
    /// ticked, otherwise the one the report is on, or on its way to.</summary>
    private PivotLayout PaneLayout => _pending ?? _layout;

    /// <summary>
    /// Excel's Refresh (ADR-0065): the source is told to refresh, and the report is asked for
    /// again, whether or not the answer held would lay it out. The report stays as it was until the
    /// answer lands. A source that cannot be refreshed — the bundled one — answers the same data
    /// again; it is refreshed by handing ExPivot a new source.
    /// </summary>
    public Task RefreshAsync() => InvokeAsync(async () =>
    {
        var source = _source ?? throw new InvalidOperationException("ExPivot has no Source yet.");
        var generation = _generation;
        await source.RefreshAsync();
        // A source that says its data moved on may already have been asked again, through its
        // Changed (ticket 15's seam): a question asked since is the newer one, and stands.
        if (generation == _generation && !_disposed)
        {
            await PursueAsync(raise: false, force: true);
            StateHasChanged();
        }
    });

    /// <summary>
    /// Asks again for the layout the report is on its way to, whatever the answer held — for a
    /// source whose data moved on (its <c>Changed</c>), and for a Stale Report's Retry
    /// (ADR-0066). The seam ticket 15 builds on; nothing raises it yet but Refresh.
    /// </summary>
    internal Task AskAgainAsync() => PursueAsync(raise: false, force: true);

    /// <summary>A layout the user's gesture produced, for the report (ADR-0060): applied at once
    /// when the answer held lays it out, asked of the source otherwise.</summary>
    private Task ApplyAsync(PivotLayout layout)
    {
        if (ReferenceEquals(layout, _layout))
        {
            _refusal = null;
            StateHasChanged();
            return Task.CompletedTask;
        }
        _layout = layout;
        return PursueAsync(raise: true);
    }

    /// <summary>A change the pane made (ADR-0060): while Defer Layout Update is ticked it builds
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

    /// <summary>A gesture on the report or the toolbar — a ± button, a Context Menu command, the
    /// Layout menu, the report filter band — applied to the layout the report is on. While Defer
    /// Layout Update holds a pending layout the gesture reaches it too, so Update does not take it
    /// back. A gesture on a field that no longer stands where it did changes nothing.</summary>
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

    // ---- Defer Layout Update (ADR-0060) -------------------------------------------------------

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
    /// Brings the report to <see cref="_layout"/> (ADR-0065). A layout the answer held lays out is
    /// laid out at once, and nothing is asked. Otherwise the question goes to the source and this
    /// returns: the Field List already shows the new layout, and the report stays as it was under
    /// the grid's loading indication until the answer lands. A question still in flight is
    /// cancelled, and its answer will be discarded. An Aggregation the source does not answer is
    /// refused here, by name, and never asked for.
    /// </summary>
    /// <param name="raise">Whether the layout, once shown, is raised through LayoutChanged — a
    /// user's change; not one the Consumer handed in.</param>
    /// <param name="force">Ask even when the answer held would lay the layout out: a new source,
    /// new caps, Refresh.</param>
    private async Task PursueAsync(bool raise, bool force = false)
    {
        var source = _source!;
        var layout = _layout;
        _refusal = null;
        if (NotOffered(source, layout) is { } aggregation)
        {
            Supersede();
            Refuse(PivotWords.Fill(Word("aggregation-not-offered"), Word(PivotWords.AggregationName(aggregation))));
            return;
        }
        if (!force && _cube is { } cube && ReferenceEquals(_cubeSource, source) && cube.Holds(layout))
        {
            // A change that needs no new question asks none (ADR-0059/0065).
            Supersede();
            await ShowAsync(cube, source, layout, raise);
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
        Supersede();
        var generation = _generation;
        var asking = new CancellationTokenSource();
        _asking = asking;
        SetLoading(true);
        _ = AskAsync(source, query, layout, generation, asking, raise);
    }

    /// <summary>
    /// One question, out to the source. Its answer is laid out only while it is the current
    /// question: one a further change superseded is discarded, always, whether it arrives, fails or
    /// is cancelled. Run detached, so asking never blocks; whatever fails in ExPivot's own code
    /// meanwhile reaches the renderer rather than an unobserved task.
    /// </summary>
    private async Task AskAsync(
        PivotSource source, PivotQuery query, PivotLayout layout, int generation, CancellationTokenSource asking, bool raise)
    {
        try
        {
            PivotAnswer answer;
            try
            {
                answer = await source.AggregateAsync(query, asking.Token);
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
                FinishAsking();
                // A failure is never silent (ADR-0025): the report stays as it was, and says why.
                Fail(error);
                StateHasChanged();
                return;
            }
            if (generation != _generation || _disposed)
                return;
            FinishAsking();
            if (answer.IsRefused)
            {
                Refuse(RefusalOf(answer.Refusal!));
                StateHasChanged();
                return;
            }
            PivotCube cube;
            try
            {
                cube = PivotEngine.Cube(query, answer, source.Fields);
            }
            catch (InvalidOperationException error)
            {
                // An answer to another question than the one asked is refused by name.
                Fail(error);
                StateHasChanged();
                return;
            }
            await ShowAsync(cube, source, layout, raise);
            StateHasChanged();
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

    private void FinishAsking()
    {
        _asking = null;
        SetLoading(false);
    }

    /// <summary>A question in flight is for a layout no longer wanted: its answer is discarded
    /// whenever it comes, and the source is told it need not finish.</summary>
    private void Supersede()
    {
        _generation++;
        if (_asking is { } asking)
        {
            _asking = null;
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

    /// <summary>
    /// Lays <paramref name="layout"/> out from <paramref name="cube"/> and shows it — unless the
    /// report would pass a cap on its rows or columns, which refuses it by name and leaves the
    /// report on the layout before (ADR-0065). A user's layout, once shown, is raised.
    /// </summary>
    private async Task ShowAsync(PivotCube cube, PivotSource source, PivotLayout layout, bool raise)
    {
        _cube = cube;
        _cubeSource = source;
        var report = PivotEngine.Report(cube, layout, _options);
        if (CapBrokenBy(report) is { } refusal)
        {
            Refuse(refusal);
            return;
        }
        _reportSource = source;
        Show(report, layout);
        _lastError = null;
        LoadBandItems();
        if (raise && !_emitted.TryGetValue(layout, out _))
        {
            _emitted.AddOrUpdate(layout, Emitted);
            StateHasChanged();
            await LayoutChanged.InvokeAsync(layout);
        }
    }

    private void Show(PivotReport report, PivotLayout layout)
    {
        // The order a selection is written in (ADR-0011): kept when only values moved.
        if (_report is null || !report.HasSameRowsAs(_report))
            _rowSequenceVersion++;
        _report = report;
        _shown = layout;
        BuildColumns();
    }

    /// <summary>The words or the culture changed: the report on screen is laid out again from the
    /// answer held, in the new ones, asking nothing.</summary>
    private void LayOutAgain()
    {
        // The report's own answer, which holds its layout by construction.
        if (_report is { } report)
            Show(PivotEngine.Report(report.Cube, _shown, _options), _shown);
        else
            BuildColumns();
    }

    /// <summary>The cap a report breaks, in words, or null (ADR-0065): Excel's rows and columns
    /// unless the Consumer set others.</summary>
    private string? CapBrokenBy(PivotReport report)
    {
        if (report.Rows.Count > _caps.MaxRows)
            return PivotWords.Fill(Word("refused-too-many-rows"), Count(_caps.MaxRows));
        if ((long)report.LabelColumns.Count + report.ValueColumns.Count > _caps.MaxColumns)
            return PivotWords.Fill(Word("refused-too-many-columns"), Count(_caps.MaxColumns));
        return null;
    }

    private string Count(long count) => count.ToString("N0", _culture);

    /// <summary>A refusal said where the user sees it, and the Pivot Layout back to the one the
    /// report shows (ADR-0065): nothing is raised for the refused layout. The pending layout, while
    /// Defer Layout Update is ticked, is the user's work in the pane, and stays.</summary>
    private void Refuse(string sentence)
    {
        _refusal = sentence;
        _layout = _shown;
    }

    /// <summary>The source failed: the report stays as it was and says why; the layout the pane
    /// shows is kept, so asking again asks for it.</summary>
    private void Fail(Exception error)
    {
        _lastError = error;
        _refusal = PivotWords.Fill(Word("source-failed"), error.Message);
    }

    private string RefusalOf(PivotSourceRefusal refusal) => refusal.Kind switch
    {
        PivotSourceRefusalKind.TooManyLeaves => PivotWords.Fill(Word("refused-too-many-cells"), Count(refusal.Limit ?? _caps.MaxLeaves)),
        PivotSourceRefusalKind.SourceVersionNotHeld => Word("data-changed"),
        _ => PivotWords.Fill(Word("source-refused"), refusal.Message),
    };

    /// <summary>The first Aggregation of <paramref name="layout"/>'s Value Fields that the source
    /// does not answer, or null: ExPivot never asks a source for one (ADR-0065).</summary>
    private static PivotAggregation? NotOffered(PivotSource source, PivotLayout layout)
    {
        foreach (var value in layout.Values)
        {
            if (!source.Features.Offers(value.Aggregation))
                return value.Aggregation;
        }
        return null;
    }

    /// <summary>Refuses by name a layout that places a field the source does not offer (ADR-0059).</summary>
    private static void CheckFields(PivotSource source, PivotLayout layout)
    {
        var offered = source.Fields.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var (area, field) in layout.Filters.Select(p => ("Filters", p.Field))
                     .Concat(layout.Columns.Select(p => ("Columns", p.Field)))
                     .Concat(layout.Rows.Select(p => ("Rows", p.Field)))
                     .Concat(layout.Values.Select(v => ("Values", v.Field))))
        {
            if (!offered.Contains(field))
                throw new InvalidOperationException($"The layout places '{field}' in {area}, and the source offers no Pivot Field of that name (ADR-0059).");
        }
    }
}
