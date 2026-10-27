Product: ExGrid
Kind: Added
ADR: 0390, 0030, 0016

`@ex-grid/mui` is ExGrid for React's MUI Wrapper: `MuiExGrid` stands the grid in MUI's Paper, draws every Chrome slot in MUI's components at the size the Density maps onto, and maps the Visual Tokens onto a MUI theme's CSS variables in light and dark. It needs a theme made with `createTheme({ cssVariables: true })` and refuses one without, naming the option. The cells are drawn in MUI's default font stack with widths measured for every face it resolves to, so `####` is judged by the face on screen; `muiChrome` is the Wrapper's slots alone. It is published with 0.3.0.
