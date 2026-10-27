/**
 * ExPivot for Mantine (ADR-0390): `MantinePivot` draws ExPivot for React inside Mantine's Paper, and
 * `mantinePivotChrome` draws its Chrome slots — the Field List, the Pivot Toolbar, its menus and panels, the report
 * filters, the Stale Report's notice and Show Details' tabs and dialog — in Mantine's components. Its stylesheet,
 * `@ex-grid/pivot-mantine/styles.css`, maps the pivot's Visual Tokens onto the Mantine theme, so a change of scheme
 * recolours the pivot with no render.
 *
 * @packageDocumentation
 */
export { mantinePivotChrome } from './pivot-chrome';
export { MantinePivot, type MantinePivotProps } from './mantine-pivot';
