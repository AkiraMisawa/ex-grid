using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using ExGrid.Cells;
using ExGrid.Chrome;
using ExGrid.Clipboard;
using ExGrid.Finding;
using ExGrid.Keys;
using ExGrid.Rows;
using ExGrid.Selection;
using ExGrid.Summarizing;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using ScriptFunction = global::ExGrid.Data.ScriptFunction;

namespace ExGrid.Script;

// What the C# core's component carried for its Consumers beyond the first slice of this adapter, carried over the Script
// Core (ADR-0440): what the core asks as it paints comes with each row, a question the Consumer answers later is asked in
// its turn among the grid's events, and a function the browser asks by position as it paints or decides a key is the
// Consumer's own script.
public sealed partial class ExGrid<TRow>
{
    /// <summary>What role each row plays, detail, group or total (ADR-0024): asked of each row as it crosses to the
    /// browser, so it stays a cheap read of the row. Hold it in a field and hand over a new one when the kinds change: the
    /// delegate's identity is the change signal, and the rows the browser shows are sent again. None, and every row is a
    /// detail row.</summary>
    [Parameter] public Func<TRow, RowKind>? RowKind { get; set; }

    /// <summary>A cell's Font, Fill and Borders (ADR-0050 item 15), asked of each cell of each row as it crosses to the
    /// browser, as <see cref="CellState"/> is. Its identity is the change signal.</summary>
    [Parameter] public CellAppearanceOf<TRow>? CellAppearance { get; set; }

    /// <summary>When a cell's text last changed (ADR-0068), asked of each cell of each row as it crosses to the browser:
    /// the cell wears the Change Highlight for <see cref="ChangeHighlightDuration"/> after it. Its identity is the change
    /// signal. A live source with a Row Key offers one as its <c>CellChangedAt</c>.</summary>
    [Parameter] public CellChangeOf<TRow>? CellChangedAt { get; set; }

    /// <summary>How long a Change Highlight lasts after the time <see cref="CellChangedAt"/> answers; the core's second
    /// unless set.</summary>
    [Parameter] public TimeSpan? ChangeHighlightDuration { get; set; }

    /// <summary>Who holds the Row Marks of the Mark Column (ADR-0043), where the source keeps none: a
    /// <see cref="GridSource.From{TRow}(IReadOnlyList{TRow})"/> source keeps its own, and setting this beside one is
    /// refused, since one of the two would have to win quietly. Each row crosses with whether it is marked, and the state
    /// with the counts; a gesture is handed to it in its turn among the grid's events.</summary>
    [Parameter] public IRowMarks<TRow>? Marks { get; set; }

    /// <summary>Answers each copy the grid's own rules approved, in place of the grid's assembly (ADR-0050 item 9): asked
    /// in its turn among the grid's events, and written as given, or refused in the Consumer's sentence. The token is
    /// cancelled when a newer copy supersedes it. Exactly one of this and <see cref="CopyAnswer"/>.</summary>
    [Parameter] public Func<GridCopyRequest, CancellationToken, Task<GridCopyAnswer>>? CopyAnswerAsync { get; set; }

    /// <summary><see cref="CopyAnswerAsync"/>, answered at once.</summary>
    [Parameter] public Func<GridCopyRequest, GridCopyAnswer>? CopyAnswer { get; set; }

    /// <summary>Replace All (ADR-0332): the Consumer writes every match or none, as one step of its undo stack, and answers
    /// the count or the cells it could not write, under the Row Sequence Version it read its rows in. It is handed the
    /// request in its turn among the grid's events, and what it writes crosses before the browser hears it answered. None,
    /// and Ctrl+H is refused as Replace unavailable.</summary>
    [Parameter] public Func<GridReplaceRequest, Task<GridReplaceResult>>? OnReplace { get; set; }

    /// <summary>The answer to a Selection Summary (ADR-0130), where the source does not summarise: a source that does,
    /// as <see cref="GridSource.From{TRow}(IReadOnlyList{TRow})"/>'s does, answers through its own
    /// <see cref="IGridSource{TRow}.SummarizeAsync"/>, and setting this beside it is refused by name. The token is
    /// cancelled when the selection asked of is gone.</summary>
    [Parameter] public Func<GridSummaryRequest, CancellationToken, Task<GridSummaryResult>>? OnSummarize { get; set; }

    /// <summary>Whether the grid's own status line shows the Selection Summary (ADR-0130): on unless set off.</summary>
    [Parameter] public bool ShowSelectionSummary { get; set; } = true;

