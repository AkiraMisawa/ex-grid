using System.Globalization;
using System.Reflection;
using System.Text;
using global::ExChart.Engine;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExChart.Components;

/// <summary>
/// The files a chart saves (ADR-0252, ADR-0282), written on the click and never during a render.
/// The SVG is the very drawing on screen: the same part components, rendered to text by Blazor's
/// own HTML renderer, so a file cannot drift from the screen by a second drawing path. It carries
/// the stylesheet inside it, since a file has no page to take it from.
/// </summary>
internal static class ChartFiles
{
    private static readonly Lazy<string> Stylesheet = new(() =>
    {
        using var stream = typeof(ChartFiles).Assembly.GetManifestResourceStream("ExChart.ex-chart.min.css")
            ?? throw new InvalidOperationException("ExChart's stylesheet is missing from its assembly.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    });

    /// <summary>The names of the Visual Tokens the stylesheet reads, without their prefix (ADR-0282).</summary>
    internal static readonly Lazy<string[]> TokenNames = new(() =>
        System.Text.RegularExpressions.Regex.Matches(Stylesheet.Value, @"--_ex-chart-([a-z0-9-]+):")
            .Select(m => m.Groups[1].Value).Distinct().ToArray());

    /// <summary>
    /// The style a saved file's root carries: each token's value as the screen has it, and the font,
    /// so the file has the theme's colours (ADR-0282). A value that would end the declaration is left
    /// out, and the stylesheet's default stands for it.
    /// </summary>
    internal static string Theme(IReadOnlyList<string> values)
    {
        var names = TokenNames.Value;
        var b = new StringBuilder();
        for (var i = 0; i < names.Length && i < values.Count; i++)
        {
            if (Declarable(values[i])) b.Append("--ex-chart-").Append(names[i]).Append(':').Append(values[i]).Append(';');
        }
        if (values.Count > names.Length && Declarable(values[names.Length])) b.Append("font-family:").Append(values[names.Length]).Append(';');
        return b.ToString();
    }

    private static bool Declarable(string value) =>
        value.Length > 0 && value.IndexOfAny([';', '{', '}', '<', '>']) < 0;

    /// <summary>The media type a format is saved as.</summary>
    internal static string MediaType(ChartFileFormat format) => format switch
    {
        ChartFileFormat.Csv => "text/csv",
        ChartFileFormat.Svg => "image/svg+xml",
        _ => "image/png",
    };

    /// <summary>The file name: the chart's own, or its title's, made safe for a file system, with the format's extension.</summary>
    internal static string Name(string? fileName, string? title, ChartFileFormat format)
    {
        var stem = !string.IsNullOrWhiteSpace(fileName) ? fileName : !string.IsNullOrWhiteSpace(title) ? title : "chart";
        var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        var safe = new string(stem.Trim().Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray());
        // A file system drops trailing dots, so "." or ".." would name no file at all.
        safe = safe.TrimEnd('.', ' ');
        if (safe.Length == 0) safe = "chart";
        return safe + format switch
        {
            ChartFileFormat.Csv => ".csv",
            ChartFileFormat.Svg => ".svg",
            _ => ".png",
        };
    }

    /// <summary>The CSV: every visible series in the range shown, one row a position (ADR-0275's table).</summary>
    internal static byte[] Csv(ChartDefinition definition, ChartMetrics metrics, ChartView view) =>
        Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(ChartCsv.Wide(definition, metrics, view))).ToArray();

    /// <summary>The SVG text of the drawing as it stands, without the pointer's guide, tooltip or band.</summary>
    internal static async Task<string> SvgAsync(IServiceProvider services, ChartFileDrawing.Input input)
    {
        var loggers = services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        await using var renderer = new HtmlRenderer(services, loggers);
        var body = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(ChartFileDrawing.Drawing)] = input });
            var output = await renderer.RenderComponentAsync<ChartFileDrawing>(parameters);
            return output.ToHtmlString();
        });
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + body;
    }

    /// <summary>The stylesheet as an SVG's style element holds it: inside CDATA, so a selector's &gt; stays CSS.</summary>
    internal static string StyleMarkup => "<![CDATA[" + Stylesheet.Value.Replace("]]>", "]]]]><![CDATA[>", StringComparison.Ordinal) + "]]>";
}

