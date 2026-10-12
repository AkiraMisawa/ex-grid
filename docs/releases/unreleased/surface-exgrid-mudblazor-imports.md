Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.MudBlazor._Imports` is no longer public: Razor generated it from the package's own `_Imports.razor`, which only held that project's `@using` lines, and it was never a component or anything a Consumer could use. Nothing replaces it, and code that does not name it has nothing to change.
