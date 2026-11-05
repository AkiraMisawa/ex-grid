using System.Net;
using Microsoft.AspNetCore.Http.Features;
using ExGrid.DemoHost.Server;
using ExGrid.DemoHost.Server.Components;
using ExGrid.DemoPages;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
// MudBlazor's services, for the /mud pages (ADR-0030).
builder.Services.AddMudServices();
// One store for the whole process — every user's circuit reads it — with a Grid Source
// per circuit on top (ADR-0018 §5). The /shared page is its fixture.
builder.Services.AddSingleton<SharedTradeStore>();
builder.Services.AddSingleton<InspectorTradeStore>();
// The demo API server the database and live pages call (ADR-0069): at the page's port plus 3000
// unless DemoApi:Address names it. The circuits share one handler, which asks for HTTP's
// compression and undoes it, as ExGrid.Data.Arrow's README says a server's client should.
builder.Services.AddDemoApi(builder.Configuration[DemoApiClient.AddressKey],
    new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.All });

// CON-6 reads this host's log: a circuit's unhandled exception is written here, not to
// the browser console as it is on WebAssembly. Layer 3 names the file.
var hostLog = Environment.GetEnvironmentVariable("EXGRID_HOST_LOG") is { Length: > 0 } named ? named : null;
if (hostLog is not null)
{
    builder.Logging.AddProvider(new FileLoggerProvider(hostLog));
}

var app = builder.Build();

if (hostLog is not null)
{
    // A page that goes away tells the host with a beacon, and the host disposes its circuit. A beacon cut before the
    // host read it leaves the circuit standing for its retention period, and a call to the browser in flight then is
    // called off by the circuit's timeout a minute later, as an unhandled exception in whatever test runs then (CON-6).
    // Under layer 3 a cut was the latency proxy's (latency-proxy.mjs, the Hunt's widened run of 2026-11-04), and then the
    // harness's, closing a browser context with a page's beacon still on its way (the Hunt of 2026-11-05; leaveCircuits in
    // fixtures.mjs now sends each page's beacon and waits here until it is read). So it is said as an error here, in the
    // test whose page went away.
    var disconnects = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ExGrid.DemoHost.Server.Disconnect");
    var read = new DisconnectsRead();
    app.Use(async (context, next) =>
    {
        var beacon = context.Request.Path == "/_blazor/disconnect";
        try
        {
            await next(context);
        }
        finally
        {
            // Said whether the disconnect's reading of the form returned or threw on the cut.
            if (beacon && context.RequestAborted.IsCancellationRequested)
            {
                disconnects.LogError("layer 3: a page's disconnect was cut before the host read it, so its circuit stays standing (leaveCircuits in fixtures.mjs, latency-proxy.mjs)");
            }
            // The form the disconnect read, kept by the request: a beacon the harness sent carries its token.
            else if (beacon && context.Features.Get<IFormFeature>()?.Form is { } form
                && form.TryGetValue(DisconnectsRead.TokenField, out var token) && token.ToString() is { Length: > 0 } named)
            {
                read.Add(named);
            }
        }
    });
    // What leaveCircuits waits on before it closes a page or a browser context (DisconnectsRead): 200 once the beacon
    // carrying the token has been read, or 504 when it has not been within the time given. Asked on the host's own port,
    // past the latency proxy.
    app.MapGet("/_layer3/disconnects", async (string? token, int? within, CancellationToken cancellation) =>
    {
        if (token is not { Length: > 0 } wanted || within is not { } milliseconds || milliseconds < 0)
            return Results.BadRequest("GET /_layer3/disconnects?token=<token>&within=<milliseconds>");
        return await read.Read(wanted, TimeSpan.FromMilliseconds(milliseconds), cancellation)
            ? Results.Text("read")
            : Results.Text($"the disconnect carrying {wanted} was not read within {milliseconds} ms", statusCode: 504);
    });
}

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Routes).Assembly);

app.Run();
