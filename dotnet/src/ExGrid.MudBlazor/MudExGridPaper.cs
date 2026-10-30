namespace ExGrid.MudBlazor;

/// <summary>
/// The Wrapper's outer element (ADR-0030): a Material surface around one or more grids, with
/// MudBlazor's elevation, corners and outline, a toolbar slot above them, and the presentation
/// defaults it cascades to every grid inside: Roboto's widths, and the <see cref="Dense"/>,
/// <see cref="Hover"/> and <see cref="Striped"/> options, each of which a parameter written on the
/// grid still overrides. It sets Visual Tokens and nothing else of the grid's, mapped onto
/// MudBlazor's palette variables, so a theme or dark-mode switch re-renders nothing (ADR-0027). It
/// forwards no grid parameter: those are written on the grid.
/// </summary>
public partial class MudExGridPaper
{
}
