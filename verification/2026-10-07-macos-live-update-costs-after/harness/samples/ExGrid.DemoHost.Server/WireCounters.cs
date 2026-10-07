using System.Collections.Concurrent;
using System.IO.Pipelines;
using Microsoft.AspNetCore.Connections;

namespace ExGrid.DemoHost.Server;

/// <summary>Harness (ticket 01), copied from spikes/render-bench/Bench.Server (M2):
/// The bytes Kestrel writes to each TCP connection's transport: after the WebSocket's framing and
/// its per-message compression, when that is on, so what crosses the wire (there is no TLS on this
/// host). Read by the driver through <c>GET /api/wire</c> before and after each tick.
/// </summary>
public static class WireCounters
{
    private static readonly ConcurrentDictionary<string, Counter> Live = new(StringComparer.Ordinal);

    public sealed class Counter
    {
        private long _written;

        public long Written => Interlocked.Read(ref _written);

        public void Add(int bytes) => Interlocked.Add(ref _written, bytes);
    }

    public static object Snapshot() => Live.Select(c => new { id = c.Key, written = c.Value.Written }).ToArray();

    /// <summary>Kestrel connection middleware that counts what each connection writes.</summary>
    public static Func<ConnectionDelegate, ConnectionDelegate> Middleware => next => async context =>
    {
        var counter = new Counter();
        Live[context.ConnectionId] = counter;
        var transport = context.Transport;
        context.Transport = new CountingPipe(transport, counter);
        try
        {
            await next(context);
        }
        finally
        {
            context.Transport = transport;
            Live.TryRemove(context.ConnectionId, out _);
        }
    };

    private sealed class CountingPipe(IDuplexPipe inner, Counter counter) : IDuplexPipe
    {
        public PipeReader Input { get; } = inner.Input;

        public PipeWriter Output { get; } = new CountingWriter(inner.Output, counter);
    }

    private sealed class CountingWriter(PipeWriter inner, Counter counter) : PipeWriter
    {
        public override void Advance(int bytes)
        {
            counter.Add(bytes);
            inner.Advance(bytes);
        }

        public override Memory<byte> GetMemory(int sizeHint = 0) => inner.GetMemory(sizeHint);

        public override Span<byte> GetSpan(int sizeHint = 0) => inner.GetSpan(sizeHint);

        public override void CancelPendingFlush() => inner.CancelPendingFlush();

        public override void Complete(Exception? exception = null) => inner.Complete(exception);

        public override ValueTask CompleteAsync(Exception? exception = null) => inner.CompleteAsync(exception);

        public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default) => inner.FlushAsync(cancellationToken);

        public override bool CanGetUnflushedBytes => inner.CanGetUnflushedBytes;

        public override long UnflushedBytes => inner.UnflushedBytes;
    }
}
