using Fluxor;
using FluxorGrid.Grid;
using FluxorGrid.Store;
using Microsoft.Extensions.DependencyInjection;

namespace FluxorGrid;

/// <summary>What both hosts register: the store, scanned from this assembly, and the spike's
/// scoped services. No ReduxDevTools middleware: it keeps the history of states, which would hold
/// every old state reachable and spoil the census (check 6).</summary>
public static class FluxorRegistration
{
    public static IServiceCollection AddFluxorGrid(this IServiceCollection services)
    {
        services.AddFluxor(options => options.ScanAssemblies(typeof(TradesState).Assembly));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<LiveFeed>();
        services.AddScoped<RenderCounts>();
        services.AddScoped<Census>();
        return services;
    }
}
