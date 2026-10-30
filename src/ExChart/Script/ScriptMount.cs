using System.Globalization;
using System.Text.Json;
using global::ExGrid.Chrome;
using global::ExGrid.Script;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace ExChart.Script;

/// <summary>How an attach ended: the Script Core drew on the root, it was refused as another version's, or the
/// component was disposed while it loaded.</summary>
internal enum Mounted
{
    Attached,
    Refused,
    Gone,
}

/// <summary>What a component hands its drawing: its options as JSON, and its definition with the generation that names it.</summary>
internal readonly record struct DrawingState(string Options, string Definition, int Generation);

/// <summary>
/// The browser's half of a component, as <see cref="ExChart"/> and <see cref="ExSurface"/> both hold it (ADR-0370
/// section 1): the module loaded once, held to the stamp this build was made with, the handle the attach answered, what
/// the browser was last sent, and the events the browser raised, handed to the component one at a time in the order
/// raised. A handler that awaits finishes before the next is called, and what it throws fails the component, as
/// Blazor's own do.
/// </summary>
/// <param name="js">The component's runtime.</param>
/// <param name="invoke">The component's <c>InvokeAsync</c>: each event is read in the component's turn.</param>
/// <param name="fail">The component's <c>DispatchExceptionAsync</c>.</param>
/// <param name="prepare">What to hand the Consumer for an event, read against the parameters as they stand when it
/// arrives; null for one nobody hears.</param>
/// <param name="slots">The Chrome slots the component fills with Razor (ADR-0410), where it takes a Chrome.</param>
/// <param name="slotsChanged">Renders the component again, as the script mounts, updates or unmounts a slot.</param>
internal sealed class ScriptMount(
    IJSRuntime js, Func<Func<Task>, Task> invoke, Func<Exception, Task> fail, Func<string, JsonElement, Func<Task>?> prepare,
    RazorSlotHolders? slots = null, Action? slotsChanged = null)
{
    /// <summary>The script's address under the application's base: ExChart's minified Script Core and bridge.</summary>
    private const string ScriptPath = "_content/ExChart/ex-chart-core.min.js";

    private readonly Queue<Func<Task>> _heard = new();
    private bool _hearing;
    private IJSObjectReference? _module;
    private IJSObjectReference? _handle;
    private DotNetObjectReference<ScriptBridge>? _bridge;
    private bool _attaching;
    // What the browser holds: the options as last sent, and the generation of the definition.
    private string? _sentOptions;
    private int _sentGeneration;

    /// <summary>The calls the browser makes into .NET, in the order it made them, a call too large for a message of its
    /// own among them (ADR-0005).</summary>
    internal HeldCalls Calls { get; } = new();

    /// <summary>Whether the component was disposed: nothing more crosses.</summary>
    public bool Disposed { get; private set; }

    /// <summary>Whether the script was refused as another version's: the root says so, and nothing is drawn.</summary>
    public bool Refused { get; private set; }

    /// <summary>
    /// Renders the root: an element the Script Core draws in, or, once the script was refused, the notice in the
    /// built-in words (ADR-0123). A <paramref name="refusal"/> is said in the chart's
    /// place, in a box of the chart's own <paramref name="width"/> and <paramref name="height"/> under its tokens, and
    /// the root is hidden beside it, as a spec its sources cannot answer is (ADR-0344): beside the root, whose children
    /// are the Script Core's. <paramref name="busy"/> marks the root Busy while an answer is awaited.
    /// </summary>
    public void Render(RenderTreeBuilder builder, Action<ElementReference> capture, string? refusal = null, bool busy = false,
        double width = 0, double height = 0)
    {
        if (refusal is not null && !Refused)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "ex-chart");
            builder.AddAttribute(2, "style", string.Create(CultureInfo.InvariantCulture, $"width:{width}px;min-height:{height}px"));
            builder.OpenElement(3, "p");
            builder.AddAttribute(4, "class", "ex-chart-source-refusal");
            builder.AddAttribute(5, "role", "alert");
            builder.AddContent(6, refusal);
            builder.CloseElement();
            builder.CloseElement();
        }
        builder.OpenElement(7, "div");
        if (Refused)
            builder.AddAttribute(8, "class", "ex-chart");
        builder.AddAttribute(9, "hidden", refusal is not null && !Refused);
        builder.AddAttribute(10, "aria-busy", busy && !Refused ? "true" : null);
        builder.AddElementReferenceCapture(11, capture);
        if (Refused)
        {
            builder.OpenElement(12, "p");
            builder.AddAttribute(13, "class", "ex-chart-script-notice");
            builder.AddAttribute(14, "role", "alert");
            builder.AddContent(15, BuiltInCommandLabels.For(ReloadNoticeLabelIds.Script));
            builder.CloseElement();
        }
        builder.CloseElement();
    }

    /// <summary>
    /// Attaches the drawing with <paramref name="function"/>, handing it the state as it stands once the module is
    /// loaded, and then whatever changed while the attach was on its way. True when the script was refused, and the
    /// root must render again to say so.
    /// </summary>
    public async Task<bool> StartAsync(ResourceAssetCollection assets, ElementReference root, string function, Func<DrawingState> state)
    {
        var mounted = await AttachAsync(assets, root, function, () => Compose(state(), withDefinition: true));
        if (mounted == Mounted.Attached)
        {
            // A slot the chart mounted as it attached may have been drawn already, before there was a chart to hand it to.
            await ReportSlotsAsync();
            await SendAsync(state());
        }
        Refused = mounted == Mounted.Refused;
        return Refused;
    }

    /// <summary>Hands the browser the options if they changed since it last heard them, and the definition if the
    /// component holds another; nothing before the drawing is attached, or once the circuit has gone.</summary>
    public async Task SendAsync(DrawingState state)
    {
        if (_handle is not { } handle || Disposed)
            return;
        var moved = state.Generation != _sentGeneration;
        if (state.Options == _sentOptions && !moved)
            return;
        try
        {
            await handle.InvokeVoidAsync("update", Compose(state, moved));
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone, and the drawing with it.
        }
    }

    private string Compose(DrawingState state, bool withDefinition)
    {
        _sentOptions = state.Options;
        return ChartWire.Json(json =>
        {
            json.WriteStartObject();
            // Across a circuit, Blazor Server's, a call larger than its hub takes would close it: the browser holds one.
            json.WriteBoolean("circuit", !OperatingSystem.IsBrowser());
            json.WritePropertyName("options");
            json.WriteRawValue(state.Options);
            if (withDefinition)
            {
                json.WritePropertyName("definition");
                json.WriteRawValue(state.Definition);
                json.WriteNumber("generation", state.Generation);
                _sentGeneration = state.Generation;
            }
            json.WriteEndObject();
        });
    }

    /// <summary>
    /// Loads the script this build was made with, by an address that changes with its content (ADR-0123), and refuses
    /// it when the browser holds another version's; then calls <paramref name="function"/> on <paramref name="root"/>
    /// with what <paramref name="attach"/> writes, asked once the module is loaded, so it carries the parameters as
    /// they stand then.
    /// </summary>
    private async Task<Mounted> AttachAsync(ResourceAssetCollection assets, ElementReference root, string function, Func<string> attach)
    {
        var address = "./" + AddressOf(assets, ScriptPath, ScriptCoreAsset.Version);
        IJSObjectReference? module = null;
        string? stamp;
        try
        {
            module = await js.InvokeAsync<IJSObjectReference>("import", address);
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
        if (Disposed)
        {
            // Disposed while the module loaded: what DisposeAsync could not yet see is let go here.
            await ReleaseAsync(null, module);
            return Mounted.Gone;
        }
        if (module is null || stamp != ScriptCoreAsset.Stamp)
        {
            // Another version's module is let go at once: nothing of it is called.
            _module = null;
            await ReleaseAsync(null, module);
            return Mounted.Refused;
        }
        var json = attach();
        var bridge = DotNetObjectReference.Create(new ScriptBridge(this));
        _bridge = bridge;
        _attaching = true;
        IJSObjectReference handle;
        try
        {
            handle = await module.InvokeAsync<IJSObjectReference>(function, root, bridge, json);
        }
        catch (Exception) when (Disposed)
        {
            // Disposed while it attached, and the attach failed with it, as it does when the circuit closes: what
            // DisposeAsync left to the attach is let go here, and nobody is left to tell of the failure.
            await ReleaseAsync(null, module);
            bridge.Dispose();
            return Mounted.Gone;
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
        if (Disposed)
        {
            // Disposed while it attached: the browser's drawing is stopped before the object it calls is let go.
            await ReleaseAsync(handle, module);
            bridge.Dispose();
            return Mounted.Gone;
        }
        _handle = handle;
        Calls.Attached(handle);
        return Mounted.Attached;
    }

    /// <summary>The script mounted slot <paramref name="id"/> for the Chrome, with its context as data (ADR-0410).</summary>
    internal void MountSlot(long id, int version, string name, string context)
    {
        if (Disposed)
            return;
        Slots.Mount(id, version, name, context);
        slotsChanged?.Invoke();
    }

    /// <summary>The slot <paramref name="id"/> was mounted again in place, with another context.</summary>
    internal void UpdateSlot(long id, int version, string context)
    {
        if (Disposed || !Slots.Update(id, version, context))
            return;
        slotsChanged?.Invoke();
    }

    /// <summary>The chart took the slot <paramref name="id"/> away; the script has moved its contents back.</summary>
    internal void UnmountSlot(long id)
    {
        if (Disposed || !Slots.Unmount(id))
            return;
        slotsChanged?.Invoke();
    }

    /// <summary>Each slot drawn again is handed to the script, which moves it in, or wires it for the context it now
    /// holds; nothing before the drawing is attached, which hands over what was drawn meanwhile.</summary>
    public Task ReportSlotsAsync() => slots is null ? Task.CompletedTask : slots.ReportAsync(_handle, () => Disposed);

    // A drawing that takes no Chrome mounts no slot: a call to one is a defect of the bridge's.
    private RazorSlotHolders Slots => slots ?? throw new InvalidOperationException("Internal: the script mounted a Chrome slot on a drawing that takes no Chrome.");

    /// <summary>Asks the browser's drawing for a file or its text; <paramref name="none"/> before it is attached, once
    /// it is stopped, or when the circuit has gone.</summary>
    public async Task<T> AskAsync<T>(string method, T none, params object?[] args)
    {
        if (_handle is not { } handle || Disposed)
            return none;
        try
        {
            return await handle.InvokeAsync<T>(method, args);
        }
        catch (JSDisconnectedException)
        {
            return none;
        }
    }

    /// <summary>Tells the browser's drawing something that answers nothing; nothing before it is attached, once it is
    /// stopped, or when the circuit has gone.</summary>
    public async Task TellAsync(string method, params object?[] args)
    {
        if (_handle is not { } handle || Disposed)
            return;
        try
        {
            await handle.InvokeVoidAsync(method, args);
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone, and the drawing with it.
        }
    }

    /// <summary>Asks the browser's drawing for a text, which crosses held when it is too large for a message of its own
    /// (<see cref="HeldCalls"/>); null before it is attached, once it is stopped, or when the circuit has gone.</summary>
    public async Task<string?> AskTextAsync(string method)
    {
        if (_handle is not { } handle || Disposed)
            return null;
        try
        {
            var crossed = await handle.InvokeAsync<JsonElement>(method);
            if (HeldCalls.IsHeld(crossed))
                crossed = await HeldCalls.TakeAsync(handle, crossed);
            return crossed.ValueKind == JsonValueKind.Null ? null : crossed.GetString();
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
    }

    // The browser's events, handed to the Consumer one at a time in the order the browser raised them.
    internal async Task HeardAsync(string kind, JsonElement payload)
    {
        await invoke(async () =>
        {
            // Read in the component's turn, against the parameters as they stand when the event arrives.
            Func<Task>? hand;
            try
            {
                hand = prepare(kind, payload);
            }
            catch (Exception ex)
            {
                // An event .NET cannot read fails the component in its turn, as a handler's exception does.
                hand = () => Task.FromException(ex);
            }
            if (hand is null)
                return;
            _heard.Enqueue(hand);
            if (_hearing)
                return;
            _hearing = true;
            try
            {
                while (_heard.TryDequeue(out var next))
                {
                    if (Disposed)
                        continue;
                    try
                    {
                        await next();
                    }
                    catch (Exception ex)
                    {
                        await fail(ex);
                    }
                }
            }
            finally
            {
                _hearing = false;
            }
        });
    }

    /// <summary>Stops the browser's drawing, and lets go what .NET holds of the browser.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Disposed)
            return;
        Disposed = true;
        Calls.Stopped();
        // While it attaches, the drawing it makes is stopped once it is made, and the bridge let go after it.
        if (_attaching)
            return;
        await ReleaseAsync(_handle, _module);
        _bridge?.Dispose();
    }

    private static async Task ReleaseAsync(IJSObjectReference? handle, IJSObjectReference? module)
    {
        try
        {
            if (handle is not null)
            {
                await handle.InvokeVoidAsync("dispose");
                await handle.DisposeAsync();
            }
            if (module is not null)
                await module.DisposeAsync();
        }
        catch (Exception ex) when (ex is JSDisconnectedException or OperationCanceledException)
        {
            // The circuit is gone, or is closing and has called off what it had sent the browser, and the browser's
            // drawing goes with it: nothing is left there to stop.
        }
    }

    // The address a package's file is asked for at (ADR-0123), as ExGrid's PackageStylesheet gives it: the host's
    // fingerprinted address where it knows one, else the path with the version as `?v=`. Written again here rather than
    // taken from ExGrid's internals, which no package of the family reads (ADR-0201).
    private static string AddressOf(ResourceAssetCollection assets, string path, string version)
    {
        var fingerprinted = assets[path];
        return fingerprinted != path ? fingerprinted : $"{path}?v={version}";
    }
}
