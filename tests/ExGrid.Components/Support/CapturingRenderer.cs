using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

// The render batch is what ADR-0140's claim is about, and its types are the ones BL0006 keeps to
// the framework: read here, never shipped.
#pragma warning disable BL0006

namespace ExGrid.Components.Tests.Support;

/// <summary>One edit of a captured render batch, with what its reference frame was.</summary>
internal sealed record CapturedEdit(
    RenderTreeEditType Type, RenderTreeFrameType? FrameType, string? ElementName, Type? ComponentType, string? AttributeName);

/// <summary>One component's diff in a captured render batch.</summary>
internal sealed record CapturedDiff(int ComponentId, IReadOnlyList<CapturedEdit> Edits);

/// <summary>What one render batch carried: each component's diff, and the components it disposed.</summary>
internal sealed record CapturedBatch(IReadOnlyList<CapturedDiff> Diffs, IReadOnlyList<int> DisposedComponents);

/// <summary>
/// A renderer with no DOM that keeps a copy of every render batch Blazor's diff produces: what
/// Blazor Server sends over the circuit and what WebAssembly hands the browser. bUnit keeps no
/// batch a test can read, and ADR-0140's claim is about the batch — a changed row under its Row
/// Key is edited in place, not inserted again (LV-1). The grid's JavaScript is stood in for by a
/// runtime that answers every call with a default and every module or handle with itself, as
/// <c>spikes/render-bench</c>'s batch counter does.
/// </summary>
internal sealed class CapturingRenderer(TimeProvider clock) : Renderer(new Services(clock), NullLoggerFactory.Instance)
{
    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

    /// <summary>Interactive, as on a circuit: the grid reads it.</summary>
    protected override RendererInfo RendererInfo { get; } = new("Server", isInteractive: true);

    /// <summary>Every batch since the last <see cref="Take"/>.</summary>
    public List<CapturedBatch> Batches { get; } = [];

    /// <summary>The batches captured so far, and a fresh start.</summary>
    public CapturedBatch[] Take()
    {
        var taken = Batches.ToArray();
        Batches.Clear();
        return taken;
    }

    /// <summary>A component made by the renderer's own factory, so its [Inject] properties are filled.</summary>
    public TComponent Create<TComponent>() where TComponent : IComponent => (TComponent)InstantiateComponent(typeof(TComponent));

    public int Attach(IComponent component) => AssignRootComponentId(component);

    /// <summary>Sets the root's parameters and renders, then lets whatever the grid renders again
    /// on its own continuations run.</summary>
    public async Task RenderRootAsync(int componentId, IDictionary<string, object?> parameters)
    {
        await Dispatcher.InvokeAsync(() => RenderRootComponentAsync(componentId, ParameterView.FromDictionary(parameters)));
        for (var settle = 0; settle < 5; settle++)
            await Dispatcher.InvokeAsync(() => Task.Yield());
    }

    /// <summary>The child components a component's current render tree holds, with their ids, in
    /// order.</summary>
    public IReadOnlyList<(int Id, IComponent Component)> ChildComponents(int componentId)
    {
        var frames = GetCurrentRenderTreeFrames(componentId);
        var children = new List<(int, IComponent)>();
        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames.Array[i];
            if (frame.FrameType == RenderTreeFrameType.Component)
                children.Add((frame.ComponentId, frame.Component));
        }
        return children;
    }

    protected override void HandleException(Exception exception) => ExceptionDispatchInfo.Throw(exception);

    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
    {
        // Copied out: the batch's arrays are the renderer's own, and are reused once this returns.
        var references = renderBatch.ReferenceFrames;
        var diffs = new List<CapturedDiff>(renderBatch.UpdatedComponents.Count);
        for (var d = 0; d < renderBatch.UpdatedComponents.Count; d++)
        {
            var diff = renderBatch.UpdatedComponents.Array[d];
            var edits = new List<CapturedEdit>(diff.Edits.Count);
            for (var e = 0; e < diff.Edits.Count; e++)
            {
                var edit = diff.Edits.Array[diff.Edits.Offset + e];
                RenderTreeFrame? frame = edit.Type is RenderTreeEditType.PrependFrame or RenderTreeEditType.UpdateText
                    or RenderTreeEditType.UpdateMarkup or RenderTreeEditType.SetAttribute
                    ? references.Array[edit.ReferenceFrameIndex]
                    : null;
                edits.Add(new CapturedEdit(
                    edit.Type,
                    frame?.FrameType,
                    frame is { FrameType: RenderTreeFrameType.Element } element ? element.ElementName : null,
                    frame is { FrameType: RenderTreeFrameType.Component } component ? component.ComponentType : null,
                    frame is { FrameType: RenderTreeFrameType.Attribute } attribute ? attribute.AttributeName : edit.RemovedAttributeName));
            }
            diffs.Add(new CapturedDiff(diff.ComponentId, edits));
        }
        var disposed = new int[renderBatch.DisposedComponentIDs.Count];
        for (var i = 0; i < disposed.Length; i++)
            disposed[i] = renderBatch.DisposedComponentIDs.Array[i];
        Batches.Add(new CapturedBatch(diffs, disposed));
        return Task.CompletedTask;
    }

    private sealed class Services(TimeProvider clock) : IServiceProvider
    {
        private readonly FakeJS _js = new();

        public object? GetService(Type serviceType)
            => serviceType == typeof(IJSRuntime) ? _js
             : serviceType == typeof(TimeProvider) ? clock
             : serviceType == typeof(IServiceProvider) ? this
             : null;
    }

    /// <summary>Every call answered with a default; a module or a handle is this object again.</summary>
    private sealed class FakeJS : IJSRuntime, IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(Make<TValue>());

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromResult(Make<TValue>());

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private TValue Make<TValue>() => typeof(TValue) == typeof(IJSObjectReference) ? (TValue)(object)this : default!;
    }
}
