using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ExChart.Chrome;
using ExChart.Engine;
using ExChart.Script;
using ExGrid;
using ExGrid.Chrome;
using ExGrid.Data;
using ExGrid.Data.Arrow;
using ExGrid.Script;
using ExPivot;
using ExPivot.Chrome;
using ExPivot.Engine;
using ExPivot.Script;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace ExBoard.Script;

/// <summary>
/// A Board (ADR-0340) for Blazor, drawn by ExBoard's Script Core (ADR-0370 section 1): Blazor's Framework Adapter over
/// <c>@ex-grid/board</c>. The component renders one element, the Board's root, mounts the Script Core on it, hands it
/// the Board as its JSON, each input's Snapshot as its Arrow stream (ADR-0065) and its parameters as options, and
/// raises the Board's outputs and Diffs as <see cref="EventCallback"/>s. It holds no state of the Board's, draws
/// nothing inside the root and decides nothing: the binding, the layout, the links, the outputs and the Board Diff are
/// the Script Core's, in the browser, on both hosting models.
/// </summary>
/// <remarks>
/// <para>A Snapshot crosses once: a later render names it by the number it crossed under, and the browser says when it
/// has let one go. An output is read back against the very Snapshot it was taken from, so
/// <see cref="BoardOutput.Take"/> holds it to that data's version as a Board bound in .NET does.</para>
/// <para>A source held where the data lives (<see cref="Sources"/>, ADR-0343) stays in .NET: the Board in the browser asks
/// it through .NET for its shape, a grid's pages, a chart's points and the values under a pivot's cells, and each page
/// of rows crosses as its Arrow stream. A pivot over one reads the Pivot Source it makes (<see cref="BoardSource.Pivot"/>)
/// through .NET, as ExPivot's adapter reads one: the engine in the browser builds the report and its totals from the
/// Leaf Aggregates the source answers (ADR-0370 section 3).</para>
/// <para>It stands beside <see cref="Components.ExBoard"/>, the Razor component, until ExBoard's Blazor step holds
/// (ADR-0370 section 11).</para>
/// <para>Its views are dressed by the Chromes it is handed as the Razor component's are: <see cref="Chrome"/> draws the
/// grids and the pivots' report grids, <see cref="PivotChrome"/> the pivots, its grid Chrome the report grids where no
/// <see cref="Chrome"/> is given, and <see cref="ChartChrome"/> the charts. Each slot a Chrome names in its <c>ScriptSlots</c> is filled with Razor (ADR-0410),
/// rendered beside the Board's root and moved into the slot the Script Core made; the rest are the Script Core's
/// own.</para>
/// </remarks>
public sealed class ExBoard : ComponentBase, IAsyncDisposable
{
    /// <summary>The script's address under the application's base: the package's minified Script Core and bridge.</summary>
    private const string ScriptPath = "_content/ExBoard/ex-board.min.js";

    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>The Board to draw, as <see cref="BoardSpec.Read(string)"/> read it. A new one binds again.</summary>
    [Parameter, EditorRequired] public BoardReading Board { get; set; } = default!;

    /// <summary>The Snapshot each input is bound to, by the input's name. A new dictionary binds again; a Snapshot the
    /// browser already holds is not sent again.</summary>
    [Parameter, EditorRequired] public IReadOnlyDictionary<string, Snapshot> Data { get; set; } = default!;

    /// <summary>The source each input held where the data lives is bound to, by the input's name (ADR-0343): asked for its
    /// shape before anything is drawn, for a grid's rows a page at a time, and for a chart's points thinned where the data
    /// lives. A new dictionary binds again.</summary>
    [Parameter] public IReadOnlyDictionary<string, BoardSource>? Sources { get; set; }

    /// <summary>The Board's width in CSS pixels, from which a chart's width is its share of its row (a chart is drawn
    /// at a width it is told). Grids and pivots stretch to their share.</summary>
    [Parameter] public double Width { get; set; } = 1200;

    /// <summary>The culture a view's text is written in; the invariant culture unless set, as the Razor component has
    /// it (ADR-0370 section 10).</summary>
    [Parameter] public CultureInfo? Culture { get; set; }

    /// <summary>Raised when the user confirms an output the Board declares (ADR-0340, Q6), and only then: a selection on
    /// the way raises nothing.</summary>
    [Parameter] public EventCallback<BoardOutput> OnOutput { get; set; }

    /// <summary>What the user changed last time, laid over the Board as it binds (ADR-0340, Q7). What no longer fits the
    /// Board is dropped and said above its views, and the rest applies. One this Board raised through
    /// <see cref="OnDiff"/> and handed back, however late, is its own and binds nothing again, unless a Diff of your
    /// own was handed in after it was raised: the screen has moved on from it then, and it binds again like any
    /// other.</summary>
    [Parameter] public BoardDiff? Diff { get; set; }

