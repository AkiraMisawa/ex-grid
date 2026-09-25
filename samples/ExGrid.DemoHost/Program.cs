using ExGrid.DemoHost;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// MudBlazor's services, for the /mud page: the Wrapper's paper and Chrome are plain
// components, but MudThemeProvider — which emits the palette variables the Wrapper's
// stylesheet reads — needs them.
builder.Services.AddMudServices();
// The /shared page's store (ADR-0018 §5). One per process — on WebAssembly that is one
// per tab, so the page has nobody to share with here; the Server host is where it
// matters.
builder.Services.AddSingleton<global::ExGrid.DemoPages.SharedTradeStore>();

await builder.Build().RunAsync();
