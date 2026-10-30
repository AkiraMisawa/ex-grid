using ExGrid.Components;
using FreshnessApp;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
// ExGrid's stylesheet as a standalone application places it (ADR-0123): a root component in the
// head, which the page's own HeadContent below does not displace.
builder.RootComponents.Add<ExGridStylesheet>("head::after");
await builder.Build().RunAsync();