    /// <summary>The figures the status line shows, Excel's Average, Count and Sum unless set (ADR-0130).</summary>
    [Parameter] public SummaryFigures SummaryFigures { get; set; } = SummaryFigures.Default;

    /// <summary>Hears a change of figures made on the status line (ADR-0130): set, a right-click there opens Excel's menu
    /// of the six figures. The grid holds no choice: the Consumer hands the figures back through
    /// <see cref="SummaryFigures"/>.</summary>
    [Parameter] public EventCallback<SummaryFigures> SummaryFiguresChanged { get; set; }

    /// <summary>Hears the Selection Summary as it now stands (ADR-0130), for a Consumer that shows the figures
    /// elsewhere.</summary>
    [Parameter] public EventCallback<SelectionSummary> OnSelectionSummaryChanged { get; set; }

    /// <summary>Keys the grid takes from the page for the Consumer (ADR-0050 item 14), each in the canonical form
    /// <c>Control+Shift+K</c>; a key the core answers itself is refused by name. Requires
    /// <see cref="OnDeclaredKey"/>.</summary>
    [Parameter] public IReadOnlyCollection<string>? DeclaredKeys { get; set; }

    /// <summary>Hears a declared key pressed (ADR-0050 item 14), in its turn among the grid's events: every input made in
    /// the grid after the key waits until the handler completes, and what it writes crosses before.</summary>
    [Parameter] public EventCallback<GridDeclaredKeyPress> OnDeclaredKey { get; set; }

    /// <summary>Rows per page: a pager exists when this is set, and its absence is continuous scrolling (ADR-0015).</summary>
    [Parameter] public int? PageSize { get; set; }

    /// <summary>How many rows past the Window's edge the grid asks for early (ADR-0004); 0, the default, asks once the
    /// rows on screen are not all held.</summary>
    [Parameter] public int RangeRequestMargin { get; set; }

    /// <summary>
    /// The Row Headings' labels (ADR-0050 item 1): the Consumer's own script (ADR-0440), since the browser asks a label as
    /// it paints each position, a row not yet arrived included. The function is called with the row's position in the
    /// result and answers its text. Set as the grid is made, as the functions of Formula entry are (ADR-0431). None, and
    /// there is no band.
    /// </summary>
    [Parameter] public ScriptFunction? RowHeadings { get; set; }

    /// <summary>The Row Headings' width in pixels (ADR-0050, ADR-0028); estimated from the Cell Metrics unless set.</summary>
    [Parameter] public double? RowHeadingWidth { get; set; }

    /// <summary>Takes the whole header band away, so the first row starts at the Viewport's top (ADR-0050 item 1).</summary>
    [Parameter] public bool HideHeader { get; set; }

    /// <summary>
    /// The Name Box's text for the Focus cell (ADR-0051): the Consumer's own script (ADR-0440), called with
    /// <c>{ row, column }</c>, answering the text. Set as the grid is made. None, and the Name Box names no cell until the
    /// user types in it.
    /// </summary>
    [Parameter] public ScriptFunction? NameBoxLabel { get; set; }

    /// <summary>
    /// Whether a painted cell matches the Find panel's pattern (ADR-0055), for a source that finds by a rule of its own:
    /// the Consumer's own script (ADR-0440), called with the row's values by column name, the column's name and the
    /// pattern <c>{ text, matchCase, wholeCell }</c>, answering true or false. Set as the grid is made. None: over
    /// <see cref="GridSource.From{TRow}(IReadOnlyList{TRow})"/>'s source, which finds by the reference rule, the marks are
    /// drawn by that rule over the texts the browser holds; over any other source, none.
    /// </summary>
    [Parameter] public ScriptFunction? FindMatch { get; set; }

    /// <summary>
    /// Where Ctrl+arrow stops (ADR-0050 item 2): the Consumer's own script (ADR-0440), called with the Focus
    /// <c>{ row, column }</c> and the direction, <c>"up"</c>, <c>"down"</c>, <c>"left"</c> or <c>"right"</c>, answering the
    /// cell on that line where the data ends. Set as the grid is made. None, and Ctrl+arrow goes to the grid's edge.
    /// </summary>
    [Parameter] public ScriptFunction? DataEdge { get; set; }

