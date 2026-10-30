# ExBoard

A Board for a Blazor application: JSON composed once, by an AI from a request in words or by hand,
and run again without it. A Board lays out rows of [ExGrid](https://www.nuget.org/packages/ExGrid),
[ExPivot](https://www.nuget.org/packages/ExPivot) and [ExChart](https://www.nuget.org/packages/ExChart)
views over typed Board Inputs.

- Each input declares its columns and their kinds, and is checked against its source's shape before
  anything is drawn; a source that answers otherwise is refused by name.
- A link narrows the views it reaches by the values a selection stands for, from a grid's rows or a
  pivot's cells.
- An output is handed on when the user confirms it, as a value condition and the version of the
  source it was taken against.
- The user's changes are kept as a Board Diff, laid over the Board the next time it is run.
- `BoardSpec.Tool` describes `draw_board`, a tool an AI calls with a Board and its data's handles,
  and `BoardSpec.Answer` answers it.

```razor
@using ExBoard
@using ExBoard.Components

<ExBoard Board="board" Data="data" />

@code {
    private readonly BoardReading board = BoardSpec.Read(json);
    private IReadOnlyDictionary<string, ExGrid.Data.Snapshot> data = …; // one Snapshot per input
}
```

ExBoard takes the products' Chromes as they are (`Chrome`, `PivotChrome`, `ChartChrome`), so a
MudBlazor application passes the Wrappers' Chromes and needs no package of its own.

The guide, with an Example of each part: <https://akiramisawa.github.io/ex-grid/exboard>.
