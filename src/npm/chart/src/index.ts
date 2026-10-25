/**
 * ExChart's layout in TypeScript (ADR-0370): a chart's definition, checked when it is made, and the scene
 * it is laid out as in CSS pixels — panels, axes, ticks, each series' lines, markers, candles and
 * intervals — free of any UI framework. A Framework Adapter draws the scene; the C# engine and this
 * package are held to one another by the shared cases under `tests/cases/chart/`.
 *
 * @packageDocumentation
 */
export type {
    AppearanceKeys, AxisScale, AxisSide, AxisValues, CandlePeriod, ChartAxis, ChartAxisOptions, ChartEdge, ChartPoint, ChartSeries,
    ChartSeriesOptions, ChartValue, IntervalBound, IntervalStyle, MarkerShape, NamedPosition, PixelSummary, PointColour, PointDetail,
    PointSize, PriceBar, Segment, SeriesInterval, SeriesKind, SeriesLook, SeriesSampler, StepSide,
} from './model';
export { ChartDefinition } from './definition';
export type { ChartDefinitionOptions } from './definition';
export { ChartRefusedError } from './values';
export type { Box, ChartMetrics, ChartView } from './metrics';
export type { AxisStyle, DateLabel, TickPlacement } from './ticks';
export type { Bounds, DrawnMarker, DrawnPoint, DrawnWhisker } from './series-drawing';
export type { DrawnEdge } from './edges';
export type { ColourKey, KeyTick, SizeEntry, SizeKey } from './point-scales';
export { buildScene, legendMembers, positionAt, valueAt, yOf } from './scene';
export type {
    ChartEdgeMark, ChartMark, ChartScene, HorizontalTick, LegendAlign, LegendEntry, LegendPlacement, LegendPosition, PanelScene,
    PlacedCandle, PlacedVolume, PointerPosition, SceneOptions, SeriesPiece, VerticalAxis, VerticalTick,
} from './scene';
