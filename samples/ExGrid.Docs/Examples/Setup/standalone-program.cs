using ExGrid.Components;
using ExPivot.Components;
using ExSheet.Components;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Each package's stylesheet, at the address of the file it was built with. Root components in the
// head, so that no page's <HeadContent> takes them away.
builder.RootComponents.Add<ExGridStylesheet>("head::after");
// ExSheet and ExPivot each bring a stylesheet of their own
builder.RootComponents.Add<ExSheetStylesheet>("head::after");
builder.RootComponents.Add<ExPivotStylesheet>("head::after");

await builder.Build().RunAsync();
