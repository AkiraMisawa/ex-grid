using System.Text.Json;
using System.Text.Json.Serialization;
using ExGrid.DemoApi;

// The demo API server (ADR-0068): the trades in a SQLite file the server generates and owns, a
// SignalR hub that says when they change, and CORS for the demo hosts' pages. The pages find it at
// their own port plus 3000; it listens wherever --urls says (8299 in launchSettings, beside the
// WebAssembly DemoHost's 5299). EXGRID_DEMO_TRADES sets how many trades, EXGRID_DEMO_DATA where
// they live (DemoApiOptions).
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(DemoApiOptions.From(builder.Configuration));
builder.Services.AddSingleton<TradeStore>();
builder.Services.AddHostedService<TradeStoreStartup>();
builder.Services.AddSingleton<LiveUpdater>();
builder.Services.AddHostedService(services => services.GetRequiredService<LiveUpdater>());
builder.Services.AddHostedService<TradesHubBroadcaster>();
builder.Services.AddSignalR();
builder.Services.AddCors(DemoCors.AddPolicies);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
// A malformed question is the caller's mistake: a 400 with a problem body. In Development the
// default throws instead, and the exception page logs the caller's mistake as the server's failure.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseStatusCodePages();
app.UseCors();
app.MapStatusEndpoints();
app.MapTradeEndpoints();
app.MapLiveEndpoints();
app.MapPivotEndpoints();
app.MapSnapshotEndpoints();
app.MapHub<TradesHub>(TradesHub.Path).RequireCors(DemoCors.HubPolicy);

app.Run();
