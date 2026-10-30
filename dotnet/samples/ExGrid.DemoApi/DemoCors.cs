using Microsoft.AspNetCore.Cors.Infrastructure;

namespace ExGrid.DemoApi;

/// <summary>
/// Which pages may call the server from a browser (ADR-0069): any page this machine serves —
/// <c>http</c> or <c>https</c>, on <c>localhost</c>, <c>127.0.0.1</c> or <c>[::1]</c>, at any port —
/// because the demo hosts run on whatever ports a checkout or a test run gives them. Each allowed
/// origin is answered with itself, never with <c>*</c>.
/// <para>
/// Two policies, which differ only in credentials. The API allows none: nothing on it reads a
/// cookie or a sign-in, and a page's <c>fetch</c> sends none to another origin unless asked to.
/// The hub allows them (<see cref="HubPolicy"/>), because SignalR's JavaScript client sends its
/// negotiate request with credentials unless the page turns <c>withCredentials</c> off, and a
/// browser discards the answer to such a request unless it carries
/// <c>Access-Control-Allow-Credentials: true</c> beside the page's own origin. Allowing them costs
/// nothing here — the server has no cookies and no sign-in — and it lets a page connect with the
/// client's defaults, over any transport, rather than only by skipping negotiation for WebSockets.
/// </para>
/// </summary>
internal static class DemoCors
{
    /// <summary>The hub's policy: this machine's pages, with credentials allowed.</summary>
    public const string HubPolicy = "hub";

    /// <summary>The default policy, for the API, and the hub's. The API's lets a page read the
    /// Source Version header beside an Arrow stream (<see cref="SnapshotEndpoints.VersionHeader"/>):
    /// a browser hides from a page on another origin every response header not exposed by name.</summary>
    public static void AddPolicies(CorsOptions options)
    {
        options.AddDefaultPolicy(policy => policy
            .SetIsOriginAllowed(IsLocalPage)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders(SnapshotEndpoints.VersionHeader));
        options.AddPolicy(HubPolicy, policy => policy
            .SetIsOriginAllowed(IsLocalPage)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
    }

    /// <summary>Whether an origin is a page this machine serves.</summary>
    public static bool IsLocalPage(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && uri.IsLoopback;
}
