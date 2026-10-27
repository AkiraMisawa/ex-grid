Product: ExGrid
Kind: Fixed
ADR: 0053

A grid on the TypeScript core that is taken out of layout, under `display: none` or removed by its host before it is disposed (a Blazor Server navigation away from `ExGrid.Script.ExGrid`), no longer throws "The browser lays out at most 1px of rows here": a Layout Ceiling probe laid out at nothing tells no ceiling, and the one last told stands.
