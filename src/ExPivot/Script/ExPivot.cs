using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ExGrid;
using ExGrid.Chrome;
using ExGrid.Columns;
using ExGrid.Selection;
using ExGrid.Script;
using ExGrid.Summarizing;
using ExPivot.Chrome;
using ExPivot.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace ExPivot.Script;

/// <summary>
/// Excel's PivotTable for Blazor, drawn by ExPivot's Script Core (ADR-0370 section 1): Blazor's Framework Adapter
/// over <c>@ex-grid/pivot</c>. The component renders one element, the pivot's root, mounts the Script Core on it,
/// hands it its parameters as options and its <see cref="PivotSource"/> as a source the browser asks through .NET,
/// and raises the pivot's events as <see cref="EventCallback"/>s. It holds no state of the pivot's, draws nothing
/// inside the root and decides nothing: the report, the Field List, the Pivot Toolbar and what each input means are
/// the Script Core's, in the browser, on both hosting models. The data stays the Consumer's, in .NET.
/// </summary>
/// <remarks>
/// <para>Every question the report asks crosses to .NET as the <see cref="PivotJson"/> both engines read and write,
/// and the source answers it there with its Leaf Aggregates, as a server answers a fetching source (ADR-0370 section
/// 3); the engine in the browser builds the report and its totals from them. A source's <see cref="PivotSource.Changed"/>
/// crosses as a notice, and the report asks again (ADR-0067).</para>
/// <para>A field's <see cref="PivotField.OrderKey"/>, a .NET function, is called in .NET for each
/// Item an answer names, and its keys cross with the Items (ADR-0412). What this adapter does not carry yet is refused
/// by name, never left out quietly: a field with declared Items that are not text, numbers or Booleans.</para>
/// </remarks>
public sealed class ExPivot : ComponentBase, IAsyncDisposable
{
    /// <summary>The script's address under the application's base: the package's minified Script Core and bridge.</summary>
    private const string ScriptPath = "_content/ExPivot/ex-pivot.min.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>The Pivot Source the report is computed from: its fields are what the Field List offers (ADR-0066). A new
    /// instance is a new source, and the report asks it again.</summary>
    [Parameter, EditorRequired] public PivotSource Source { get; set; } = default!;

    /// <summary>The Pivot Layout, View State the Consumer holds (ADR-0059). A layout handed in replaces the one on
    /// screen; one this pivot raised through <see cref="LayoutChanged"/> and handed back, however late, is its own and
    /// asks nothing. Left out, the pivot starts from nothing and keeps the user's changes itself.</summary>
    [Parameter] public PivotLayout? Layout { get; set; }

    /// <summary>Raised with the layout the report shows after each change the user made, once it is answered.</summary>
    [Parameter] public EventCallback<PivotLayout> LayoutChanged { get; set; }

    /// <summary>Whether the Field List stands beside the report; true unless set.</summary>
    [Parameter] public bool ShowFieldList { get; set; } = true;

    /// <summary>Raised when the user shows or hides the Field List.</summary>
    [Parameter] public EventCallback<bool> ShowFieldListChanged { get; set; }

    /// <summary>The Field List's width in CSS pixels; the Script Core's (300) unless set.</summary>
    [Parameter] public double? FieldListWidth { get; set; }

    /// <summary>The report grid's height, header included: pixels, or <see cref="ViewportSize.Stretch"/>; the Script
    /// Core's (400) unless set.</summary>
    [Parameter] public ViewportSize? ViewportHeight { get; set; }

    /// <summary>The report grid's width: pixels, or <see cref="ViewportSize.Stretch"/>, what the Field List leaves; the
    /// Script Core's (stretch) unless set.</summary>
    [Parameter] public ViewportSize? ViewportWidth { get; set; }

    /// <summary>The report's row height in CSS pixels; the density's unless set.</summary>
    [Parameter] public double? RowHeight { get; set; }

    /// <summary>The report's density preset (ADR-0028); a Wrapper's, or the grid's, unless set.</summary>
    [Parameter] public GridDensity? Density { get; set; }

    /// <summary>The widths the report's grids charge each glyph (ADR-0016), beating a Wrapper's and the Density's: a
    /// Consumer that sets the font the cells are drawn in hands that font's widths, so <c>####</c> and the label columns'
    /// widths are judged by the face on screen. The Density's unless set.</summary>
    [Parameter] public CellTextMetrics? CellMetrics { get; set; }

    /// <summary>What a Wrapper cascades (ADR-0030): the widths of the font its Theme sets and its Density, which the
    /// report's grids and its label columns are sized by where the pivot is given none.</summary>
    [CascadingParameter] public GridPresentationDefaults? Presentation { get; set; }

    /// <summary>The culture numbers, dates and counts are written in and text Items ordered by; the component's
    /// <see cref="CultureInfo.CurrentCulture"/> unless set (ADR-0370 section 10). It changes no word.</summary>
    [Parameter] public CultureInfo? Culture { get; set; }

