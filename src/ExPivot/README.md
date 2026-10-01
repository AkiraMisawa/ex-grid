# ExPivot

Excel's PivotTable for Blazor, drawn by [ExGrid](https://www.nuget.org/packages/ExGrid) as that
grid's Consumer. The application hands ExPivot a **Pivot Source**: its own records with the
**Pivot Fields** they carry, answered in the process, or a server that answers the same questions.
The user builds a report in the **PivotTable Fields** pane — Filters, Columns, Rows, Values, with
drag and drop, each field's menu, Filter…, Field Settings… and Value Field Settings… — sets its
form from the toolbar's Layout menu, and reads it with ExGrid's selection, keyboard and clipboard.
The application persists the **Pivot Layout**.

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
@using ExPivot
@using ExPivot.Components
@using ExPivot.Engine

<ExPivot Source="_source" @bind-Layout="_layout" ViewportHeight="420" />

@code {
    private static readonly PivotField<Sale>[] Fields =
    [
        new("Region", PivotFieldType.Text, s => s.Region),
        new("Product", PivotFieldType.Text, s => s.Product),
        new("Date", PivotFieldType.Date, s => s.Date, format: "yyyy-MM"),
        new("Amount", PivotFieldType.Number, s => s.Amount),
    ];

    // Held in a field: a new source is a refresh.
    private PivotSource _source = PivotSource.From(LoadSales(), Fields);
    private PivotLayout _layout = new()
    {
        Rows = [new PivotFieldPlacement("Region")],
        Values = [new PivotValueField("Amount", PivotAggregation.Sum)],
    };
}
```

- **Source** is what the report is computed from. `PivotSource.From(records, fields)` answers in
  the process; `PivotSource.Fetch` carries your own transport to a server. Hand over a new source
  to refresh the report. ExPivot asks without blocking: a slow answer leaves the report as it was
  under a loading indication, and a newer question cancels the old one.
- **Layout** is View State. `PivotLayoutJson.Write` and `Read` give it a versioned JSON form to
  store as a user's setting. `LayoutChanged` is raised for the layout the report shows.
- **Show Details**, from a double click on a value or the Context Menu, opens the records behind
  it in a tab at the report's foot. `DetailsView="PivotDetailsView.Dialog"` opens a dialog
  instead, and a Consumer that listens to `OnShowDetails` takes them itself: `PivotDetails` names
  the cell and pages its records under the report's Source Version.
- **Caps** (`PivotCaps`) refuse, by name, a layout whose report is too large to read: 200,000
  leaves and Excel's 1,048,576 rows and 16,384 columns by default.
- **A large report never holds the page.** The answer, its cube and the report's layout are built
  in slices of about 30 ms (`Slicing`, `PivotSlicing.Default` when left out), under the loading
  indication, with the report on screen kept until the new one is complete; a newer gesture
  supersedes the work.
- **Live data.** ExPivot listens to the source's `Changed` — a server's source raises it when you
  call `NotifyChanged`, however you learn of the change (SignalR, polling, a message bus) — and
  asks again for the whole answer. Changes are gathered and the report is redrawn at most every
  `RedrawInterval` (250 ms; zero redraws on every change). A change of values keeps the Selection
  and any open menu or panel. A value cell whose shown text changed is marked for
  `ChangeHighlightDuration` (one second); a new layout, a sort or a collapse marks nothing. When
  the newest data cannot be shown — it breaks a cap, the source fails, or a Refresh fails — the
  report stays as it was, and a notice under the toolbar says what happened and as of when, with
  Retry. While a new version's Items are on their way, the report filter band and Filter… keep
  the ones they listed. `Clock` takes a `TimeProvider` for tests.
- **ShowFieldList** can be bound (`@bind-ShowFieldList`) to remember the pane the user hid or
  showed from the toolbar.
- **Label** replaces any word by its id. `Label="PivotWords.Japanese"` speaks the words of
  Excel's Japanese edition; the culture alone changes no word.
- **PivotChrome** draws the Field List, the toolbar, the menus, the panels, Show Details' tabs
  and dialog, and the Stale Report's notice in another design system; **Chrome** is the report
  grid's. For MudBlazor, use `ExPivot.MudBlazor`.
