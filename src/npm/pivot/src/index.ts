/**
 * ExPivot's Script Core (ADR-0370, section 1): Excel's PivotTable drawn by ExGrid as that grid's Consumer,
 * with its Field List, in TypeScript and free of any UI framework. The report is computed by
 * `@ex-grid/pivot-engine` and drawn by `@ex-grid/core`; a Framework Adapter mounts it.
 *
 * @packageDocumentation
 */
export { browserPivotLocale } from './browser-locale';
export { createPivot } from './create-pivot';
export type { PivotDetailItem, PivotDetailsView, PivotShowDetailsEvent } from './details';
export type { PivotHandle } from './create-pivot';
export type {
    PivotFieldListShownEvent, PivotLayoutChangedEvent, PivotRefusedEvent, PivotSelectionChangedEvent, PivotViewOptions,
} from './pivot-view';
export type { PivotCellItems } from './report-grid';
