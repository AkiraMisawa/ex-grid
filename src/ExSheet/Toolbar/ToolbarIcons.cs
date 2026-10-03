using Microsoft.AspNetCore.Components;

namespace ExSheet;

/// <summary>
/// The built-in Chrome's pictures for the Toolbar Items (ADR-0100): inline SVG drawn in
/// <c>currentColor</c>, so the stylesheet's Visual Tokens colour them, with no icon font and no
/// script. Each is a 16-unit square drawn with strokes, and scales with the item.
/// </summary>
internal static class ToolbarIcons
{
    private const string Open = "<svg class=\"ex-sheet-toolbar-icon\" viewBox=\"0 0 16 16\" aria-hidden=\"true\" focusable=\"false\" fill=\"none\" stroke=\"currentColor\" stroke-linecap=\"round\" stroke-linejoin=\"round\">";
    private const string Close = "</svg>";

    private static readonly Dictionary<ToolbarIcon, MarkupString> Drawn = new()
    {
        [ToolbarIcon.Bold] = Svg("<path stroke-width=\"2\" d=\"M4.5 2.5h4a2.75 2.75 0 0 1 0 5.5h-4zM4.5 8h4.75a2.75 2.75 0 0 1 0 5.5H4.5z\"/>"),
        [ToolbarIcon.Italic] = Svg("<path stroke-width=\"1.5\" d=\"M7 2.5h6M3 13.5h6M10 2.5 6 13.5\"/>"),
        [ToolbarIcon.Underline] = Svg("<path stroke-width=\"1.5\" d=\"M4.5 2v5.5a3.5 3.5 0 0 0 7 0V2M3 14.5h10\"/>"),
        [ToolbarIcon.Strikethrough] = Svg("<path stroke-width=\"1.5\" d=\"M11 4.5C11 3.2 9.7 2.2 8 2.2S5 3.2 5 4.5c0 1.4 1.2 2.2 3 2.6M5 11.5c0 1.3 1.3 2.3 3 2.3s3-1 3-2.3M2 8.5h12\"/>"),
        [ToolbarIcon.FontColour] = Svg("<path stroke-width=\"1.5\" d=\"M4 11.5 8 2l4 9.5M5.5 8h5\"/>"),
        [ToolbarIcon.Fill] = Svg("<path stroke-width=\"1.25\" d=\"M2.5 7.5 7 3l4.5 4.5L7 12zM5 1.5 7 3M13 9.5s1.5 1.8 1.5 2.8a1.5 1.5 0 0 1-3 0c0-1 1.5-2.8 1.5-2.8\"/>"),
        [ToolbarIcon.Borders] = Svg("<path stroke-width=\"1.25\" stroke-dasharray=\"1.5 1.5\" d=\"M2 8h12M8 2v12\"/><path stroke-width=\"1.5\" d=\"M2 2h12v12H2z\"/>"),
        [ToolbarIcon.AlignLeft] = Svg("<path stroke-width=\"1.5\" d=\"M2 3h12M2 6.5h8M2 10h12M2 13.5h8\"/>"),
        [ToolbarIcon.AlignCenter] = Svg("<path stroke-width=\"1.5\" d=\"M2 3h12M4 6.5h8M2 10h12M4 13.5h8\"/>"),
        [ToolbarIcon.AlignRight] = Svg("<path stroke-width=\"1.5\" d=\"M2 3h12M6 6.5h8M2 10h12M6 13.5h8\"/>"),
        [ToolbarIcon.Percent] = Svg("<path stroke-width=\"1.5\" d=\"M3 13 13 3\"/><circle cx=\"4.5\" cy=\"4.5\" r=\"1.75\" stroke-width=\"1.25\"/><circle cx=\"11.5\" cy=\"11.5\" r=\"1.75\" stroke-width=\"1.25\"/>"),
        [ToolbarIcon.Comma] = Svg("<circle cx=\"8\" cy=\"9\" r=\"1.5\" fill=\"currentColor\" stroke=\"none\"/><path stroke-width=\"1.5\" d=\"M9.4 9.2c0 2.2-.8 3.6-2.4 4.6\"/>"),
        [ToolbarIcon.FormatCells] = Svg("<path stroke-width=\"1.25\" d=\"M2 2.5h12v11H2zM2 5.5h12M5 5.5v8\"/><path stroke-width=\"1.25\" d=\"M8 9h4M8 11.5h3\"/>"),
    };

    /// <summary>The picture for <paramref name="icon"/>; nothing for <see cref="ToolbarIcon.None"/>.</summary>
    internal static MarkupString? For(ToolbarIcon icon) => Drawn.TryGetValue(icon, out var svg) ? svg : null;

    /// <summary>The arrow a menu or a split control's list opens from.</summary>
    internal static MarkupString Caret { get; } = Svg("<path stroke-width=\"1.5\" d=\"M4.5 6.5 8 10l3.5-3.5\"/>");

    private static MarkupString Svg(string body) => new(Open + body + Close);
}