    /// <summary>The Consumer's word for an id, or null to keep the English (ADR-0060): <see cref="PivotWords.Japanese"/>
    /// gives Excel's Japanese edition's words in one line. A function cannot cross to the browser, so it is asked for
    /// every id the pivot and its grid have words for each time the parameters are set, and the words cross only
    /// when they changed. It answers from the id alone: a Remove command's id
    /// that names a field (<c>remove-named-field:Region</c>) is not asked, and its words are the
    /// <c>remove-named-field</c> template filled with the caption.</summary>
    [Parameter] public Func<string, string?>? Label { get; set; }

    /// <summary>The caps on what a question may ask for and a report may hold (ADR-0066); Excel's unless set.</summary>
    [Parameter] public PivotCaps? Caps { get; set; }

    /// <summary>How long after a change of data reached the screen the next is asked for (ADR-0067); 250 ms unless set.</summary>
    [Parameter] public TimeSpan? RedrawInterval { get; set; }

    /// <summary>How long the Change Highlight marks a value the data changed (ADR-0068); one second unless set. Zero
    /// marks nothing.</summary>
    [Parameter] public TimeSpan? ChangeHighlightDuration { get; set; }

    /// <summary>Where the records behind a value cell open on a double click: a tab at the report's foot, or a dialog;
    /// a tab unless set.</summary>
    [Parameter] public PivotDetailsView? DetailsView { get; set; }

    /// <summary>Hears Show Details (ADR-0059, ADR-0063): the records behind a value cell double clicked. A Consumer
    /// that listens takes them, and the pivot shows nothing; otherwise they open as <see cref="DetailsView"/> says, as
    /// do records that reach .NET after the handler was let go, while the browser had not yet heard so. They are asked of the source the report
    /// was computed from, under its Source Version.</summary>
    [Parameter] public EventCallback<PivotDetails> OnShowDetails { get; set; }

    /// <summary>Hears the report's Selection each time it changes, in the report grid's rows and columns.</summary>
    [Parameter] public EventCallback<GridSelection> SelectionChanged { get; set; }

    /// <summary>Whether the Selection Summary is shown under the report (ADR-0130): Excel's status-bar figures over the
    /// cells selected, summed over the whole report in the browser. On unless set off.</summary>
    [Parameter] public bool ShowSelectionSummary { get; set; } = true;

    /// <summary>The Selection Summary's figures (ADR-0130); Excel's Average, Count and Sum unless set. The report holds the
    /// figures menu's choice from then on; figures handed here that differ from the ones handed before replace it.</summary>
    [Parameter] public SummaryFigures? SummaryFigures { get; set; }

    /// <summary>Raised when the figures menu changes the figures shown (ADR-0130).</summary>
    [Parameter] public EventCallback<SummaryFigures> SummaryFiguresChanged { get; set; }

    /// <summary>The Selection Summary as it now stands (ADR-0130), for an application that shows the figures
    /// elsewhere.</summary>
    [Parameter] public EventCallback<SelectionSummary> OnSelectionSummaryChanged { get; set; }

    /// <summary>How the report's work shares the browser's thread (ADR-0066, PV-40): its <see cref="PivotSlicing.Budget"/>
    /// is the slice the Script Core works for before it yields; the Script Core's (30 ms) unless set. It is read once, when
    /// the pivot attaches, as the Script Core reads it. The rest of a <see cref="PivotSlicing"/> is .NET's (a clock, a
    /// yield, how often a clock is read) and cannot reach the browser, so one that sets any of it is refused by name.</summary>
    [Parameter] public PivotSlicing? Slicing { get; set; }

    /// <summary>
    /// The Chrome that fills the Script Core's slots with Razor (ADR-0410): each of the pivot's slots its
    /// <see cref="IPivotChrome.ScriptSlots"/> names is drawn by its view overload, and each of the report grid's slots
    /// its grid Chrome's (<see cref="IPivotChrome.GridChrome"/>) <c>ScriptSlots</c> names, rendered beside the pivot's
    /// root and moved into the slot by the script, which wires their controls in the browser. Null, the default, leaves
    /// every part the Script Core's own. Which slots are filled is read as the pivot attaches; another Chrome naming
    /// the same slots draws them from then on.
    /// </summary>
    [Parameter] public IPivotChrome? Chrome { get; set; }

    // The slots the script mounted for the Chrome, by the number it gave each (ADR-0410 section 1).
    private readonly RazorSlotHolders _slots = new(PivotRazorSlot.ViewOf);
    // The Chrome the slots are drawn by, and the report grid's Chrome it answered, asked once per Chrome.
    private IPivotChrome? _renderedChrome;
    private IGridChrome? _gridChrome;
    // The slots the pivot attached with: another Chrome must name the same.
    private (PivotScriptSlots Pivot, ScriptSlots Grid)? _attachedSlots;

    // The Slicing the pivot attached with, as the parameter was first set, and as it was last set.
    private bool _slicingRead;
    private PivotSlicing? _attachedSlicing;
    private PivotSlicing? _slicingGiven;

