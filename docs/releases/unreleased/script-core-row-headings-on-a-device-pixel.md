Product: ExGrid
Kind: Fixed
ADR: 0090

On the Script Core (`@ex-grid/core`, `@ex-grid/react` and the Blazor components over them), the Row Headings band is now put on a Device Pixel before the columns are counted from it, as the C# grid does. At a display scale such as 225%, a band whose width fell between Device Pixels could put a column edge where its gridline was blended over three pixels instead of drawn in two, and an ExSheet's column widths differed by a Device Pixel from the C# Sheet's.