    /// <summary>
    /// The grid's own words, by id (ADR-0036): null keeps the grid's, "" says nothing. It is asked of every id the grid
    /// words, the commands' (<see cref="GridCommandIds"/>), its fields' names (<see cref="GridLabelIds"/>), its
    /// refusals (<see cref="RefusalLabelIds"/>, <see cref="KeyRefusalLabelIds"/>), its summary
    /// (<see cref="SummaryLabelIds"/>), its find panel's (<see cref="FindPanelLabelIds"/>) and its reload notice
    /// (<see cref="ReloadNoticeLabelIds"/>), as the grid is made and whenever another function is set, and the browser
    /// words the grid from what it answered (ADR-0440 section 4). Pass a method or a function held in a field: a lambda
    /// that captures a local is another function on every render, and every id is asked again. The commands
    /// <see cref="ContextCommands"/> answers take their words from <see cref="ContextCommandLabel"/>.
    /// </summary>
    [Parameter] public Func<string, string?>? CommandLabel { get; set; }

    // Every id the grid words, read once from the classes that name them; a placeholder ("{column}") is a mark inside
    // the words, not an id.
    private static readonly string[] LabelIds =
    [
        .. new[]
        {
            IdsOf(typeof(GridCommandIds)), IdsOf(typeof(GridLabelIds)), IdsOf(typeof(RefusalLabelIds)), IdsOf(typeof(KeyRefusalLabelIds)),
            IdsOf(typeof(SummaryLabelIds)), IdsOf(typeof(FindPanelLabelIds)), IdsOf(typeof(ReloadNoticeLabelIds)),
        }.SelectMany(ids => ids).Where(id => !id.StartsWith('{')).Distinct(),
    ];

