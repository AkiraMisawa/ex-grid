Product: ExPivot
Kind: Added
ADR: 0390

New package `@ex-grid/pivot-mui`: `MuiPivot` draws ExPivot for React in MUI's outlined Paper, its Field List, Pivot Toolbar, menus, panels, report filters, Stale Report notice and Show Details' tabs and dialog in MUI's components (`muiPivotChrome`) and its report grid's Chrome in `@ex-grid/mui`'s. Its stylesheet maps the pivot's Visual Tokens onto a MUI theme made with CSS variables, so a change of scheme recolours it with no render; a theme without them is refused by name. `@ex-grid/mui` now exports that refusal, `refuseThemeWithoutVariables`, which every MUI Wrapper of the family calls. It is published with 0.3.0.