    private PivotSource? _source;
    private Action<PivotSourceChanged>? _hearSource;
    private int _generation;
    private bool _sourceReplaced;
    private string? _sourceJson;
    // The sources the browser may still ask, by generation: the current one, and every one before it that the browser
    // still holds, as an open Details tab holds the source its records came from. The browser says when it has let one
    // go, and only then is it let go here.
    private readonly Dictionary<int, PivotSource> _sources = [];

    /// <summary>The browser's calls into .NET, made in the order the browser made them, one too large for a message of
    /// its own among them (ADR-0005, as amended 2026-10-20).</summary>
    internal global::ExGrid.Script.HeldCalls Calls { get; } = new();
    // A notice a source gave while the pivot was attaching, and the generation it is of, sent once it is attached.
    private (int Generation, string Notice)? _noticeOwed;

    // The layouts the pivot raised, by identity, each with the number of the Consumer's layouts the browser had been
    // sent when it raised it. Handed back while no other layout of the Consumer's has been sent since, it is the one on
    // screen and is not sent; handed back after one was, the screen has moved on, and it is sent like any other.
    private readonly ConditionalWeakTable<PivotLayout, StrongBox<int>> _raised = [];
    // The Layout parameter as last set: a layout is the Consumer's to send only when the parameter moves to one, so a
    // layout set once and never bound leaves the user's changes on screen, and one set again after the user's is sent.
    private PivotLayout? _layoutParameter;
    private PivotLayout? _sentLayout;
    private bool _layoutOwed;
    // How many of the Consumer's layouts have been put on their way to the browser.
    private int _layoutsSent;

    private readonly ConcurrentDictionary<long, CancellationTokenSource> _asked = new();
    private readonly Queue<Func<Task>> _heard = new();
    private bool _hearing;

