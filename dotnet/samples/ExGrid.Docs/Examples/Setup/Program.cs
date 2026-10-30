using ExGrid.Components;
using ExGrid.MudBlazor;
using ExPivot.Components;
using ExPivot.MudBlazor;
using ExSheet.Components;
using ExSheet.MudBlazor;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Each package's stylesheet, then its Wrapper's (in a Blazor Web App, in App.razor's <head>).
builder.RootComponents.Add<ExGridStylesheet>("head::after");
builder.RootComponents.Add<MudExGridStylesheet>("head::after");
builder.RootComponents.Add<ExSheetStylesheet>("head::after");
builder.RootComponents.Add<MudExSheetStylesheet>("head::after");
builder.RootComponents.Add<ExPivotStylesheet>("head::after");
builder.RootComponents.Add<MudExPivotStylesheet>("head::after");

// Only for a MudBlazor application: the Wrappers draw with MudBlazor's own controls.
builder.Services.AddMudServices();

await builder.Build().RunAsync();
