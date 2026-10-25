using ExGrid.Components;
using ExGrid.Docs;
using ExGrid.MudBlazor;
using ExPivot.Components;
using ExPivot.MudBlazor;
using ExChart.Components;
using ExChart.MudBlazor;
using ExSheet.Components;
using ExSheet.MudBlazor;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
// The family's stylesheets, each at the address of the file its package was built with (ADR-0123): root
// components in the head, as a standalone application places them, so that no page's HeadContent — the
// layouts' dark-scheme one among them — takes them away.
builder.RootComponents.Add<ExGridStylesheet>("head::after");
builder.RootComponents.Add<MudExGridStylesheet>("head::after");
builder.RootComponents.Add<ExSheetStylesheet>("head::after");
builder.RootComponents.Add<MudExSheetStylesheet>("head::after");
builder.RootComponents.Add<ExPivotStylesheet>("head::after");
builder.RootComponents.Add<MudExPivotStylesheet>("head::after");
builder.RootComponents.Add<ExChartStylesheet>("head::after");
builder.RootComponents.Add<MudExChartStylesheet>("head::after");
builder.Services.AddMudServices();
builder.Services.AddScoped<FrameworkMemory>();
builder.Services.AddScoped<DocsState>();

await builder.Build().RunAsync();
