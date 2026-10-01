using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ExGrid.DemoApi.Tests;

/// <summary>
/// The demo API server, in process on ASP.NET Core's test server, over trades of its own in a
/// data directory of its own. One per test class, so a class that turns live updates on never
/// moves another's data.
/// </summary>
public class DemoApiServer : IAsyncLifetime
{
    /// <summary>How many trades the server generates, unless a fixture says otherwise
    /// (<see cref="TradeCount"/>).</summary>
    public const int Trades = 3_000;

    private readonly TempDirectory _directory = new();
    private WebApplicationFactory<Program>? _factory;

    internal WebApplicationFactory<Program> Factory => _factory ?? throw new InvalidOperationException("Not started.");

    internal TradeStore Store => Factory.Services.GetRequiredService<TradeStore>();

    /// <summary>The data directory the server's trades live in.</summary>
    internal string DataDirectory => _directory.Path;

    /// <summary>How many trades this server generates.</summary>
    public virtual int TradeCount => Trades;

    /// <summary>Whether the server makes its trades ready as it starts; a test of the answers
    /// before that turns it off.</summary>
    protected virtual bool MakesTradesReady => true;

    public async ValueTask InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            // Warnings and errors still reach the output; the progress lines do not.
            .ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
                [new KeyValuePair<string, string?>("Logging:LogLevel:Default", "Warning")]))
            .ConfigureTestServices(services =>
            {
                services.AddSingleton(new DemoApiOptions(TradeCount, _directory.Path));
                if (!MakesTradesReady)
                {
                    services.Remove(services.Single(service =>
                        service.ServiceType == typeof(IHostedService) && service.ImplementationType == typeof(TradeStoreStartup)));
                }
            }));
        if (MakesTradesReady)
            await Store.Ready.WaitAsync(TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        else
            _ = Factory.Server;
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
            await _factory.DisposeAsync();
        _directory.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>The server with its trades never made ready: what it answers while it generates.</summary>
public sealed class DemoApiServerNotReady : DemoApiServer
{
    protected override bool MakesTradesReady => false;
}

/// <summary>The server over twenty thousand trades — every book, date and month among them — for
/// holding its Pivot Source to <c>PivotSource.From</c>'s answers (PV-22).</summary>
public sealed class PivotApiServer : DemoApiServer
{
    public override int TradeCount => 20_000;
}
