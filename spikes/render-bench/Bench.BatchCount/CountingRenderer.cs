using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bench.BatchCount;

/// <summary>What one RenderBatch carried.</summary>
public sealed class BatchCounts
{
    public int Batches;
    public int Diffs;
    public int Edits;
    public readonly Dictionary<string, int> EditsByType = new(StringComparer.Ordinal);
    public int Frames;
    public readonly Dictionary<string, int> FramesByType = new(StringComparer.Ordinal);
    public int DisposedComponents;
    public int DisposedEventHandlers;
    /// <summary>UTF-8 bytes of the strings the batch would write, after the writer's own
    /// deduplication (attribute and element names, empty and whitespace strings).</summary>
    public int StringBytes;
    public int Strings;
    /// <summary>The batch's size under Blazor's RenderBatchWriter format, transcribed (see
    /// <see cref="CountingRenderer"/>): an estimate, not the writer's own output.</summary>
    public int EstimatedBytes;

    public void Add(string type, Dictionary<string, int> into)
        => into[type] = into.GetValueOrDefault(type) + 1;
}

/// <summary>
/// A Renderer with no DOM: it captures each RenderBatch Blazor's diff produces, which is what
/// Blazor Server hands its RenderBatchWriter and sends over the circuit, and what WebAssembly hands
/// the browser. With <see cref="Counting"/> off, it does nothing with the batch, for timing the
/// render alone.
///
/// <para>The estimate follows RenderBatchWriter's layout (dotnet/aspnetcore,
/// src/Components/Shared/src/RenderBatchWriter.cs): per component diff 8 bytes and 16 per edit, plus
/// a 4-byte offset; 20 bytes per reference frame; 4 per disposed component id and 8 per disposed
/// event handler id; each section's 4-byte count; each string once in a table of UTF-8 bytes with a
/// 7-bit length prefix and a 4-byte offset, deduplicated only for attribute and element names, the
/// empty or whitespace string, and a bool attribute's value; a 24-byte trailer of section offsets.
/// The writer is internal, so this is a transcription; the Server host measures the real bytes.</para>
/// </summary>
public sealed class CountingRenderer : Renderer
{
    public CountingRenderer() : this(new NoServices())
    {
    }

    public CountingRenderer(IServiceProvider services) : base(services, NullLoggerFactory.Instance)
    {
    }

    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

    /// <summary>Interactive, as on a circuit: ExGrid reads it.</summary>
    protected override RendererInfo RendererInfo { get; } = new("Server", isInteractive: true);

    public bool Counting { get; set; } = true;

    public BatchCounts Counts { get; private set; } = new();

    public BatchCounts Take()
    {
        var taken = Counts;
        Counts = new BatchCounts();
        return taken;
    }

    public int Attach(IComponent component) => AssignRootComponentId(component);

    /// <summary>A component made by the renderer's own factory, so that its [Inject] properties
    /// are filled.</summary>
    public TComponent Create<TComponent>() where TComponent : IComponent => (TComponent)InstantiateComponent(typeof(TComponent));

    public Task RenderRootAsync(int componentId, ParameterView parameters) => RenderRootComponentAsync(componentId, parameters);

    protected override void HandleException(Exception exception) => throw exception;

    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
    {
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
            c.Strings++;
            c.StringBytes += length;
            bytes += SevenBitLength(length) + length + 4;
        }

        // Updated components.
        var diffs = renderBatch.UpdatedComponents;
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
                c.Add(edit.Type.ToString(), c.EditsByType);
                bytes += 16;
                WriteString(edit.RemovedAttributeName, deduplicate: true);
            }
        }

        // Reference frames.
        var frames = renderBatch.ReferenceFrames;
        bytes += 4;
        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames.Array[i];
            c.Frames++;
            c.Add(frame.FrameType.ToString(), c.FramesByType);
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
        c.DisposedEventHandlers += renderBatch.DisposedEventHandlerIDs.Count;
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

    private sealed class NoServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