    /// <summary>Raised when the user changes a view, with the whole Board Diff. The Board is never rewritten; the host
    /// keeps this (<see cref="BoardDiff.ToJson"/>) and hands it back as <see cref="Diff"/> next time.</summary>
    [Parameter] public EventCallback<BoardDiff> OnDiff { get; set; }

    /// <summary>The grid Chrome the grids and the pivots' report grids are drawn with; the Script Core's own markup by
    /// default. Each slot its <see cref="IGridChrome.ScriptSlots"/> names is drawn by its view overload (ADR-0410). Which
    /// slots are filled is read as the Board attaches; another Chrome naming other slots needs a new ExBoard.</summary>
    [Parameter] public IGridChrome? Chrome { get; set; }

    /// <summary>The pivot Chrome the pivots are drawn with; the Script Core's own markup by default. Each slot its
    /// <see cref="IPivotChrome.ScriptSlots"/> names is drawn by its view overload, and its grid Chrome
    /// (<see cref="IPivotChrome.GridChrome"/>) draws the report grids where no <see cref="Chrome"/> is given, as the Razor
    /// component has it (ADR-0410). Which slots are filled is read as the Board attaches.</summary>
    [Parameter] public IPivotChrome? PivotChrome { get; set; }

    /// <summary>The chart Chrome the charts are drawn with; the Script Core's own markup by default. Each slot its
    /// <see cref="ExChart.Chrome.IChartChrome.ScriptSlots"/> names, the toolbar and the context menu, is drawn by its
    /// view overload (ADR-0410). Which slots are filled is read as the Board attaches.</summary>
    [Parameter] public IChartChrome? ChartChrome { get; set; }

    private readonly SnapshotCrossing _crossing = new();
    private readonly SourceCrossing _sourceCrossing = new();
    // The questions the browser has asked a source and not had answered, by the number it asked each under.
    private readonly ConcurrentDictionary<long, CancellationTokenSource> _asked = new();
    // The Pivot Sources the browser's pivots made over sources, by the number the browser made each under, and the
    // questions asked of them, numbered apart from the sources' own.
    private readonly Dictionary<int, HeldPivot> _pivots = [];
    private readonly ConcurrentDictionary<long, (int Pivot, CancellationTokenSource Cancellation)> _pivotAsked = new();
    // Why the source's Pivot failed for a pivot the browser made, which its questions are refused with.
    private readonly Dictionary<int, Exception> _pivotsFailed = [];
    // What a Pivot Source said before the browser's Board was attached, said once it is, in the order it was said.
    private readonly List<(int Pivot, string Notice)> _noticesOwed = [];

    private BoardReading? _board;
    private string? _boardJson;
    private bool _boardOwed;
    private IReadOnlyDictionary<string, Snapshot>? _data;
    private bool _dataOwed;
    private bool _sourcesRead;
    private IReadOnlyDictionary<string, BoardSource>? _sources;
    private bool _sourcesOwed;

    // The Diffs the Board raised, by identity, each with the number of the Consumer's Diffs the browser had been sent
    // when it raised it. Handed back while no other Diff of the Consumer's has been sent since, it is the one on screen
    // and is not sent; handed back after one was, the screen has moved on, and it is sent like any other.
    private readonly ConditionalWeakTable<BoardDiff, StrongBox<int>> _raised = [];
    private bool _diffRead;
    private BoardDiff? _diffParameter;
    private bool _diffOwed;
    // How many of the Consumer's Diffs have been put on their way to the browser.
    private int _diffsSent;

    /// <summary>The browser's calls, in the order it made them, a call too large for a circuit's message among them.</summary>
    internal global::ExGrid.Script.HeldCalls Calls { get; } = new();

    private readonly Queue<Func<Task>> _heard = new();
    private bool _hearing;

    private ElementReference _root;
    private IJSObjectReference? _module;
    private IJSObjectReference? _view;
    private DotNetObjectReference<ScriptBridge>? _bridge;
    private string? _sentOptions;
    private Task _sending = Task.CompletedTask;
    private bool _attached;
    private bool _attaching;
    private bool _refused;
    private bool _disposed;
    private bool _renderOwed = true;

    // The slots the script mounted for the Chromes, by the number it gave each (ADR-0410 section 1): a chart's by
    // ExChart's reading, a pivot's by ExPivot's, a grid's by ExGrid's.
    private readonly RazorSlotHolders _slots = new((name, context) => ChartSlotName(name) is { } chart
        ? ChartRazorSlot.ViewOf(chart, context)
        : PivotRazorSlot.ViewOf(name, context));
    private IChartChrome? _renderedChartChrome;
    private bool _chromesRead;
    private IGridChrome? _renderedChrome;
    private IPivotChrome? _renderedPivotChrome;
    // The pivot Chrome's grid Chrome, asked once per pivot Chrome, as the Razor ExPivot asks it.
    private IGridChrome? _pivotGridChrome;
    // The slots the Board attached with: other Chromes must name the same.
    private BoardSlots? _attachedSlots;

