using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace ExGrid.DemoPages;

/// <summary>
/// The demo API server as the pages reach it (ADR-0068): its address, and an
/// <see cref="HttpClient"/> for it. The server is a process of its own, which the pages on both
/// hosts call over HTTP with the same code. It is found at the page's own port plus 3000 — 8299
/// beside the WebAssembly host's 5299 — unless the host names it, so two checkouts on two ports
/// never share a server, as they never share a host.
/// <para>
/// One per user: per tab on WebAssembly, and per circuit on the Server host, because the address
/// is read from the user's own page. On WebAssembly the requests go out through the browser's
/// fetch, which asks for HTTP's compression and undoes it natively. On the Server host the host
/// hands in one handler that every circuit shares, set to do the same
/// (<see cref="DemoApiServices.AddDemoApi"/>).
/// </para>
/// </summary>
public sealed class DemoApiClient : IDisposable
{
    /// <summary>How far above the page's own port the server listens unless the host names it.</summary>
    public const int PortOffset = 3000;

    /// <summary>The configuration key a host reads the server's address from, when it is not the
    /// page's port plus 3000.</summary>
    public const string AddressKey = "DemoApi:Address";

    /// <summary>Where the server's SignalR hub is mapped, under its address.</summary>
    public const string HubPath = "hubs/trades";

    /// <summary>Finds the server for the page <paramref name="navigation"/> is on.</summary>
    /// <param name="navigation">The user's page, whose port the server's is found from.</param>
    /// <param name="options">What the host registered: the server's address, when it named one,
    /// and the handler its requests go out through.</param>
    public DemoApiClient(NavigationManager navigation, DemoApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(options);
        var page = new Uri(navigation.BaseUri);
        // http://localhost:5299/ → http://localhost:8299/.
        Address = options.Address ?? new UriBuilder(page.Scheme, page.Host, page.Port + PortOffset).Uri;
        Http = options.Handler is { } handler
            ? new HttpClient(handler, disposeHandler: false) { BaseAddress = Address }
            : new HttpClient { BaseAddress = Address };
    }

    /// <summary>The server's address, ending in a slash: what the pages' relative requests go to.</summary>
    public Uri Address { get; }

    /// <summary>The client every request of this user's pages goes out through, its
    /// <see cref="HttpClient.BaseAddress"/> the server's.</summary>
    public HttpClient Http { get; }

    /// <summary>The server's SignalR hub, which says when the trades change (ADR-0066/0067).</summary>
    public Uri Hub => new(Address, HubPath);

    /// <summary>What a page shows when a request to the server fails: where the server was looked
    /// for, and what to do about it — start it there, or wait for it to finish generating its trades.
    /// Shown in place of what the page would have read, never beside an answer that looks
    /// complete.</summary>
    /// <param name="error">What the request failed with.</param>
    public string NotAnswering(HttpRequestException error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.StatusCode is { } status
            ? $"The demo API server at {Address} answered {(int)status} ({status}). At its first start it "
                + "generates its trades and answers 503 until they are ready (GET /api/status says how far it has "
                + "got); reload the page once they are."
            : $"The demo API server did not answer at {Address} ({error.Message}). It is started with "
                + $"\"dotnet run --project samples/ExGrid.DemoApi --urls {Address.GetLeftPart(UriPartial.Authority)}\" "
                + "beside the host, as layer 3 does (ADR-0068).";
    }

    /// <inheritdoc />
    public void Dispose() => Http.Dispose();
}

/// <summary>What a host registers for <see cref="DemoApiClient"/>.</summary>
/// <param name="Address">The server's address, or null for the page's own port plus 3000.</param>
/// <param name="Handler">The handler every client's requests go out through, shared and never
/// disposed by a client; or null for <see cref="HttpClient"/>'s own, which in a browser is fetch.</param>
public sealed record DemoApiOptions(Uri? Address, HttpMessageHandler? Handler);

/// <summary>Registers <see cref="DemoApiClient"/> in a host.</summary>
public static class DemoApiServices
{
    /// <summary>
    /// Registers one <see cref="DemoApiClient"/> per user (scoped). <paramref name="address"/> names
    /// the server when it is not at the page's port plus 3000; <paramref name="handler"/> is the one
    /// the Server host's circuits share, and WebAssembly passes none, so the browser's fetch carries
    /// the requests.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="address">The server's absolute address, or null or empty for the page's port
    /// plus 3000.</param>
    /// <param name="handler">The handler the requests go out through, or null for the default.</param>
    /// <returns><paramref name="services"/>.</returns>
    public static IServiceCollection AddDemoApi(this IServiceCollection services, string? address = null, HttpMessageHandler? handler = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        Uri? named = null;
        if (!string.IsNullOrWhiteSpace(address))
        {
            // A relative request resolves against the base address only up to its last slash.
            named = new Uri(address.EndsWith('/') ? address : address + "/", UriKind.Absolute);
        }
        services.AddSingleton(new DemoApiOptions(named, handler));
        services.AddScoped<DemoApiClient>();
        return services;
    }
}
