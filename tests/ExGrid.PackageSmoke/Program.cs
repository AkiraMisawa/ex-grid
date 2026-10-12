using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using PackageSmoke;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
// The six stylesheets as a standalone Consumer places them: root components in the head (ADR-0123).
builder.RootComponents.Add<ExGrid.Components.ExGridStylesheet>("head::after");
builder.RootComponents.Add<ExGrid.MudBlazor.MudExGridStylesheet>("head::after");
builder.RootComponents.Add<ExSheet.Components.ExSheetStylesheet>("head::after");
builder.RootComponents.Add<ExSheet.MudBlazor.MudExSheetStylesheet>("head::after");
builder.RootComponents.Add<ExPivot.Components.ExPivotStylesheet>("head::after");
builder.RootComponents.Add<ExPivot.MudBlazor.MudExPivotStylesheet>("head::after");
builder.Services.AddMudServices();

await builder.Build().RunAsync();
