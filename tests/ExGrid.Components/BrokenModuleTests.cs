using Bunit;
using ExGrid.Components.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ERR-6: a missing or broken script module does not take the Range Request down with it. The
/// module is how the grid hears scrolling and keys (ADR-0021); without it the grid is a grid that
/// cannot be scrolled or typed at, but it still asks the Consumer for the rows it paints and paints
/// them when they come — never a page of Placeholders waiting on a module that will not land.
/// </summary>
public class BrokenModuleTests : GridTestContext
{
    private readonly BrokenModuleRuntime _js = new();

    public BrokenModuleTests()
    {
        // Registered after the base class's own runtime, so this is the one the grid resolves.
        Services.AddSingleton<IJSRuntime>(_js);
    }

    public enum Breakage
    {
        /// <summary>The file is not there: the browser's <c>import</c> rejects.</summary>
        Missing,

        /// <summary>The file is there and is not the grid's: it has no <c>attach</c>, or it throws.</summary>
        AttachThrows,

        /// <summary>The module attached and its handle breaks on the first question asked of it.</summary>
        HandleThrows,
    }

    [Theory] // ERR-6 / ADR-0001 / ADR-0021: whatever the module does, the rows are still asked for and painted
    [InlineData(Breakage.Missing)]
    [InlineData(Breakage.AttachThrows)]
    [InlineData(Breakage.HandleThrows)]
    public void ERR6_a_broken_module_does_not_stop_the_range_request(Breakage breakage)
    {
        _js.Breakage = breakage;

        var host = Render<PushHost>(ps => ps.Add(h => h.AllRows, TestRows.Many(1_000)));

        // Asked once, for the rows on screen, by the grid's own render path (ADR-0001)…
        var asked = Assert.Single(host.Instance.Requests);
        Assert.Equal(0, asked.Start);
        // …and once the Consumer answered, no Placeholder is left: the grid is not waiting on
        // the module for anything it paints.
        host.WaitForAssertion(() =>
        {
            Assert.Empty(host.FindAll(".ex-placeholder"));
            Assert.Equal("Row 000000", host.FindAll(".ex-row")[0].QuerySelector(".ex-cell")!.TextContent);
        });
        // The module's failure is the grid's to absorb, not the renderer's to report.
        Assert.False(Renderer.UnhandledException.IsCompleted);
        Assert.True(_js.Imports > 0);
    }

    [Theory] // ERR-6 / ADR-0001: a Source behind the grid is asked the same way when the module never lands
    [InlineData(Breakage.Missing)]
    [InlineData(Breakage.AttachThrows)]
    public void ERR6_a_source_is_still_asked_for_rows_when_the_module_is_broken(Breakage breakage)
    {
        _js.Breakage = breakage;
        var source = new TestSource();
        source.Push([], totalCount: 500);

        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Source, source)
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.RowHeight, 20)
            .Add(g => g.ViewportHeight, 100));

        var asked = Assert.Single(source.Requested);
        Assert.Equal(0, asked.Start);
        source.Push(TestRows.Many(20), totalCount: 500);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".ex-placeholder")));
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    /// <summary>The browser's side of a module that is missing or broken.</summary>
    private sealed class BrokenModuleRuntime : IJSRuntime
    {
        internal Breakage Breakage { get; set; }

        internal int Imports { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier != "import")
                throw new InvalidOperationException($"The grid called '{identifier}' on the runtime itself.");
            Imports++;
            return Breakage == Breakage.Missing
                ? ValueTask.FromException<TValue>(new JSException(
                    "Failed to fetch dynamically imported module: ./_content/ExGrid/ex-grid.min.js"))
                : ValueTask.FromResult((TValue)(object)new BrokenReference(Breakage, isModule: true));
        }
    }

    private sealed class BrokenReference(Breakage breakage, bool isModule) : IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (isModule && identifier == "attach" && breakage == Breakage.HandleThrows)
                return ValueTask.FromResult((TValue)(object)new BrokenReference(breakage, isModule: false));
            return ValueTask.FromException<TValue>(new JSException(
                $"TypeError: module.{identifier} is not a function"));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
