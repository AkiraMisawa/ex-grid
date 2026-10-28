Product: ExBoard
Kind: Added
ADR: 0390, 0370

ExBoard in TypeScript takes the Chrome of each kind of view: `BoardOptions.chrome` holds `grid`, `pivot` and `chart`, handed to every view of that kind, the grid's also to the pivots' report grids where `pivot` names no grid of its own, as the Razor ExBoard's `Chrome` does. React's `ExBoard` takes them as render functions, so a Mantine or MUI application hands it the Wrappers' Chromes (`mantineChrome`, `mantinePivotChrome`, `mantineChartChrome`) inside an element of the Wrappers' classes. `@ex-grid/pivot-react` now exports `pivotChromeMounts` and `@ex-grid/chart-react` `chartChromeMounts`, for the family's Framework Adapters.
