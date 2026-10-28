using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ExChart.Engine;
using ExGrid;
using ExGrid.Chrome;
using ExGrid.Data;
using ExGrid.Data.Arrow;
using ExGrid.Script;
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
/// it through .NET for its shape, a grid's pages and a chart's points, and each page of rows crosses as its Arrow stream.</para>
/// <para>It stands beside <see cref="Components.ExBoard"/>, the Razor component, until ExBoard's Blazor step holds
/// (ADR-0370 section 11). What it does not carry yet is refused by name, never left out quietly: a pivot over a source
/// (<see cref="BoardSource.Report"/>), which asks a report of .NET, not the Leaf Aggregates the browser's engine reads
/// (ADR-0370 section 3); and Chromes.</para>
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

    private readonly SnapshotCrossing _crossing = new();
    private readonly SourceCrossing _sourceCrossing = new();
    // The questions the browser has asked a source and not had answered, by the number it asked each under.
    private readonly ConcurrentDictionary<long, CancellationTokenSource> _asked = new();

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
        if (!_diffRead || !ReferenceEquals(Diff, _diffParameter))
        {
            _diffRead = true;
            _diffParameter = Diff;
            var onScreen = Diff is { } raised && _raised.TryGetValue(raised, out var sentThen) && sentThen.Value == _diffsSent;
            if (!onScreen)
                _diffOwed = true;
        }
    }

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
        if (board.Views.OfType<BoardPivotView>().FirstOrDefault(pivot => sources.ContainsKey(pivot.Input)) is { } over)
        {
            throw new NotSupportedException(
                $"Pivot '{over.Id}' is over input '{over.Input}', held on a server, and ExBoard.Script.ExBoard does not carry a pivot over a source yet: "
                + "its source makes a report (BoardSource.Report), and the pivot in the browser reads Leaf Aggregates (ADR-0370 section 3). Draw this Board with ExBoard.Components.ExBoard.");
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
    internal Task ReleaseSourcesAsync(int[] numbers) => InvokeAsync(() => _sourceCrossing.Release(numbers));

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
        foreach (var asked in _asked.Values)
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
