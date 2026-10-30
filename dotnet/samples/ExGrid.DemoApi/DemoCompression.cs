using System.IO.Compression;
using ExGrid.Data.Arrow;
using Microsoft.AspNetCore.ResponseCompression;

namespace ExGrid.DemoApi;

/// <summary>
/// HTTP's own compression, which ADR-0065 leaves an Arrow stream to: the stream is written
/// uncompressed, and the server compresses it as it goes out — and the JSON answers too — at the
/// fastest level, since a body is compressed again for every request. ADR-0065 measured gzip at
/// level 6 at 1.5 s for a million trades and Brotli at level 11 at minutes; the fastest levels
/// were measured here, and both are offered, Brotli first. A browser undoes either natively.
/// </summary>
internal static class DemoCompression
{
    /// <summary>Adds response compression for JSON and Arrow streams, gzip and Brotli at their
    /// fastest levels.</summary>
    public static IServiceCollection AddDemoCompression(this IServiceCollection services)
    {
        services.AddResponseCompression(options =>
        {
            // Off by default over HTTPS, for fear of BREACH: an attacker's text compressed beside a
            // secret. The demo serves no secret, and a copy of it run over HTTPS should not send
            // its trades five times larger.
            options.EnableForHttps = true;
            options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Append(SnapshotArrow.StreamMediaType);
            // When a browser accepts both, the first listed is used.
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });
        services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
        return services;
    }
}
