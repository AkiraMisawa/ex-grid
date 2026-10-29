Product: ExGrid
Kind: Behaviour
ADR: 0370

Every Blazor Example on the Docs Site, and every DemoHost page that draws an ExGrid, now draws `ExGrid.Script.ExGrid`, the grid on the TypeScript core, in place of `ExGrid.Components.ExGrid`, ahead of the Razor grid's removal in this release (ADR-0370 step 3). A Consumer moving over writes `@using ExGrid.Script`, passes its rows as `Rows` or through a Grid Source in place of a pushed `Window`, and hears every refusal through `OnRefused`; the Examples show each, including a source of the page's own for Range Requests asked early.
