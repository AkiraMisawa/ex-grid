using System.Globalization;
using ExGrid.Chrome;
using ExGrid.Data;
using ExGrid.Selection;
using ExGrid.Summarizing;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace ExGrid.Components;

// The Selection Summary (ADR-0130): Excel's status-bar figures over the selection. The grid asks
// and whoever holds the data answers, as for Find (ADR-0055); the grid shows only the answer to
// the current question. The question is the selection's ranges, the order they were read in, the
// visible columns, the figures shown and how many times the rows have moved under a standing
// selection: any of these moving clears the figures at once — in the render that shows the move —
// and asks again after it. The previous answer is never left beside a new selection, and a late
// answer is dropped by the question it answers, never by a time.
public partial class ExGrid<TRow>
{
    /// <summary>
    /// The push mode's answer to a Selection Summary (ADR-0130): the figures over the cells the
    /// request names, in the order it was read in, or a decline with a reason — never a partial
    /// figure. <see cref="GridSummary.Of{TRow}"/> is the reference for what each figure is. A bound
    /// <see cref="Source"/> answers through its own <c>SummarizeAsync</c>, and passing this beside
    /// one is refused by name. Neither: no figure is shown.
    /// </summary>
    [Parameter] public Func<GridSummaryRequest, CancellationToken, Task<GridSummaryResult>>? OnSummarize { get; set; }

    /// <summary>Whether the grid's own status line shows the Selection Summary (ADR-0130): on by
    /// default, so a grid that can summarise shows Excel's status-bar figures without being asked.
    /// Off: no strip and no figure on the grid. A Consumer listening to
    /// <see cref="OnSelectionSummaryChanged"/> is still told — the figures for a status bar of its
    /// own; with nobody listening either, nothing is asked.</summary>
    [Parameter] public bool ShowSelectionSummary { get; set; } = true;

    /// <summary>The figures the status line shows — Excel's Average, Count and Sum by default
    /// (ADR-0130). The Consumer's state: the figures menu reports a change through
    /// <see cref="SummaryFiguresChanged"/>, and the grid holds none.</summary>
    [Parameter] public SummaryFigures SummaryFigures { get; set; } = SummaryFigures.Default;

    /// <summary>The figures menu's choice (ADR-0130). The menu, Excel's right-click on the status
    /// line, is offered only where this has a delegate: nobody else would hold the choice.</summary>
    [Parameter] public EventCallback<SummaryFigures> SummaryFiguresChanged { get; set; }

    /// <summary>The Selection Summary as it now stands, raised after the render that shows each
    /// change (ADR-0130) — for a Consumer that shows the figures elsewhere, such as an
    /// application-wide status bar. It is never written to a live region by the grid.</summary>
    [Parameter] public EventCallback<SelectionSummary> OnSelectionSummaryChanged { get; set; }

    /// <summary>Whether anything can answer a Selection Summary (ADR-0130): <see cref="OnSummarize"/>,
    /// or a bound Source that can summarise. False: no figure ever appears.</summary>
    public bool CanSummarize => OnSummarize is not null || (Source?.CanSummarize ?? false);

    // The question the figures on screen belong to, and where its answer stands.
    private SummaryQuestion? _summaryQuestion;
    private SelectionSummary _summary = SelectionSummary.None;
    private CancellationTokenSource? _summaryCancellation;
    private bool _summaryAskOwed;
    private bool _summaryRaiseOwed;

    // How many times the rows under a standing selection may have moved: a changed row at a
    // selected position the Window held before, a changed row count, or an edit the grid handed over.
    private int _summaryRowsStamp;
    private IReadOnlyList<TRow>? _summaryRows;
    private int _summaryRowsStart;
    private int? _summaryRowsTotal;

    private sealed record SummaryQuestion(
        IReadOnlyList<SelectionRange> Ranges, CellPosition Focus, int Version, string[] Columns, SummaryFigures Figures, int RowsStamp);

    /// <summary>The status line stands whenever the grid can summarise, figures or none, so the
    /// strip the Viewport gives it does not come and go with every selection (ADR-0130).</summary>
    private bool ShowsSummaryStrip => ShowSelectionSummary && CanSummarize;