    private static IEnumerable<string> IdsOf([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] Type type)
        => type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)(field.GetRawConstantValue() ?? throw new InvalidOperationException($"Internal: {type.Name}.{field.Name} holds no id.")));

    private Func<string, string?>? _labelled;
    private KeyValuePair<string, string>[] _labels = [];

    // Asked once per function: another function is another wording, and the same one answers as it did (ADR-0003).
    private void WriteCommandLabels(Utf8JsonWriter json)
    {
        if (CommandLabel is not { } label)
            return;
        // Delegate equality, so a method group made again on each render is the same function.
        if (label != _labelled)
        {
            var labels = new List<KeyValuePair<string, string>>();
            foreach (var id in LabelIds)
            {
                if (label(id) is { } words)
                    labels.Add(KeyValuePair.Create(id, words));
            }
            _labels = [.. labels];
            _labelled = label;
        }
        json.WriteStartObject("commandLabels");
        foreach (var (id, words) in _labels)
            json.WriteString(id, words);
        json.WriteEndObject();
    }

    private RowPaint<TRow>? _paint;
    private IRowMarks<TRow>? _marks;

    // What the grid paints with each row, the same object while every answer is the same function: another is another
    // judgement, and the rows the browser shows are sent again with it (ADR-0003).
    private RowPaint<TRow> Paint => _paint ?? throw new InvalidOperationException("Internal: rows written before the parameters were set.");

    private void TakeSurface(IGridSource<TRow> source)
    {
        if (CopyAnswer is not null && CopyAnswerAsync is not null)
            throw new InvalidOperationException("ExGrid takes a copy's answer from one of CopyAnswer and CopyAnswerAsync, not both (ADR-0050 item 9).");
        // Over Rows the grid makes its own source, and OnSummarize answers in its place, as the core's onSummarize does
        // over rows it holds.
        var own = Rows is not null;
        if (OnSummarize is not null && source.CanSummarize && !own)
        {
            throw new InvalidOperationException($"OnSummarize answers a Selection Summary where the source does not, and {source.GetType().Name} summarises "
                + "(CanSummarize): one of the two would have to win quietly (ADR-0130).");
        }
        if (DeclaredKeys is { Count: > 0 } && !OnDeclaredKey.HasDelegate)
            throw new InvalidOperationException("DeclaredKeys needs OnDeclaredKey: a key taken from the page for nobody would be taken for nothing (ADR-0050 item 14).");
        if (PageSize is < 1)
            throw new ArgumentOutOfRangeException(nameof(PageSize), PageSize, "A page holds at least one row (ADR-0015).");
        if (RangeRequestMargin < 0)
            throw new ArgumentOutOfRangeException(nameof(RangeRequestMargin), RangeRequestMargin, "A margin is 0 rows or more (ADR-0004).");

        // The source the grid makes of Rows is made again for each new array, and marks it kept would go with it without a
        // word: over Rows the marks are the Consumer's Marks, as the core has them over rows it holds.
        var sourceMarks = own ? null : source.Marks;
        if (Marks is not null && sourceMarks is not null)
        {
            throw new InvalidOperationException($"Marks is set beside {source.GetType().Name}, which keeps Row Marks of its own: one of the two would have to win "
                + "quietly (ADR-0043).");
        }
        // Marks cross only where a Mark Column shows them: every other grid's rows go without (ADR-0440 section 1).
        var markColumn = Columns.FirstOrDefault(column => column.IsMarkColumn);
        var marks = markColumn is null ? null : Marks ?? sourceMarks;
        if (markColumn is not null && marks is null)
        {
            throw new InvalidOperationException(own
                ? $"Column '{markColumn.Name}' is the Mark Column over Rows, which the grid makes a source of again for each new array, and the marks would go "
                    + "with them: set Marks, or give the grid a Source, GridSource.From(rows) for rows held in memory (ADR-0043)."
                : $"Column '{markColumn.Name}' is the Mark Column, and nothing keeps its marks: set Marks, or give the grid a source that keeps them, as "
                    + "GridSource.From does (ADR-0043).");
        }
        var marksReplaced = !ReferenceEquals(marks, _marks);
        if (marksReplaced)
        {
            if (_marks is not null)
                _marks.Changed -= OnSourceChanged;
            _marks = marks;
            if (marks is not null)
                marks.Changed += OnSourceChanged;
        }
        // Handed only when it changes: marks recount on another RowKind (ADR-0043).
        if (marks is not null && (marksReplaced || !ReferenceEquals(RowKind, _paint?.RowKind)))
            marks.OnRowKindChanged(RowKind);

        var paint = new RowPaint<TRow>(CellState, RowKind, CellAppearance, CellChangedAt, _marks);
        if (_paint is null || !paint.Same(_paint))
        {
            // Another judgement of what a row paints: the rows the browser shows go again, with it (ADR-0006).
            _paint = paint;
            _stateOwed = true;
        }
    }

    private void LetGoOfSurface()
    {
        if (_marks is not null)
            _marks.Changed -= OnSourceChanged;
    }

    private IEnumerable<ScriptFunction> PaintScripts()
    {
        if (RowHeadings is { } rowHeadings)
            yield return rowHeadings;
        if (NameBoxLabel is { } nameBoxLabel)
            yield return nameBoxLabel;
        if (FindMatch is { } findMatch)
            yield return findMatch;
        if (DataEdge is { } dataEdge)
            yield return dataEdge;
    }

    private void WriteSurfaceOptions(Utf8JsonWriter json)
    {
        if (RowKind is not null)
            json.WriteBoolean("rowKinds", true);
        if (CellAppearance is not null)
            json.WriteBoolean("appearances", true);
        if (CellChangedAt is not null)
            json.WriteBoolean("changeTimes", true);
        if (ChangeHighlightDuration is { } duration)
            json.WriteNumber("changeHighlightDuration", duration.TotalMilliseconds);
        if (_marks is not null)
            json.WriteBoolean("marks", true);
        if (CopyAnswer is not null || CopyAnswerAsync is not null)
            json.WriteBoolean("copies", true);
        if (OnReplace is not null)
            json.WriteBoolean("replaces", true);
        // GridSource.From finds by the reference rule over the texts that cross with the rows (ADR-0332 section 4).
        if (FindMatch is null && _source is InMemoryGridSource<TRow>)
            json.WriteBoolean("referenceMarks", true);
        if (OnSummarize is not null)
            json.WriteBoolean("summarizes", true);
        // Only what the Consumer set: a default is the core's, written once there.
        if (!ShowSelectionSummary)
            json.WriteBoolean("showSelectionSummary", false);
        if (SummaryFigures != SummaryFigures.Default)
        {
            json.WriteStartArray("summaryFigures");
            foreach (var figure in SummaryFigureOrder.Each)
            {
                if (SummaryFigures.HasFlag(figure))
                    json.WriteStringValue(ScriptJson.Camel(figure));
            }
            json.WriteEndArray();
        }
        if (DeclaredKeys is { Count: > 0 } keys)
        {
            json.WriteStartArray("declaredKeys");
            foreach (var key in keys)
                json.WriteStringValue(key);
            json.WriteEndArray();
        }
        if (PageSize is { } pageSize)
            json.WriteNumber("pageSize", pageSize);
        if (RangeRequestMargin != 0)
            json.WriteNumber("rangeRequestMargin", RangeRequestMargin);
        if (RowHeadingWidth is { } headingWidth)
            json.WriteNumber("rowHeadingWidth", headingWidth);
        if (HideHeader)
            json.WriteBoolean("hideHeader", true);
        WriteCommandLabels(json);
    }

    private void WriteSurfaceScripts(Utf8JsonWriter json)
    {
        WriteScript(json, "rowHeadings", RowHeadings);
        WriteScript(json, "nameBoxLabel", NameBoxLabel);
        WriteScript(json, "findMatch", FindMatch);
        WriteScript(json, "dataEdge", DataEdge);
    }

    private IEnumerable<(string Kind, bool Heard)> SurfaceHears()
    {
        yield return ("declaredKey", OnDeclaredKey.HasDelegate);
        yield return ("selectionSummaryChanged", OnSelectionSummaryChanged.HasDelegate);
        yield return ("summaryFiguresChanged", SummaryFiguresChanged.HasDelegate);
    }

    // What the state carries for what crosses with each row: .NET's clock, by which the browser reads the change times on
    // its own (ADR-0068), and the marks' counts (ADR-0043).
    private void WriteSurfaceState(Utf8JsonWriter json)
    {
        if (CellChangedAt is not null)
            json.WriteNumber("now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        if (_marks is { } marks)
        {
            if (marks.Counts is { } counts)
            {
                json.WriteStartObject("markCounts");
                json.WriteNumber("markedInResult", counts.MarkedInResult);
                json.WriteNumber("rowsInResult", counts.RowsInResult);
                json.WriteNumber("markedOutsideResult", counts.MarkedOutsideResult);
                json.WriteEndObject();
            }
            else
            {
                json.WriteNull("markCounts");
            }
        }
    }

    /// <summary>A Replace All, a declared key and a marking gesture, as the browser hands them over: each is a write of
    /// the user's, handed to the Consumer in its turn, and what it wrote crosses before the browser hears it was.</summary>
    private Func<Task<string?>>? PrepareSurface(string kind, JsonElement payload)
    {
        switch (kind)
        {
            case "replace":
            {
                var request = new GridReplaceRequest
                {
                    Text = ScriptJson.Text(payload, "text"),
                    MatchCase = payload.GetProperty("matchCase").GetBoolean(),
                    WholeCell = payload.GetProperty("wholeCell").GetBoolean(),
                    Replacement = ScriptJson.Text(payload, "replacement"),
                    Scope = ScriptJson.Present(payload, "scope", out var scope) ? ScriptJson.Ranges(scope) : null,
                    Columns = ScriptJson.Texts(payload.GetProperty("columns")),
                    EditableColumns = ScriptJson.Texts(payload.GetProperty("editableColumns")),
                    RowSequenceVersion = payload.GetProperty("rowSequenceVersion").GetInt32(),
                };
                return async () =>
                {
                    var replace = OnReplace ?? throw new InvalidOperationException("Internal: the browser replaced through OnReplace, which the Consumer took away.");
                    return ReplaceJson(await replace(request));
                };
            }
            case "declaredKey":
            {
                var press = new GridDeclaredKeyPress(ScriptJson.Text(payload, "key"), payload.GetProperty("editOpen").GetBoolean(),
                    ScriptJson.Selection(payload.GetProperty("selection")), payload.GetProperty("rowSequenceVersion").GetInt32());
                return async () =>
                {
                    await OnDeclaredKey.InvokeAsync(press);
                    return null;
                };
            }
            case "mark":
            {
                var intent = MarkIntentOf(payload);
                return async () =>
                {
                    var marks = _marks ?? throw new InvalidOperationException("Internal: the browser marked a row of a grid with no Row Marks.");
                    await marks.OnMarkIntentAsync(intent);
                    return null;
                };
            }
            case "selectionSummaryChanged":
            {
                var summary = SummaryOf(payload);
                return async () =>
                {
                    await OnSelectionSummaryChanged.InvokeAsync(summary);
                    return null;
                };
            }
            case "summaryFiguresChanged":
            {
                var figures = FiguresOf(payload);
                return async () =>
                {
                    await SummaryFiguresChanged.InvokeAsync(figures);
                    return null;
                };
            }
            default:
                return null;
        }
    }

    private RowMarkIntent<TRow> MarkIntentOf(JsonElement payload) => ScriptJson.Text(payload, "kind") switch
    {
        "oneRow" => new RowMarkIntent<TRow>.OneRow(_rows.RowOf(payload.GetProperty("row").GetInt64()), payload.GetProperty("marked").GetBoolean()),
        "allRows" => new RowMarkIntent<TRow>.AllRows(payload.GetProperty("marked").GetBoolean(), payload.GetProperty("rowSequenceVersion").GetInt32()),
        "positions" => new RowMarkIntent<TRow>.Positions(
            [.. payload.GetProperty("ranges").EnumerateArray().Select(range => new RowRange(range.GetProperty("start").GetInt32(), range.GetProperty("count").GetInt32()))],
            payload.GetProperty("rowSequenceVersion").GetInt32()),
        var other => throw new InvalidOperationException($"Internal: the Script Core marked rows by '{other}'."),
    };

    private static string ReplaceJson(GridReplaceResult result) => Json(json =>
    {
        ArgumentNullException.ThrowIfNull(result);
        json.WriteStartObject();
        if (result.IsReplaced)
        {
            json.WriteString("kind", "replaced");
            json.WriteNumber("count", result.Count);
        }
        else
        {
            json.WriteString("kind", "refused");
            json.WriteStartArray("cells");
            foreach (var cell in result.RefusedCells)
            {
                json.WriteStartObject();
                json.WriteNumber("row", cell.Row);
                json.WriteString("column", cell.Column);
                if (cell.Message is { } message)
                    json.WriteString("message", message);
                else
                    json.WriteNull("message");
                json.WriteEndObject();
            }
            json.WriteEndArray();
        }
        json.WriteNumber("rowSequenceVersion", result.RowSequenceVersion);
        json.WriteEndObject();
    });

    /// <summary>The Consumer's answer to the copy the browser numbered <paramref name="number"/> (ADR-0050 item 9), asked
    /// in its turn among the events and written as the core reads a copy's answer. The token is cancelled when the
    /// browser gives the copy up for a newer one.</summary>
    internal Task<string?> CopyAsync(long number, JsonElement request)
    {
        var plan = request.GetProperty("plan");
        var asked = new GridCopyRequest(
            new CopyPlan(ScriptJson.Ranges(plan.GetProperty("segments")), ScriptJson.Member<CopyOrientation>(plan.GetProperty("orientation")),
                plan.GetProperty("totalRows").GetInt32(), plan.GetProperty("totalColumns").GetInt32()),
            request.GetProperty("withHeaders").GetBoolean(), request.GetProperty("rowSequenceVersion").GetInt32());
        return InTurnAsync(new Heard(Answered(number, async cancelled =>
        {
            Func<GridCopyRequest, CancellationToken, Task<GridCopyAnswer>> ask = CopyAnswer is { } now
                ? (request, _) => Task.FromResult(now(request))
                : CopyAnswerAsync ?? throw new InvalidOperationException("Internal: the browser asked a copy's answer of a Consumer that took CopyAnswer away.");
            GridCopyAnswer answer;
            try
            {
                answer = await ask(asked, cancelled);
                ArgumentNullException.ThrowIfNull(answer);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && cancelled.IsCancellationRequested))
            {
                // A copy whose answer failed is said where the copy was made, and writes nothing (CON-8, ADR-0005): the
                // browser refuses it aloud and surfaces the failure, as the core does for a script's failed answer. The
                // grid goes on, where handing it to the renderer would end the circuit over one copy. Only the type
                // crosses: an exception's message is the server's, which Blazor keeps from the browser, so the whole
                // exception is logged here, where the server keeps it.
                Logger.LogError(ex, "ExGrid: the Consumer's copy answer threw, and the copy wrote nothing (ADR-0005).");
                return Json(json =>
                {
                    json.WriteStartObject();
                    json.WriteString("kind", "failed");
                    json.WriteString("error", ex.GetType().FullName);
                    json.WriteEndObject();
                });
            }
            return Json(json =>
            {
                json.WriteStartObject();
                if (answer.Sentence is { } sentence)
                {
                    json.WriteString("kind", "refuse");
                    json.WriteString("sentence", sentence);
                }
                else
                {
                    json.WriteString("kind", "write");
                    json.WriteString("text", answer.Text);
                    json.WriteString("html", answer.Html);
                }
                json.WriteEndObject();
            });
        }), Answers: true, SendsState: false));
    }

    /// <summary>The Selection Summary the browser numbered <paramref name="number"/> (ADR-0130), asked of the source
    /// numbered <paramref name="generation"/>, or of <see cref="OnSummarize"/> where it is null, in its turn among the
    /// events, and answered aside: the events and the next question after it do not wait for its answer, so one the user
    /// replaced, which a Consumer may go on answering, holds nothing up. One asked of a source since replaced is
    /// answered with nothing, and the browser shows no figure for it.</summary>
    internal Task<string?> SummarizeAsync(long number, int? generation, JsonElement request)
    {
        var asked = new GridSummaryRequest
        {
            Ranges = ScriptJson.Ranges(request.GetProperty("ranges")),
            Columns = ScriptJson.Texts(request.GetProperty("columns")),
            RowSequenceVersion = request.GetProperty("rowSequenceVersion").GetInt32(),
            Figures = FiguresOf(request.GetProperty("figures")),
            Focus = ScriptJson.Present(request, "focus", out var focus) ? ScriptJson.Cell(focus) : null,
        };
        return InTurnAsync(new Heard(Answered(number, async cancelled =>
        {
            GridSummaryResult result;
            if (generation is { } numbered)
            {
                if (SourceOf(numbered) is not { } source)
                    return null;
                result = await source.SummarizeAsync(asked, cancelled);
            }
            else
            {
                var summarize = OnSummarize ?? throw new InvalidOperationException("Internal: the browser asked OnSummarize, which the Consumer took away.");
                result = await summarize(asked, cancelled);
            }
            return SummaryJson(result, FocusFormat(asked));
        }), Answers: true, SendsState: false, Aside: true));
    }

    private static SummaryFigures FiguresOf(JsonElement json)
    {
        var figures = SummaryFigures.None;
        foreach (var figure in json.EnumerateArray())
            figures |= ScriptJson.Member<SummaryFigures>(figure);
        return figures;
    }

    // The format of the Focus's column where it is a Number column with one (ADR-0130): the figures take it, a grid's
    // formats being its columns', as Excel's status bar takes the active cell's.
    // The Focus's column is named by its place among the request's columns, the grid's order, which a reorder the Consumer
    // has yet to hand back may still differ from Columns'.
    private Func<object, string>? FocusFormat(GridSummaryRequest asked)
        => asked.Focus is { } cell && cell.Column < asked.Columns.Count
            && Columns.FirstOrDefault(column => column.Name == asked.Columns[cell.Column]) is { Type: ColumnType.Number, Format: { } format }
            ? format
            : null;

    // A figure's text where the answer gave none: in the Focus column's format, written here because the format is the
    // Consumer's .NET function, which the browser cannot run on a figure no row holds. Counts keep their own text, and
    // a format written for the column's own type that cannot take the figure leaves the browser to write it (ADR-0130).
    private static string? FormattedFigure(SummaryFigures figure, Data.AggregateResult value, Func<object, string>? format)
    {
        if (format is null || SummaryFigureOrder.IsCount(figure) || !value.IsNumber)
            return null;
        try
        {
            return format(value.Exact is { } exact ? exact : value.Number);
        }
        catch (InvalidCastException)
        {
            return null;
        }
    }

    private static string SummaryJson(GridSummaryResult result, Func<object, string>? format) => Json(json =>
    {
        ArgumentNullException.ThrowIfNull(result);
        json.WriteStartObject();
        if (result.DeclineReason is { } reason)
        {
            json.WriteString("kind", "declined");
            json.WriteString("reason", reason);
        }
        else
        {
            json.WriteString("kind", "answered");
            json.WriteStartObject("figures");
            foreach (var figure in SummaryFigureOrder.Each)
            {
                if (!result.Figures.HasFlag(figure))
                    continue;
                json.WritePropertyName(ScriptJson.Camel(figure));
                WriteAggregate(json, result[figure] ?? throw new InvalidOperationException($"Internal: a summary answered '{figure}' without its figure."));
            }
            json.WriteEndObject();
            json.WriteStartObject("texts");
            foreach (var figure in SummaryFigureOrder.Each)
            {
                if (result.Figures.HasFlag(figure)
                    && (result.TextOf(figure) ?? FormattedFigure(figure, result[figure] ?? throw new InvalidOperationException($"Internal: a summary answered '{figure}' without its figure."), format)) is { } text)
                    json.WriteString(ScriptJson.Camel(figure), text);
            }
            json.WriteEndObject();
        }
        json.WriteEndObject();
    });

    private static void WriteAggregate(Utf8JsonWriter json, Data.AggregateResult result)
    {
        json.WriteStartObject();
        if (result.IsEmpty)
        {
            json.WriteString("kind", "empty");
        }
        else if (result.Error != Data.AggregateError.None)
        {
            json.WriteString("kind", "error");
            json.WriteString("error", ScriptJson.Camel(result.Error));
        }
        else
        {
            json.WriteString("kind", "number");
            WriteFigureNumber(json, result.Number);
            if (result.Exact is { } exact)
            {
                // The decimal's digits and scale as they stand, trailing zeros and all, as the Script Core holds one.
                var bits = decimal.GetBits(exact);
                var digits = new decimal(bits[0], bits[1], bits[2], isNegative: exact < 0, scale: 0);
                json.WriteStartObject("exact");
                json.WriteString("mantissa", digits.ToString(CultureInfo.InvariantCulture));
                json.WriteNumber("scale", exact.Scale);
                json.WriteEndObject();
            }
        }
        json.WriteEndObject();
    }

    // A figure's double, which JSON holds only when finite: a sum past double's range crosses as the name JavaScript gives
    // it, never as another number.
    private static void WriteFigureNumber(Utf8JsonWriter json, double number)
    {
        if (double.IsFinite(number))
            json.WriteNumber("number", number);
        else
            json.WriteString("number", double.IsNaN(number) ? "NaN" : number > 0 ? "Infinity" : "-Infinity");
    }

    private static double FigureNumberOf(JsonElement number) => number.ValueKind == JsonValueKind.String
        ? number.GetString() switch
        {
            "NaN" => double.NaN,
            "Infinity" => double.PositiveInfinity,
            "-Infinity" => double.NegativeInfinity,
            var other => throw new InvalidOperationException($"Internal: the Script Core sent a figure's number as '{other}'."),
        }
        : number.GetDouble();

    // The summary as the browser shows it, read back for a Consumer that shows the figures elsewhere.
    private static SelectionSummary SummaryOf(JsonElement payload)
    {
        var status = ScriptJson.Member<SelectionSummaryStatus>(payload.GetProperty("status"));
        GridSummaryRequest? request = null;
        if (ScriptJson.Present(payload, "request", out var asked))
        {
            request = new GridSummaryRequest
            {
                Ranges = ScriptJson.Ranges(asked.GetProperty("ranges")),
                Columns = ScriptJson.Texts(asked.GetProperty("columns")),
                RowSequenceVersion = asked.GetProperty("rowSequenceVersion").GetInt32(),
                Figures = FiguresOf(asked.GetProperty("figures")),
                Focus = ScriptJson.Present(asked, "focus", out var focus) ? ScriptJson.Cell(focus) : null,
            };
        }
        GridSummaryResult? result = null;
        if (ScriptJson.Present(payload, "result", out var answered))
            result = ResultOf(answered);
        return new SelectionSummary(status, request, result);
    }

    private static GridSummaryResult ResultOf(JsonElement json)
    {
        if (ScriptJson.Text(json, "kind") == "declined")
            return GridSummaryResult.Declined(ScriptJson.Text(json, "reason"));
        var figures = new Dictionary<SummaryFigures, Data.AggregateResult>();
        foreach (var figure in json.GetProperty("figures").EnumerateObject())
            figures[ScriptJson.Member<SummaryFigures>(figure.Name)] = AggregateOf(figure.Value);
        var texts = new Dictionary<SummaryFigures, string>();
        foreach (var text in json.GetProperty("texts").EnumerateObject())
        {
            texts[ScriptJson.Member<SummaryFigures>(text.Name)] = text.Value.GetString()
                ?? throw new InvalidOperationException($"Internal: the Script Core sent the text of '{text.Name}' as null.");
        }
        return GridSummaryResult.Answered(figures, texts);
    }

    private static Data.AggregateResult AggregateOf(JsonElement json) => ScriptJson.Text(json, "kind") switch
    {
        "empty" => Data.AggregateResult.Empty,
        "error" => Data.AggregateResult.Of(ScriptJson.Member<Data.AggregateError>(json.GetProperty("error"))),
        "number" when ScriptJson.Present(json, "exact", out var exact) => Data.AggregateResult.Of(DecimalOf(exact)),
        "number" => Data.AggregateResult.Of(FigureNumberOf(json.GetProperty("number"))),
        var other => throw new InvalidOperationException($"Internal: the Script Core sent a figure of the kind '{other}'."),
    };

    // A decimal as the bridge writes the Script Core's: its digits, as text, and how many stand after the point.
    private static decimal DecimalOf(JsonElement exact)
    {
        var digits = decimal.Parse(ScriptJson.Text(exact, "mantissa"), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var bits = decimal.GetBits(digits);
        return new decimal(bits[0], bits[1], bits[2], isNegative: digits < 0, scale: checked((byte)exact.GetProperty("scale").GetInt32()));
    }
}
