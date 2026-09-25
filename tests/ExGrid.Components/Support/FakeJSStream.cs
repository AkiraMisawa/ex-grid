using System.Text;
using Microsoft.JSInterop;

namespace ExGrid.Components.Tests.Support;

/// <summary>
/// Stands in for the reference <c>DotNet.createJSStreamReference</c> hands .NET: the
/// length is known up front, the bytes only once the stream is opened. Records whether
/// it was opened, so a test can say that a refused paste read nothing (CP-22).
/// </summary>
internal sealed class FakeJSStream(byte[] bytes) : IJSStreamReference
{
    public static FakeJSStream Of(string text) => new(Encoding.UTF8.GetBytes(text));

    public static FakeJSStream OfLength(long length) => new(new byte[length]);

    public bool Opened { get; private set; }

    public bool Disposed { get; private set; }

    public long Length => bytes.LongLength;

    public ValueTask<Stream> OpenReadStreamAsync(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
    {
        if (Length > maxAllowedSize)
            throw new ArgumentOutOfRangeException(nameof(maxAllowedSize), "the stream is longer than allowed");
        Opened = true;
        return ValueTask.FromResult<Stream>(new MemoryStream(bytes, writable: false));
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
