using ExChart.Engine;
using ScriptFunction = global::ExGrid.Data.ScriptFunction;

namespace ExChart.Engine.Tests;

// ADR-0420 section 3: a function of the Consumer's own script is .NET's to name and the browser's to call, so what is
// checked of it is C#'s own: its address, and that it never stands beside another writer of the same values.
public class ScriptFunctionTests
{
    private static readonly ScriptFunction Tenor = ScriptFunction.Of("./js/chart-formats.js", "tenor");

    [Fact] // ADR-0420: a module is a file the application serves, never script carried in its address
    public void A_module_is_addressed_by_a_path_or_http()
    {
        Assert.Equal("./js/chart-formats.js", Tenor.Module);
        Assert.Equal(ScriptFunction.Of("/js/f.js", "f"), ScriptFunction.Of("/js/f.js", "f"));
        Assert.Equal("https://cdn.example.com/f.js", ScriptFunction.Of("https://cdn.example.com/f.js", "f").Module);
        Assert.Contains("scheme \"data\"", Assert.Throws<ArgumentException>(() => ScriptFunction.Of("data:text/javascript,export const f = () => 1", "f")).Message,
            StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => ScriptFunction.Of("./f.js", " "));
    }

    [Fact] // ADR-0420: a script function beside a Format or a code would write one value two ways, and is refused
    public void A_script_function_beside_another_writer_is_refused()
    {
        var points = ChartSeries.Of("a", "A", SeriesKind.Level, [new ChartPoint(0, 1.0), new ChartPoint(1, 2.0)]);
        Assert.Contains("beside a format code", Assert.Throws<ChartRefusedException>(
            () => new ChartDefinition(ChartAxis.Numbers() with { FormatCode = "0.0", FormatScript = Tenor }, [points])).Message, StringComparison.Ordinal);
        Assert.Contains("'A' has the script function", Assert.Throws<ChartRefusedException>(
            () => new ChartDefinition(ChartAxis.Numbers(), [points with { Format = v => "", FormatScript = Tenor }])).Message, StringComparison.Ordinal);
        Assert.Throws<ChartRefusedException>(() => SurfaceAxis.Of("x", [1.0, 2.0]) with { FormatCode = "0.0", FormatScript = Tenor });
        Assert.Throws<ChartRefusedException>(() => SurfaceAxis.Of("x", [1.0, 2.0]) with { FormatScript = Tenor, FormatCode = "0.0" });

        var drawn = new ChartDefinition(ChartAxis.Numbers() with { FormatScript = Tenor },
            [points, ChartSeries.Sampled("s", "S", SeriesKind.Level, 0, 1, ScriptFunction.Of("./js/curves.js", "s"))]);
        Assert.Equal([Tenor, ScriptFunction.Of("./js/curves.js", "s")], drawn.ScriptFunctions);
    }

    [Fact] // ADR-0420: a series sampled by the Consumer's script cannot be asked in .NET, and says so; one given a .NET function is no longer the script's
    public void A_script_sampler_says_it_runs_in_the_browser()
    {
        var sampler = ChartSeries.Sampled("s", "Smile", SeriesKind.Level, 0, 1, Tenor).Sampler!;
        Assert.Equal(Tenor, sampler.Script);
        Assert.Contains("only a chart drawn in the browser calls", Assert.Throws<NotSupportedException>(() => sampler.ValueAt(0.5)).Message, StringComparison.Ordinal);
        Assert.Null((sampler with { ValueAt = x => ChartValue.Of(x) }).Script);
        Assert.Equal(ChartSeries.Sampled("s", "Smile", SeriesKind.Level, 0, 1, Tenor), ChartSeries.Sampled("s", "Smile", SeriesKind.Level, 0, 1, Tenor));
    }
}