    /// <summary>
    /// Notes whether the rows moved under the figures (ADR-0130): a selected position the Window
    /// held before now holding a different row, or a different row count. A Window that only
    /// scrolled shows the same rows where both hold them, and moves nothing.
    ///
    /// <para>Walked only while a question stands — figures shown, or being asked for — and only
    /// over the positions it asks about, so the cost follows the Selection's rows in the Window,
    /// not the Window (ADR-0130, 2026-10-07). With no question, nothing on screen can be wrong, and
    /// the stamp moves so that the next question asks afresh. A question asked under another order
    /// goes with the Selection (ADR-0011), and is not walked either.</para>
    /// </summary>
    private void NoteRowsForSummary()
    {
        var moved = _summaryQuestion is not { } question || question.Version != _sequenceVersion
            || (_summaryRows is not null && SelectedRowsMoved(question.Ranges));
        if (moved)
            _summaryRowsStamp++;
        _summaryRows = _window;
        _summaryRowsStart = _windowStart;
        _summaryRowsTotal = _total;
    }

    /// <summary>Whether a row the figures were taken over moved, between the Window taken over them
    /// and the one in hand: a different row count, or a selected position both hold now holding a
    /// different row, by the row type's equality.</summary>
    private bool SelectedRowsMoved(IReadOnlyList<SelectionRange> ranges)
    {
        var rows = _window!;
        var previous = _summaryRows!;
        // A row added or removed anywhere may have shifted the selected rows, and a selected row
        // neither Window holds cannot show that it stayed (ADR-0130).
        if (_total != _summaryRowsTotal)
            return true;
        if (ReferenceEquals(previous, rows) && _windowStart == _summaryRowsStart)
            return false;
        var from = Math.Max(_windowStart, _summaryRowsStart);
        var to = Math.Min(_windowStart + rows.Count, _summaryRowsStart + previous.Count);
        var comparer = EqualityComparer<TRow>.Default;
        for (var i = 0; i < ranges.Count; i++)
        {
            var end = Math.Min(to, ranges[i].BottomRow + 1);
            for (var position = Math.Max(from, ranges[i].TopRow); position < end; position++)
            {
                if (!comparer.Equals(rows[position - _windowStart], previous[position - _summaryRowsStart]))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Tells the grid that the values under the selection may have moved where it cannot see them —
    /// a change outside the Window, a recalculation (ADR-0130). The figures clear at once and the
    /// question is asked again; nothing happens while no figure stands or is being asked for.
    /// </summary>
    public Task RefreshSummaryAsync()
        => InvokeAsync(() =>
        {
            if (_disposed || _summaryQuestion is null)
                return;
            AskSummaryAgain();
        });

    /// <summary>The values summed may have moved — an edit the grid handed over, a change it was
    /// told of: the next render of the root, which this one is not allowed to skip, clears the
    /// figures and asks again.</summary>
    private void AskSummaryAgain()
    {
        _summaryRowsStamp++;
        // A key reaches the grid through JavaScript, which re-renders nothing by itself.
        _suppressRender = false;
        StateHasChanged();
    }

    /// <summary>
    /// Run as the status line renders: the question the current state asks. A different one from
    /// the question on screen clears the figures in this very render and owes the asking to the
    /// render's end.
    /// </summary>
    private void ReviewSummary()
    {
        var selection = _selection.Selection;
        var asks = CanSummarize && (ShowSelectionSummary || OnSelectionSummaryChanged.HasDelegate) && SummaryFigures != SummaryFigures.None && SelectsTwoCells(selection);
        if (!asks)
        {
            if (_summaryQuestion is null && _summary.Status == SelectionSummaryStatus.None)
                return;
            CancelSummary();
            _summaryQuestion = null;
            _summary = SelectionSummary.None;
            _summaryRaiseOwed = true;
            return;
        }
        var figures = SummaryFigures & SummaryFigures.All;
        // Compared in place: this runs on every render of the root, and a question unchanged —
        // the common case, a scroll — allocates nothing.
        if (_summaryQuestion is { } standing && standing.Version == _sequenceVersion && standing.Figures == figures
            && standing.RowsStamp == _summaryRowsStamp && standing.Focus == selection.Focus && SameColumns(standing.Columns)
            && (ReferenceEquals(standing.Ranges, selection.Ranges) || standing.Ranges.SequenceEqual(selection.Ranges)))
        {
            return;
        }

        var names = new string[Columns.Count];
        for (var i = 0; i < Columns.Count; i++)
            names[i] = Columns[i].Name;
        CancelSummary();
        _summaryQuestion = new SummaryQuestion(selection.Ranges, selection.Focus, _sequenceVersion, names, figures, _summaryRowsStamp);
        _summary = new SelectionSummary(SelectionSummaryStatus.Pending, RequestFor(_summaryQuestion), null);
        _summaryAskOwed = true;
        _summaryRaiseOwed = true;
    }

    // Two distinct cells or more: a range of two, or two ranges that are not the same one cell —
    // Ctrl+clicking one cell twice selects one cell, whatever the areas add up to.
    private static bool SelectsTwoCells(GridSelection selection)
    {
        if (selection.IsEmpty)
            return false;
        var first = selection.Ranges[0];
        foreach (var range in selection.Ranges)
        {
            if (range.CellCount > 1 || range.TopRow != first.TopRow || range.LeftColumn != first.LeftColumn)
                return true;
        }
        return false;
    }

    private bool SameColumns(string[] names)
    {
        if (names.Length != Columns.Count)
            return false;
        for (var i = 0; i < names.Length; i++)
        {
            if (names[i] != Columns[i].Name)
                return false;
        }
        return true;
    }

    private static GridSummaryRequest RequestFor(SummaryQuestion question) => new()
    {
        Ranges = question.Ranges,
        Columns = question.Columns,
        RowSequenceVersion = question.Version,
        Figures = question.Figures,
        Focus = question.Focus,
    };

    private void CancelSummary()
    {
        _summaryCancellation?.Cancel();
        _summaryCancellation = null;
        _summaryAskOwed = false;
    }

    /// <summary>Run after the render: asks the question owed, and tells the Consumer where the
    /// summary stands now.</summary>
    private async Task SettleSummaryAsync()
    {
        if (_summaryRaiseOwed)
        {
            _summaryRaiseOwed = false;
            if (!_disposed && OnSelectionSummaryChanged.HasDelegate)
                await OnSelectionSummaryChanged.InvokeAsync(_summary);
        }
        if (_summaryAskOwed)
        {
            _summaryAskOwed = false;
            // Started, not awaited: a slow answerer must not hold back the rest of this render's
            // work, and its answer is matched to its question whenever it lands (ADR-0130).
            _ = AskOrDispatchAsync();
        }
    }

    private async Task AskOrDispatchAsync()
    {
        try
        {
            await AskSummaryAsync();
        }
        catch (Exception ex)
        {
            // The Consumer's failure, or its defect named (an answer that is not the answer), reaches
            // the host's error UI as any other handler's would.
            await DispatchExceptionAsync(ex);
        }
    }

    private async Task AskSummaryAsync()
    {
        if (_summaryQuestion is not { } question || _summary.Request is not { } request || _disposed)
            return;
        var cancellation = _summaryCancellation = new CancellationTokenSource();

        GridSummaryResult answer;
        try
        {
            answer = OnSummarize is { } summarize
                ? await summarize(request, cancellation.Token)
                : await Source!.SummarizeAsync(request, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return;
        }
        // Another question has been asked since, or none stands: this answer is about cells that
        // are no longer the selection, and is shown nowhere (ADR-0025's rule).
        if (_disposed || cancellation.IsCancellationRequested || !ReferenceEquals(question, _summaryQuestion))
            return;
        _summaryCancellation = null;

        ArgumentNullException.ThrowIfNull(answer);
        // An answer that is not the answer to the question is the Consumer's defect, named rather
        // than shown in part (ADR-0130, as ADR-0055 names one for Find).
        if (!answer.IsDeclined && answer.Figures != request.Figures)
        {
            throw new InvalidOperationException(FormattableString.Invariant(
                $"ExGrid: the Selection Summary answered the figures {answer.Figures}, and was asked for {request.Figures}. An answer gives every figure asked and no other, or declines (ADR-0130)."));
        }
        _summary = new SelectionSummary(
            answer.IsDeclined ? SelectionSummaryStatus.Declined : SelectionSummaryStatus.Answered, request, answer);
        _summaryRaiseOwed = true;
        _suppressRender = false;
        StateHasChanged();
    }

    /// <summary>The figures as the line shows them: those answered that are numbers, in Excel's
    /// order, each formatted by the core (ADR-0130).</summary>
    private IReadOnlyList<SummaryFigureText> SummaryTexts()
    {
        if (_summary is not { Status: SelectionSummaryStatus.Answered, Result: { } result })
            return [];
        var texts = new List<SummaryFigureText>();
        foreach (var figure in SummaryFigureOrder.Each)
        {
            if (result[figure] is { IsNumber: true } value)
                texts.Add(new SummaryFigureText(figure, SummaryLabelIds.For(figure), result.TextOf(figure) ?? SummaryText(figure, value)));
            // A number that is not finite makes the figure #NUM!, which is said rather than left out:
            // a Sum missing from the line reads as nothing to sum (ADR-0060, the spine's first rule).
            else if (result[figure] is { Error: AggregateError.NotANumber })
                texts.Add(new SummaryFigureText(figure, SummaryLabelIds.For(figure), "#NUM!"));
        }
        return texts;
    }

    /// <summary>
    /// A figure's text. Counts are whole numbers in the invariant culture. The others take the
    /// format of the Focus's column where it is a Number column with a format of its own — until
    /// Excel's own rule is read in a Windows run (ADR-0130) — and the number's own text otherwise:
    /// exact as it is, or a <c>double</c> to ten significant digits, as Excel's status bar shows one.
    /// </summary>
    private string SummaryText(SummaryFigures figure, AggregateResult value)
    {
        if (SummaryFigureOrder.IsCount(figure))
            return (value.Exact ?? (decimal)value.Number).ToString("0", CultureInfo.InvariantCulture);
        object number = value.Exact is { } exact ? exact : value.Number;
        var selection = _selection.Selection;
        if (!selection.IsEmpty && selection.Focus.Column < Columns.Count
            && Columns[selection.Focus.Column] is { Type: ColumnType.Number, Format: { } format })
        {
            try
            {
                return format(number);
            }
            catch (InvalidCastException)
            {
                // A format written for the column's own type — (int)v — cannot take the figure's
                // decimal or double; the figure's own text is shown rather than nothing.
            }
        }
        return value.Exact is { } exactValue
            ? AggregateArithmetic.Canonical(Math.Round(exactValue, 10)).ToString(CultureInfo.InvariantCulture)
            : value.Number.ToString("G10", CultureInfo.InvariantCulture);
    }

    /// <summary>The status line's summary box: the Chrome's fragment, or the core's own text. The
    /// box carries the right-click that opens the figures menu, so the menu is the core's whatever
    /// the Chrome (ADR-0130, ADR-0010).</summary>
    private void RenderSummary(RenderTreeBuilder builder)
    {
        var context = new SelectionSummaryContext(
            _summary.Status, SummaryTexts(), _summary.Result?.DeclineReason);
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", context.Status switch
        {
            SelectionSummaryStatus.Pending => "ex-summary ex-summary-pending",
            SelectionSummaryStatus.Declined => "ex-summary ex-summary-declined",
            _ => "ex-summary",
        });
        if (SummaryFiguresChanged.HasDelegate)
        {
            builder.AddAttribute(2, "oncontextmenu", EventCallback.Factory.Create<MouseEventArgs>(this, OpenSummaryMenu));
            builder.AddEventPreventDefaultAttribute(3, "oncontextmenu", true);
            builder.AddEventStopPropagationAttribute(4, "oncontextmenu", true);
        }
        if (Chrome?.SelectionSummary(context) is { } fragment)
        {
            builder.AddContent(5, fragment);
        }
        else
        {
            switch (context.Status)
            {
                case SelectionSummaryStatus.Pending:
                    builder.AddContent(6, Label(SummaryLabelIds.Pending));
                    break;
                case SelectionSummaryStatus.Declined:
                    builder.AddContent(7, context.DeclineReason);
                    break;
                default:
                    foreach (var figure in context.Figures)
                    {
                        builder.OpenElement(8, "span");
                        builder.SetKey(figure.Figure);
                        builder.AddAttribute(9, "class", "ex-summary-figure");
                        builder.AddContent(10, Label(figure.LabelId));
                        builder.AddContent(11, ": ");
                        builder.AddContent(12, figure.Text);
                        builder.CloseElement();
                    }
                    break;
            }
        }
        builder.CloseElement();
    }

    /// <summary>Excel's right-click on the status line: the six figures, each ticked or not, in the
    /// grid's popover frame (ADR-0039/0040). A tick reports the new choice; the menu stays, as
    /// Excel's does, and closes as any popover does.</summary>
    private void OpenSummaryMenu()
        => OpenConsumerPopover(context => builder =>
        {
            builder.OpenComponent<SummaryFiguresMenu>(0);
            builder.AddComponentParameter(1, nameof(SummaryFiguresMenu.Shown), SummaryFigures);
            builder.AddComponentParameter(2, nameof(SummaryFiguresMenu.Label), (Func<string, string>)Label);
            builder.AddComponentParameter(3, nameof(SummaryFiguresMenu.Toggle), (Func<SummaryFigures, Task>)ToggleSummaryFigureAsync);
            builder.AddComponentParameter(4, nameof(SummaryFiguresMenu.Popover), context);
            builder.CloseComponent();
        }, Label(SummaryLabelIds.Menu));

    private Task ToggleSummaryFigureAsync(SummaryFigures figure)
        => SummaryFiguresChanged.InvokeAsync(SummaryFigures ^ figure);
}
