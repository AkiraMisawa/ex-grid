Product: ExPivot
Kind: Breaking
ADR: 0201

`ExPivot.MudBlazor.MudPivotIcons` is no longer public: it is the Wrapper's own Material icons for ExPivot's commands and Areas. A Consumer replaces a command's icon with `MudPivotChrome.Icon`, and returns null there to keep the Wrapper's.