    private ElementReference _root;
    private IJSObjectReference? _module;
    private IJSObjectReference? _pivot;
    private DotNetObjectReference<ScriptBridge>? _bridge;
    private string? _sentOptions;
    // Every id the pivot and its grid have words for, as the module answers them: what Label is asked for.
    private IReadOnlyList<string> _labelIds = [];
    // The Consumer's words the browser holds, as they were sent.
    private string? _sentWords;
    private bool _attached;
    private bool _attaching;
    private bool _refused;
    private bool _disposed;
    private bool _renderOwed = true;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Source is null)
            throw new InvalidOperationException("ExPivot takes its data from a Source (ADR-0066).");
        if (CellMetrics is { DigitWidthPx: <= 0 } empty)
            throw new ArgumentOutOfRangeException(nameof(CellMetrics), empty, "default(CellTextMetrics) is not valid; construct it with real metrics.");
        if (!_slicingRead)
        {
            _attachedSlicing = Slicing is { } slicing ? Carried(slicing) : null;
            _slicingRead = true;
        }
        else if (!Equals(Slicing, _slicingGiven))
        {
            throw new NotSupportedException(
                "ExPivot's Slicing is read once, when the pivot attaches, as the Script Core reads it (ADR-0370 section 1): make a new ExPivot for another.");
        }
        _slicingGiven = Slicing;
        if (!ReferenceEquals(Chrome, _renderedChrome))
        {
            var gridChrome = Chrome?.GridChrome(CommandLabelFor);
            var slots = SlotsOf(Chrome, gridChrome);
            if (_attachedSlots is { } attached && attached != slots)
            {
                throw new NotSupportedException(
                    "ExPivot's Chrome fills the slots it named as the pivot attached (ADR-0410): a Chrome naming other slots needs a new ExPivot.");
            }
            // Another Chrome draws the slots standing, and the script wires what it drew.
            _renderedChrome = Chrome;
            _gridChrome = gridChrome;
            _slots.Redrawn();
            _renderOwed |= _slots.Any;
        }
        if (!ReferenceEquals(Source, _source))
        {
            // Checked before anything moves: a source this adapter cannot carry leaves the pivot on the one it had.
            var generation = _generation + 1;
            var sourceJson = SourceJson(Source, generation);
            if (_source is not null)
            {
                _source.Changed -= _hearSource;
                _sourceReplaced = true;
            }
            _source = Source;
            _generation = generation;
            _sources[generation] = Source;
            _sourceJson = sourceJson;
            // Each source's notices carry its own generation, whatever thread it says them on.
            _hearSource = change => OnSourceChanged(generation, change);
            Source.Changed += _hearSource;
        }
        if (!ReferenceEquals(Layout, _layoutParameter))
        {
            _layoutParameter = Layout;
            var onScreen = Layout is { } raised && _raised.TryGetValue(raised, out var sentThen) && sentThen.Value == _layoutsSent;
            if (Layout is { } layout && !onScreen)
            {
                _sentLayout = layout;
                _layoutOwed = true;
            }
        }
    }

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
        // The element the script mounts on says it is Busy until the script has attached, which lifts it: before then
        // there is nothing to press, and a page holding several reads which have come (A11Y-20's note of 2026-10-30).
        // Constant in this tree until a refusal, so no later render writes either back over the script's own.
        if (!_refused)
        {
            builder.AddAttribute(7, "data-ex-mounting", "pivot");
            builder.AddAttribute(8, "aria-busy", "true");
        }
        builder.AddElementReferenceCapture(1, element => _root = element);
        if (_refused)
        {
            // The Script Core never attached: the root is Blazor's to say why, in the built-in words (ADR-0123).
            builder.OpenElement(2, "div");
            builder.AddAttribute(3, "role", "alert");
            builder.AddAttribute(4, "data-ex-notice", "script");
            // The Consumer's words first; null keeps the English (ADR-0060).
            builder.AddContent(5, Label?.Invoke(ReloadNoticeLabelIds.Script) ?? BuiltInCommandLabels.For(ReloadNoticeLabelIds.Script));
            builder.CloseElement();
        }
        builder.CloseElement();
        _slots.Render(builder, 6, (name, view) => PivotRazorSlot.Fragment(_renderedChrome, _gridChrome, name, view));
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            await ReportSlotsAsync();
            return;
        }
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
        catch (JSException)
        {
            // A module that does not load, or one older than the stamp, which has no `stamp` to call.
            stamp = null;
        }
        catch (JsonException)
        {
            // An answer that is not a string is no stamp of this family's.
            stamp = null;
        }
        if (_disposed)
        {
            // Disposed while the module loaded: what DisposeAsync could not yet see is let go here.
            await ReleaseAsync(null, module);
            return;
        }
        if (module is null || stamp != ScriptCoreAsset.Stamp)
        {
            // Another version's module is let go at once: nothing of it is called.
            _module = null;
            await ReleaseAsync(null, module);
            _refused = true;
            _renderOwed = true;
            StateHasChanged();
            return;
        }
        // The ids the Consumer's Label is asked for, as this version's module names them: a failure here is a defect of
        // this build, never another version's, so it fails the component rather than ask for a reload.
        _labelIds = await module.InvokeAsync<string[]>("labelIds");
        if (_disposed)
        {
            await ReleaseAsync(null, module);
            return;
        }
        // What the attach carries is what the browser holds once it returns: anything that changes while it is on
        // its way is owed again, and sent after it, in the order it changed.
        var options = OptionsJson();
        var attach = AttachJson(options);
        _sentOptions = options;
        _sourceReplaced = false;
        _layoutOwed = false;
        var bridge = DotNetObjectReference.Create(new ScriptBridge(this));
        _bridge = bridge;
        _attaching = true;
        IJSObjectReference pivot;
        try
        {
            pivot = await module.InvokeAsync<IJSObjectReference>("attach", _root, bridge, attach);
        }
        catch (Exception) when (_disposed)
        {
            // Disposed while it attached, and the attach failed with it, as it does when the circuit closes: what
            // DisposeAsync left to the attach is let go here, and nobody is left to tell of the failure.
            await ReleaseAsync(null, module);
            bridge.Dispose();
            return;
        }
        catch
        {
            // Never attached: a call held as it attached is never taken, and every call after it is let through.
            Calls.Stopped();
            throw;
        }
        finally
        {
            _attaching = false;
        }
        if (_disposed)
        {
            // Disposed while it attached: the browser's pivot is stopped before the object it calls is let go.
            await ReleaseAsync(pivot, module);
            bridge.Dispose();
            return;
        }
        _pivot = pivot;
        _attached = true;
        Calls.Attached(pivot);
        // A slot the pivot mounted as it attached may have been drawn already, before there was a pivot to hand it to.
        await ReportSlotsAsync();
        await SendUpdateAsync();
        if (_noticeOwed is var (generation, notice))
        {
            // A source replaced meanwhile went with its notice: the update sent the next, which is asked afresh.
            _noticeOwed = null;
            if (generation == _generation)
                await pivot.InvokeVoidAsync("changed", generation, notice);
        }
    }

    // The parameters, another source and the Consumer's layout, whichever changed since the browser last heard them.
    private async Task SendUpdateAsync()
    {
        var pivot = _pivot;
        if (pivot is null || _disposed)
            return;
        var options = OptionsJson();
        var words = WordsJson();
        var wordsMoved = words != _sentWords;
        if (options == _sentOptions && !_sourceReplaced && !_layoutOwed && !wordsMoved)
            return;
        _sentOptions = options;
        _sentWords = words;
        var update = Json(json =>
        {
            json.WriteStartObject();
            json.WritePropertyName("options");
            json.WriteRawValue(options);
            if (_sourceReplaced)
            {
                json.WritePropertyName("source");
                json.WriteRawValue(_sourceJson ?? throw new InvalidOperationException("Internal: the pivot has no source."));
            }
            if (_layoutOwed)
            {
                json.WriteString("layout", PivotLayoutJson.Write(_sentLayout ?? throw new InvalidOperationException("Internal: a layout owed and none held.")));
                json.WriteNumber("layoutsSent", ++_layoutsSent);
            }
            if (wordsMoved)
            {
                json.WritePropertyName("words");
                json.WriteRawValue(words);
            }
            json.WriteEndObject();
        });
        _sourceReplaced = false;
        _layoutOwed = false;
        await pivot.InvokeVoidAsync("update", update);
    }

    private string AttachJson(string options) => Json(json =>
    {
        json.WriteStartObject();
        json.WritePropertyName("options");
        json.WriteRawValue(options);
        json.WritePropertyName("source");
        json.WriteRawValue(_sourceJson ?? throw new InvalidOperationException("Internal: the pivot has no source."));
        if (_layoutOwed && _sentLayout is { } layout)
        {
            json.WriteString("layout", PivotLayoutJson.Write(layout));
            _layoutsSent += 1;
        }
        else
        {
            json.WriteNull("layout");
        }
        json.WriteNumber("layoutsSent", _layoutsSent);
        var words = WordsJson();
        _sentWords = words;
        json.WritePropertyName("words");
        json.WriteRawValue(words);
        // Across a circuit, Blazor Server's, a call larger than its hub takes would close it: the browser holds one.
        json.WriteBoolean("circuit", !OperatingSystem.IsBrowser());
        // The slots the Chrome fills with Razor, for the pivot's life (ADR-0410).
        var (pivotSlots, gridSlots) = SlotsOf(_renderedChrome, _gridChrome);
        _attachedSlots = (pivotSlots, gridSlots);
        if (pivotSlots == PivotScriptSlots.None && gridSlots == ScriptSlots.None)
        {
            json.WriteNull("razorSlots");
        }
        else
        {
            json.WriteStartObject("razorSlots");
            json.WriteStartArray("pivot");
            foreach (var name in PivotRazorSlot.NamesOf(pivotSlots))
                json.WriteStringValue(name);
            json.WriteEndArray();
            json.WriteStartArray("grid");
            foreach (var name in RazorSlot.NamesOf(gridSlots))
                json.WriteStringValue(name);
            json.WriteEndArray();
            json.WriteEndObject();
        }
        json.WriteEndObject();
    });

    // The slots a Chrome fills with Razor: the pivot's, and its grid Chrome's.
    private static (PivotScriptSlots Pivot, ScriptSlots Grid) SlotsOf(IPivotChrome? chrome, IGridChrome? gridChrome)
        => (chrome?.ScriptSlots ?? PivotScriptSlots.None, gridChrome?.ScriptSlots ?? ScriptSlots.None);

    // Each word the Consumer gives, asked here, since a .NET function cannot cross (ADR-0370 section 10); null keeps
    // the English.
    private string WordsJson() => Json(json =>
    {
        if (Label is not { } label)
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
        // The culture the component reads (ADR-0370 section 10).
        var culture = (Culture ?? CultureInfo.CurrentCulture).Name;
        json.WriteString("locale", culture.Length == 0 ? "invariant" : culture);
        json.WriteBoolean("showFieldList", ShowFieldList);
        if (FieldListWidth is { } fieldListWidth)
            json.WriteNumber("fieldListWidth", fieldListWidth);
        if (ViewportHeight is { } height)
            WriteSize(json, "viewportHeight", height);
        if (ViewportWidth is { } width)
            WriteSize(json, "viewportWidth", width);
        if (RowHeight is { } rowHeight)
            json.WriteNumber("rowHeight", rowHeight);
        if (Density is { } density)
            json.WriteString("density", Camel(density));
        if (CellMetrics is { } metrics)
            ScriptMetricsJson.WriteCellMetrics(json, "cellMetrics", metrics);
        if (Presentation is { } presentation)
            ScriptMetricsJson.WritePresentationDefaults(json, "presentationDefaults", presentation);
        if (Caps is { } caps)
        {
            json.WriteNumber("maxLeaves", caps.MaxLeaves);
            json.WriteNumber("maxRows", caps.MaxRows);
            json.WriteNumber("maxColumns", caps.MaxColumns);
        }
        if (RedrawInterval is { } redraw)
            json.WriteNumber("redrawInterval", redraw.TotalMilliseconds);
        if (ChangeHighlightDuration is { } highlight)
            json.WriteNumber("changeHighlightDuration", highlight.TotalMilliseconds);
        if (DetailsView is { } detailsView)
            json.WriteString("detailsView", Camel(detailsView));
        json.WriteBoolean("showSelectionSummary", ShowSelectionSummary);
        if (SummaryFigures is { } figures)
        {
            json.WriteStartArray("summaryFigures");
            foreach (var figure in SummaryFigureOrder.Each)
            {
                if ((figures & figure) == figure)
                    json.WriteStringValue(Camel(figure));
            }
            json.WriteEndArray();
        }
        // Read once by the Script Core, so the one it attached with is sent each time.
        if (_attachedSlicing is { } slicing)
        {
            json.WriteStartObject("slicing");
            json.WriteNumber("sliceBudgetMs", slicing.Budget.TotalMilliseconds);
            json.WriteEndObject();
        }
        // The stylesheets the Chrome brings, whose notices the pivot's root renders beside its own (ADR-0123, ERR-11):
        // the Pivot Chrome's, and its report grid's Chrome's, which the browser's grid does not render.
        var stylesheets = (_renderedChrome?.Stylesheets ?? []).Concat(_gridChrome?.Stylesheets ?? []).DistinctBy(sheet => sheet.Name).ToList();
        if (stylesheets.Count > 0)
        {
            json.WriteStartArray("stylesheets");
            foreach (var stylesheet in stylesheets)
            {
                json.WriteStartObject();
                json.WriteString("name", stylesheet.Name);
                json.WriteString("stamp", stylesheet.Stamp);
                json.WriteEndObject();
            }
            json.WriteEndArray();
        }
        json.WriteStartArray("hears");
        if (ShowFieldListChanged.HasDelegate)
            json.WriteStringValue("showFieldListChanged");
        if (OnShowDetails.HasDelegate)
            json.WriteStringValue("showDetails");
        if (SelectionChanged.HasDelegate)
            json.WriteStringValue("selectionChanged");
        if (SummaryFiguresChanged.HasDelegate)
            json.WriteStringValue("summaryFiguresChanged");
        if (OnSelectionSummaryChanged.HasDelegate)
            json.WriteStringValue("selectionSummaryChanged");
        json.WriteEndArray();
        json.WriteEndObject();
    });

    // The source as the browser declares it: its fields and what it answers. A field this adapter cannot carry is
    // refused by name.
    private static string SourceJson(PivotSource source, int generation) => Json(json =>
    {
        json.WriteStartObject();
        json.WriteNumber("generation", generation);
        global::ExGrid.Script.PivotSourceCrossing.WriteMembers(json, source);
        json.WriteEndObject();
    });

    private void OnSourceChanged(int generation, PivotSourceChanged change)
    {
        if (_refreshes.Out(generation))
            return;
        var text = PivotJson.Write(change);
        // A source may say it changed from any thread: its notices cross from the component's context, in the order
        // it said them.
        _ = InvokeAsync(async () =>
        {
            if (_disposed || generation != _generation)
                return;
            var pivot = _pivot;
            if (pivot is null)
            {
                // Attaching: the browser may already have asked the data before this change. Said once it is attached.
                _noticeOwed = (generation, text);
                return;
            }
            try
            {
                await pivot.InvokeVoidAsync("changed", generation, text);
            }
            catch (Exception ex) when (ex is not JSDisconnectedException)
            {
                await DispatchExceptionAsync(ex);
            }
        });
    }

    // The pivot's events, handed to the Consumer one at a time in the order the browser raised them: a handler that
    // awaits finishes before the next is called. What a handler throws fails the component, as Blazor's own do. What
    // it answers is whether a handler took the event: records nobody takes now open in the browser.
    internal async Task<bool> HeardAsync(string kind, JsonElement payload)
    {
        var taken = true;
        await InvokeAsync(async () =>
        {
            // Read in the component's turn, against the parameters as they stand when the event arrives.
            Func<Task>? hand;
            try
            {
                hand = Prepare(kind, payload);
            }
            catch (Exception ex)
            {
                // An event .NET cannot read fails the component in its turn, as a handler's exception does.
                hand = () => Task.FromException(ex);
            }
            if (hand is null)
            {
                taken = false;
                return;
            }
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
        });
        return taken;
    }

    // What to hand the Consumer for an event, or null for records no handler takes.
    private Func<Task>? Prepare(string kind, JsonElement payload)
    {
        switch (kind)
        {
            case "layoutChanged":
            {
                var layout = PivotLayoutJson.Read(payload.GetProperty("layout").GetString()
                    ?? throw new InvalidOperationException("Internal: the Script Core raised no layout."));
                // The one on screen as soon as it is read, unless a layout of the Consumer's reached the browser after it.
                _raised.AddOrUpdate(layout, new StrongBox<int>(payload.GetProperty("layoutsSent").GetInt32()));
                return () => LayoutChanged.InvokeAsync(layout);
            }
            case "showFieldListChanged":
            {
                var shown = payload.GetBoolean();
                return () => ShowFieldListChanged.InvokeAsync(shown);
            }
            case "selectionChanged":
            {
                var selection = Selection(payload);
                return () => SelectionChanged.InvokeAsync(selection);
            }
            case "summaryFiguresChanged":
            {
                var figures = FiguresOf(payload);
                return () => SummaryFiguresChanged.InvokeAsync(figures);
            }
            case "selectionSummaryChanged":
            {
                var summary = SummaryOf(payload);
                return () => OnSelectionSummaryChanged.InvokeAsync(summary);
            }
            case "showDetails":
            {
                // The handler that took them is the one handed them, whatever the parameter holds once their turn comes.
                var take = OnShowDetails;
                if (!take.HasDelegate || _disposed)
                    return null;
                var details = Details(payload);
                return () => take.InvokeAsync(details);
            }
            default:
                throw new InvalidOperationException($"Internal: the Script Core raised an event of the kind '{kind}'.");
        }
    }

    // The cell the user asked the records of, and the question for them, asked of the source the report on screen was
    // computed from: the browser holds it, so .NET has not let it go.
    private PivotDetails Details(JsonElement payload)
    {
        var generation = payload.GetProperty("generation").GetInt32();
        if (!_sources.TryGetValue(generation, out var source))
            throw new InvalidOperationException($"Internal: Show Details named the source of generation {generation}, which the browser let go.");
        var valueField = payload.GetProperty("valueField");
        return new PivotDetails(
            source.Fields,
            source.DetailsAsync,
            PivotJson.ReadDetailsQuery(Text(payload.GetProperty("query"))),
            DetailItems(payload.GetProperty("rowItems")),
            DetailItems(payload.GetProperty("columnItems")),
            valueField.ValueKind == JsonValueKind.Null ? null : Text(valueField),
            Text(payload.GetProperty("title")));
    }

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

    private static IReadOnlyList<PivotDetailItem> DetailItems(JsonElement json)
        => [.. json.EnumerateArray().Select(item => new PivotDetailItem(Text(item.GetProperty("field")), Text(item.GetProperty("item"))))];

    private static string Text(JsonElement json) => json.GetString() ?? throw new InvalidOperationException("Internal: the Script Core sent null for a text.");

    // A command's words in the report grid's menus, as IPivotChrome.GridChrome has them: the Consumer's through Label,
    // then ExPivot's English, the Remove command filled with its field's caption.
    private string? CommandLabelFor(string id)
    {
        if (Label?.Invoke(id) is { } own)
            return own;
        return PivotCommandIds.CaptionOfRemove(id) is { } caption
            ? PivotWords.Fill(Label?.Invoke(PivotCommandIds.RemoveNamedField) ?? PivotWords.EnglishFor(PivotCommandIds.RemoveNamedField)
                ?? throw new InvalidOperationException("Internal: ExPivot has no English for removing a named field."), caption)
            : PivotWords.EnglishFor(id);
    }

    // Each question the browser asks, answered by the source of its generation, or refused as one the Consumer replaced.
    internal async Task<string> AskAsync(string kind, int generation, long call, string? query)
    {
        if (_disposed || !_sources.TryGetValue(generation, out var source))
            throw new OperationCanceledException("The Pivot Source asked was replaced, and the browser has heard so.");
        using var cancellation = new CancellationTokenSource();
        if (!_asked.TryAdd(call, cancellation))
            throw new InvalidOperationException($"Internal: the browser asked question {call} twice.");
        try
        {
            return await global::ExGrid.Script.PivotSourceCrossing.AnswerAsync(source, kind, query, cancellation.Token);
        }
        finally
        {
            _asked.TryRemove(call, out _);
        }
    }

    /// <summary>The script mounted slot <paramref name="id"/> for the Chrome, with its context as data (ADR-0410).</summary>
    internal void MountSlot(long id, int version, string name, string context)
    {
        if (_disposed)
            return;
        _slots.Mount(id, version, name, context);
        RenderSlots();
    }

    /// <summary>The slot <paramref name="id"/> was mounted again in place, or its context changed.</summary>
    internal void UpdateSlot(long id, int version, string context)
    {
        if (_disposed || !_slots.Update(id, version, context))
            return;
        RenderSlots();
    }

    /// <summary>The pivot took the slot <paramref name="id"/> away; the script has moved its contents back.</summary>
    internal void UnmountSlot(long id)
    {
        if (_disposed || !_slots.Unmount(id))
            return;
        RenderSlots();
    }

    private void RenderSlots()
    {
        _renderOwed = true;
        StateHasChanged();
    }

    // Each slot drawn again is handed to the script, which moves it in, or wires it for the context it now holds.
    private Task ReportSlotsAsync() => _slots.ReportAsync(_pivot, () => _disposed);

    internal async Task RefreshAsync(int generation)
    {
        if (_disposed || !_sources.TryGetValue(generation, out var source))
            throw new OperationCanceledException("The Pivot Source asked was replaced, and the browser has heard so.");
        await _refreshes.RefreshAsync(generation, source);
    }

    private readonly global::ExGrid.Script.PivotRefreshes _refreshes = new();

    // The browser let go of the source of `generation`: it asks it nothing more.
    internal void Release(int generation)
    {
        if (generation != _generation)
            _sources.Remove(generation);
    }

    internal void Cancel(long call)
    {
        if (_asked.TryGetValue(call, out var asked))
            asked.Cancel();
    }

    // A Slicing whose every setting but its budget is the default's: the rest is .NET's, and refused by name.
    private static PivotSlicing Carried(PivotSlicing slicing)
    {
        var standard = PivotSlicing.Default;
        string? named = slicing.Yield is not null ? nameof(PivotSlicing.Yield)
            : !ReferenceEquals(slicing.TimeProvider, standard.TimeProvider) ? nameof(PivotSlicing.TimeProvider)
            : slicing.RecordsPerCheck != standard.RecordsPerCheck ? nameof(PivotSlicing.RecordsPerCheck)
            : null;
        if (named is not null)
        {
            throw new NotSupportedException(
                $"ExPivot's Slicing sets its {named}, which is .NET's and cannot reach the pivot in the browser (ADR-0370 section 1): set its Budget alone.");
        }
        return slicing;
    }

    // A figure as the Script Core spells it, read back.
    private static SummaryFigures Figure(string name)
        => Enum.TryParse<SummaryFigures>(name, ignoreCase: true, out var figure) && SummaryFigureOrder.Each.Contains(figure)
            ? figure
            : throw new InvalidOperationException($"Internal: the Script Core named the figure '{name}'.");

    // A Selection Summary as the browser writes it: its status, the question and the answer.
    private static SelectionSummary SummaryOf(JsonElement json)
    {
        var status = Enum.Parse<SelectionSummaryStatus>(Text(json.GetProperty("status")), ignoreCase: true);
        var request = json.GetProperty("request");
        var result = json.GetProperty("result");
        return new SelectionSummary(status,
            request.ValueKind == JsonValueKind.Null ? null : SummaryRequestOf(request),
            result.ValueKind == JsonValueKind.Null ? null : SummaryResultOf(result));
    }

    // A list of the Script Core's figure names, as the flags .NET writes them in.
    private static ExGrid.Summarizing.SummaryFigures FiguresOf(JsonElement names)
    {
        var figures = ExGrid.Summarizing.SummaryFigures.None;
        foreach (var name in names.EnumerateArray())
            figures |= Figure(Text(name));
        return figures;
    }

    private static GridSummaryRequest SummaryRequestOf(JsonElement json)
    {
        var figures = FiguresOf(json.GetProperty("figures"));
        var focus = json.GetProperty("focus");
        return new GridSummaryRequest
        {
            Ranges = [.. json.GetProperty("ranges").EnumerateArray().Select(range => new SelectionRange(
                range.GetProperty("topRow").GetInt32(), range.GetProperty("leftColumn").GetInt32(),
                range.GetProperty("rowCount").GetInt32(), range.GetProperty("columnCount").GetInt32()))],
            Columns = [.. json.GetProperty("columns").EnumerateArray().Select(Text)],
            RowSequenceVersion = json.GetProperty("rowSequenceVersion").GetInt32(),
            Figures = figures,
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
        "error" => ExGrid.Data.AggregateResult.Of(Enum.Parse<ExGrid.Data.AggregateError>(Text(json.GetProperty("error")), ignoreCase: true)),
        "number" when json.TryGetProperty("exact", out var exact) => ExGrid.Data.AggregateResult.Of(decimal.Parse(Text(exact), NumberStyles.Number, CultureInfo.InvariantCulture)),
        "number" => ExGrid.Data.AggregateResult.Of(json.GetProperty("number").GetDouble()),
        var kind => throw new InvalidOperationException($"Internal: the Script Core answered a figure of the kind '{kind}'."),
    };

    private static void WriteSize(Utf8JsonWriter json, string name, ViewportSize size)
    {
        if (size.IsStretch)
            json.WriteString(name, "stretch");
        else
            json.WriteNumber(name, size.Px);
    }

    // An enum member as the Script Core spells it: `CountNumbers` is `countNumbers`. A value that names no member is
    // refused here, rather than sent as a number the browser would refuse later.
    private static string Camel<TEnum>(TEnum value) where TEnum : struct, Enum => global::ExGrid.Script.PivotSourceCrossing.Camel(value);

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
        Calls.Stopped();
        if (_source is not null)
            _source.Changed -= _hearSource;
        foreach (var asked in _asked.Values.ToArray())
            asked.Cancel();
        // While it attaches, the pivot it makes is stopped once it is made, and the bridge let go after it.
        if (_attaching)
            return;
        await ReleaseAsync(_pivot, _module);
        _bridge?.Dispose();
    }

    /// <summary>Stops the browser's pivot, and lets go what .NET holds of the browser.</summary>
    private static async Task ReleaseAsync(IJSObjectReference? pivot, IJSObjectReference? module)
    {
        try
        {
            if (pivot is not null)
            {
                await pivot.InvokeVoidAsync("dispose");
                await pivot.DisposeAsync();
            }
            if (module is not null)
                await module.DisposeAsync();
        }
        catch (Exception ex) when (ex is JSDisconnectedException or OperationCanceledException)
        {
            // The circuit is gone, or is closing and has called off what it had sent the browser, and the browser's
            // pivot goes with it: nothing is left there to stop.
        }
    }
}
