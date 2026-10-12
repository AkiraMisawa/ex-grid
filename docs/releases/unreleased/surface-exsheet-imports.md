Product: ExSheet
Kind: Breaking
ADR: 0201

`ExSheet._Imports` is no longer public: Razor generated it from the package's own `_Imports.razor`, which only held the package's `@using` lines, and it was never a component or anything a Consumer could use. Nothing replaces it, and code that does not name it has nothing to change.
