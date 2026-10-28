using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ExGrid;
using ExGrid.Chrome;
using ExGrid.Selection;
using ExGrid.Summarizing;
using ExSheet.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace ExSheet.Script;

/// <summary>
/// A general-purpose Excel-like sheet for Blazor, drawn by ExSheet's Script Core (ADR-0370 section 1): Blazor's
/// Framework Adapter over <c>@ex-grid/sheet</c>. The component renders one element, the Sheet's root, mounts the
/// Script Core on it, hands it its parameters as options and its documents as the JSON both engines read and write,
/// and raises the Sheet's events as <see cref="EventCallback"/>s. It holds no state of the Sheet's, draws nothing
/// inside the root and decides nothing: the Workbook, its Formulas and what each input means are the Script Core's,
/// in the browser, on both hosting models.
/// </summary>
/// <remarks>
/// <para>Its parameters are the Razor component's (<see cref="Components.ExSheet"/>), under the same names and with the
/// same defaults, so a page moves to it by its type alone. It stands beside the Razor component until ExSheet's Blazor
/// step holds (ADR-0370 section 11).</para>
/// <para>Not carried yet, and so not offered: a <c>Chrome</c>, <c>ToolbarContent</c>, a <c>PointingScope</c>,
/// <c>OnLinkedColumnColoursChanged</c> and <c>SetCellFormatFromFocusAsync</c>. What the Razor component answers at once is asked of the browser here, so
/// it is awaited: <see cref="CellFormatAtAsync(CellAddress)"/> and <see cref="LinkedTablesAsync"/>.</para>
/// </remarks>
public sealed partial class ExSheet : ComponentBase, IAsyncDisposable
{
    /// <summary>The script's address under the application's base: the package's minified Script Core and bridge.</summary>
    private const string ScriptPath = "_content/ExSheet/ex-sheet.min.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>
    /// The Sheet Document to show (ADR-0048). Handing over a different document replaces the Sheet and clears the undo
    /// stack; handing back a document this component raised through <see cref="DocumentChanged"/> — as binding
    /// <c>Document</c> two ways does — changes nothing, unless a document of the Consumer's reached the Sheet after it.
    /// Null is an empty Sheet in <see cref="Culture"/>.
    /// </summary>
    [Parameter] public SheetDocument? Document { get; set; }

    /// <summary>
    /// The Workbook Document to show (ADR-0331): every Sheet behind a Sheet Tab, the one it records as shown in front.
    /// It acts as <see cref="Document"/> does, and the two are not set together.
    /// </summary>
    /// <exception cref="InvalidOperationException">Both <see cref="Document"/> and this are set.</exception>
    [Parameter] public WorkbookDocument? Workbook { get; set; }

    /// <summary>Raised after every change to the Workbook with its Workbook Document (ADR-0331). The application
    /// persists it; ExSheet never does.</summary>
    [Parameter] public EventCallback<WorkbookDocument> WorkbookChanged { get; set; }

    /// <summary>Raised after every change to the Sheet with its Sheet Document — Entries, never Values (ADR-0048) —
    /// while the Workbook holds one Sheet, before <see cref="WorkbookChanged"/>; with several, only
    /// <see cref="WorkbookChanged"/> is raised.</summary>
    [Parameter] public EventCallback<SheetDocument> DocumentChanged { get; set; }

    /// <summary>Raised after a Sheet is added, deleted, renamed, moved or copied, by the user or by an undo or redo of
    /// one, with the Sheets' names before and after (ADR-0331 §6).</summary>
    [Parameter] public EventCallback<SheetsChange> SheetsChanged { get; set; }

    /// <summary>
    /// The declared culture of a Sheet started empty: how typed constants are read and how Values are shown (ADR-0048).
    /// A document carries its own. Null is the current culture when the Sheet is created. Changing it on a live Sheet is
    /// refused by name: Entries already typed would not be read again.
    /// </summary>
    [Parameter] public CultureInfo? Culture { get; set; }

    /// <summary>How many leading columns are pinned — the Sheet's frozen panes, columns only (ADR-0046).</summary>
    [Parameter] public int PinnedColumnCount { get; set; }

    /// <summary>Every row's height, in pixels (ADR-0013/0046); a height at which Excel's rows do not fit under the
    /// browser's scroll ceiling is refused by name (SH-3).</summary>
    [Parameter] public double RowHeight { get; set; } = Components.ExSheet.DefaultRowHeightPx;

    /// <summary>The outer height of the sheet, the Formula Bar, the toolbar and the Sheet Tabs included.</summary>
    [Parameter] public ViewportSize ViewportHeight { get; set; } = 600;

    /// <summary>The outer width of the sheet.</summary>
    [Parameter] public ViewportSize ViewportWidth { get; set; } = 800;

    /// <summary>Whether the Row Headings — the row numbers — are shown (ADR-0050).</summary>
    [Parameter] public bool ShowRowHeadings { get; set; } = true;

    /// <summary>Whether the Column Headings — the column letters — are shown (ADR-0050).</summary>
    [Parameter] public bool ShowColumnHeadings { get; set; } = true;

    /// <summary>Whether the Formula Bar with its Name Box is shown (ADR-0051).</summary>
    [Parameter] public bool ShowFormulaBar { get; set; } = true;

    /// <summary>Whether the Sheet Tabs are shown at the foot of the Sheet (ADR-0331): on by default, as in Excel.</summary>
    [Parameter] public bool ShowSheetTabs { get; set; } = true;

    /// <summary>Whether the Sheet Toolbar, Excel's Home tab's formatting commands, is shown above the Formula Bar
    /// (ADR-0100): off unless asked for.</summary>
    [Parameter] public bool ShowToolbar { get; set; }

    /// <summary>Raised with the other value when the user asks to hide or show the Sheet Toolbar with Ctrl+F1 (ADR-0100).
    /// Unbound, Ctrl+F1 is not claimed and stays the browser's.</summary>
    [Parameter] public EventCallback<bool> ShowToolbarChanged { get; set; }

    /// <summary>The Selection Summary's figures (ADR-0130): Excel's Average, Count and Sum by default. The Sheet holds
    /// the choice made in the figures menu from then on; a new value handed here replaces it.</summary>
    [Parameter] public SummaryFigures SummaryFigures { get; set; } = SummaryFigures.Default;

