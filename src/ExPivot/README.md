# ExPivot

Excel's PivotTable for Blazor, drawn by [ExGrid](https://www.nuget.org/packages/ExGrid) as that
grid's Consumer. The application hands ExPivot its own records and declares the **Pivot Fields**
they carry; the user builds a report in the **PivotTable Fields** pane — Filters, Columns, Rows,
Values, with drag and drop, each field's menu, Filter…, Field Settings… and Value Field Settings…
— and reads it with ExGrid's selection, keyboard and clipboard. The application persists the
**Pivot Layout**.

- Excel's semantics, computed by [ExPivot.Engine](https://www.nuget.org/packages/ExPivot.Engine):
  Sum to Varp, subtotals and grand totals, the Compact, Outline and Tabular forms, expand and
  collapse, Hidden Items, sorting by label or by value, Show Values As, and Show Details.
- A number that does not fit is `####`, never a shorter number; a copy carries the full-precision
  values; money is summed exactly.

> **This is a prerelease (`0.x`).** ExPivot is built alongside ExGrid and is not part of its
> release. The API may change between prereleases.

## Requirements

- **.NET 10 or newer**, Blazor WebAssembly or Server, on a Chromium browser (as ExGrid).

## Install

```sh
dotnet add package ExPivot --prerelease
```

Link both stylesheets in your host page:

```html
<link href="_content/ExGrid/ex-grid.css" rel="stylesheet" />
<link href="_content/ExPivot/ex-pivot.css" rel="stylesheet" />
```

## A first pivot

```razor
@using ExPivot.Components
@using ExPivot.Engine

<ExPivot TRecord="Sale" Records="_sales" Fields="_fields" @bind-Layout="_layout"
         OnShowDetails="ShowDetails" ViewportHeight="420" />

@code {
    private readonly Sale[] _sales = LoadSales();
    private static readonly PivotField<Sale>[] _fields =
    [
        new("Region", PivotFieldType.Text, s => s.Region),
        new("Product", PivotFieldType.Text, s => s.Product),
        new("Date", PivotFieldType.Date, s => s.Date, format: "yyyy-MM"),
        new("Amount", PivotFieldType.Number, s => s.Amount),
    ];
    private PivotLayout _layout = new()
    {
        Rows = [new PivotFieldPlacement("Region")],
        Values = [new PivotValueField("Amount", PivotAggregation.Sum)],
    };

    private void ShowDetails(PivotDetails<Sale> details) { /* show details.Records */ }
}
```

- **Records** is a snapshot: hand over a new list to refresh the report. A list changed in place
  is not seen.
- **Layout** is View State. `PivotLayoutJson.Write` and `Read` give it a versioned JSON form to
  store as a user's setting.
- **OnShowDetails** receives the records behind a value cell, from a double click or the Context
  Menu.
- **PivotChrome** draws the Field List and its panels in another design system; **Chrome** is the
  report grid's. For MudBlazor, use `ExPivot.MudBlazor`.
