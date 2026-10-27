/**
 * ExChart for Mantine (ADR-0390): `mantineChartChrome` draws ExChart for React's toolbar and context menu in
 * Mantine's components, and `MantineChartDock` docks the charts made from a grid's selection beside the grid or below
 * it in Mantine's tabs. Its stylesheet, `@ex-grid/chart-mantine/styles.css`, maps the chart's Visual Tokens onto the
 * Mantine theme, so a change of scheme recolours every chart with no render.
 *
 * @packageDocumentation
 */
export { mantineChartChrome } from './chart-chrome';
export { MantineChartDock, type MantineChartDockProps } from './mantine-chart-dock';
