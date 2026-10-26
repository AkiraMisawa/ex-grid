/**
 * ExPivot's Script Core (ADR-0370, section 1): Excel's PivotTable drawn by ExGrid as that grid's Consumer,
 * with its Field List, in TypeScript and free of any UI framework. The report is computed by
 * `@ex-grid/pivot-engine` and drawn by `@ex-grid/core`; a Framework Adapter mounts it.
 *
 * @packageDocumentation
 */
export { createPivot } from './create-pivot';
export type { PivotHandle } from './create-pivot';
export type {
    PivotFieldListShownEvent, PivotLayoutChangedEvent, PivotRefusedEvent, PivotViewOptions,
} from './pivot-view';
