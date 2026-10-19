namespace ExChart.MudBlazor;

/// <summary>Where the Chart Dock puts its charts: beside the grid or below it, never over it (ADR-0258).</summary>
public enum ChartDockPlacement
{
    /// <summary>A panel to the grid's right.</summary>
    Right,

    /// <summary>A panel under the grid.</summary>
    Bottom,
}
