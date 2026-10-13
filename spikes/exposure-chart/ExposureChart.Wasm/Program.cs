using ExGrid.Components;
using ExposureChart.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.RootComponents.Add<ExGridStylesheet>("head::after");
await builder.Build().RunAsync();
