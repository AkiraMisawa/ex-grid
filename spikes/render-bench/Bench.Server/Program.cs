// Blazor Server host for the live-tick grid (M2), on 127.0.0.1:5199 (BENCH_PORT to change it).
// BENCH_NO_WS_COMPRESSION=1 turns the WebSocket's per-message compression off, so that what Chrome
// reports as a frame's payload is also what crosses the wire. GET /api/wire says how many bytes
// Kestrel has written to each live connection (WireCounters).
using Bench.Server;

var builder = WebApplication.CreateBuilder(args);
var port = int.Parse(Environment.GetEnvironmentVariable("BENCH_PORT") ?? "5199");
// Run from the build output in any environment: serve blazor.web.js from the static web assets.
builder.WebHost.UseStaticWebAssets();
builder.WebHost.ConfigureKestrel(kestrel => kestrel.ListenLocalhost(port, listen => listen.Use(WireCounters.Middleware)));
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapGet("/api/wire", () => WireCounters.Snapshot());
var noCompression = Environment.GetEnvironmentVariable("BENCH_NO_WS_COMPRESSION") == "1";
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode(options => options.DisableWebSocketCompression = noCompression);
Console.WriteLine($"[bench.server] http://localhost:{port}, WebSocket compression {(noCompression ? "off" : "on (the default)")}");
app.Run();
