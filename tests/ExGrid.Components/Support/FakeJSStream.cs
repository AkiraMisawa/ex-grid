using System.Text;
using Microsoft.JSInterop;

namespace ExGrid.Components.Tests.Support;

/// <summary>
/// Stands in for the reference <c>DotNet.createJSStreamReference</c> hands .NET: the
/// length is known up front, the bytes only once the stream is opened. Records whether
/// it was opened, so a test can say that a refused paste read nothing (CP-22).
/// </summary>
internal sealed class FakeJSStream(byte[] bytes, Exception? readFails = null) : IJSStreamReference
{
    public static FakeJSStream Of(string text) => new(Encoding.UTF8.GetBytes(text));

    /// <summary>A stream of <paramref name="length"/> bytes whose read throws <paramref name="failure"/>,
    /// as a Server circuit's <c>RemoteJSDataStream</c> does when no chunk arrives in time.</summary>
    public static FakeJSStream Failing(long length, Exception failure) => new(new byte[length], failure);

    public static FakeJSStream OfLength(long length) => new(new byte[length]);

    public bool Opened { get; private set; }

    public bool Disposed { get; private set; }

    public long Length => bytes.LongLength;

    public ValueTask<Stream> OpenReadStreamAsync(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
    {
        if (Length > maxAllowedSize)
            throw new ArgumentOutOfRangeException(nameof(maxAllowedSize), "the stream is longer than allowed");
        Opened = true;
        return ValueTask.FromResult<Stream>(readFails is null ? new MemoryStream(bytes, writable: false) : new FailingStream(readFails));
    }

    private sealed class FailingStream(Exception failure) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw failure;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(failure);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Task.FromException<int>(failure);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        throw new NotSupportedException();

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
        throw new NotSupportedException();
}
