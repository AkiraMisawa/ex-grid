# ExPivot.MudBlazor

[ExPivot](https://www.nuget.org/packages/ExPivot), Excel's PivotTable for Blazor, inside a
MudBlazor application:

- **`MudPivotChrome`** draws every surface of the pivot with MudBlazor's own controls:
  checkboxes, text fields, selects, radio groups, buttons, tabs, alerts and Material icons.
  - The PivotTable Fields pane, with Defer Layout Update, each field's menu, Filter…, Field
    Settings… and Value Field Settings….
  - The Pivot Toolbar above the report: the report filter band, Layout ▾ with its menu, Refresh when
    the source can be refreshed, and the pane's toggle, with a refusal as an error `MudAlert`.
  - Show Details' tabs, as `MudTabs` at the report's foot, and the content of its dialog. The
    dialog itself is ExPivot's frame, not a `MudDialog`.
  - The Stale Report's notice, a warning `MudAlert` with a Retry button.

  It also dresses the report's grid with `ExGrid.MudBlazor`'s `MudGridChrome`, so you set one
  parameter for both.
- **`mud-ex-pivot.css`** maps ExPivot's Visual Tokens onto MudBlazor's palette variables and
  lays out the Chrome's controls. It follows the theme, dark mode included.
- The surface is `ExGrid.MudBlazor`'s **`MudExGridPaper`**, unchanged. It brings the Material
  surface, the grid's palette mapping, and the Roboto widths the report's label column is sized
  with.

The pivot's behaviour does not change. The Chrome draws and calls back, and ExPivot decides what
every tick, drop and command means, which tab is selected, and where every menu, panel and dialog
opens. The same gestures make the same layout under either Chrome.

> **It ships at the same version as ExPivot,** and depends on exactly that version and on the
> matching `ExGrid.MudBlazor`, so upgrade them together. While the version is `0.x`, the API may
> change from one version to the next; each version's
> [Release Notes](https://akiramisawa.github.io/ex-grid/release-notes) say what changed.

## Install

```sh
dotnet add package ExPivot.MudBlazor
```

Add `--prerelease` for a prerelease, a version with a suffix such as `-rc.1`.

This brings in ExPivot, ExGrid.MudBlazor and MudBlazor 9: the package takes `[9.6.0,10.0.0)`, any
9.x from 9.6.0. An application that references an earlier 9.x is stopped at restore (NU1605, an
error by default) and moves MudBlazor up within 9; one on MudBlazor 10 is warned (NU1608, a warning
unless the application treats it as an error). Set MudBlazor up as usual:

- `builder.Services.AddMudServices()`
- MudBlazor's stylesheet and script
- `<MudThemeProvider />` and `<MudPopoverProvider />` in your layout. The panels' selects open
  their lists through the popover provider.

Then add the four stylesheets, as components in the head: in `App.razor`'s `<head>` in a Blazor
Web App, or as root components in a standalone application's `Program.cs`:

```csharp
builder.RootComponents.Add<ExGrid.Components.ExGridStylesheet>("head::after");
builder.RootComponents.Add<ExGrid.MudBlazor.MudExGridStylesheet>("head::after");
builder.RootComponents.Add<ExPivot.Components.ExPivotStylesheet>("head::after");
builder.RootComponents.Add<ExPivot.MudBlazor.MudExPivotStylesheet>("head::after");
```

## A pivot on a paper

```razor
@using ExGrid.MudBlazor
@using ExPivot.Components
@using ExPivot.Engine
@using ExPivot.MudBlazor

<MudExGridPaper Elevation="1" Hover="true">
    <ExPivot Source="_source" @bind-Layout="_layout"
             PivotChrome="MudPivotChrome.Default" ViewportHeight="420" />
</MudExGridPaper>
```

The source and the layout are the same as for a plain ExPivot. See the ExPivot README.

- **Words.** Every word comes from ExPivot and is replaced through ExPivot's `Label` parameter.
  That one function covers the pane, the panels and the report's Context Menu drawn by
  `MudGridChrome`.
- **Icons.** `new MudPivotChrome { Icon = id => … }` replaces a command's Material icon. Keep
  the instance in a field.
- **The `±` buttons** in the report's label cells stay ExPivot's own plain markup, recoloured by
  the palette. A MudBlazor component there would be one more component in every row painted.

## More

The decisions are recorded in the repository's ADRs:
[ADR-0061](https://github.com/AkiraMisawa/ex-grid/blob/main/docs/adr/0061-the-field-list-is-excels-pane-and-the-core-decides-what-a-move-means.md)
covers the Field List and the Chrome seam, and
[ADR-0062](https://github.com/AkiraMisawa/ex-grid/blob/main/docs/adr/0062-expivot-mudblazor-is-a-chrome-and-a-stylesheet.md)
covers this Wrapper.
