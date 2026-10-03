using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

// Only for a MudBlazor application: the Wrappers draw with MudBlazor's own controls.
builder.Services.AddMudServices();

await builder.Build().RunAsync();
