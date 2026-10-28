using global::MudBlazor;

namespace ExSheet.MudBlazor;

/// <summary>The Material picture a Toolbar Item shows under the MudBlazor Chrome (ADR-0100), over either core.</summary>
internal static class MudToolbarIcons
{
    /// <summary>The picture for <paramref name="icon"/>, or null for one Material has none for.</summary>
    internal static string? IconOf(ToolbarIcon icon) => icon switch
    {
        ToolbarIcon.Bold => Icons.Material.Filled.FormatBold,
        ToolbarIcon.Italic => Icons.Material.Filled.FormatItalic,
        ToolbarIcon.Underline => Icons.Material.Filled.FormatUnderlined,
        ToolbarIcon.Strikethrough => Icons.Material.Filled.StrikethroughS,
        ToolbarIcon.FontColour => Icons.Material.Filled.FormatColorText,
        ToolbarIcon.Fill => Icons.Material.Filled.FormatColorFill,
        ToolbarIcon.Borders => Icons.Material.Filled.BorderBottom,
        ToolbarIcon.AlignLeft => Icons.Material.Filled.FormatAlignLeft,
        ToolbarIcon.AlignCenter => Icons.Material.Filled.FormatAlignCenter,
        ToolbarIcon.AlignRight => Icons.Material.Filled.FormatAlignRight,
        ToolbarIcon.Percent => Icons.Material.Filled.Percent,
        ToolbarIcon.FormatCells => Icons.Material.Outlined.TableChart,
        _ => null,
    };

    /// <summary>The text shown in place of a picture: Material has none for the comma style, which shows Excel's.</summary>
    internal static string? TextOf(ToolbarIcon icon) => icon == ToolbarIcon.Comma ? "," : null;
}
