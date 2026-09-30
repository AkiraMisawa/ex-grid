# ExPivot.MudBlazor

[ExPivot](https://www.nuget.org/packages/ExPivot), Excel's PivotTable for Blazor, inside a
MudBlazor application:

- **`MudPivotChrome`** draws the PivotTable Fields pane, each field's menu, Filter…, Field
  Settings…, Value Field Settings… and the report filter band with MudBlazor's own controls:
  checkboxes, text fields, selects, radio groups, buttons and Material icons. It also dresses the
  report's grid with `ExGrid.MudBlazor`'s `MudGridChrome`, so you set one parameter for both.
- **`mud-ex-pivot.css`** maps ExPivot's Visual Tokens onto MudBlazor's palette variables and
  lays out the Chrome's controls. It follows the theme, dark mode included.
- The surface is `ExGrid.MudBlazor`'s **`MudExGridPaper`**, unchanged. It brings the Material
  surface, the grid's palette mapping, and the Roboto widths the report's label column is sized
  with.

The pivot's behaviour does not change. The Chrome draws and calls back, and ExPivot decides what
every tick, drop and command means. The same gestures make the same layout under either Chrome.

> **This is a prerelease (`0.x`).** It ships at the same version as ExPivot and depends on
> exactly that version and on the matching `ExGrid.MudBlazor`, so upgrade them together.

## Install

```sh
dotnet add package ExPivot.MudBlazor --prerelease
```

This brings in ExPivot, ExGrid.MudBlazor and MudBlazor (9.0 or newer). Set MudBlazor up as
usual:

- `builder.Services.AddMudServices()`
- MudBlazor's stylesheet and script
- `<MudThemeProvider />` and `<MudPopoverProvider />` in your layout. The panels' selects open
  their lists through the popover provider.

Then add the four stylesheets:

```html
<link rel="stylesheet" href="_content/ExGrid/ex-grid.css" />
<link rel="stylesheet" href="_content/ExGrid.MudBlazor/mud-ex-grid.css" />
<link rel="stylesheet" href="_content/ExPivot/ex-pivot.css" />
<link rel="stylesheet" href="_content/ExPivot.MudBlazor/mud-ex-pivot.css" />
```

## A pivot on a paper

```razor
@using ExGrid.MudBlazor
@using ExPivot.Components
@using ExPivot.Engine
@using ExPivot.MudBlazor

<MudExGridPaper Elevation="1" Hover="true">
    <ExPivot TRecord="Sale" Records="_sales" Fields="_fields" @bind-Layout="_layout"
             PivotChrome="MudPivotChrome.Default" ViewportHeight="420" />
</MudExGridPaper>
```

The records, fields and layout are the same as for a plain ExPivot. See the ExPivot README.

- **Words.** Every word comes from ExPivot and is replaced through ExPivot's `Label` parameter.
  That one function covers the pane, the panels and the report's Context Menu drawn by
  `MudGridChrome`.
- **Icons.** `new MudPivotChrome { Icon = id => … }` replaces a command's Material icon. Keep
  the instance in a field.
- **The `±` buttons** in the report's label cells stay ExPivot's own plain markup, recoloured by
  the palette. A MudBlazor component there would be one more component in every row painted.

## More

The decisions are recorded in the repository's ADRs:
[ADR-0060](https://github.com/AkiraMisawa/ex-grid/blob/main/docs/adr/0060-the-field-list-is-excels-pane-and-the-core-decides-what-a-move-means.md)
covers the Field List and the Chrome seam, and
[ADR-0061](https://github.com/AkiraMisawa/ex-grid/blob/main/docs/adr/0061-expivot-mudblazor-is-a-chrome-and-a-stylesheet.md)
covers this Wrapper.