/// <summary>The drawing a file holds: a standalone SVG of the chart's panels, axes, series and legends.</summary>
internal sealed class ChartFileDrawing : ComponentBase
{
    /// <summary>What the file is drawn from.</summary>
    internal sealed record Input(ChartScene Scene, double Width, double Height, double FontSize, double LineHeight, int Angle, string Summary, string Theme = "");

    [Parameter] public Input Drawing { get; set; } = null!;

    protected override void BuildRenderTree(RenderTreeBuilder b)
    {
        var d = Drawing;
        b.OpenElement(0, "svg");
        b.AddAttribute(1, "xmlns", "http://www.w3.org/2000/svg");
        b.AddAttribute(2, "class", "ex-chart ex-chart-svg ex-chart-file");
        b.AddAttribute(3, "width", Svg.N(d.Width));
        b.AddAttribute(4, "height", Svg.N(d.Height));
        b.AddAttribute(5, "viewBox", string.Create(CultureInfo.InvariantCulture, $"0 0 {Svg.N(d.Width)} {Svg.N(d.Height)}"));
        b.AddAttribute(6, "style", string.Create(CultureInfo.InvariantCulture, $"font-size:{Svg.N(d.FontSize)}px;--ex-chart-line-height:{Svg.N(d.LineHeight)}px;{d.Theme}"));
        b.OpenElement(7, "style");
        b.AddMarkupContent(8, ChartFiles.StyleMarkup);
        b.CloseElement();
        b.OpenElement(9, "title");
        b.AddContent(10, d.Summary);
        b.CloseElement();
        // A file has no page behind it: the background is drawn, so a PNG is not transparent.
        b.OpenElement(11, "rect");
        b.AddAttribute(12, "class", "ex-chart-file-background");
        b.AddAttribute(13, "width", Svg.N(d.Width));
        b.AddAttribute(14, "height", Svg.N(d.Height));
        b.CloseElement();
        b.AddContent(15, ChartDrawing.Panels(d.Scene, d.LineHeight, d.Angle, default));
        b.CloseElement();
    }
}

/// <summary>The panels' parts, opened once for the screen and for a file alike.</summary>
internal static class ChartDrawing
{
    /// <summary>Each panel's axes, series and legend, keyed as the screen keys them.</summary>
    internal static RenderFragment Panels(ChartScene scene, double lineHeight, int angle, EventCallback<string> toggle) => b =>
    {
        for (var i = 0; i < scene.Panels.Count; i++)
        {
            var panel = scene.Panels[i];
            b.OpenComponent<ChartAxesView>(0);
            b.SetKey("axes:" + panel.Name);
            b.AddComponentParameter(1, nameof(ChartAxesView.Panel), panel);
            b.AddComponentParameter(2, nameof(ChartAxesView.XTicks), scene.XTicks);
            b.AddComponentParameter(3, nameof(ChartAxesView.Last), i == scene.Panels.Count - 1);
            b.AddComponentParameter(4, nameof(ChartAxesView.LineHeight), lineHeight);
            b.AddComponentParameter(5, nameof(ChartAxesView.Angle), angle);
            b.CloseComponent();
            foreach (var piece in panel.Series)
            {
                b.OpenComponent<ChartSeriesView>(6);
                b.SetKey((panel.VolumeOf is null ? "series:" : "volume:") + piece.Id);
                b.AddComponentParameter(7, nameof(ChartSeriesView.Piece), piece);
                b.AddComponentParameter(8, nameof(ChartSeriesView.Plot), panel.Plot);
                b.AddComponentParameter(9, nameof(ChartSeriesView.LineHeight), lineHeight);
                b.CloseComponent();
            }
            b.OpenComponent<ChartLegendView>(10);
            b.SetKey("legend:" + panel.Name);
            b.AddComponentParameter(11, nameof(ChartLegendView.Entries), panel.Legend);
            b.AddComponentParameter(12, nameof(ChartLegendView.LineHeight), lineHeight);
            b.AddComponentParameter(13, nameof(ChartLegendView.Toggle), toggle);
            b.CloseComponent();
        }
    };
}