    /// <summary>Whether the Selection Summary is shown under the Sheet (ADR-0130): on by default.</summary>
    [Parameter] public bool ShowSelectionSummary { get; set; } = true;

    /// <summary>Raised when the figures menu changes the figures shown (ADR-0130).</summary>
    [Parameter] public EventCallback<SummaryFigures> SummaryFiguresChanged { get; set; }

    /// <summary>The Selection Summary as it now stands (ADR-0130), for an application that shows the figures
    /// elsewhere. The Sheet in the browser answers it, as the Razor component's Sheet does.</summary>
    [Parameter] public EventCallback<SelectionSummary> OnSelectionSummaryChanged { get; set; }

    /// <summary>Raised whenever the grid's Selection changes, as ExGrid raises it (ADR-0011/0014).</summary>
    [Parameter] public EventCallback<GridSelection> SelectionChanged { get; set; }

    /// <summary>
    /// The wording for a command id (ADR-0036): the grid's own ids and ExSheet's (<see cref="SheetCommandIds"/>), and a
    /// refusal of a copy or a paste by its id (ADR-0203). Null for an id keeps the English, and the empty string
    /// silences the announcement of a refusal. A function cannot cross to the browser, so it is asked for every id each
    /// time the parameters are set, and the words cross only when they changed.
    /// </summary>
    [Parameter] public Func<string, string?>? CommandLabel { get; set; }

    /// <summary>Whether the Context Menu offers copy with headers (ADR-0048). Off by default: a Sheet's column letters
    /// are addresses, not headers (ADR-0050 item 13).</summary>
    [Parameter] public bool AllowCopyWithHeaders { get; set; }

    /// <summary>Raised when <see cref="IsEditing"/> changes (ADR-0048).</summary>
    [Parameter] public EventCallback<bool> EditingChanged { get; set; }

    /// <summary>Raised when one of Excel's formatting keys is pressed while an edit is open (ADR-0071): the key changed
    /// nothing, and the Sheet's notice says why.</summary>
    [Parameter] public EventCallback<SheetRefusal> OnFormatKeyRefused { get; set; }

    /// <summary>A fixed Sheet Seed (ADR-0180): what <c>RAND()</c> and <c>RANDBETWEEN</c> draw from. Unless given, the
    /// Sheet chooses one whenever it opens a document.</summary>
    [Parameter] public ulong? Seed { get; set; }

    /// <summary>A fixed Sheet Day: what <c>TODAY()</c> answers whatever the clock says (ADR-0121).</summary>
    [Parameter] public DateOnly? Today { get; set; }

    /// <summary>The time zone whose day <c>TODAY()</c> and whose time of day <c>NOW()</c> answer (ADR-0121, ADR-0124);
    /// the browser's unless given.</summary>
    /// <exception cref="NotSupportedException">The zone has no IANA name the browser could know it by.</exception>
    [Parameter] public TimeZoneInfo? TimeZone { get; set; }

    /// <summary>
    /// Whether an edit is open in the grid, as the browser last told it: from the moment a cell or the Formula Bar is
    /// typed in until the edit is committed, cancelled or discarded. While it is, the commands are refused by name.
    /// </summary>
    public bool IsEditing { get; private set; }

    // The documents the Sheet raised, by identity, each with the number of the Consumer's documents the browser had been
    // sent when it raised it. Handed back while no other document of the Consumer's has been sent since, it is the one
    // on screen and is not sent; handed back after one was, the screen has moved on, and it is sent like any other.
    private readonly ConditionalWeakTable<object, StrongBox<int>> _raised = [];
    private bool _initialised;
    private CultureInfo? _cultureParameter;
    // The culture the Sheet reads, fixed when it is created: the parameter, or the current culture then (ADR-0370
    // section 10: the locale is said once).
    private CultureInfo _culture = CultureInfo.InvariantCulture;
    // The document parameter as last set, Workbook or Document, whichever is set.
    private object? _documentParameter;
    // The document owed to the browser, as JSON, or null for an empty Sheet.
    private string? _documentJson;
    private bool _documentOwed;
    // How many of the Consumer's documents have been put on their way to the browser.
    private int _documentsSent;

    private readonly Queue<Func<Task>> _heard = new();
    private bool _hearing;

