using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace Costs;

/// <summary>What the render batches of one update carried, and what they rendered.</summary>
public sealed class BatchCounts
{
    public int Batches;
    public int Diffs;
    public int Edits;
    public int Frames;
    public int DisposedComponents;
    public int StringBytes;
    /// <summary>The batches' size under Blazor's RenderBatchWriter format, transcribed (M2's
    /// estimate, spikes/render-bench/Bench.BatchCount/CountingRenderer.cs): not the writer's own
    /// output.</summary>
    public int EstimatedBytes;
    /// <summary>Diffs of components whose type the renderer was asked to watch (a row).</summary>
    public int WatchedRendered;
    /// <summary>Of those, components the renderer had not seen render before.</summary>
    public int WatchedMounted;
}

/// <summary>
/// A Renderer with no DOM, M2's (spikes/render-bench/Bench.BatchCount), with two additions: the
/// time spent in <see cref="ProcessPendingRender"/> — every component's render and diff, and the
/// parameters a parent sets on its children on the way — and the components of one watched type
/// that render, so the rows a live update renders and mounts can be counted.
/// </summary>
public sealed class CountingRenderer : Renderer
{
    private readonly HashSet<int> _seen = [];
    private readonly Dictionary<int, IComponent> _live = [];

    public CountingRenderer(IServiceProvider services) : base(services, NullLoggerFactory.Instance)
    {
    }

    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

    /// <summary>Interactive, as on a circuit: ExGrid reads it.</summary>
    protected override RendererInfo RendererInfo { get; } = new("Server", isInteractive: true);

    public bool Counting { get; set; }

    /// <summary>The component type whose renders are counted (an open generic is matched by its
    /// definition).</summary>
    public Type? Watched { get; set; }

    public BatchCounts Counts { get; private set; } = new();

    /// <summary>Ticks spent in <see cref="ProcessPendingRender"/> since the last <see cref="Take"/>.</summary>
    public long RenderTicks { get; private set; }

    public (BatchCounts Counts, double RenderMs) Take()
    {
        var taken = (Counts, RenderTicks * 1000.0 / Stopwatch.Frequency);
        Counts = new BatchCounts();
        RenderTicks = 0;
        return taken;
    }

    /// <summary>The watched components alive now: those rendered and not disposed since.</summary>
    public IEnumerable<IComponent> LiveWatched => _live.Values;

    public int Attach(IComponent component) => AssignRootComponentId(component);

    /// <summary>A component made by the renderer's own factory, so that its [Inject] properties
    /// are filled.</summary>
    public TComponent Create<TComponent>() where TComponent : IComponent => (TComponent)InstantiateComponent(typeof(TComponent));

    public Task RenderRootAsync(int componentId, ParameterView parameters) => RenderRootComponentAsync(componentId, parameters);

    public void RemoveRoot(int componentId) => RemoveRootComponent(componentId);

    protected override void HandleException(Exception exception) => throw exception;

    protected override void ProcessPendingRender()
    {
        var t0 = Stopwatch.GetTimestamp();
        base.ProcessPendingRender();
        RenderTicks += Stopwatch.GetTimestamp() - t0;
    }

    private bool IsWatched(IComponent component)
    {
        if (Watched is null)
            return false;
        var type = component.GetType();
        return type == Watched || (Watched.IsGenericTypeDefinition && type.IsGenericType && type.GetGenericTypeDefinition() == Watched);
    }

    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
    {
        var diffs = renderBatch.UpdatedComponents;
        if (Watched is not null)
        {
            for (var i = 0; i < diffs.Count; i++)
            {
                var id = diffs.Array[i].ComponentId;
                var component = GetComponentState(id).Component;
                if (!IsWatched(component))
                    continue;
                Counts.WatchedRendered++;
                if (_seen.Add(id))
                    Counts.WatchedMounted++;
                _live[id] = component;
            }
            for (var i = 0; i < renderBatch.DisposedComponentIDs.Count; i++)
                _live.Remove(renderBatch.DisposedComponentIDs.Array[i]);
        }
        if (!Counting)
            return Task.CompletedTask;

        var c = Counts;
        c.Batches++;
        var strings = new HashSet<string>(StringComparer.Ordinal);
        var bytes = 0;

        void WriteString(string? value, bool deduplicate)
        {
            if (value is null)
                return;
            if (deduplicate && !strings.Add(value))
                return;
            var length = Encoding.UTF8.GetByteCount(value);
            c.StringBytes += length;
            bytes += SevenBitLength(length) + length + 4;
        }

        bytes += 4;
        for (var i = 0; i < diffs.Count; i++)
        {
            var diff = diffs.Array[i];
            c.Diffs++;
            bytes += 4 + 8;
            var edits = diff.Edits;
            for (var e = 0; e < edits.Count; e++)
            {
                var edit = edits.Array[edits.Offset + e];
                c.Edits++;
                bytes += 16;
                WriteString(edit.RemovedAttributeName, deduplicate: true);
            }
        }

        var frames = renderBatch.ReferenceFrames;
        bytes += 4;
        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames.Array[i];
            c.Frames++;
            bytes += 20;
            switch (frame.FrameType)
            {
                case RenderTreeFrameType.Attribute:
                    WriteString(frame.AttributeName, deduplicate: true);
                    if (frame.AttributeValue is bool flag)
                        WriteString(flag ? string.Empty : null, deduplicate: true);
                    else
                    {
                        var text = frame.AttributeValue as string;
                        WriteString(text, deduplicate: string.IsNullOrEmpty(text));
                    }
                    break;
                case RenderTreeFrameType.Element:
                    WriteString(frame.ElementName, deduplicate: true);
                    break;
                case RenderTreeFrameType.Text:
                    WriteString(frame.TextContent, deduplicate: string.IsNullOrWhiteSpace(frame.TextContent));
                    break;
                case RenderTreeFrameType.Markup:
                    WriteString(frame.MarkupContent, deduplicate: false);
                    break;
                case RenderTreeFrameType.ElementReferenceCapture:
                    WriteString(frame.ElementReferenceCaptureId, deduplicate: false);
                    break;
            }
        }

        c.DisposedComponents += renderBatch.DisposedComponentIDs.Count;
        bytes += 4 + 4 * renderBatch.DisposedComponentIDs.Count;
        bytes += 4 + 8 * renderBatch.DisposedEventHandlerIDs.Count;
        bytes += 4; // named event changes: none here
        bytes += 24; // the trailer of section offsets
        c.EstimatedBytes += bytes;
        return Task.CompletedTask;
    }

    private static int SevenBitLength(int value)
    {
        var length = 1;
        while (value >= 0x80)
        {
            value >>= 7;
            length++;
        }
        return length;
    }
}

/// <summary>The services ExGrid and ExPivot ask for: a JavaScript runtime that answers every call
/// with a default (every module or handle is itself again), and a clock.</summary>
public sealed class Services(TimeProvider clock) : IServiceProvider
{
    private readonly FakeJS _js = new();

    public object? GetService(Type serviceType)
        => serviceType == typeof(IJSRuntime) ? _js
         : serviceType == typeof(TimeProvider) ? clock
         : serviceType == typeof(IServiceProvider) ? this
         : null;

    private sealed class FakeJS : IJSRuntime, IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(Make<TValue>());

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(Make<TValue>());

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private TValue Make<TValue>() => typeof(TValue) == typeof(IJSObjectReference) ? (TValue)(object)this : default!;
    }
}
