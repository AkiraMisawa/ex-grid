using ExposureChart.Server.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(ExposureChart.Components.Routes).Assembly);
app.Run();
