/**
 * ExPivot for MUI (ADR-0390): `MuiPivot` draws ExPivot for React inside MUI's Paper, and `muiPivotChrome` draws its
 * Chrome slots — the Field List, the Pivot Toolbar, its menus and panels, the report filters, the Stale Report's notice
 * and Show Details' tabs and dialog — in MUI's components. Its stylesheet, `@ex-grid/pivot-mui/styles.css`, maps the
 * pivot's Visual Tokens onto a MUI theme made with CSS variables, so a change of scheme recolours the pivot with no
 * render.
 *
 * @packageDocumentation
 */
export { muiPivotChrome } from './pivot-chrome';
export { MuiPivot, type MuiPivotProps } from './mui-pivot';
