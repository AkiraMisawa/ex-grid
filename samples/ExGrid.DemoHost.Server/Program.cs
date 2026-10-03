using System.Net;
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
if (Environment.GetEnvironmentVariable("EXGRID_HOST_LOG") is { Length: > 0 } hostLog)
    builder.Logging.AddProvider(new FileLoggerProvider(hostLog));

var app = builder.Build();

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Routes).Assembly);

app.Run();