    private ElementReference _root;
    private IJSObjectReference? _module;
    private IJSObjectReference? _sheet;
    // The browser's Sheet once it is attached, or null once it never will be: refused, or disposed first. Never a
    // failed task, which nobody may be waiting to observe.
    private readonly TaskCompletionSource<IJSObjectReference?> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    // What this version's script failed with while it attached, which a command waiting on the Sheet is told.
    private Exception? _attachFailure;
    private DotNetObjectReference<ScriptBridge>? _bridge;
    private string? _sentOptions;
    // Every id CommandLabel words, as the module answers them.
    private IReadOnlyList<string> _labelIds = [];
    private string? _sentWords;
    private bool _attached;
    private bool _attaching;
    private bool _refused;
    private bool _disposed;
    private bool _renderOwed = true;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // The refusal is worded through CommandLabel, which may have changed (ADR-0203).
        if (_refused)
            _renderOwed = true;
        if (Document is not null && Workbook is not null)
        {
            throw new InvalidOperationException(
                "Document and Workbook are both set. A Sheet Document opens as a Workbook of one Sheet; hand over one or the other (ADR-0331).");
        }
        object? document = Workbook is not null ? Workbook : Document;
        if (!_initialised)
        {
            _initialised = true;
            _cultureParameter = Culture;
            _culture = Culture ?? CultureInfo.CurrentCulture;
            _documentParameter = document;
            _documentJson = JsonOf(document);
            _documentOwed = document is not null;
            return;
        }
        if (!Equals(Culture, _cultureParameter))
        {
            throw new InvalidOperationException(
                "The Sheet's culture is its own, recorded in its document: Entries already typed were read under it (ADR-0048). " +
                "To show a Sheet under another culture, hand over a Sheet Document declaring that culture.");
        }
        if (ReferenceEquals(document, _documentParameter))
            return;
        _documentParameter = document;
        var onScreen = document is not null && _raised.TryGetValue(document, out var sentThen) && sentThen.Value == _documentsSent;
        if (onScreen)
        {
            // The parameter is the Sheet on screen: a document of the Consumer's still owed is superseded by it.
            _documentOwed = false;
            return;
        }
        // Null after a document opens an empty Sheet, as the Razor component does.
        _documentJson = JsonOf(document);
        _documentOwed = true;
    }

    private static string? JsonOf(object? document) => document switch
    {
        null => null,
        WorkbookDocument workbook => workbook.ToJson(),
        SheetDocument sheet => sheet.ToJson(),
        _ => throw new InvalidOperationException($"Internal: a document of the type {document.GetType().Name}."),
    };

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (_attached)
            await SendUpdateAsync();
    }

    /// <inheritdoc />
    protected override bool ShouldRender() => _renderOwed;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        _renderOwed = false;
        builder.OpenElement(0, "div");
        builder.AddElementReferenceCapture(1, element => _root = element);
        if (_refused)
        {
            // The Script Core never attached: the root is Blazor's to say why, in the built-in words (ADR-0123).
            builder.OpenElement(2, "div");
            builder.AddAttribute(3, "role", "alert");
            builder.AddAttribute(4, "data-ex-notice", "script");
            // The Consumer's words first; null keeps the English (ADR-0203).
            builder.AddContent(5, CommandLabel?.Invoke(ReloadNoticeLabelIds.Script) ?? BuiltInCommandLabels.For(ReloadNoticeLabelIds.Script));
            builder.CloseElement();
        }
        builder.CloseElement();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;
        // The script this build was made with, by an address that changes with its content (ADR-0123), and refused
        // when the browser holds another version's: nothing is attached, and the root says so.
        var address = "./" + AddressOf(Assets, ScriptPath, ScriptCoreAsset.Version);
        IJSObjectReference? module = null;
        string? stamp;
        try
        {
            module = await JS.InvokeAsync<IJSObjectReference>("import", address);
            _module = module;
            stamp = await module.InvokeAsync<string?>("stamp");
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
        {
            // A module that does not load, by a fault of the page's or of the circuit's, or one older than the stamp,
            // which has no `stamp` to call: refused as another version's is (ADR-0123), so a command waiting is told.
            stamp = null;
        }
        catch (JsonException)
        {
            // An answer that is not a string is no stamp of this family's.
            stamp = null;
        }
        if (_disposed)
        {
            await ReleaseAsync(null, module);
            return;
        }
        if (module is null || stamp != ScriptCoreAsset.Stamp)
        {
            _module = null;
            await ReleaseAsync(null, module);
            _refused = true;
            _ready.TrySetResult(null);
            _renderOwed = true;
            StateHasChanged();
            return;
        }
        try
        {
            await AttachAsync(module);
        }
        catch (Exception ex) when (!_disposed)
        {
            // This version's script, failing for a reason of its own — a document or an option it refused: a command
            // waiting on the Sheet is told so, and the component fails with it.
            _attachFailure = ex;
            _ready.TrySetResult(null);
            throw;
        }
    }

    private async Task AttachAsync(IJSObjectReference module)
    {
        // The ids CommandLabel is asked for, as this version's module names them.
        _labelIds = await module.InvokeAsync<string[]>("labelIds");
        if (_disposed)
        {
            await ReleaseAsync(null, module);
            return;
        }
        // What the attach carries is what the browser holds once it returns: anything that changes while it is on its
        // way is owed again, and sent after it, in the order it changed.
        var options = OptionsJson();
        var words = WordsJson();
        if (_documentOwed)
            _documentsSent += 1;
        var attach = AttachJson(options, _documentOwed ? _documentJson : null, words);
        _sentOptions = options;
        _sentWords = words;
        _documentOwed = false;
        var bridge = DotNetObjectReference.Create(new ScriptBridge(this));
        _bridge = bridge;
        _attaching = true;
        IJSObjectReference sheet;
        try
        {
            sheet = await module.InvokeAsync<IJSObjectReference>("attach", _root, bridge, attach);
        }
        catch (Exception) when (_disposed)
        {
            // Disposed while it attached, and the attach failed with it, as it does when the circuit closes.
            await ReleaseAsync(null, module);
            bridge.Dispose();
            return;
        }
        catch (Exception)
        {
            // Refused by the Sheet itself: nothing of the browser's is held for a Sheet that never opened.
            _module = null;
            _bridge = null;
            await ReleaseAsync(null, module);
            bridge.Dispose();
            throw;
        }
        finally
        {
            _attaching = false;
        }
        if (_disposed)
        {
            await ReleaseAsync(sheet, module);
            bridge.Dispose();
            return;
        }
        _sheet = sheet;
        _attached = true;
        await SendUpdateAsync();
        _ready.TrySetResult(sheet);
    }

    // The parameters, a document of the Consumer's and the words, whichever changed since the browser last heard them.
    private async Task SendUpdateAsync()
    {
        var sheet = _sheet;
        if (sheet is null || _disposed)
            return;
        var options = OptionsJson();
        var words = WordsJson();
        var wordsMoved = words != _sentWords;
        if (options == _sentOptions && !_documentOwed && !wordsMoved)
            return;
        _sentOptions = options;
        _sentWords = words;
        var documentOwed = _documentOwed;
        if (documentOwed)
            _documentsSent += 1;
        var update = Json(json =>
        {
            json.WriteStartObject();
            json.WritePropertyName("options");
            json.WriteRawValue(options);
            if (documentOwed)
            {
                WriteDocument(json, _documentJson);
                json.WriteNumber("documentsSent", _documentsSent);
            }
            if (wordsMoved)
            {
                json.WritePropertyName("words");
                json.WriteRawValue(words);
            }
            json.WriteEndObject();
        });
        _documentOwed = false;
        await sheet.InvokeVoidAsync("update", update);
    }

    private string AttachJson(string options, string? document, string words) => Json(json =>
    {
        json.WriteStartObject();
        json.WritePropertyName("options");
        json.WriteRawValue(options);
        WriteDocument(json, document);
        json.WriteNumber("documentsSent", _documentsSent);
        json.WritePropertyName("words");
        json.WriteRawValue(words);
        // Across a circuit, Blazor Server's, a message larger than its hub takes would close it: the browser holds one.
        json.WriteBoolean("circuit", !OperatingSystem.IsBrowser());
        json.WriteEndObject();
    });

    private static void WriteDocument(Utf8JsonWriter json, string? document)
    {
        if (document is null)
            json.WriteNull("document");
        else
            json.WriteString("document", document);
    }

    // Each word the Consumer gives, asked here, since a .NET function cannot cross (ADR-0370 section 10); null keeps
    // the English.
    private string WordsJson() => Json(json =>
    {
        if (CommandLabel is not { } label)
        {
            json.WriteNullValue();
            return;
        }
        json.WriteStartObject();
        foreach (var id in _labelIds)
        {
            if (label(id) is { } word)
                json.WriteString(id, word);
        }
        json.WriteEndObject();
    });

    private string OptionsJson() => Json(json =>
    {
        json.WriteStartObject();
        // The culture the component read when the Sheet was created (ADR-0370 section 10).
        var culture = _culture.Name;
        json.WriteString("locale", culture.Length == 0 ? "invariant" : culture);
        json.WriteBoolean("showFormulaBar", ShowFormulaBar);
        json.WriteNumber("pinnedColumnCount", PinnedColumnCount);
        json.WriteBoolean("showRowHeadings", ShowRowHeadings);
        json.WriteBoolean("showColumnHeadings", ShowColumnHeadings);
        json.WriteBoolean("showSheetTabs", ShowSheetTabs);
        json.WriteBoolean("showToolbar", ShowToolbar);
        json.WriteBoolean("showSelectionSummary", ShowSelectionSummary);
        json.WriteStartArray("summaryFigures");
        foreach (var figure in FiguresOf(SummaryFigures))
            json.WriteStringValue(figure);
        json.WriteEndArray();
        json.WriteBoolean("allowCopyWithHeaders", AllowCopyWithHeaders);
        json.WriteNumber("rowHeight", RowHeight);
        WriteSize(json, "viewportWidth", ViewportWidth);
        WriteSize(json, "viewportHeight", ViewportHeight);
        if (Today is { } today)
        {
            json.WriteStartObject("today");
            json.WriteNumber("year", today.Year);
            json.WriteNumber("month", today.Month);
            json.WriteNumber("day", today.Day);
            json.WriteEndObject();
        }
        if (TimeZone is { } zone)
            json.WriteString("timeZone", IanaNameOf(zone));
        if (Seed is { } seed)
            json.WriteString("seed", seed.ToString(CultureInfo.InvariantCulture));
        json.WriteStartArray("hears");
        if (WorkbookChanged.HasDelegate)
            json.WriteStringValue("workbookChanged");
        if (DocumentChanged.HasDelegate)
            json.WriteStringValue("documentChanged");
        if (SheetsChanged.HasDelegate)
            json.WriteStringValue("sheetsChanged");
        if (ShowToolbarChanged.HasDelegate)
            json.WriteStringValue("showToolbarChanged");
        if (SummaryFiguresChanged.HasDelegate)
            json.WriteStringValue("summaryFiguresChanged");
        if (SelectionChanged.HasDelegate)
            json.WriteStringValue("selectionChanged");
        if (OnSelectionSummaryChanged.HasDelegate)
            json.WriteStringValue("selectionSummaryChanged");
        if (OnFormatKeyRefused.HasDelegate)
            json.WriteStringValue("formatKeyRefused");
        json.WriteEndArray();
        json.WriteEndObject();
    });

    // The figures in the order Excel's status bar shows them, as the Script Core spells them.
    private static IEnumerable<string> FiguresOf(SummaryFigures figures)
    {
        if ((figures & ~SummaryFigures.All) != 0)
            throw new ArgumentOutOfRangeException(nameof(figures), figures, $"{figures} names a figure the Selection Summary does not have (ADR-0130).");
        return SummaryFigureOrder.Each.Where(figure => figures.HasFlag(figure)).Select(Camel);
    }

    // One figure as the Script Core spells it, refused when it names a set of figures or none.
    private static SummaryFigures Figure(string camel)
        => Member<SummaryFigures>(camel) is var figure && SummaryFigureOrder.Each.Contains(figure)
            ? figure
            : throw new InvalidOperationException($"Internal: the Script Core named the figure '{camel}'.");

    private static SummaryFigures FiguresFrom(JsonElement json)
    {
        var figures = SummaryFigures.None;
        foreach (var figure in json.EnumerateArray())
            figures |= Figure(Text(figure));
        return figures;
    }

    // A Selection Summary as the browser writes it: its status, the question and the answer, an exact figure as its
    // decimal's digits. Read as Blazor's ExPivot reads its own, but for the columns, which are the Sheet's letters
    // always: the browser checks that and sends none, so 16,384 names do not cross on every selection.
    private static SelectionSummary SummaryOf(JsonElement json)
    {
        var status = Member<SelectionSummaryStatus>(Text(json.GetProperty("status")));
        var request = json.GetProperty("request");
        var result = json.GetProperty("result");
        return new SelectionSummary(status,
            request.ValueKind == JsonValueKind.Null ? null : SummaryRequestOf(request),
            result.ValueKind == JsonValueKind.Null ? null : SummaryResultOf(result));
    }

    // The Sheet's columns by their ids, A to XFD, as the Razor component's grid names them: made the first time a
    // summary is heard, and only then.
    private static IReadOnlyList<string>? _letters;

    private static IReadOnlyList<string> Letters
        => _letters ??= Array.AsReadOnly(Enumerable.Range(0, Sheet.ColumnCount).Select(CellAddress.ColumnName).ToArray());

    private static GridSummaryRequest SummaryRequestOf(JsonElement json)
    {
        var focus = json.GetProperty("focus");
        return new GridSummaryRequest
        {
            Ranges = [.. json.GetProperty("ranges").EnumerateArray().Select(range => new SelectionRange(
                range.GetProperty("topRow").GetInt32(), range.GetProperty("leftColumn").GetInt32(),
                range.GetProperty("rowCount").GetInt32(), range.GetProperty("columnCount").GetInt32()))],
            Columns = Letters,
            RowSequenceVersion = json.GetProperty("rowSequenceVersion").GetInt32(),
            Figures = FiguresFrom(json.GetProperty("figures")),
            Focus = focus.ValueKind == JsonValueKind.Null ? null : new CellPosition(focus.GetProperty("row").GetInt32(), focus.GetProperty("column").GetInt32()),
        };
    }

    private static GridSummaryResult SummaryResultOf(JsonElement json)
    {
        if (json.TryGetProperty("reason", out var reason))
            return GridSummaryResult.Declined(Text(reason));
        var figures = new Dictionary<SummaryFigures, ExGrid.Data.AggregateResult>();
        foreach (var figure in json.GetProperty("figures").EnumerateObject())
            figures[Figure(figure.Name)] = AggregateOf(figure.Value);
        var texts = new Dictionary<SummaryFigures, string>();
        foreach (var text in json.GetProperty("texts").EnumerateObject())
            texts[Figure(text.Name)] = Text(text.Value);
        return GridSummaryResult.Answered(figures, texts);
    }

    // A figure's answer: nothing, an error, an exact decimal written as its digits, or a double.
    private static ExGrid.Data.AggregateResult AggregateOf(JsonElement json) => Text(json.GetProperty("kind")) switch
    {
        "empty" => ExGrid.Data.AggregateResult.Empty,
        "error" => ExGrid.Data.AggregateResult.Of(Member<ExGrid.Data.AggregateError>(Text(json.GetProperty("error")))),
        "number" when json.TryGetProperty("exact", out var exact) => ExGrid.Data.AggregateResult.Of(decimal.Parse(Text(exact), NumberStyles.Number, CultureInfo.InvariantCulture)),
        "number" => ExGrid.Data.AggregateResult.Of(json.GetProperty("number").GetDouble()),
        var kind => throw new InvalidOperationException($"Internal: the Script Core answered a figure of the kind '{kind}'."),
    };

    // The zone as the browser knows it: by its IANA name, which a Windows zone is converted to.
    private static string IanaNameOf(TimeZoneInfo zone)
    {
        if (zone.HasIanaId)
            return zone.Id;
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana))
            return iana;
        throw new NotSupportedException($"The time zone '{zone.Id}' has no IANA name, which is how the browser knows a zone (ADR-0121).");
    }

    // The Sheet's events, handed to the Consumer one at a time in the order the browser raised them: a handler that
    // awaits finishes before the next is called. What a handler throws fails the component, as Blazor's own do.
    internal Task HeardAsync(string kind, JsonElement payload) => InvokeAsync(() => HearAsync(Prepared(kind, payload)));

    // An event too large for a message of its own, taken from the browser in its turn: the events after it wait for it.
    internal Task HeardHeldAsync(string kind, JsonElement held) => InvokeAsync(() => HearAsync(async () =>
    {
        var payload = await CrossedMessage.OfAsync(await ReadyAsync(), held);
        await Prepared(kind, payload)();
    }));

    // Read in the component's turn, against the parameters as they stand when the event arrives.
    private Func<Task> Prepared(string kind, JsonElement payload)
    {
        try
        {
            return Prepare(kind, payload);
        }
        catch (Exception ex)
        {
            // An event .NET cannot read fails the component in its turn, as a handler's exception does.
            return () => Task.FromException(ex);
        }
    }

    private async Task HearAsync(Func<Task> hand)
    {
        _heard.Enqueue(hand);
        if (_hearing)
            return;
        _hearing = true;
        try
        {
            while (_heard.TryDequeue(out var next))
            {
                if (_disposed)
                    continue;
                try
                {
                    await next();
                }
                catch (Exception ex)
                {
                    await DispatchExceptionAsync(ex);
                }
            }
        }
        finally
        {
            _hearing = false;
        }
    }

    private Func<Task> Prepare(string kind, JsonElement payload)
    {
        switch (kind)
        {
            case "workbookChanged":
            {
                // The ones on screen as soon as they are read, unless a document of the Consumer's reached the browser
                // after them.
                var sentThen = payload.GetProperty("documentsSent").GetInt32();
                var sheet = payload.GetProperty("sheet") is { ValueKind: JsonValueKind.String } sheetJson ? SheetDocument.FromJson(Text(sheetJson)) : null;
                var workbook = payload.GetProperty("workbook") is { ValueKind: JsonValueKind.String } workbookJson ? WorkbookDocument.FromJson(Text(workbookJson)) : null;
                if (sheet is not null)
                    _raised.AddOrUpdate(sheet, new StrongBox<int>(sentThen));
                if (workbook is not null)
                    _raised.AddOrUpdate(workbook, new StrongBox<int>(sentThen));
                return async () =>
                {
                    if (sheet is not null)
                        await DocumentChanged.InvokeAsync(sheet);
                    if (workbook is not null)
                        await WorkbookChanged.InvokeAsync(workbook);
                };
            }
            case "sheetsChanged":
            {
                var change = new SheetsChange(Names(payload.GetProperty("before")), Names(payload.GetProperty("after")), Text(payload.GetProperty("shown")));
                return () => SheetsChanged.InvokeAsync(change);
            }
            case "showToolbarChanged":
            {
                var shown = payload.GetBoolean();
                return () => ShowToolbarChanged.InvokeAsync(shown);
            }
            case "summaryFiguresChanged":
            {
                var figures = FiguresFrom(payload);
                return () => SummaryFiguresChanged.InvokeAsync(figures);
            }
            case "selectionChanged":
            {
                var selection = Selection(payload);
                return () => SelectionChanged.InvokeAsync(selection);
            }
            case "selectionSummaryChanged":
            {
                var summary = SummaryOf(payload);
                return () => OnSelectionSummaryChanged.InvokeAsync(summary);
            }
            case "editingChanged":
            {
                var open = payload.GetBoolean();
                return () =>
                {
                    IsEditing = open;
                    return EditingChanged.InvokeAsync(open);
                };
            }
            case "formatKeyRefused":
            {
                var refusal = Refusal(payload);
                return () => OnFormatKeyRefused.InvokeAsync(refusal);
            }
            default:
                throw new InvalidOperationException($"Internal: the Script Core raised an event of the kind '{kind}'.");
        }
    }

    /// <summary>
    /// Undoes the latest operation, on whichever Sheet it was done, and shows that Sheet with the range it changed
    /// selected, as Excel's undo does (ADR-0048, ADR-0331 §3). Answers false when there is nothing to undo.
    /// </summary>
    /// <exception cref="SheetRefusedException">An edit is open; nothing changed.</exception>
    public Task<bool> UndoAsync() => CommandAsync("undo");

    /// <summary>Redoes the operation undone last, and shows and selects what it changed as <see cref="UndoAsync"/> does.
    /// Answers false when there is nothing to redo.</summary>
    /// <exception cref="SheetRefusedException">An edit is open; nothing changed.</exception>
    public Task<bool> RedoAsync() => CommandAsync("redo");

    /// <summary>
    /// Opens Format Cells over the Selection the grid holds when the command runs, from the Consumer's own button
    /// (ADR-0071): OK sets the parts the user touched as one undo step, and Cancel or Escape sets nothing. Answers false
    /// when nothing is selected.
    /// </summary>
    /// <exception cref="SheetRefusedException">An edit is open; nothing opened.</exception>
    public Task<bool> OpenFormatCellsAsync() => CommandAsync("openFormatCells");

    /// <summary>
    /// A Consumer command: does <paramref name="edit"/> as one operation on the user's undo stack, in its place in the
    /// order (ADR-0048), and raises <see cref="DocumentChanged"/>. It names no Sheet, so it is taken only while the
    /// Workbook holds one; with several, name the Sheet with <see cref="DoAsync(string, SheetEdit)"/> (ADR-0331 §6).
    /// The edit crosses to the browser as <see cref="SheetWire"/> writes it, and the Sheet there does it as the Razor
    /// component's would.
    /// </summary>
    /// <exception cref="SheetRefusedException">The Sheet refuses the operation, an edit is open
    /// (<see cref="IsEditing"/>), or the Workbook holds several Sheets
    /// (<see cref="SheetRefusalReason.SheetNotNamed"/>); nothing changed.</exception>
    /// <exception cref="FormulaSyntaxException">A typed Formula cannot be read; nothing changed.</exception>
    public Task DoAsync(SheetEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        return AnswerAsync("do", null, SheetWire.Write(edit));
    }

    /// <summary>
    /// Does <paramref name="edit"/> on the Sheet named <paramref name="sheet"/>, as one operation on the user's undo stack
    /// (ADR-0331 §6), as <see cref="DoAsync(SheetEdit)"/> does it on the one Sheet. The Sheet shown stays shown, unless
    /// the edit makes or moves a Sheet as Excel's tab commands do.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">No Sheet of the Workbook is named so.</exception>
    /// <exception cref="SheetRefusedException">The Sheet refuses the operation, or an edit is open; nothing changed.</exception>
    /// <exception cref="FormulaSyntaxException">A typed Formula cannot be read; nothing changed.</exception>
    public Task DoAsync(string sheet, SheetEdit edit)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(edit);
        return AnswerAsync("do", sheet, SheetWire.Write(edit));
    }

    /// <summary>
    /// Sets the parts of a Cell Format that <paramref name="change"/> names on the Selection as the grid holds it when
    /// the command runs, as one operation on the undo stack, and widens as the formatting keys do (ADR-0071, ADR-0048):
    /// what the Razor component's <c>SetCellFormatAsync</c> does, done by the Sheet in the browser. Answers false when
    /// nothing is selected.
    /// </summary>
    /// <exception cref="ArgumentException">The change sets no part of a Cell Format.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The change's alignment is not one.</exception>
    /// <exception cref="SheetRefusedException">An edit is open (<see cref="IsEditing"/>); nothing changed.</exception>
    public async Task<bool> SetCellFormatAsync(CellFormatChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.IsEmpty) throw new ArgumentException("The change sets no part of a Cell Format.", nameof(change));
        if (change.Alignment is { } alignment && !Enum.IsDefined(alignment))
            throw new ArgumentOutOfRangeException(nameof(change), alignment, "Not an alignment.");
        return (await AnswerAsync("setCellFormat", SheetWire.Write(change))).GetBoolean();
    }

    /// <summary>
    /// Sets the Number Format of the Selection; <see langword="null"/> is General. A shorthand for
    /// <see cref="SetCellFormatAsync"/> with a change that names the Number Format alone (ADR-0071).
    /// </summary>
    /// <exception cref="SheetRefusedException">An edit is open (<see cref="IsEditing"/>); nothing changed.</exception>
    public Task<bool> SetNumberFormatAsync(NumberFormat? format) =>
        SetCellFormatAsync(new CellFormatChange { NumberFormat = format ?? NumberFormat.General });

    /// <summary>
    /// Sets the horizontal alignment of the Selection. A shorthand for <see cref="SetCellFormatAsync"/> with a change
    /// that names the Alignment alone (ADR-0071).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The alignment is not one.</exception>
    /// <exception cref="SheetRefusedException">An edit is open (<see cref="IsEditing"/>); nothing changed.</exception>
    public Task<bool> SetAlignmentAsync(HorizontalAlignment alignment) =>
        SetCellFormatAsync(new CellFormatChange { Alignment = alignment });

    /// <summary>
    /// The Cell Format of the cell at <paramref name="address"/> as it shows (ADR-0071): each part the cell's own, else
    /// its row's, else its column's, else the part's default, and each Border side the edge as shown. It is a read, so
    /// it answers while an edit is open too. The address names no Sheet, so it is read only while the Workbook holds
    /// one; with several, name the Sheet with <see cref="CellFormatAtAsync(string, CellAddress)"/> (ADR-0331 §6).
    /// </summary>
    /// <exception cref="SheetRefusedException">The Workbook holds several Sheets (<see cref="SheetRefusalReason.SheetNotNamed"/>).</exception>
    public async Task<CellFormat> CellFormatAtAsync(CellAddress address)
        => SheetWire.ReadCellFormat(Text(await AnswerAsync("cellFormatAt", null, address.ToString())));

    /// <summary>The Cell Format of the cell at <paramref name="address"/> on the Sheet named <paramref name="sheet"/>, as it shows (ADR-0071, ADR-0331 §6).</summary>
    /// <exception cref="ArgumentOutOfRangeException">No Sheet of the Workbook is named so.</exception>
    public async Task<CellFormat> CellFormatAtAsync(string sheet, CellAddress address)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return SheetWire.ReadCellFormat(Text(await AnswerAsync("cellFormatAt", sheet, address.ToString())));
    }

    /// <summary>
    /// The Linked Tables declared on the Workbook, in the order they were declared (ADR-0049): each one's name, columns
    /// and key, whether it is waiting for a snapshot, and how many rows the last one held.
    /// </summary>
    public async Task<IReadOnlyList<LinkedTable>> LinkedTablesAsync()
    {
        var sheet = await ReadyAsync();
        var tables = await AskAsync(sheet, "linkedTables");
        return [.. tables.EnumerateArray().Select(table =>
        {
            IReadOnlyList<string> key = Names(table.GetProperty("keyColumns"));
            return new LinkedTable(Text(table.GetProperty("name")), Names(table.GetProperty("columns")), table.GetProperty("isWaiting").GetBoolean(),
                table.GetProperty("rowCount").GetInt32(), key.Count == 1 ? key[0] : null) { KeyColumns = key };
        })];
    }

    /// <summary>
    /// Declares a Linked Table by name, columns and key, before any of its rows arrive (ADR-0049), as the Razor
    /// component's <c>DeclareLinkedTableAsync</c> does: Formulas naming it show <c>#GETTING_DATA</c> until the first
    /// snapshot is pushed, and the declaration is recorded in the Sheet Document. It is the Consumer's data, not a
    /// user's operation, so it is no step on the undo stack.
    /// </summary>
    /// <param name="name">The name Formulas read the table by.</param>
    /// <param name="columns">The column names, in order.</param>
    /// <param name="key">The key column, one of <paramref name="columns"/>, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentException">The name or the columns are not ones a structured reference can name, or the key is not one of the columns.</exception>
    public Task DeclareLinkedTableAsync(string name, IReadOnlyList<string> columns, string? key = null) =>
        DeclareLinkedTableAsync(name, columns, key is null ? (IReadOnlyList<string>)[] : [key]);

    /// <summary>
    /// Declares a Linked Table whose rows are told apart by the Values of several columns together (ADR-0058); an empty
    /// list declares no key. Otherwise as the overload that takes one key column.
    /// </summary>
    /// <param name="name">The name Formulas read it by.</param>
    /// <param name="columns">The column names, in order.</param>
    /// <param name="key">The key's columns, each one of <paramref name="columns"/>, none twice.</param>
    /// <exception cref="ArgumentException">The name or the columns are not ones a structured reference can name, or a key column is not one of the columns, or is named twice.</exception>
    public Task DeclareLinkedTableAsync(string name, IReadOnlyList<string> columns, IReadOnlyList<string> key)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(key);
        return AnswerAsync("declareLinkedTable", name, columns, key);
    }

    /// <summary>
    /// Replaces a Linked Table's data with a whole snapshot, in one step (ADR-0049), as the Razor component's
    /// <c>PushLinkedTableAsync</c> does: no recalculation sees part of one snapshot and part of another, nothing is
    /// raised through <see cref="DocumentChanged"/>, and nothing goes on the undo stack. The rows cross to the browser
    /// as <see cref="SheetWire.WriteRows"/> writes them.
    /// </summary>
    /// <param name="name">The declared table's name.</param>
    /// <param name="rows">The rows, each holding one Value per declared column, in order; null is a blank.</param>
    /// <exception cref="InvalidOperationException">No table of that name is declared.</exception>
    /// <exception cref="RepeatedKeyException">The table was declared with a key, and a key repeats in the snapshot: the
    /// table waits again, and every Formula that reads it shows <c>#GETTING_DATA</c> before the refusal is thrown.</exception>
    /// <exception cref="ArgumentException">A row does not have one Value per column, or holds a Value that is not data.</exception>
    public Task PushLinkedTableAsync(string name, IEnumerable<IReadOnlyList<Value?>> rows)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(rows);
        return AnswerAsync("pushLinkedTable", name, SheetWire.WriteRows(rows));
    }

    /// <summary>The Sheet as it stands, as a Sheet Document: Entries, never Values (ADR-0048).</summary>
    /// <exception cref="InvalidOperationException">The Workbook holds several Sheets, which a Sheet Document cannot
    /// record (ADR-0331): <see cref="ToWorkbookDocumentAsync"/> records them.</exception>
    public async Task<SheetDocument> ToDocumentAsync()
    {
        var documents = await DocumentsAsync();
        return documents.GetProperty("sheet") is { ValueKind: JsonValueKind.String } sheet
            ? SheetDocument.FromJson(Text(sheet))
            : throw new InvalidOperationException("The Workbook holds several Sheets, which a Sheet Document cannot record; ToWorkbookDocumentAsync records them (ADR-0331).");
    }

    /// <summary>The Workbook as it stands, as a Workbook Document: every Sheet in order, the Sheet shown, never a Value
    /// (ADR-0331).</summary>
    public async Task<WorkbookDocument> ToWorkbookDocumentAsync()
        => WorkbookDocument.FromJson(Text((await DocumentsAsync()).GetProperty("workbook")));

    private async Task<JsonElement> DocumentsAsync()
    {
        var sheet = await ReadyAsync();
        return await AskAsync(sheet, "documents");
    }

    private async Task<bool> CommandAsync(string command) => (await AnswerAsync(command)).GetBoolean();

    // A command, in its place behind the parameters sent before it: the browser takes the calls in the order they are
    // made. A refusal, and what else the Sheet threw at the caller, cross as data, and are thrown here as the Razor
    // component's engine throws them.
    private async Task<JsonElement> AnswerAsync(string command, params object?[] args)
    {
        var sheet = await ReadyAsync();
        return Answered(await AskAsync(sheet, command, args));
    }

    // What the browser answers: in the answer itself, or held when it is too large for a message of its own. Asked
    // under the circuit's timeout, unless a token is given, which none cuts short.
    private Task<JsonElement> AskAsync(IJSObjectReference sheet, string method, params object?[] args)
        => TakenAsync(sheet, sheet.InvokeAsync<JsonElement>(method, args));

    private Task<JsonElement> AskAsync(IJSObjectReference sheet, string method, CancellationToken cancel, object?[] args)
        => TakenAsync(sheet, sheet.InvokeAsync<JsonElement>(method, cancel, args));

    private async Task<JsonElement> TakenAsync(IJSObjectReference sheet, ValueTask<JsonElement> answer)
    {
        var crossed = await answer;
        try
        {
            return await CrossedMessage.OfAsync(sheet, crossed);
        }
        catch (JSException) when (_disposed)
        {
            // Disposed between the answer and its taking: the browser let go of what it held.
            throw new ObjectDisposedException(GetType().Name);
        }
    }

    private static JsonElement Answered(JsonElement answer)
    {
        if (answer.TryGetProperty("refused", out var refused))
            throw new SheetRefusedException(Refusal(refused));
        if (answer.TryGetProperty("thrown", out var thrown))
            throw Thrown(thrown);
        return answer.GetProperty("done");
    }

    // An error the Sheet threw, as the C# engine throws the same (tests/cases/README.md: the kinds both engines refuse alike).
    private static Exception Thrown(JsonElement json)
    {
        var message = Text(json.GetProperty("message"));
        return Text(json.GetProperty("kind")) switch
        {
            "argument" => new ArgumentException(message),
            "outOfRange" => new ArgumentOutOfRangeException(null, message),
            "invalidOperation" => new InvalidOperationException(message),
            "formulaSyntax" => new FormulaSyntaxException(Text(json.GetProperty("formula")), json.GetProperty("position").GetInt32(), Text(json.GetProperty("reason"))),
            "repeatedKey" => SheetWire.ReadRepeatedKey(Text(json.GetProperty("repeatedKey"))),
            "sheetDocument" => new SheetDocumentException(message)
            {
                DocumentVersion = json.GetProperty("documentVersion") is { ValueKind: JsonValueKind.Number } version ? version.GetInt32() : null,
            },
            var kind => new InvalidOperationException($"Internal: the Script Core threw an error of the kind '{kind}': {message}"),
        };
    }

    // The browser's Sheet, once it is attached: a command made before waits for it, and one made after the component
    // is disposed, or once the script was refused, is refused.
    private async Task<IJSObjectReference> ReadyAsync()
    {
        var sheet = await _ready.Task;
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (sheet is null && _attachFailure is { } failure)
            throw new InvalidOperationException("The Sheet did not open in the browser, so nothing was run: " + failure.Message, failure);
        return sheet ?? throw new InvalidOperationException(
            "The browser holds another version's ExSheet script, so the Sheet did not open; reloading the page loads this version's (ADR-0123).");
    }

    private static SheetRefusal Refusal(JsonElement json)
        => new(Member<SheetRefusalReason>(Text(json.GetProperty("reason"))), Text(json.GetProperty("message")));

    private static string[] Names(JsonElement json) => [.. json.EnumerateArray().Select(Text)];

    // A Selection as the browser writes it: its ranges, its Focus, and which range holds it; no range for none.
    private static GridSelection Selection(JsonElement json)
    {
        var ranges = json.GetProperty("ranges").EnumerateArray().Select(range => new SelectionRange(
            range.GetProperty("topRow").GetInt32(), range.GetProperty("leftColumn").GetInt32(),
            range.GetProperty("rowCount").GetInt32(), range.GetProperty("columnCount").GetInt32())).ToArray();
        if (ranges.Length == 0)
            return GridSelection.Empty;
        var focus = json.GetProperty("focus");
        return GridSelection.Reported(
            ranges, new CellPosition(focus.GetProperty("row").GetInt32(), focus.GetProperty("column").GetInt32()), json.GetProperty("focusRangeIndex").GetInt32());
    }

    private static string Text(JsonElement json) => json.GetString() ?? throw new InvalidOperationException("Internal: the Script Core sent null for a text.");

    private static void WriteSize(Utf8JsonWriter json, string name, ViewportSize size)
    {
        if (size.IsStretch)
            json.WriteString(name, "stretch");
        else
            json.WriteNumber(name, size.Px);
    }

    // An enum member as the Script Core spells it: `NumericalCount` is `numericalCount`.
    private static string Camel<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var name = value.ToString();
        return string.Concat(char.ToLowerInvariant(name[0]).ToString(), name.AsSpan(1));
    }

    // The member the Script Core spelt, refused when it names none.
    private static TEnum Member<TEnum>(string camel) where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(camel, ignoreCase: true, out var value) && Enum.IsDefined(value) && Camel(value) == camel
            ? value
            : throw new InvalidOperationException($"Internal: the Script Core sent '{camel}', which is no {typeof(TEnum).Name}.");

    private static string Json(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(buffer))
            write(json);
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    // The address a package's file is asked for at (ADR-0123), as ExGrid's PackageStylesheet gives it: the host's
    // fingerprinted address where it knows one, else the path with the version as `?v=`. Written again here rather than
    // taken from ExGrid's internals, which no package of the family reads (ADR-0201).
    private static string AddressOf(ResourceAssetCollection assets, string path, string version)
    {
        var fingerprinted = assets[path];
        return fingerprinted != path ? fingerprinted : $"{path}?v={version}";
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        _ready.TrySetResult(null);
        // An opening under way reads no further, and lands nowhere.
        foreach (var opening in _openings.Values)
            opening.Cancel.Cancel();
        // While it attaches, the Sheet it makes is stopped once it is made, and the bridge let go after it.
        if (_attaching)
            return;
        await ReleaseAsync(_sheet, _module);
        _bridge?.Dispose();
    }

    /// <summary>Stops the browser's Sheet, and lets go what .NET holds of the browser.</summary>
    private static async Task ReleaseAsync(IJSObjectReference? sheet, IJSObjectReference? module)
    {
        try
        {
            if (sheet is not null)
            {
                await sheet.InvokeVoidAsync("dispose");
                await sheet.DisposeAsync();
            }
            if (module is not null)
                await module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone, and the browser's Sheet with it: nothing is left there to stop.
        }
    }
}
