using MudBlazor;

namespace ExGrid.Docs.Layout;

/// <summary>The site's MudBlazor theme: Roboto, and a quiet palette that lets the grids lead.</summary>
public static class DocsTheme
{
    /// <summary>The theme every layout provides.</summary>
    public static MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#1b6ac9",
            Secondary = "#0f8a6c",
            AppbarBackground = "#ffffff",
            AppbarText = "#1f2329",
            DrawerBackground = "#ffffff",
            Background = "#ffffff",
            TextPrimary = "#1f2329",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#6ea8ff",
            Secondary = "#4fd1a5",
            AppbarBackground = "#1b1e23",
            DrawerBackground = "#1b1e23",
            Background = "#15171b",
            Surface = "#1e2126",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = ["Roboto", "Helvetica", "Arial", "sans-serif"] },
        },
    };
}
