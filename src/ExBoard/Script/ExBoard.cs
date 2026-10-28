using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ExGrid.Chrome;
using ExGrid.Data;
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
/// <para>It stands beside <see cref="Components.ExBoard"/>, the Razor component, until ExBoard's Blazor step holds
/// (ADR-0370 section 11). It does not take sources held where the data lives (ADR-0343) or Chromes yet.</para>
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

    private BoardReading? _board;
    private string? _boardJson;
    private bool _boardOwed;
    private IReadOnlyDictionary<string, Snapshot>? _data;
    private bool _dataOwed;

    // The Diffs the Board raised, by identity, each with the number of the Consumer's Diffs the browser had been sent
    // when it raised it. Handed back while no other Diff of the Consumer's has been sent since, it is the one on screen
    // and is not sent; handed back after one was, the screen has moved on, and it is sent like any other.
    private readonly ConditionalWeakTable<BoardDiff, StrongBox<int>> _raised = [];
    private bool _diffRead;
    private BoardDiff? _diffParameter;
    private bool _diffOwed;
    // How many of the Consumer's Diffs have been put on their way to the browser.
    private int _diffsSent;

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
        if (!ReferenceEquals(Board, _board))
        {
            // Written before anything moves; a new one binds again, as the Razor component's does.
            _boardJson = BoardSpec.Write(Board);
            _board = Board;
            _boardOwed = true;
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
            json.WriteString("board", _boardJson ?? throw new InvalidOperationException("Internal: the Board was never written."));
            naming.Write(json);
            WriteDiff(json);
            json.WriteEndObject();
        });
        _sentOptions = options;
        _boardOwed = false;
        _dataOwed = false;
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
        if (options == _sentOptions && !_boardOwed && !_dataOwed && !_diffOwed)
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
            if (_diffOwed)
                WriteDiff(json);
            json.WriteEndObject();
        });
        _boardOwed = false;
        _dataOwed = false;
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
        // Read in the component's turn, as the event arrives, against the Snapshot it names, which the browser still
        // holds: it lets one go only after telling what it raised from it.
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
                var output = BoardOutput.Read(Text(payload.GetProperty("output")), _crossing[payload.GetProperty("snapshot").GetInt32()]);
                return () => OnOutput.InvokeAsync(output);
            }
            case "diff":
            {
                var diff = BoardDiff.Read(Text(payload.GetProperty("diff")));
                // The one on screen as soon as it is read, unless a Diff of the Consumer's reached the browser after it.
                _raised.AddOrUpdate(diff, new StrongBox<int>(payload.GetProperty("diffsSent").GetInt32()));
                return () => OnDiff.InvokeAsync(diff);
            }
            default:
                throw new InvalidOperationException($"Internal: the Script Core raised an event of the kind '{kind}'.");
        }
    }

    // The browser let go of these Snapshots: no message names them again.
    internal Task ReleaseSnapshotsAsync(int[] numbers) => InvokeAsync(() => _crossing.Release(numbers));

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
        // While it attaches, the Board it makes is stopped once it is made, and the bridge let go after it.
        if (_attaching)
            return;
        await ReleaseAsync(_view, _module);
        _bridge?.Dispose();
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
