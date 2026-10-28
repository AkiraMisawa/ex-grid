using System.Text.Json;
using global::ExGrid.Chrome;
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
internal sealed class ScriptMount(
    IJSRuntime js, Func<Func<Task>, Task> invoke, Func<Exception, Task> fail, Func<string, JsonElement, Func<Task>?> prepare)
{
    /// <summary>The script's address under the application's base: ExChart's minified Script Core and bridge. Not
    /// <c>ex-chart.min.js</c>, which is the Razor component's pointer script.</summary>
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

    /// <summary>Whether the component was disposed: nothing more crosses.</summary>
    public bool Disposed { get; private set; }

    /// <summary>Whether the script was refused as another version's: the root says so, and nothing is drawn.</summary>
    public bool Refused { get; private set; }

    /// <summary>
    /// Renders the root: an element the Script Core draws in, or, once the script was refused, the notice in the
    /// built-in words, as the Razor component says it (ADR-0123).
    /// </summary>
    public void Render(RenderTreeBuilder builder, Action<ElementReference> capture)
    {
        builder.OpenElement(0, "div");
        if (Refused)
            builder.AddAttribute(1, "class", "ex-chart");
        builder.AddElementReferenceCapture(2, capture);
        if (Refused)
        {
            builder.OpenElement(3, "p");
            builder.AddAttribute(4, "class", "ex-chart-script-notice");
            builder.AddAttribute(5, "role", "alert");
            builder.AddContent(6, BuiltInCommandLabels.For(ReloadNoticeLabelIds.Script));
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
            await SendAsync(state());
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
        return Mounted.Attached;
    }

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
        catch (JSDisconnectedException)
        {
            // The circuit is gone, and the browser's drawing with it: nothing is left there to stop.
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