    /// <summary>The slots the Chromes fill with Razor: the grids', the pivots', the report grids' and the charts'.</summary>
    private readonly record struct BoardSlots(ScriptSlots Grid, PivotScriptSlots Pivot, ScriptSlots PivotGrid, ChartScriptSlots Chart)
    {
        public bool None => this == default;
    }

    /// <summary>The name a chart's slot crosses under, apart from the pivot's of the same name (razor-board-slots.ts).</summary>
    private const string ChartSlotPrefix = "chart.";

    // The chart's own name of a slot the script names, or none for a grid's or a pivot's.
    private static string? ChartSlotName(string name) => name.StartsWith(ChartSlotPrefix, StringComparison.Ordinal) ? name[ChartSlotPrefix.Length..] : null;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Board is null)
            throw new InvalidOperationException("ExBoard draws a Board, read by BoardSpec.Read (ADR-0340).");
        if (Data is null)
            throw new InvalidOperationException("ExBoard binds each input to a Snapshot in Data (ADR-0340).");
        var boardMoved = !ReferenceEquals(Board, _board);
        var sourcesMoved = !_sourcesRead || !ReferenceEquals(Sources, _sources);
        if (boardMoved || sourcesMoved)
            RefuseWhatIsNotCarried(Board, Sources);
        if (boardMoved)
        {
            // Written before anything moves; a new one binds again, as the Razor component's does.
            _boardJson = BoardSpec.Write(Board);
            _board = Board;
            _boardOwed = true;
        }
        if (sourcesMoved || boardMoved)
        {
            // A new Board numbers each source again under its own declaration of the input.
            _sourcesRead = true;
            _sources = Sources;
            _sourcesOwed = true;
        }
        if (!ReferenceEquals(Data, _data))
        {
            // Refused here, by name, before any Snapshot is numbered.
            foreach (var (input, snapshot) in Data)
            {
                if (snapshot is null)
                    throw new InvalidOperationException($"ExBoard's Data hands input '{input}' no Snapshot.");
            }
            _data = Data;
            _dataOwed = true;
        }
        if (!_chromesRead || !ReferenceEquals(Chrome, _renderedChrome) || !ReferenceEquals(PivotChrome, _renderedPivotChrome)
            || !ReferenceEquals(ChartChrome, _renderedChartChrome))
            ReadChromes();
        if (!_diffRead || !ReferenceEquals(Diff, _diffParameter))
        {
            _diffRead = true;
            _diffParameter = Diff;
            var onScreen = Diff is { } raised && _raised.TryGetValue(raised, out var sentThen) && sentThen.Value == _diffsSent;
            if (!onScreen)
                _diffOwed = true;
        }
    }

    // Other Chromes draw the slots standing, and the script wires what they drew; Chromes naming other slots than the
    // Board attached with are refused, as ExPivot's are.
    private void ReadChromes()
    {
        var pivotGridChrome = _chromesRead && ReferenceEquals(PivotChrome, _renderedPivotChrome)
            ? _pivotGridChrome
            : PivotChrome?.GridChrome(PivotCommandLabel);
        var slots = SlotsOf(Chrome, PivotChrome, pivotGridChrome, ChartChrome);
        if (_attachedSlots is { } attached && attached != slots)
        {
            throw new NotSupportedException(
                "ExBoard's Chromes fill the slots they named as the Board attached (ADR-0410): Chromes naming other slots need a new ExBoard.");
        }
        _chromesRead = true;
        _renderedChrome = Chrome;
        _renderedPivotChrome = PivotChrome;
        _renderedChartChrome = ChartChrome;
        _pivotGridChrome = pivotGridChrome;
        _slots.Redrawn();
        _renderOwed |= _slots.Any;
    }

    // ExPivot's word for a command of its Context Menu, as the Razor ExBoard's pivots answer it with no Label: ExPivot's
    // English, null for an id that is not ExPivot's (IPivotChrome.GridChrome).
    private static string? PivotCommandLabel(string id) => PivotCommandIds.CaptionOfRemove(id) is { } caption
        ? PivotWords.Fill(PivotWords.EnglishFor(PivotCommandIds.RemoveNamedField)
            ?? throw new InvalidOperationException("Internal: ExPivot has no English for removing a named field."), caption)
        : PivotWords.EnglishFor(id);

    // The slots the Chromes fill with Razor: the grids', the pivots', the report grids', which the grid Chrome draws
    // where one is given and the pivot Chrome's grid Chrome otherwise, and the charts'.
    private static BoardSlots SlotsOf(IGridChrome? chrome, IPivotChrome? pivotChrome, IGridChrome? pivotGridChrome, IChartChrome? chartChrome)
        => new(chrome?.ScriptSlots ?? ScriptSlots.None, pivotChrome?.ScriptSlots ?? PivotScriptSlots.None,
            (chrome ?? pivotGridChrome)?.ScriptSlots ?? ScriptSlots.None, chartChrome?.ScriptSlots ?? ChartScriptSlots.None);

    // What the browser's Board cannot be handed yet, refused here, by name, before anything crosses.
    private static void RefuseWhatIsNotCarried(BoardReading board, IReadOnlyDictionary<string, BoardSource>? sources)
    {
        if (sources is null)
            return;
        foreach (var (input, source) in sources)
        {
            if (source is null)
                throw new InvalidOperationException($"ExBoard's Sources hands input '{input}' no source.");
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
        builder.AddElementReferenceCapture(1, element => _root = element);
        if (_refused)
        {
            // The Script Core never attached: the root is Blazor's to say why, in the built-in words (ADR-0123).
            builder.OpenElement(2, "div");
            builder.AddAttribute(3, "role", "alert");
            builder.AddAttribute(4, "data-ex-notice", "script");
            builder.AddContent(5, BuiltInCommandLabels.For(ReloadNoticeLabelIds.Script));
            builder.CloseElement();
        }
        builder.CloseElement();
        // A grid's slot is drawn by the Chrome its grid has: the grid Chrome where one is given, which then draws the
        // report grids too, and the pivot Chrome's grid Chrome otherwise, when only report grids have slots.
        _slots.Render(builder, 6, (name, view) => ChartSlotName(name) is { } chart
            ? ChartRazorSlot.Fragment(_renderedChartChrome, chart, view)
            : PivotRazorSlot.Fragment(_renderedPivotChrome, _renderedChrome ?? _pivotGridChrome, name, view));
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
        // What the attach carries is what the browser holds once it returns: anything that changes while it is on its
        // way is owed again, and sent after it, in the order it changed.
        var options = OptionsJson();
        var naming = _crossing.Name(Data);
        var attach = Json(json =>
        {
            json.WriteStartObject();
            json.WritePropertyName("options");
            json.WriteRawValue(options);
            // Across a circuit, Blazor Server's, a call larger than its hub takes would close it: the browser holds one.
            json.WriteBoolean("circuit", !OperatingSystem.IsBrowser());
            json.WriteString("board", _boardJson ?? throw new InvalidOperationException("Internal: the Board was never written."));
            naming.Write(json);
            _sourceCrossing.Write(json, Sources, Board);
            WriteDiff(json);
            WriteSlots(json);
            json.WriteEndObject();
        });
        _sentOptions = options;
        _boardOwed = false;
        _dataOwed = false;
        _sourcesOwed = false;
        var streams = await naming.StreamsAsync();
        var bridge = DotNetObjectReference.Create(new ScriptBridge(this));
        _bridge = bridge;
        _attaching = true;
        IJSObjectReference view;
        try
        {
            view = await module.InvokeAsync<IJSObjectReference>("attach", _root, bridge, attach, streams);
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
            // Disposed while it attached: the browser's Board is stopped before the object it calls is let go.
            await ReleaseAsync(view, module);
            bridge.Dispose();
            return;
        }
        _view = view;
        _attached = true;
        Calls.Attached(view);
        // A slot a view mounted as the Board attached may have been drawn already, before there was a Board to hand it to.
        await ReportSlotsAsync();
        // Each in its turn, taken off before it is sent: one said meanwhile waits behind those still owed.
        while (_noticesOwed.Count > 0)
        {
            var (pivot, notice) = _noticesOwed[0];
            _noticesOwed.RemoveAt(0);
            if (_pivots.ContainsKey(pivot))
                await view.InvokeVoidAsync("pivotChanged", pivot, notice);
        }
        await SendUpdateAsync();
    }

    // The parameters, another Board, other data and the Consumer's Diff, whichever changed since the browser last heard
    // them. One update at a time: each names its Snapshots against the one before, so they leave in the order they
    // were made, whatever writing their streams awaits.
    private Task SendUpdateAsync()
    {
        var next = SendAfterAsync(_sending);
        _sending = next;
        return next;
    }

    private async Task SendAfterAsync(Task before)
    {
        // One that failed has failed the component already.
        await before.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing);
        var view = _view;
        if (view is null || _disposed)
            return;
        var options = OptionsJson();
        if (options == _sentOptions && !_boardOwed && !_dataOwed && !_sourcesOwed && !_diffOwed)
            return;
        _sentOptions = options;
        var naming = _dataOwed ? _crossing.Name(Data) : null;
        var update = Json(json =>
        {
            json.WriteStartObject();
            json.WritePropertyName("options");
            json.WriteRawValue(options);
            if (_boardOwed)
                json.WriteString("board", _boardJson ?? throw new InvalidOperationException("Internal: the Board was never written."));
            if (naming is not null)
            {
                naming.Write(json);
            }
            else
            {
                json.WriteStartArray("sent");
                json.WriteEndArray();
            }
            if (_sourcesOwed)
                _sourceCrossing.Write(json, Sources, Board);
            if (_diffOwed)
                WriteDiff(json);
            json.WriteEndObject();
        });
        _boardOwed = false;
        _dataOwed = false;
        _sourcesOwed = false;
        DotNetStreamReference[] streams = naming is null ? [] : await naming.StreamsAsync();
        await view.InvokeVoidAsync("update", update, streams);
    }

    // The Consumer's Diff, or null for none, and how many of theirs have been sent with it.
    private void WriteDiff(Utf8JsonWriter json)
    {
        if (Diff is { } diff)
            json.WriteString("diff", diff.ToJson());
        else
            json.WriteNull("diff");
        json.WriteNumber("diffsSent", ++_diffsSent);
        _diffOwed = false;
    }

    // The slots the Chromes fill with Razor, for the Board's life (ADR-0410), or null for none.
    private void WriteSlots(Utf8JsonWriter json)
    {
        var slots = SlotsOf(_renderedChrome, _renderedPivotChrome, _pivotGridChrome, _renderedChartChrome);
        _attachedSlots = slots;
        if (slots.None)
        {
            json.WriteNull("razorSlots");
            return;
        }
        json.WriteStartObject("razorSlots");
        WriteNames(json, "grid", RazorSlot.NamesOf(slots.Grid));
        WriteNames(json, "pivot", PivotRazorSlot.NamesOf(slots.Pivot));
        WriteNames(json, "pivotGrid", RazorSlot.NamesOf(slots.PivotGrid));
        WriteNames(json, "chart", ChartRazorSlot.NamesOf(slots.Chart));
        json.WriteEndObject();
    }

    private static void WriteNames(Utf8JsonWriter json, string property, IEnumerable<string> names)
    {
        json.WriteStartArray(property);
        foreach (var name in names)
            json.WriteStringValue(name);
        json.WriteEndArray();
    }

    /// <summary>The script mounted slot <paramref name="id"/> for a Chrome, with its context as data (ADR-0410).</summary>
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

    /// <summary>A view took the slot <paramref name="id"/> away; the script has moved its contents back.</summary>
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
    private Task ReportSlotsAsync() => _slots.ReportAsync(_view, () => _disposed);

    private string OptionsJson() => Json(json =>
    {
        json.WriteStartObject();
        if (!double.IsFinite(Width) || Width <= 0)
            throw new ArgumentOutOfRangeException(nameof(Width), Width, "A Board's width is a number of CSS pixels above 0.");
        json.WriteNumber("width", Width);
        // The culture the Razor component reads, the invariant one unless set (ADR-0370 section 10).
        var culture = (Culture ?? CultureInfo.InvariantCulture).Name;
        json.WriteString("locale", culture.Length == 0 ? "invariant" : culture);
        json.WriteStartArray("hears");
        if (OnOutput.HasDelegate)
            json.WriteStringValue("output");
        if (OnDiff.HasDelegate)
            json.WriteStringValue("diff");
        json.WriteEndArray();
        json.WriteEndObject();
    });

    // The Board's events, handed to the Consumer one at a time in the order the browser raised them: a handler that
    // awaits finishes before the next is called. What a handler throws fails the component, as Blazor's own do.
    internal Task HeardAsync(string kind, JsonElement payload) => InvokeAsync(async () =>
    {
        // What it names is found in the component's turn, as the event arrives: the Snapshot or the source it was raised
        // from, which the browser still holds, since it lets one go only after telling what it raised from it.
        Func<Task> hand;
        try
        {
            hand = Prepare(kind, payload);
        }
        catch (Exception ex)
        {
            // An event .NET cannot read fails the component in its turn, as a handler's exception does.
            hand = () => Task.FromException(ex);
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

    private Func<Task> Prepare(string kind, JsonElement payload)
    {
        switch (kind)
        {
            case "output":
            {
                // Taken from a Snapshot sent, read against it; taken from rows a source held, read as the Board that
                // numbered the source declares the input.
                var text = Text(payload.GetProperty("output"));
                var snapshot = payload.GetProperty("snapshot");
                var source = payload.GetProperty("source");
                if ((snapshot.ValueKind == JsonValueKind.Null) == (source.ValueKind == JsonValueKind.Null))
                    throw new InvalidOperationException("Internal: the Script Core raised an output naming not exactly one of a Snapshot and a source.");
                Func<string, BoardOutput> read;
                if (snapshot.ValueKind == JsonValueKind.Null)
                {
                    var held = _sourceCrossing[source.GetInt32()];
                    read = json => BoardOutput.ReadFromSource(json, held.Input, held.Declared);
                }
                else
                {
                    var from = _crossing[snapshot.GetInt32()];
                    read = json => BoardOutput.Read(json, from);
                }
                return async () => await OnOutput.InvokeAsync(read(text));
            }
            case "diff":
            {
                var diff = BoardDiff.Read(Text(payload.GetProperty("diff")));
                var diffsSent = payload.GetProperty("diffsSent").GetInt32();
                return async () =>
                {
                    // The one on screen as soon as it is read, unless a Diff of the Consumer's reached the browser after it.
                    _raised.AddOrUpdate(diff, new StrongBox<int>(diffsSent));
                    await OnDiff.InvokeAsync(diff);
                };
            }
            default:
                throw new InvalidOperationException($"Internal: the Script Core raised an event of the kind '{kind}'.");
        }
    }

    // The browser let go of these Snapshots: no message names them again.
    internal Task ReleaseSnapshotsAsync(int[] numbers) => InvokeAsync(() => _crossing.Release(numbers));

    // The browser let go of these sources: it asks them nothing more.
    internal Task ReleaseSourcesAsync(int[] numbers) => InvokeAsync(() =>
    {
        _sourceCrossing.Release(numbers);
        // And the Pivot Sources made over them, which the Board that held them asks nothing more.
        foreach (var (pivot, held) in _pivots.Where(p => numbers.Contains(p.Value.Source)).ToArray())
        {
            _pivots.Remove(pivot);
            LetGo(pivot, held);
        }
    });

    // A question the browser asked a source, which no longer waits for the answer: whether it was being asked.
    internal bool TryCancel(long call)
    {
        if (!_asked.TryGetValue(call, out var asked))
            return false;
        asked.Cancel();
        return true;
    }

    // One question to a source, under the number the browser asked it by, cancelled when the browser says it no longer
    // waits; the source is the Consumer's, called as the Razor component calls it, and what it is asked is read as the
    // Board that numbered it declares the input.
    private async Task<T> AskAsync<T>(int number, long call, Func<SourceCrossing.Held, CancellationToken, Task<T>> ask)
    {
        if (_disposed)
            throw new OperationCanceledException("The Board was disposed, and asks its sources nothing more.");
        var held = _sourceCrossing[number];
        using var cancellation = new CancellationTokenSource();
        if (!_asked.TryAdd(call, cancellation))
            throw new InvalidOperationException($"Internal: the browser asked question {call} twice.");
        try
        {
            return await ask(held, cancellation.Token);
        }
        finally
        {
            _asked.TryRemove(call, out _);
        }
    }

    // The input's shape, as a Board declares its columns: {"columns": [{"name": "desk", "type": "text"}, …]}.
    internal Task<string> ShapeAsync(int number, long call) => AskAsync(number, call, async (held, token) =>
        BoardSpec.DeclarationOf(await held.Source.ShapeAsync(token) ?? throw new InvalidOperationException($"The source of input '{held.Input}' answered no shape.")));

    // A page of a grid's rows: where it starts, how many the whole result holds, and the rows as their Arrow stream
    // (ADR-0065), which crosses as bytes.
    internal Task<SourcePage> FetchAsync(int number, long call, string question) => AskAsync(number, call, async (held, token) =>
    {
        using var document = JsonDocument.Parse(question);
        var root = document.RootElement;
        var query = new BoardQuery(Text(root.GetProperty("view")), GridViewStateJson.ReadQuery(Text(root.GetProperty("grid"))), NarrowingOf(held, root.GetProperty("narrowing")));
        var page = await held.Source.Fetch(query, token);
        using var stream = new MemoryStream();
        await SnapshotArrow.WriteAsync(page.Rows, stream, token);
        return new SourcePage(page.Start, page.TotalCount, stream.ToArray());
    });

    // A chart's points, thinned where the data lives (ADR-0344): the rows kept as JSON text, each value written as the
    // input declares its column so the browser reads it as a Board Input reads a row, and what each thinned pixel held.
    internal Task<string> ThinAsync(int number, long call, string question) => AskAsync(number, call, async (held, token) =>
    {
        var input = held.Input;
        var thin = held.Source.Thin ?? throw new InvalidOperationException($"Internal: the browser asked the source of input '{input}' for a chart's points, and it thins none.");
        using var document = JsonDocument.Parse(question);
        var root = document.RootElement;
        var at = root.GetProperty("chart");
        var chart = new ChartSourceQuery(Text(at.GetProperty("data")), Text(at.GetProperty("x")), Text(at.GetProperty("y")),
            Enum.Parse<AxisValues>(Text(at.GetProperty("values")), ignoreCase: true), Position(at.GetProperty("from")), Position(at.GetProperty("to")), at.GetProperty("pixels").GetInt32());
        var answer = await thin(new BoardChartQuery(Text(root.GetProperty("view")), chart, NarrowingOf(held, root.GetProperty("narrowing"))), token);
        string[] columns = chart.X == chart.Y ? [chart.X] : [chart.X, chart.Y];
        var points = Json(json =>
        {
            json.WriteStartArray();
            for (var row = 0; row < answer.Points.RowCount; row++)
            {
                json.WriteStartObject();
                foreach (var column in columns)
                {
                    json.WritePropertyName(column);
                    WritePoint(json, answer.Points.Value(column, row), input, column);
                }
                json.WriteEndObject();
            }
            json.WriteEndArray();
        });
        return Json(json =>
        {
            json.WriteStartObject();
            json.WriteString("version", answer.Version.ToString(CultureInfo.InvariantCulture));
            json.WriteString("points", points);
            json.WriteStartArray("thinned");
            foreach (var pixel in answer.Thinned)
            {
                json.WriteStartArray();
                foreach (var value in new[] { pixel.From, pixel.To, pixel.Min, pixel.Max })
                {
                    if (!double.IsFinite(value))
                        throw new InvalidOperationException($"The source of input '{input}' answered chart '{chart.Data}' with a thinned pixel that holds {value}.");
                    json.WriteNumberValue(value);
                }
                json.WriteNumberValue(pixel.Count);
                json.WriteEndArray();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        });
    });

    // The rows under a pivot's selected cells, read where the data lives (ADR-0345): the layout as PivotLayoutJson, each
    // cell's paths as a Details query names them, the one form both engines read a path in; answered as the link's
    // columns and their tuples, each value written as the Board that numbered the source declares its column.
    internal Task<string> UnderAsync(int number, long call, string question) => AskAsync(number, call, async (held, token) =>
    {
        var input = held.Input;
        var under = held.Source.Under ?? throw new InvalidOperationException($"Internal: the browser asked the source of input '{input}' for the values under a pivot's cells, and it answers none.");
        using var document = JsonDocument.Parse(question);
        var root = document.RootElement;
        var cells = root.GetProperty("cells").EnumerateArray().Select(cell => PivotJson.ReadDetailsQuery(Text(cell))).Select(path => new PivotCellItems(path.RowItems, path.ColumnItems)).ToArray();
        var on = root.GetProperty("on").EnumerateArray().Select(Text).ToArray();
        var query = new BoardCellsQuery(Text(root.GetProperty("view")), PivotLayoutJson.Read(Text(root.GetProperty("layout"))), cells, on, NarrowingOf(held, root.GetProperty("narrowing")));
        if (on.Distinct(StringComparer.Ordinal).Count() != on.Length)
            throw new InvalidOperationException($"Internal: a link from pivot '{query.View}' compares a column twice.");
        var condition = await under(query, token);
        if (!condition.On.SequenceEqual(on, StringComparer.Ordinal))
            throw new InvalidOperationException($"The source of input '{input}' answered the values under pivot '{query.View}' on [{string.Join(", ", condition.On)}], not the link's [{string.Join(", ", on)}].");
        return Json(json =>
        {
            json.WriteStartArray();
            foreach (var tuple in condition.Values)
            {
                json.WriteStartObject();
                for (var i = 0; i < on.Length; i++)
                {
                    json.WritePropertyName(on[i]);
                    WritePoint(json, tuple[i], input, on[i]);
                }
                json.WriteEndObject();
            }
            json.WriteEndArray();
        });
    });

    // A Pivot Source the browser's pivot made over a source, under the number the browser made it under: over the rows
    // the narrowing lets through, read as the Board that numbered the source declares the input. What it says it
    // changed is told the browser's pivot, in the order it said it.
    internal Task MakePivotAsync(int source, int pivot, string narrowing) => InvokeAsync(async () =>
    {
        try
        {
            if (_disposed)
                return;
            var held = _sourceCrossing[source];
            using var document = JsonDocument.Parse(narrowing);
            var made = _sourceCrossing.PivotOver(source, NarrowingOf(held, document.RootElement));
            Action<PivotSourceChanged> hear = Hear;
            if (!_pivots.TryAdd(pivot, new HeldPivot(source, made, hear)))
                throw new InvalidOperationException($"Internal: the browser made pivot {pivot} twice.");
            made.Changed += hear;
        }
        catch (Exception ex)
        {
            // A source whose Pivot fails fails the component, as it does the Razor component's binding, and the
            // pivot's questions are refused with why.
            _pivotsFailed[pivot] = ex;
            await DispatchExceptionAsync(ex);
        }

        void Hear(PivotSourceChanged change)
        {
            var text = PivotJson.Write(change);
            // A source may say it changed from any thread: its notices cross from the component's context.
            _ = InvokeAsync(async () =>
            {
                if (_disposed || !_pivots.ContainsKey(pivot))
                    return;
                if (_view is not { } view || _noticesOwed.Count > 0)
                {
                    _noticesOwed.Add((pivot, text));
                    return;
                }
                try
                {
                    await view.InvokeVoidAsync("pivotChanged", pivot, text);
                }
                catch (Exception ex) when (ex is not JSDisconnectedException)
                {
                    await DispatchExceptionAsync(ex);
                }
            });
        }
    });

    // A question the browser's pivot asked a Pivot Source made over a source, answered as ExPivot's adapter answers
    // one; one the browser made over a source it has since let go is answered with nothing to act on.
    internal async Task<string> PivotAskAsync(string kind, int pivot, long call, string? query)
    {
        var held = PivotAsked(pivot);
        using var cancellation = new CancellationTokenSource();
        if (!_pivotAsked.TryAdd(call, (pivot, cancellation)))
            throw new InvalidOperationException($"Internal: the browser asked pivot question {call} twice.");
        try
        {
            return await PivotSourceCrossing.AnswerAsync(held.Pivot, kind, query, cancellation.Token);
        }
        finally
        {
            _pivotAsked.TryRemove(call, out _);
        }
    }

    internal async Task PivotRefreshAsync(int pivot) => await PivotAsked(pivot).Pivot.RefreshAsync();

    private HeldPivot PivotAsked(int pivot)
    {
        if (_pivotsFailed.TryGetValue(pivot, out var failed))
            throw new InvalidOperationException($"The source made no Pivot Source for this pivot: {failed.Message}", failed);
        if (_disposed || !_pivots.TryGetValue(pivot, out var held))
            throw new OperationCanceledException("The pivot's source was let go, and the browser has heard so.");
        return held;
    }

    // The browser let go of the Pivot Source made under `pivot`, or of the source it was made over first.
    internal void ReleasePivot(int pivot)
    {
        _pivotsFailed.Remove(pivot);
        if (!_pivots.Remove(pivot, out var held))
            return;
        LetGo(pivot, held);
    }

    // A Pivot Source the browser asks nothing more: what it says is heard no longer, and what it was asked is called off.
    private void LetGo(int pivot, HeldPivot held)
    {
        held.Pivot.Changed -= held.Hear;
        foreach (var (_, asked) in _pivotAsked.Values.Where(asked => asked.Pivot == pivot))
            asked.Cancel();
    }

    internal bool TryCancelPivot(long call)
    {
        if (!_pivotAsked.TryGetValue(call, out var asked))
            return false;
        asked.Cancellation.Cancel();
        return true;
    }

    private sealed record HeldPivot(int Source, PivotSource Pivot, Action<PivotSourceChanged> Hear);

    private static double? Position(JsonElement json) => json.ValueKind == JsonValueKind.Null ? null : json.GetDouble();

    // A link's conditions as the browser writes them, each value read as the Board that numbered the source declares
    // its column.
    private static List<LinkCondition> NarrowingOf(SourceCrossing.Held held, JsonElement narrowing)
    {
        var (input, _, declared) = held;
        var conditions = new List<LinkCondition>();
        foreach (var condition in narrowing.EnumerateArray())
        {
            var columns = condition.GetProperty("on").EnumerateArray().Select(Text).Select(name => declared.TryGetColumn(name, out var column)
                ? column
                : throw new InvalidOperationException($"Internal: a link narrows input '{input}' on column '{name}', which it does not declare.")).ToArray();
            conditions.Add(BoardOutput.ConditionOf(input, columns, condition.GetProperty("values"), $"a link's condition on input '{input}'"));
        }
        return conditions;
    }

    // A point's value as a Board Input reads one: a number as JSON's, a date as its day and time, a Blank as null. The
    // browser reads it as the input declares the column, and refuses one of another kind by name.
    private static void WritePoint(Utf8JsonWriter json, object? value, string input, string column)
    {
        switch (value)
        {
            case int or short or byte or sbyte or ushort or uint:
                json.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                break;
            case float number:
                BoardOutput.Write(json, (double)number);
                break;
            case DateOnly day:
                BoardOutput.Write(json, day.ToDateTime(TimeOnly.MinValue));
                break;
            case null or string or decimal or double or long or bool or DateTime:
                BoardOutput.Write(json, value);
                break;
            default:
                throw new InvalidOperationException($"The source of input '{input}' answered a point whose '{column}' is a {value.GetType().Name}, which no column of a Board holds.");
        }
    }

    private static string Text(JsonElement json) => json.GetString() ?? throw new InvalidOperationException("Internal: the Script Core sent null for a text.");

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
        // While it attaches, the Board it makes is stopped once it is made, and the bridge let go after it.
        if (!_attaching)
        {
            await ReleaseAsync(_view, _module);
            _bridge?.Dispose();
        }
        // The questions still out are nobody's now: cancelled once the rest is let go, so what a source's own
        // cancellation callback throws reaches the caller with nothing left held.
        foreach (var held in _pivots.Values)
            held.Pivot.Changed -= held.Hear;
        _pivots.Clear();
        foreach (var asked in _asked.Values.Concat(_pivotAsked.Values.Select(asked => asked.Cancellation)))
            asked.Cancel();
    }

    /// <summary>Stops the browser's Board, and lets go what .NET holds of the browser.</summary>
    private static async Task ReleaseAsync(IJSObjectReference? view, IJSObjectReference? module)
    {
        try
        {
            if (view is not null)
            {
                await view.InvokeVoidAsync("dispose");
                await view.DisposeAsync();
            }
            if (module is not null)
                await module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone, and the browser's Board with it: nothing is left there to stop.
        }
    }
}
