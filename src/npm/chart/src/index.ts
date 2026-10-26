/**
 * ExChart in TypeScript (ADR-0370): a chart's definition, checked when it is made, and the scene it is
 * laid out as in CSS pixels — panels, axes, ticks, each series' lines, markers, candles and intervals —
 * what its tooltip, its summary and its files say, and a 3D surface as the mesh its entry draws and the 2D
 * charts its slices are; the Chart Spec, a chart said in JSON as an AI writes it and a Board holds it, read
 * against the tables its Data Handles name (ADR-0320); and its Script Core, `createChart`, which draws a chart
 * as SVG into an element and hears the pointer, the legend and Chart Filters, free of any UI framework. A
 * Framework Adapter mounts it; the C# engine and this package are held to one another by the shared cases
 * under `tests/cases/chart/`.
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
export { readingAt, readingOfEdge, readingOfMark } from './reading';
export type { ChartReading, ReadingLine } from './reading';
export { chartSummary } from './summary';
export { chartCsvLong, chartCsvWide, chartTable } from './csv';
export type { ChartTable } from './csv';
export { SurfaceAxis, SurfaceDefinition, defaultSurfaceView, trianglesMethod } from './surface';
export type {
    SurfaceAxisOptions, SurfaceGridOptions, SurfaceNode, SurfacePoint, SurfacePointsOptions, SurfaceSampledOptions, SurfaceScaleOptions,
    SurfaceSlice, SurfaceSliceAxis, SurfaceTriangle, SurfaceTrianglesOptions, SurfaceValue, SurfaceView,
} from './surface';
export { surfaceBandCount, surfaceMesh, surfaceSlicePath } from './surface-mesh';
export { chartDataOf } from './chart-data';
export type { ChartData } from './chart-data';
export { ChartSpecReading, ChartSpecRefusedError, chartSpecVersion, readChartSpec } from './spec';
export type { ChartSpecReadingOptions } from './spec';
export type { SurfaceColourBar, SurfaceFace, SurfaceMesh, SurfaceMeshNode, SurfaceMeshPoint, SurfaceTick } from './surface-mesh';
export { createChart } from './dom/create-chart';
export type { ChartHandle } from './dom/create-chart';
export type { ChartOptions, ChartPointClick, ChartRange, SeriesVisibility } from './dom/chart-element';
export type { ChartFileFormat } from './dom/chart-files';
export { chartWordIdList, chartWordIds, japaneseChartWords } from './dom/chart-words';
export type { ChartLabel } from './dom/chart-words';
