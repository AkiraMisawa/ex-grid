using Bunit;
using ExGrid.Chrome;
using ExGrid.Components.Tests.Support;
using ExGrid.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace ExGrid.Components.Tests;

/// <summary>
/// ERR-6 and ERR-10: a script that is missing, broken, or not the one the grid was built with. The
/// module is how the grid hears scrolling and keys (ADR-0021); before it attaches anything the grid
/// asks it for its stamp, the SHA-256 of its source, and refuses one that answers another, or none
/// (ADR-0123, ADR-0021's note of 2026-10-11). A refused script attaches nothing, and the grid says so
/// in place of its rows, in the built-in Chrome's words, and once in its live region: never busy and
/// silent. And it still asks the Consumer for the rows (ERR-6): the Range Request is not taken down
/// with the script, and no Placeholder waits on a module that will not land.
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

        /// <summary>A module older than the stamp: it has no <c>stamp</c> to call.</summary>
        NoStamp,

        /// <summary>Another version's module: its stamp is not the one this build was made with.</summary>
        OtherStamp,

        /// <summary>This build's module, whose <c>attach</c> throws.</summary>
        AttachThrows,

        /// <summary>This build's module, which attached and whose handle breaks on the first question asked of it.</summary>
        HandleThrows,
    }

    private static readonly string Refusal = BuiltInCommandLabels.For(ReloadNoticeLabelIds.Script);

    [Theory] // ERR-6 / ADR-0001 / ADR-0021 / ADR-0123: whatever the module does, the rows are still asked for, and no Placeholder waits on it
    [InlineData(Breakage.Missing)]
    [InlineData(Breakage.NoStamp)]
    [InlineData(Breakage.OtherStamp)]
    [InlineData(Breakage.AttachThrows)]
    [InlineData(Breakage.HandleThrows)]
    public void ERR6_a_broken_module_does_not_stop_the_range_request(Breakage breakage)
    {
        _js.Breakage = breakage;

        var host = Render<PushHost>(ps => ps.Add(h => h.AllRows, TestRows.Many(1_000)));

        // Asked once, for the rows on screen, by the grid's own render path (ADR-0001)…
        var asked = Assert.Single(host.Instance.Requests);
        Assert.Equal(0, asked.Start);
        // …and once the Consumer answered, no Placeholder is left: the grid is not waiting on the
        // module for anything it paints. The refusal stands where the rows would (ADR-0123).
        host.WaitForAssertion(() =>
        {
            Assert.Empty(host.FindAll(".ex-placeholder"));
            Assert.Equal(Refusal, host.Find(".ex-viewport > [data-ex-notice='script']").TextContent);
        });
        // The module's failure is the grid's to absorb, not the renderer's to report.
        Assert.False(Renderer.UnhandledException.IsCompleted);
        Assert.True(_js.Imports > 0);
    }

    [Theory] // ERR-6 / ADR-0001: a Source behind the grid is asked the same way when the module never lands
    [InlineData(Breakage.Missing)]
    [InlineData(Breakage.NoStamp)]
    [InlineData(Breakage.OtherStamp)]
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

    [Theory] // ERR-10 / ADR-0123 / ADR-0021's note of 2026-10-11: another version's script, an older one with no stamp, or none, is refused before anything attaches
    [InlineData(Breakage.Missing)]
    [InlineData(Breakage.NoStamp)]
    [InlineData(Breakage.OtherStamp)]
    public void ERR10_a_script_that_is_not_this_builds_is_refused_before_anything_attaches(Breakage breakage)
    {
        _js.Breakage = breakage;

        var host = Render<PushHost>(ps => ps.Add(h => h.AllRows, TestRows.Many(1_000)));

        host.WaitForAssertion(() => Assert.Single(host.FindAll(".ex-viewport > [data-ex-notice='script']")));
        // Nothing attached: attach was never asked of the module.
        Assert.Equal(0, _js.Attaches);
        AssertRefusedAndSaid(host);
    }

    [Theory] // ERR-10 / ADR-0123 / ADR-0018: a script that breaks while it attaches is shown the same way, and what it had attached is let go
    [InlineData(Breakage.AttachThrows, 0)]
    [InlineData(Breakage.HandleThrows, 1)]
    public void ERR10_a_script_that_breaks_while_it_attaches_is_refused_and_let_go(Breakage breakage, int handlesLetGo)
    {
        _js.Breakage = breakage;

        var host = Render<PushHost>(ps => ps.Add(h => h.AllRows, TestRows.Many(1_000)));

        host.WaitForAssertion(() => Assert.Single(host.FindAll(".ex-viewport > [data-ex-notice='script']")));
        Assert.Equal(1, _js.Attaches);
        // A handle that had attached its listeners is asked to take them off again.
        Assert.Equal(handlesLetGo, _js.HandleDisposes);
        AssertRefusedAndSaid(host);
    }

    [Fact] // ERR-10 / ADR-0203: the refusal is the built-in Chrome's words, and a Consumer's label function words it instead
    public void ERR10_the_refusal_is_worded_through_the_label_function()
    {
        _js.Breakage = Breakage.OtherStamp;
        const string words = "Diese Seite enthält eine andere Version. Bitte neu laden.";

        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(20))
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.ViewportHeight, 100)
            .Add(g => g.CommandLabel, id => id == ReloadNoticeLabelIds.Script ? words : null));

        cut.WaitForAssertion(() => Assert.Equal(words, cut.Find(".ex-viewport > [data-ex-notice='script']").TextContent));
        Assert.Equal(words, cut.Find(".ex-announce").TextContent);
    }

    [Fact] // ERR-10 / ADR-0203: a substituted Chrome words the refusal, as it words every refusal, ahead of the label function
    public void ERR10_a_substituted_chrome_words_the_refusal()
    {
        _js.Breakage = Breakage.OtherStamp;
        const string words = "La page contient une autre version du script. Rechargez-la.";

        var cut = Render<ExGrid<TestRow>>(ps => ps
            .Add(g => g.Window, TestRows.Many(20))
            .Add(g => g.Columns, TestRows.Columns())
            .Add(g => g.ViewportHeight, 100)
            .Add(g => g.Chrome, new WordingChrome(ReloadNoticeLabelIds.Script, words))
            .Add(g => g.CommandLabel, id => id == ReloadNoticeLabelIds.Script ? "Not these words." : null));

        cut.WaitForAssertion(() => Assert.Equal(words, cut.Find(".ex-viewport > [data-ex-notice='script']").TextContent));
        Assert.Equal(words, cut.Find(".ex-announce").TextContent);
    }

    /// <summary>A Chrome that words one id and leaves every other seam to the core.</summary>
    private sealed class WordingChrome(string id, string words) : IGridChrome
    {
        public Microsoft.AspNetCore.Components.RenderFragment? FilterPanel(FilterPanelContext context) => null;

        public Microsoft.AspNetCore.Components.RenderFragment? ColumnMenu(ColumnMenuContext context) => null;

        public Microsoft.AspNetCore.Components.RenderFragment? CellEditor(CellEditorContext context) => null;

        public Microsoft.AspNetCore.Components.RenderFragment? LoadingIndicator(LoadingContext context) => null;

        public string? Words(string asked) => asked == id ? words : null;
    }

    [Fact] // ERR-10 / ADR-0033: the refusal is said once, and nothing the grid does afterwards says it again
    public void ERR10_the_refusal_is_said_once()
    {
        _js.Breakage = Breakage.OtherStamp;

        var host = Render<PushHost>(ps => ps.Add(h => h.AllRows, TestRows.Many(1_000)));
        host.WaitForAssertion(() => Assert.Equal(Refusal, host.Find(".ex-announce").TextContent));

        // Rows arrive, renders follow: the region is not emptied and written again, which a screen
        // reader would hear as a second announcement.
        host.Render(ps => ps.Add(h => h.AllRows, TestRows.Many(500)));
        Assert.Equal(Refusal, host.Find(".ex-announce").TextContent);
        Assert.Equal(1, _js.Imports);
    }

    /// <summary>The refusal stands where the rows would, is said in the live region, and the grid is
    /// neither busy nor a tab stop: it waits for nothing, and nothing would hear a key (ADR-0033).</summary>
    private void AssertRefusedAndSaid(IRenderedComponent<PushHost> host)
    {
        var root = host.Find(".ex-grid");
        Assert.Equal(Refusal, host.Find(".ex-viewport > [data-ex-notice='script']").TextContent);
        Assert.Equal("true", host.Find(".ex-viewport > [data-ex-notice='script']").GetAttribute("aria-hidden"));
        Assert.Equal(Refusal, host.Find(".ex-announce").TextContent);
        Assert.Null(root.GetAttribute("aria-busy"));
        Assert.Null(root.GetAttribute("tabindex"));
        host.WaitForAssertion(() => Assert.DoesNotContain("ex-loading", host.Find(".ex-grid").ClassList));
        Assert.Empty(host.FindAll(".ex-key-field"));
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }

    /// <summary>The browser's side of a module that is missing, broken, or another version's.</summary>
    private sealed class BrokenModuleRuntime : IJSRuntime
    {
        internal Breakage Breakage { get; set; }

        internal int Imports { get; private set; }

        internal int Attaches { get; set; }

        internal int HandleDisposes { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier != "import")
                throw new InvalidOperationException($"The grid called '{identifier}' on the runtime itself.");
            Imports++;
            Assert.Equal(ShippedScript.Address, args![0]);
            return Breakage == Breakage.Missing
                ? ValueTask.FromException<TValue>(new JSException(
                    "Failed to fetch dynamically imported module: " + ShippedScript.Address))
                : ValueTask.FromResult((TValue)(object)new BrokenReference(this, isModule: true));
        }
    }

    private sealed class BrokenReference(BrokenModuleRuntime runtime, bool isModule) : IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            var breakage = runtime.Breakage;
            if (isModule && identifier == "stamp" && breakage != Breakage.NoStamp)
            {
                var stamp = breakage == Breakage.OtherStamp ? new string('0', 64) : ShippedScript.Stamp;
                return ValueTask.FromResult((TValue)(object)stamp);
            }
            if (isModule && identifier == "attach")
            {
                runtime.Attaches++;
                if (breakage == Breakage.HandleThrows)
                    return ValueTask.FromResult((TValue)(object)new BrokenReference(runtime, isModule: false));
            }
            if (!isModule && identifier == "dispose")
                runtime.HandleDisposes++;
            // What a browser says of a function the module or the handle does not have, or of one
            // that threw: Blazor's "Could not find 'x'", as a JSException.
            return ValueTask.FromException<TValue>(new JSException(
                $"Could not find '{identifier}' ('{identifier}' was undefined)."));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
