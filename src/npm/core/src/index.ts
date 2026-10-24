/**
 * ExGrid's Script Core: the grid's state and what each input means, in TypeScript and free of any UI
 * framework (ADR-0350). A Framework Adapter, `@ex-grid/react` or Blazor's ExGrid, mounts it.
 *
 * @packageDocumentation
 */
export type { CellPosition, GridDirection, GridExtent } from './selection/cell';
export type { SelectionRange } from './selection/range';
export type { GridSelection } from './selection/selection';
