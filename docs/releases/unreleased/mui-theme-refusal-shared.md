Product: ExGrid
Kind: Added
ADR: 0390

`@ex-grid/mui` exports `refuseThemeWithoutVariables(theme, who)`, the check `MuiExGrid`, `MuiExSheet` and ExChart's MUI Wrapper make before they draw: a theme made without CSS variables, or with a `cssVarPrefix` of its own, is refused, naming the option to change. `MuiChartDock`'s and `muiChartChrome`'s refusal of a custom prefix now names `cssVariables.cssVarPrefix` as `MuiExGrid`'s does.
