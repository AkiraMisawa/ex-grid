/**
 * ExChart for MUI (ADR-0390): `muiChartChrome` draws ExChart for React's toolbar and context menu in MUI's
 * components, and `MuiChartDock` docks the charts made from a grid's selection beside the grid or below it in MUI's
 * tabs. Its stylesheet, `@ex-grid/chart-mui/styles.css`, maps the chart's Visual Tokens onto a MUI theme made with
 * CSS variables, so a change of scheme recolours every chart with no render.
 *
 * @packageDocumentation
 */
export { muiChartChrome } from './chart-chrome';
export { MuiChartDock, type MuiChartDockProps } from './mui-chart-dock';
